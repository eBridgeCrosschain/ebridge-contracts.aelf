using System;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using EBridge.Contracts.MerkleTreeContract;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Hash CreateSwap(CreateSwapInput input)
    {
        Assert(input.RegimentId != null, "Regiment id cannot be null.");
        Assert(State.MerkleTreeContract.Value != null, "MerkleTree contract is not initialized.");
        var targetToken = input.SwapTargetToken;
        Assert(targetToken != null, "Invalid input.");
        var fromChainId = targetToken.FromChainId;
        var symbol = targetToken.Symbol;
        Assert(!string.IsNullOrEmpty(fromChainId) && !string.IsNullOrEmpty(symbol), "Invalid chain id and symbol.");
        Assert(State.ChainTokenSwapIdMap[fromChainId][symbol] == null,
            $"Swap already created. Chain id: {targetToken.FromChainId} Symbol: {targetToken.Symbol}. ");
        var swapId = HashHelper.ConcatAndCompute(Context.TransactionId, HashHelper.ComputeFrom(input));
        Assert(State.SwapInfo[swapId] == null, "Swap already created.");

        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(input.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "Only regiment manager can create swap.");
        Assert(State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Admins.Contains(Context.Self),
            $"Bridge Contract is not the admin of regiment. Regiment id: {input.RegimentId}");

        //Create space to record receipt.
        State.MerkleTreeContract.CreateSpace.Send(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = input.MerkleTreeLeafLimit == 0 ? DefaultMaximalLeafCount : input.MerkleTreeLeafLimit,
                Operators = input.RegimentId
            }
        });
        var spaceSalt = State.MerkleTreeContract.GetRegimentSpaceCount.Call(input.RegimentId).Value.Add(1);
        var spaceId =
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.RegimentId), HashHelper.ComputeFrom(spaceSalt));
        State.SwapSpaceIdMap[swapId] = spaceId;

        AssertSwapTargetToken(targetToken.Symbol);
        Assert(ValidateSwapRatio(targetToken.SwapRatio), "Invalidate swap ratio.");
        var swapInfo = new SwapInfo
        {
            SwapId = swapId,
            RegimentId = input.RegimentId,
            SpaceId = spaceId,
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

    public override Empty Deposit(DepositInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        Assert(input.Amount > 0, $"Invalid deposit amount.{input.Amount}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(swapInfo.RegimentId);
        var regimentMemberList = State.RegimentContract.GetRegimentMemberList.Call(regimentAddress);
        Assert(regimentMemberList.Value.Contains(Context.Sender), "No permission.");
        var swapPairInfo = State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol];
        Assert(swapPairInfo != null, $"Swap pair {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist.");
        swapPairInfo.DepositAmount = swapPairInfo.DepositAmount.Add(input.Amount);
        State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol] = swapPairInfo;
        TransferDepositFrom(input.TargetTokenSymbol, input.Amount, Context.Sender);
        return new Empty();
    }

    public override Empty SwapToken(SwapTokenInput input)
    {
        Assert(!State.IsContractPause.Value, "Contract is paused.");
        var receiverAddress = input.ReceiverAddress ?? Context.Sender;
        ValidateSwapTokenInput(input);
        Assert(TryGetReceiptIndex(input.ReceiptId, out var receiptIndex), "Incorrect receipt index.");
        Assert(TryGetOriginTokenAmount(input.OriginAmount, out var amount), "Invalid amount.");
        AssertTokenAmount(input.SwapId, input.ReceiptId, amount);
        var leafHash = ComputeLeafHash(amount, receiverAddress, input.ReceiptId);

        var spaceId = State.SwapSpaceIdMap[input.SwapId];
        var lastRecordedLeafIndex = State.MerkleTreeContract.GetLastLeafIndex.Call(new GetLastLeafIndexInput
        {
            SpaceId = spaceId
        });
        var maximalLeafCount = State.MerkleTreeContract.GetSpaceInfo.Call(spaceId).MaxLeafCount;

        // To locate the tree of specific receipt id.
        var firstLeafIndex = receiptIndex.Sub(1).Div(maximalLeafCount).Mul(maximalLeafCount);
        var maxLastLeafIndex = firstLeafIndex.Add(maximalLeafCount).Sub(1);
        var lastLeafIndex = Math.Min(maxLastLeafIndex, lastRecordedLeafIndex.Value);
        var merklePath = State.MerkleTreeContract.GetMerklePath.Call(new GetMerklePathInput
        {
            ReceiptMaker = Context.Self,
            LeafNodeIndex = receiptIndex.Sub(1),
            SpaceId = spaceId
        });

        Assert(State.MerkleTreeContract.MerkleProof.Call(new MerkleProofInput
        {
            LastLeafIndex = lastLeafIndex,
            LeafNode = leafHash,
            MerklePath = merklePath,
            SpaceId = spaceId
        }).Value, "Merkle proof failed.");

        //Transfer token.
        PerformTransferToken(input.SwapId, receiverAddress, amount, input.ReceiptId);
        return new Empty();
    }

    private void AssertTokenAmount(Hash swapId, string receiptId, decimal amount)
    {
        var swapInfo = GetTokenSwapInfo(swapId);
        var tokenSymbol = swapInfo.SwapTargetToken.Symbol;
        var maximumAmount = State.TokenMaximumAmount[tokenSymbol];
        var actualAmount = GetTargetTokenAmount(amount, swapInfo.SwapTargetToken.SwapRatio);
        if (actualAmount <= maximumAmount) return;
        Assert(State.ApproveTransfer[receiptId],
            $"{tokenSymbol} swap amount higher than maximum amount. Waiting for admin authorization. ReceiptId:{receiptId}");
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
        TransferToken(swapTargetToken.Symbol, targetTokenAmount, receiverAddress);
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

    public override Empty Withdraw(WithdrawInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        Assert(input.Amount > 0, $"Invalid withdraw amount.{input.Amount}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(swapInfo.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "No permission.");
        var swapPairInfo = State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol];
        Assert(swapPairInfo != null, $"Swap pair {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist.");
        var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = input.TargetTokenSymbol,
            Owner = Context.Self
        }).Balance;
        Assert(balance >= input.Amount, $"Contract balance not enough. Balance : {balance}");
        Assert(swapPairInfo.DepositAmount >= input.Amount,
            $"Swap pair deposits not enough. Deposit amount : {swapPairInfo.DepositAmount}");
        swapPairInfo.DepositAmount = swapPairInfo.DepositAmount.Sub(input.Amount);
        State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol] = swapPairInfo;
        WithdrawDepositTo(input.TargetTokenSymbol, input.Amount, Context.Sender);
        return new Empty();
    }

    public override Empty ChangeSwapRatio(ChangeSwapRatioInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(swapInfo.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "No permission.");
        Assert(swapInfo.SwapTargetToken.Symbol == input.TargetTokenSymbol,
            $"Swap target token {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist. ");
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