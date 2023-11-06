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
                dailyReceiptLimitInfo.DefaultTokenAmount > 0 && dailyReceiptLimitInfo.Chain != null &&
                dailyReceiptLimitInfo.Symbol != null &&
                dailyReceiptLimitInfo.StartTime.Seconds % DefaultDailyRefreshTime == 0, "Invalid input daily receipt limit info.");
            var symbol = dailyReceiptLimitInfo.Symbol;
            var targetChain = dailyReceiptLimitInfo.Chain;
            var dailyLimit = State.DailyReceiptLimit[symbol][targetChain] ?? new DailyLimitTokenInfo();
            dailyLimit = SetDailyLimit(dailyLimit, dailyReceiptLimitInfo.DefaultTokenAmount, dailyReceiptLimitInfo.StartTime);
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
                dailySwapLimitInfo.StartTime.Seconds % DefaultDailyRefreshTime == 0, "Invalid input daily swap limit info.");
            var swapInfo = State.SwapInfo[dailySwapLimitInfo.SwapId];
            Assert(swapInfo != null,"Invalid swap id.");
            var dailyLimit = State.DailySwapLimit[dailySwapLimitInfo.SwapId] ?? new DailyLimitTokenInfo();
            dailyLimit = SetDailyLimit(dailyLimit, dailySwapLimitInfo.DefaultTokenAmount, dailySwapLimitInfo.StartTime);
            
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

    private DailyLimitTokenInfo SetDailyLimit(DailyLimitTokenInfo dailyLimit, long defaultTokenAmount, Timestamp startTime)
    {
        dailyLimit.DefaultTokenAmount = defaultTokenAmount;
        dailyLimit.TokenAmount = defaultTokenAmount;
        dailyLimit.RefreshTime = startTime;
        return dailyLimit;
    }

    public override DailyLimitTokenInfo GetDailyReceiptLimit(GetDailyReceiptLimitInput input)
    {
        return State.DailyReceiptLimit[input.Symbol][input.Chain] ?? new DailyLimitTokenInfo();
    }

    public override DailyLimitTokenInfo GetDailySwapLimit(Hash input)
    {
        return State.DailySwapLimit[input] ?? new DailyLimitTokenInfo();
    }

    #endregion

    #region TokenBucket

    public override Empty ConfigReceiptTokenBucket(ConfigReceiptTokenBucketInput input)
    {
        Assert(Context.Sender == State.Admin.Value,"No permission.");
        Assert(input.ReceiptTokenBucketConfigs.Count > 0 , "Invalid input");
        foreach (var config in input.ReceiptTokenBucketConfigs)
        {
            Assert(config.Chain != null && config.Symbol != null && config.TokenCapacity > 0 && config.Rate > 0,"Invalid receipt bucket config input.");
            var bucketConfig = State.ReceiptTokenBucketInfo[config.Symbol][config.Chain] ?? new TokenBucket();
            bucketConfig = SetTokenBucketConfig(bucketConfig, config.TokenCapacity, config.Rate, config.IsEnable);
            Context.Fire(new ReceiptTokenBucketSet
            {
                Symbol = config.Symbol,
                TargetChainId = config.Chain,
                ReceiptBucketIsEnable = bucketConfig.IsEnable,
                ReceiptCapacity = bucketConfig.TokenCapacity,
                ReceiptRefillRate = bucketConfig.Rate
            });
        }
        return new Empty();
    }
    
    public override Empty ConfigSwapTokenBucket(ConfigSwapTokenBucketInput input)
    {
        Assert(Context.Sender == State.Admin.Value,"No permission.");
        Assert(input.SwapTokenBucketConfigs.Count > 0 , "Invalid input");
        foreach (var config in input.SwapTokenBucketConfigs)
        {
            var swapInfo = State.SwapInfo[config.SwapId];
            Assert(swapInfo != null && config.TokenCapacity > 0 && config.Rate > 0,"Invalid swap bucket config input.");
            var bucketConfig = State.SwapTokenBucketInfo[config.SwapId] ?? new TokenBucket();
            bucketConfig = SetTokenBucketConfig(bucketConfig, config.TokenCapacity, config.Rate, config.IsEnable);
            Context.Fire(new SwapTokenBucketSet
            {
                Symbol = swapInfo?.SwapTargetToken?.Symbol,
                FromChainId = swapInfo?.SwapTargetToken?.FromChainId,
                SwapBucketIsEnable = bucketConfig.IsEnable,
                SwapCapacity = bucketConfig.TokenCapacity,
                SwapRefillRate = bucketConfig.Rate
            });
        }
        return new Empty();
    }
    
    private TokenBucket SetTokenBucketConfig(TokenBucket bucket, long capacity, long rate, bool isEnable)
    {
        bucket.TokenCapacity = capacity;
        bucket.CurrentTokenAmount = capacity;
        bucket.Rate = rate;
        bucket.IsEnable = isEnable;
        bucket.LastUpdatedTime = Context.CurrentBlockTime;
        return bucket;
    }

    public override TokenBucket GetCurrentReceiptTokenBucketState(GetCurrentReceiptTokenBucketStateInput input)
    {
        var tokenBucket = State.ReceiptTokenBucketInfo[input.Symbol][input.Chain];
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
        var bucket = State.ReceiptTokenBucketInfo[input.Symbol][input.Chain];
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

    private long GetMinWaitTimeInSeconds(TokenBucket bucket,long amount)
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