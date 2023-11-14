using System;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    #region DailyLimit

    public override Empty SetReceiptDailyLimit(SetReceiptDailyLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.ReceiptDailyLimitInfos.Count > 0, "Invalid input.");
        foreach (var receiptDailyLimitInfo in input.ReceiptDailyLimitInfos)
        {
            Assert(
                receiptDailyLimitInfo.DefaultTokenAmount > 0 &&
                !string.IsNullOrEmpty(receiptDailyLimitInfo.TargetChain) &&
                !string.IsNullOrEmpty(receiptDailyLimitInfo.Symbol),
                "Invalid input daily receipt limit info.");
            var symbol = receiptDailyLimitInfo.Symbol;
            var targetChain = receiptDailyLimitInfo.TargetChain;
            var dailyLimit = State.ReceiptDailyLimit[symbol][targetChain] ?? new DailyLimitTokenInfo();
            dailyLimit = SetDailyLimit(dailyLimit, receiptDailyLimitInfo.DefaultTokenAmount,
                receiptDailyLimitInfo.StartTime);
            State.ReceiptDailyLimit[symbol][targetChain] = dailyLimit;
            Context.Fire(new ReceiptDailyLimitSet
            {
                Symbol = symbol,
                TargetChainId = targetChain,
                ReceiptDailyLimit = dailyLimit.DefaultTokenAmount,
                ReceiptRefreshTime = dailyLimit.RefreshTime,
                CurrentReceiptDailyLimit = dailyLimit.TokenAmount
            });
        }

        return new Empty();
    }

    public override Empty SetSwapDailyLimit(SetSwapDailyLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.SwapDailyLimitInfos.Count > 0, "Invalid input.");
        foreach (var swapDailyLimitInfo in input.SwapDailyLimitInfos)
        {
            Assert(
                swapDailyLimitInfo.DefaultTokenAmount > 0 && swapDailyLimitInfo.SwapId != null,
                "Invalid input daily swap limit info.");
            var swapInfo = State.SwapInfo[swapDailyLimitInfo.SwapId];
            Assert(swapInfo != null, "Invalid swap id.");
            var dailyLimit = State.SwapDailyLimit[swapDailyLimitInfo.SwapId] ?? new DailyLimitTokenInfo();
            dailyLimit = SetDailyLimit(dailyLimit, swapDailyLimitInfo.DefaultTokenAmount, swapDailyLimitInfo.StartTime);
            State.SwapDailyLimit[swapDailyLimitInfo.SwapId] = dailyLimit;
            Context.Fire(new SwapDailyLimitSet
            {
                Symbol = swapInfo?.SwapTargetToken?.Symbol,
                FromChainId = swapInfo?.SwapTargetToken?.FromChainId,
                SwapDailyLimit = dailyLimit.DefaultTokenAmount,
                SwapRefreshTime = dailyLimit.RefreshTime,
                CurrentSwapDailyLimit = dailyLimit.TokenAmount
            });
        }

        return new Empty();
    }

    private DailyLimitTokenInfo SetDailyLimit(DailyLimitTokenInfo dailyLimit, long defaultTokenAmount,
        Timestamp startTime)
    {
        Assert(startTime.Seconds % DefaultDailyRefreshTime == 0, "Invalid refresh time.");
        Assert(
            Context.CurrentBlockTime >= dailyLimit.RefreshTime && Context.CurrentBlockTime >= startTime &&
            (Context.CurrentBlockTime - startTime).Seconds <= DefaultDailyRefreshTime,
            $"Invalid time,current refresh time is {dailyLimit.RefreshTime},current block time is {Context.CurrentBlockTime},new refresh time is {startTime}");
        if (dailyLimit.RefreshTime != null &&
            (Context.CurrentBlockTime - dailyLimit.RefreshTime).Seconds.Div(DefaultDailyRefreshTime) < 1)
        {
            var useAmount = dailyLimit.DefaultTokenAmount.Sub(dailyLimit.TokenAmount);
            dailyLimit.TokenAmount = defaultTokenAmount.Sub(useAmount) < 0 ? 0 : defaultTokenAmount.Sub(useAmount);
        }
        else
        {
            dailyLimit.TokenAmount = defaultTokenAmount;
        }

        dailyLimit.DefaultTokenAmount = defaultTokenAmount;
        dailyLimit.RefreshTime = startTime;
        return dailyLimit;
    }

    public override DailyLimitTokenInfo GetReceiptDailyLimit(GetReceiptDailyLimitInput input)
    {
        var dailyLimit = State.ReceiptDailyLimit[input.Symbol][input.TargetChain];
        return dailyLimit == null ? new DailyLimitTokenInfo() : GetDailyLimitTokenInfo(dailyLimit);
    }

    public override DailyLimitTokenInfo GetSwapDailyLimit(Hash input)
    {
        var dailyLimit = State.SwapDailyLimit[input];
        return dailyLimit == null ? new DailyLimitTokenInfo() : GetDailyLimitTokenInfo(dailyLimit);
    }

    private DailyLimitTokenInfo GetDailyLimitTokenInfo(DailyLimitTokenInfo dailyLimit)
    {
        var refreshTime = dailyLimit.RefreshTime;
        var tokenAmount = dailyLimit.TokenAmount;
        var count = (Context.CurrentBlockTime - dailyLimit.RefreshTime).Seconds.Div(DefaultDailyRefreshTime);
        if (count > 0)
        {
            refreshTime = dailyLimit.RefreshTime.AddSeconds(DefaultDailyRefreshTime.Mul(count));
            tokenAmount = dailyLimit.DefaultTokenAmount;
        }
        return new DailyLimitTokenInfo
        {
            TokenAmount = tokenAmount,
            DefaultTokenAmount = dailyLimit.DefaultTokenAmount,
            RefreshTime = refreshTime
        };
    }

    #endregion

    #region TokenBucket

    public override Empty ConfigReceiptTokenBucket(ConfigReceiptTokenBucketInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.ReceiptTokenBucketConfigs.Count > 0, "Invalid input.");
        foreach (var config in input.ReceiptTokenBucketConfigs)
        {
            Assert(
                !string.IsNullOrEmpty(config.TargetChain) && !string.IsNullOrEmpty(config.Symbol) &&
                config.TokenCapacity > 0 && config.Rate > 0,
                "Invalid receipt bucket config input.");
            var bucketConfig = State.ReceiptTokenBucketInfo[config.Symbol][config.TargetChain] ?? new TokenBucket();
            bucketConfig = SetTokenBucketConfig(bucketConfig, config.TokenCapacity, config.Rate, config.IsEnable);
            State.ReceiptTokenBucketInfo[config.Symbol][config.TargetChain] = bucketConfig;
            Context.Fire(new ReceiptTokenBucketSet
            {
                Symbol = config.Symbol,
                TargetChainId = config.TargetChain,
                ReceiptBucketIsEnable = bucketConfig.IsEnable,
                ReceiptCapacity = bucketConfig.TokenCapacity,
                ReceiptRefillRate = bucketConfig.Rate,
                ReceiptBucketUpdateTime = Context.CurrentBlockTime,
                CurrentReceiptBucketTokenAmount = bucketConfig.CurrentTokenAmount
            });
        }

        return new Empty();
    }

    public override Empty ConfigSwapTokenBucket(ConfigSwapTokenBucketInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.SwapTokenBucketConfigs.Count > 0, "Invalid input.");
        foreach (var config in input.SwapTokenBucketConfigs)
        {
            Assert(config.SwapId != null && config.TokenCapacity > 0 && config.Rate > 0,
                "Invalid swap bucket config input.");
            var swapInfo = GetTokenSwapInfo(config.SwapId);
            var bucketConfig = State.SwapTokenBucketInfo[config.SwapId] ?? new TokenBucket();
            bucketConfig = SetTokenBucketConfig(bucketConfig, config.TokenCapacity, config.Rate, config.IsEnable);
            State.SwapTokenBucketInfo[config.SwapId] = bucketConfig;
            Context.Fire(new SwapTokenBucketSet
            {
                Symbol = swapInfo?.SwapTargetToken?.Symbol,
                FromChainId = swapInfo?.SwapTargetToken?.FromChainId,
                SwapBucketIsEnable = bucketConfig.IsEnable,
                SwapCapacity = bucketConfig.TokenCapacity,
                SwapRefillRate = bucketConfig.Rate,
                SwapBucketUpdateTime = Context.CurrentBlockTime,
                CurrentSwapBucketTokenAmount = bucketConfig.CurrentTokenAmount
            });
        }

        return new Empty();
    }

    private TokenBucket SetTokenBucketConfig(TokenBucket bucket, long capacity, long rate, bool isEnable)
    {
        if (bucket.LastUpdatedTime != null)
        {
            var timeDiff = (Context.CurrentBlockTime - bucket.LastUpdatedTime).Seconds;
            if (timeDiff != 0)
            {
                bucket.CurrentTokenAmount = CalculateRefill(bucket.TokenCapacity, bucket.CurrentTokenAmount, timeDiff,
                    bucket.Rate);
            }

            bucket.CurrentTokenAmount = Math.Min(capacity, bucket.CurrentTokenAmount);
        }
        else
        {
            bucket.CurrentTokenAmount = capacity;
        }

        bucket.TokenCapacity = capacity;
        bucket.Rate = rate;
        bucket.IsEnable = isEnable;
        bucket.LastUpdatedTime = Context.CurrentBlockTime;
        return bucket;
    }

    public override TokenBucket GetCurrentReceiptTokenBucketState(GetCurrentReceiptTokenBucketStateInput input)
    {
        var tokenBucket = State.ReceiptTokenBucketInfo[input.Symbol][input.TargetChain];
        tokenBucket = GetCurrentTokenBucket(tokenBucket);
        return tokenBucket ?? new TokenBucket();
    }

    public override TokenBucket GetCurrentSwapTokenBucketState(Hash input)
    {
        var tokenBucket = State.SwapTokenBucketInfo[input];
        tokenBucket = GetCurrentTokenBucket(tokenBucket);
        return tokenBucket ?? new TokenBucket();
    }

    public override Int64Value GetReceiptMinWaitTimeInSeconds(GetReceiptMinWaitTimeInSecondsInput input)
    {
        var tokenBucket = State.ReceiptTokenBucketInfo[input.Symbol][input.TargetChain];
        tokenBucket = GetCurrentTokenBucket(tokenBucket);
        return new Int64Value
        {
            Value = GetMinWaitTimeInSeconds(tokenBucket, input.TokenAmount)
        };
    }

    public override Int64Value GetSwapMinWaitTimeInSeconds(GetSwapMinWaitTimeInSecondsInput input)
    {
        var bucket = State.SwapTokenBucketInfo[input.SwapId];
        bucket = GetCurrentTokenBucket(bucket);
        return new Int64Value
        {
            Value = GetMinWaitTimeInSeconds(bucket, input.TokenAmount)
        };
    }

    private long GetMinWaitTimeInSeconds(TokenBucket bucket, long amount)
    {
        long minWaitInSeconds = 0;
        if (bucket != null && amount > bucket.CurrentTokenAmount)
        {
            minWaitInSeconds = amount.Sub(bucket.CurrentTokenAmount).Add(bucket.Rate.Sub(1))
                .Div(bucket.Rate);
        }

        return minWaitInSeconds;
    }
    
    private TokenBucket GetCurrentTokenBucket(TokenBucket tokenBucket)
    {
        if (tokenBucket != null)
        {
            tokenBucket.CurrentTokenAmount = CalculateRefill(tokenBucket.TokenCapacity, tokenBucket.CurrentTokenAmount,
                Context.CurrentBlockTime.Seconds.Sub(tokenBucket.LastUpdatedTime.Seconds), tokenBucket.Rate);
        }

        return tokenBucket;
    }

    #endregion
}