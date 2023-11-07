using System;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    #region DailyLimit

    public override Empty SetDailyReceiptLimit(SetDailyReceiptLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.DailyReceiptLimitInfos.Count > 0, "Invalid input.");
        foreach (var dailyReceiptLimitInfo in input.DailyReceiptLimitInfos)
        {
            Assert(
                dailyReceiptLimitInfo.DefaultTokenAmount > 0 &&
                !string.IsNullOrEmpty(dailyReceiptLimitInfo.TargetChain) &&
                !string.IsNullOrEmpty(dailyReceiptLimitInfo.Symbol) &&
                dailyReceiptLimitInfo.StartTime.Seconds % DefaultDailyRefreshTime == 0,
                "Invalid input daily receipt limit info.");
            var symbol = dailyReceiptLimitInfo.Symbol;
            var targetChain = dailyReceiptLimitInfo.TargetChain;
            var dailyLimit = State.DailyReceiptLimit[symbol][targetChain] ?? new DailyLimitTokenInfo();
            dailyLimit = SetDailyLimit(dailyLimit, dailyReceiptLimitInfo.DefaultTokenAmount,
                dailyReceiptLimitInfo.StartTime);
            State.DailyReceiptLimit[symbol][targetChain] = dailyLimit;
            Context.Fire(new DailyReceiptLimitSet
            {
                Symbol = symbol,
                TargetChainId = targetChain,
                DailyReceiptLimit = dailyLimit.DefaultTokenAmount,
                ReceiptRefreshTime = dailyLimit.RefreshTime
            });
        }

        return new Empty();
    }

    public override Empty SetDailySwapLimit(SetDailySwapLimitInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.DailySwapLimitInfos.Count > 0, "Invalid input.");
        foreach (var dailySwapLimitInfo in input.DailySwapLimitInfos)
        {
            Assert(
                dailySwapLimitInfo.DefaultTokenAmount > 0 && dailySwapLimitInfo.SwapId != null &&
                dailySwapLimitInfo.StartTime.Seconds % DefaultDailyRefreshTime == 0,
                "Invalid input daily swap limit info.");
            var swapInfo = State.SwapInfo[dailySwapLimitInfo.SwapId];
            Assert(swapInfo != null, "Invalid swap id.");
            var dailyLimit = State.DailySwapLimit[dailySwapLimitInfo.SwapId] ?? new DailyLimitTokenInfo();
            dailyLimit = SetDailyLimit(dailyLimit, dailySwapLimitInfo.DefaultTokenAmount, dailySwapLimitInfo.StartTime);
            State.DailySwapLimit[dailySwapLimitInfo.SwapId] = dailyLimit;
            Context.Fire(new DailySwapLimitSet
            {
                Symbol = swapInfo?.SwapTargetToken?.Symbol,
                FromChainId = swapInfo?.SwapTargetToken?.FromChainId,
                DailySwapLimit = dailyLimit.DefaultTokenAmount,
                SwapRefreshTime = dailyLimit.RefreshTime
            });
        }

        return new Empty();
    }

    private DailyLimitTokenInfo SetDailyLimit(DailyLimitTokenInfo dailyLimit, long defaultTokenAmount,
        Timestamp startTime)
    {
        var defaultRefreshTime = State.DailyLimitRefreshTime.Value == 0
            ? DefaultDailyRefreshTime
            : State.DailyLimitRefreshTime.Value;
        if (dailyLimit.RefreshTime != null &&
            (dailyLimit.RefreshTime - Context.CurrentBlockTime).Seconds.Div(defaultRefreshTime) <= 0)
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

    public override DailyLimitTokenInfo GetDailyReceiptLimit(GetDailyReceiptLimitInput input)
    {
        return State.DailyReceiptLimit[input.Symbol][input.TargetChain] ?? new DailyLimitTokenInfo();
    }

    public override DailyLimitTokenInfo GetDailySwapLimit(Hash input)
    {
        return State.DailySwapLimit[input] ?? new DailyLimitTokenInfo();
    }

    public override Empty SetDailyLimitRefreshTime(Int64Value input)
    {
        State.DailyLimitRefreshTime.Value = input.Value;
        return new Empty();
    }

    public override Int64Value GetDailyLimitRefreshTime(Empty input)
    {
        return new Int64Value
        {
            Value = State.DailyLimitRefreshTime.Value
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
                ReceiptBucketUpdateTime = Context.CurrentBlockTime
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
                SwapBucketUpdateTime = Context.CurrentBlockTime
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
        if (tokenBucket != null)
        {
            tokenBucket.CurrentTokenAmount = CalculateRefill(tokenBucket.TokenCapacity, tokenBucket.CurrentTokenAmount,
                Context.CurrentBlockTime.Seconds.Sub(tokenBucket.LastUpdatedTime.Seconds), tokenBucket.Rate);
        }

        return tokenBucket ?? new TokenBucket();
    }

    public override TokenBucket GetCurrentSwapTokenBucketState(Hash input)
    {
        var tokenBucket = State.SwapTokenBucketInfo[input];
        if (tokenBucket != null)
        {
            tokenBucket.CurrentTokenAmount = CalculateRefill(tokenBucket.TokenCapacity, tokenBucket.CurrentTokenAmount,
                Context.CurrentBlockTime.Seconds.Sub(tokenBucket.LastUpdatedTime.Seconds), tokenBucket.Rate);
        }

        return tokenBucket ?? new TokenBucket();
    }

    public override Int64Value GetReceiptMinWaitTimeInSeconds(GetReceiptMinWaitTimeInSecondsInput input)
    {
        var bucket = State.ReceiptTokenBucketInfo[input.Symbol][input.TargetChain];
        return new Int64Value
        {
            Value = GetMinWaitTimeInSeconds(bucket, input.TokenAmount)
        };
    }

    public override Int64Value GetSwapMinWaitTimeInSeconds(GetSwapMinWaitTimeInSecondsInput input)
    {
        var bucket = State.SwapTokenBucketInfo[input.SwapId];
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

    #endregion
}