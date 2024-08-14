using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using EBridge.Contracts.Bridge;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.TokenPool;

public partial class TokenPoolContract : TokenPoolContractContainer.TokenPoolContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(State.IsInitialized.Value == false, "Already initialized.");
        Assert(IsAddressValid(input.BridgeContractAddress), "Invalid input.");

        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        Assert(State.GenesisContract.GetContractInfo.Call(Context.Self).Deployer == Context.Sender, "No permission.");
        State.BridgeContract.Value = input.BridgeContractAddress;
        State.Admin.Value = input.Admin ?? Context.Sender;
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        State.IsInitialized.Value = true;
        return new Empty();
    }

    public override Empty Lock(LockInput input)
    {
        Assert(IsInitialized(), "Contract has not been initialized.");
        Assert(Context.Sender == State.BridgeContract.Value, "No permission.");
        Assert(input.Amount > 0, "Invalid amount.");
        Assert(IsAddressValid(input.Sender), "Invalid sender.");
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.TargetChainId, input.TargetTokenSymbol,
                out var tokenVirtualHash);
        State.TokenLiquidity[tokenVirtualAddress] += input.Amount;
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            From = State.BridgeContract.Value,
            To = tokenVirtualAddress,
            Amount = input.Amount,
            Symbol = input.TargetTokenSymbol,
            Memo = "bridge lock token to virtual address"
        });
        Context.Fire(new Locked
        {
            Amount = input.Amount,
            FromChainId = ChainHelper.ConvertChainIdToBase58(Context.ChainId),
            ToChainId = input.TargetChainId,
            Sender = input.Sender,
            TargetTokenSymbol = input.TargetTokenSymbol
        });
        return new Empty();
    }

    public override Empty Release(ReleaseInput input)
    {
        Assert(IsInitialized(), "Contract has not been initialized.");
        Assert(Context.Sender == State.BridgeContract.Value, "No permission.");
        Assert(input.Amount > 0, "Invalid amount.");
        Assert(IsAddressValid(input.Receiver), "Invalid receiver.");
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.FromChainId, input.TargetTokenSymbol,
                out var tokenVirtualHash);
        Assert(State.TokenLiquidity[tokenVirtualAddress] >= input.Amount, "Pool liquidity is not enough.");
        State.TokenLiquidity[tokenVirtualAddress] -= input.Amount;
        State.TokenContract.Transfer.VirtualSend(tokenVirtualHash, new TransferInput
        {
            To = input.Receiver,
            Amount = input.Amount,
            Symbol = input.TargetTokenSymbol,
            Memo = "bridge release token to user"
        });
        Context.Fire(new Released
        {
            Amount = input.Amount,
            FromChainId = input.FromChainId,
            ToChainId = ChainHelper.ConvertChainIdToBase58(Context.ChainId),
            Receiver = input.Receiver,
            TargetTokenSymbol = input.TargetTokenSymbol
        });
        return new Empty();
    }

    public override Empty AddLiquidity(AddLiquidityInput input)
    {
        Assert(IsInitialized(), "Contract has not been initialized.");
        Assert(input.Amount > 0, "Invalid amount.");
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.ChainId, input.TokenSymbol,
                out var tokenVirtualHash);
        var whitelist = State.BridgeContract.GetTokenWhitelist.Call(new StringValue
        {
            Value = input.ChainId
        });
        if (whitelist.Equals(new TokenSymbolList()) ||
            whitelist.Symbol.Count <= 0 || !whitelist.Symbol.Contains(input.TokenSymbol))
        {
            throw new AssertionException("Not support.");
        }

        State.LiquidityProviderBalances[Context.Sender][tokenVirtualAddress] =
            State.LiquidityProviderBalances[Context.Sender][tokenVirtualAddress].Add(input.Amount);
        State.TokenLiquidity[tokenVirtualAddress] = State.TokenLiquidity[tokenVirtualAddress].Add(input.Amount);
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            From = Context.Sender,
            To = tokenVirtualAddress,
            Amount = input.Amount,
            Symbol = input.TokenSymbol,
            Memo = "bridge add liquidity"
        });
        Context.Fire(new LiquidityAdded
        {
            ChainId = input.ChainId,
            TokenSymbol = input.TokenSymbol,
            Amount = input.Amount,
            Provider = Context.Sender
        });
        return new Empty();
    }

    public override Empty RemoveLiquidity(RemoveLiquidityInput input)
    {
        Assert(IsInitialized(), "Contract has not been initialized.");
        Assert(input.Amount > 0, "Invalid amount.");
        var tokenVirtualAddress =
            CheckParamsAndGetTokenVirtualInfo(input.ChainId, input.TokenSymbol,
                out var tokenVirtualHash);
        Assert(
            State.TokenLiquidity[tokenVirtualAddress] >= input.Amount &&
            State.LiquidityProviderBalances[Context.Sender][tokenVirtualAddress] >= input.Amount,
            "Not enough liquidity to remove.");
        State.LiquidityProviderBalances[Context.Sender][tokenVirtualAddress]= State.LiquidityProviderBalances[Context.Sender][tokenVirtualAddress].Sub(input.Amount);
        State.TokenLiquidity[tokenVirtualAddress] = State.TokenLiquidity[tokenVirtualAddress].Sub(input.Amount);
        State.TokenContract.Transfer.VirtualSend(tokenVirtualHash, new TransferInput
        {
            To = Context.Sender,
            Amount = input.Amount,
            Symbol = input.TokenSymbol,
            Memo = "bridge remove liquidity"
        });
        Context.Fire(new LiquidityRemoved
        {
            ChainId = input.ChainId,
            TokenSymbol = input.TokenSymbol,
            Amount = input.Amount,
            Provider = Context.Sender
        });
        return new Empty();
    }

    public override Empty SetAdmin(Address input)
    {
        Assert(IsInitialized(), "Contract has not been initialized.");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(IsAddressValid(input), "Invalid input.");
        State.Admin.Value = input;
        return new Empty();
    }

    public override Empty SetBridgeContract(Address input)
    {
        Assert(IsInitialized(), "Contract has not been initialized.");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(IsAddressValid(input), "Invalid input.");
        State.BridgeContract.Value = input;
        return new Empty();
    }
}