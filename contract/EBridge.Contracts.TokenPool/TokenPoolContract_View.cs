using System;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.TokenPool;

public partial class TokenPoolContract
{
    public override TokenPoolInfo GetTokenPoolInfo(GetTokenPoolInfoInput input)
    {
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.TokenSymbol,
                out var tokenVirtualHash);
        var liquidity = State.TokenLiquidity[tokenVirtualAddress];
        return new TokenPoolInfo
        {
            TokenVirtualHash = tokenVirtualHash,
            TokenVirtualAddress = tokenVirtualAddress,
            Liquidity = liquidity
        };
    }

    public override Int64Value GetLiquidity(GetLiquidityInput input)
    {
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.TokenSymbol,
                out var tokenVirtualHash);
        return new Int64Value
        {
            Value = State.LiquidityProviderBalances[input.Provider ?? Context.Sender][tokenVirtualAddress]
        };
    }

    public override Int64Value GetRemovableLiquidity(GetLiquidityInput input)
    {
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.TokenSymbol,
                out var tokenVirtualHash);
        var tokenLiquidity = State.TokenLiquidity[tokenVirtualAddress];
        var userLiquidity = State.LiquidityProviderBalances[input.Provider ?? Context.Sender][tokenVirtualAddress];
        var removableLiquidity = Math.Min(tokenLiquidity, userLiquidity);
        return new Int64Value
        {
            Value = removableLiquidity
        };
    }

    public override Address GetAdmin(Empty input)
    {
        return State.Admin.Value;
    }

    public override Address GetBridgeContract(Empty input)
    {
        return State.BridgeContract.Value;
    }
}