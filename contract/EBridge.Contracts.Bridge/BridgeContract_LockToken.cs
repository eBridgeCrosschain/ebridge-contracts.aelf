using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using EBridge.Contracts.Report;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Empty AddToken(AddTokenInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        var addedChainToken = new ChainTokenList();
        foreach (var chainToken in input.Value)
        {
            var tokenWhitelist = State.ChainTokenWhitelist[chainToken.ChainId] ?? new TokenSymbolList();
            var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = chainToken.Symbol
            });
            Assert(!string.IsNullOrEmpty(tokenInfo.Symbol), $"Token {chainToken.Symbol} info is not exist.");

            if (tokenWhitelist.Symbol.Contains(chainToken.Symbol))
            {
                continue;
            }

            tokenWhitelist.Symbol.Add(chainToken.Symbol);
            addedChainToken.Value.Add(new ChainToken
            {
                ChainId = chainToken.ChainId,
                Symbol = chainToken.Symbol
            });
            State.ChainTokenWhitelist[chainToken.ChainId] = tokenWhitelist;
        }

        Context.Fire(new TokenWhitelistAdded
        {
            ChainTokenList = addedChainToken
        });

        return new Empty();
    }

    public override Empty RemoveToken(RemoveTokenInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        var toRemoveList = input.Value.Distinct().ToList();
        var removedChainToken = new ChainTokenList();
        foreach (var chainToken in toRemoveList)
        {
            Assert(State.ChainTokenWhitelist[chainToken.ChainId] != null, $"Incorrect chain id {chainToken.ChainId}. ");
            Assert(State.ChainTokenWhitelist[chainToken.ChainId].Symbol.Contains(chainToken.Symbol),
                $"Token {chainToken.Symbol} is not in whitelist. ");
            State.ChainTokenWhitelist[chainToken.ChainId].Symbol.Remove(chainToken.Symbol);
            removedChainToken.Value.Add(new ChainToken
            {
                ChainId = chainToken.ChainId,
                Symbol = chainToken.Symbol
            });
        }

        Context.Fire(new TokenWhitelistRemoved()
        {
            ChainTokenList = removedChainToken
        });
        return new Empty();
    }

    public override Empty CreateReceipt(CreateReceiptInput input)
    {
        Assert(!State.IsContractPause.Value, "Contract is paused.");
        Assert(State.ChainTokenWhitelist[input.TargetChainId] != null,
            $"No symbol list under the chain id {input.TargetChainId}.");
        Assert(State.ChainTokenWhitelist[input.TargetChainId].Symbol.Contains(input.Symbol),
            $"Token {input.Symbol} is not in whitelist.");
        AssertPriceRatioFluctuation(input.TargetChainId);
        ConsumeReceiptAmount(input.Symbol, input.TargetChainId, input.Amount);
        var receipt = new Receipt
        {
            Symbol = input.Symbol,
            Owner = input.Owner ?? Context.Sender,
            Amount = input.Amount,
            TargetAddress = input.TargetAddress
        };
        TransferDepositTo(input.Symbol, input.Amount, Context.Sender);

        var receiptIdToken = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(Context.ChainId),
            HashHelper.ComputeFrom(input.TargetChainId),
            HashHelper.ComputeFrom(input.Symbol));

        var receiptCount = State.ReceiptCountMap[receiptIdToken].Add(1);
        var receiptId = $"{receiptIdToken.ToHex()}.{receiptCount}";
        State.ReceiptCountMap[receiptIdToken] = receiptCount;

        State.ReceiptIdInfoMap[receiptIdToken] = new ReceiptIdInfo
        {
            ChainId = input.TargetChainId,
            Symbol = input.Symbol
        };

        State.ReceiptMap[receiptId] = receipt;

        if (!decimal.TryParse(State.FeeFloatingRatio[input.TargetChainId], out var floatingRatio))
        {
            floatingRatio = 1;
        }

        var nativeTokenFee = CalculateTransactionFee(State.GasLimit[input.TargetChainId],
            State.GasPrice[input.TargetChainId],
            State.PriceRatio[input.TargetChainId], floatingRatio);
        State.TransactionFee.Value = State.TransactionFee.Value.Add(nativeTokenFee);
        TransferFee(DefaultFeeSymbol, nativeTokenFee, Context.Sender, Context.Self);

        Context.Fire(new ReceiptCreated
        {
            ReceiptId = receiptId,
            Amount = receipt.Amount,
            Owner = receipt.Owner,
            Symbol = receipt.Symbol,
            TargetAddress = receipt.TargetAddress,
            TargetChainId = input.TargetChainId
        });

        var receiptHash = CalculateReceiptHash(receiptId, receipt.Amount, receipt.TargetAddress);

        Context.SendInline(State.ReportContract.Value, nameof(State.ReportContract.QueryOracle), new QueryOracleInput
        {
            QueryInfo = new OffChainQueryInfo
            {
                Title = $"lock_token_{receiptId}",
                Options = { receiptHash.ToHex(),$"{receipt.Amount}-{receipt.TargetAddress}-{receiptIdToken.ToHex()}"}
            },
            ChainId = input.TargetChainId,
            Payment = State.QueryPayment.Value
        });

        return new Empty();
    }

    private void ConsumeReceiptAmount(string symbol, string targetChainId, long amount)
    {
        var dailyLimit = State.ReceiptDailyLimit[symbol][targetChainId];
        dailyLimit = GetDailyLimit(dailyLimit);

        var currentBucket = State.ReceiptTokenBucketInfo[symbol][targetChainId];
        currentBucket = GetTokenBucketAmount(currentBucket);

        ConsumeTokenAmount(dailyLimit, currentBucket, amount);

        Context.Fire(new ReceiptLimitChanged
        {
            Symbol = symbol,
            TargetChainId = targetChainId,
            CurrentReceiptDailyLimitAmount = dailyLimit?.TokenAmount ?? long.MaxValue,
            ReceiptDailyLimitRefreshTime = dailyLimit?.RefreshTime,
            CurrentReceiptBucketTokenAmount = currentBucket?.CurrentTokenAmount ?? long.MaxValue,
            ReceiptBucketUpdateTime = currentBucket?.LastUpdatedTime
        });
    }


    public override Empty WithdrawTransactionFee(Int64Value input)
    {
        Assert(State.Admin.Value != null, "Admin is null.");
        Assert(input.Value > 0, $"Invalid withdraw amount.{input.Value}");
        Assert(input.Value <= State.TransactionFee.Value,
            $"Insufficient amount. Current amount:{State.TransactionFee.Value}");
        DoTransferFee(DefaultFeeSymbol, input.Value, State.Admin.Value);
        State.TransactionFee.Value = State.TransactionFee.Value.Sub(input.Value);
        return new Empty();
    }
}