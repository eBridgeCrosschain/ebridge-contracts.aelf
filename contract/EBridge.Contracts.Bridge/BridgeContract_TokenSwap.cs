using AElf;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Hash CreateSwap(CreateSwapInput input)
    {
        var targetToken = input.SwapTargetToken;
        Assert(targetToken != null, "Invalid input.");
        var fromChainId = targetToken.FromChainId;
        var symbol = targetToken.Symbol;
        Assert(!string.IsNullOrEmpty(fromChainId) && !string.IsNullOrEmpty(symbol), "Invalid chain id and symbol.");
        Assert(State.ChainTokenSwapIdMap[fromChainId][symbol] == null,
            $"Swap already created. Chain id: {targetToken.FromChainId} Symbol: {targetToken.Symbol}. ");
        var swapId = HashHelper.ConcatAndCompute(Context.TransactionId, HashHelper.ComputeFrom(input));
        Assert(State.SwapInfo[swapId] == null, "Swap already created.");
        
        Assert(Context.Sender == State.Admin.Value, "Only contract admin can create swap.");
        AssertSwapTargetToken(targetToken.Symbol);
        Assert(ValidateSwapRatio(targetToken.SwapRatio), "Invalidate swap ratio.");
        var swapInfo = new SwapInfo
        {
            SwapId = swapId,
            SwapTargetToken = new SwapTargetToken
            {
                FromChainId = targetToken.FromChainId,
                SwapRatio = targetToken.SwapRatio,
                Symbol = targetToken.Symbol
            }
        };
        State.SwapInfo[swapId] = swapInfo;

        var swapPairInfo = new SwapPairInfo();
        State.SwapPairInfoMap[swapId][targetToken.Symbol] = swapPairInfo;

        State.ChainTokenSwapIdMap[targetToken.FromChainId][symbol] = swapId;

        Context.Fire(new SwapInfoAdded
        {
            SwapId = swapId,
            FromChainId = fromChainId,
            Symbol = symbol
        });
        return swapId;
    }

    private void ConsumeSwapAmount(Hash swapId, decimal amount)
    {
        var swapInfo = GetTokenSwapInfo(swapId);
        var actualAmount = GetTargetTokenAmount(amount, swapInfo.SwapTargetToken?.SwapRatio);
        var dailyLimit = State.SwapDailyLimit[swapId];
        dailyLimit = GetDailyLimit(dailyLimit);
        var currentBucket = State.SwapTokenBucketInfo[swapId];  
        currentBucket = GetTokenBucketAmount(currentBucket);
        
        if (dailyLimit == null && currentBucket == null) 
        {
            return;
        }
        ConsumeTokenAmount(dailyLimit,currentBucket,actualAmount);
        
        Context.Fire(new SwapLimitChanged
        {
            Symbol = swapInfo.SwapTargetToken?.Symbol,
            FromChainId = swapInfo.SwapTargetToken?.FromChainId,
            CurrentSwapDailyLimitAmount = dailyLimit?.TokenAmount ?? long.MaxValue,
            SwapDailyLimitRefreshTime = dailyLimit?.RefreshTime,
            CurrentSwapBucketTokenAmount = currentBucket?.CurrentTokenAmount ?? long.MaxValue,
            SwapBucketUpdateTime = currentBucket?.LastUpdatedTime
        });
    }

    private void PerformTransferToken(Hash swapId, Address receiverAddress, decimal amount, string receiptId)
    {
        var swapInfo = GetTokenSwapInfo(swapId);
        var swapAmounts = new SwapAmounts
        {
            Receiver = receiverAddress
        };
        var swapTargetToken = swapInfo.SwapTargetToken;
        var swapPairInfo = State.SwapPairInfoMap[swapInfo.SwapId][swapTargetToken.Symbol];
        Assert(swapPairInfo != null, $"Swap pair {swapInfo.SwapId}-{swapTargetToken.Symbol} is not exist.");
        var targetTokenAmount = GetTargetTokenAmount(amount, swapTargetToken.SwapRatio);

        // Update swap pair and ledger
        swapPairInfo.SwappedAmount = swapPairInfo.SwappedAmount.Add(targetTokenAmount);
        swapPairInfo.SwappedTimes = swapPairInfo.SwappedTimes.Add(1);

        State.SwapPairInfoMap[swapInfo.SwapId][swapTargetToken.Symbol] = swapPairInfo;

        // Do transfer
        TransferToken(swapTargetToken.Symbol, targetTokenAmount, receiverAddress, swapTargetToken.FromChainId);
        Context.Fire(new TokenSwapped
        {
            Amount = targetTokenAmount,
            Address = receiverAddress,
            Symbol = swapTargetToken.Symbol,
            ReceiptId = receiptId,
            FromChainId = swapTargetToken.FromChainId
        });

        swapAmounts.ReceivedAmounts[swapTargetToken.Symbol] = targetTokenAmount;

        State.Ledger[swapId][receiptId] = swapAmounts;
        State.RecorderReceiptInfoMap[swapId][receiptId] = new SwappedReceiptInfo
        {
            ReceiptId = receiptId,
            ReceivingTime = Context.CurrentBlockTime,
            ReceivingTxId = Context.TransactionId,
            AmountMap = {swapAmounts.ReceivedAmounts}
        };
    }

    public override Empty ChangeSwapRatio(ChangeSwapRatioInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(swapInfo.SwapTargetToken.Symbol == input.TargetTokenSymbol,
            $"Swap target token {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist. ");
        Assert(input.SwapRatio.OriginShare >= 1 && input.SwapRatio.TargetShare >= 1, "SwapRatio originShare or TargetShare is invalid");
        swapInfo.SwapTargetToken.SwapRatio = input.SwapRatio;
        State.SwapInfo[swapInfo.SwapId] = swapInfo;
        Context.Fire(new SwapRatioChanged
        {
            SwapId = input.SwapId,
            NewSwapRatio = input.SwapRatio,
            TargetTokenSymbol = input.TargetTokenSymbol
        });
        return new Empty();
    }
}