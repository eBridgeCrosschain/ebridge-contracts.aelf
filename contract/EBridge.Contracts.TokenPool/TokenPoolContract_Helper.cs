using AElf;
using AElf.Types;

namespace EBridge.Contracts.TokenPool;

public partial class TokenPoolContract
{
    private Address CheckParamsAndGetTokenVirtualInfo(string chainId, string symbol,
        out Hash tokenVirtualHash)
    {
        Assert(IsStringValid(chainId), "Invalid chain id.");
        Assert(IsStringValid(symbol), "Invalid symbol.");

        tokenVirtualHash = HashHelper.ConcatAndCompute(
            HashHelper.ComputeFrom(ChainHelper.ConvertChainIdToBase58(Context.ChainId)),
            HashHelper.ComputeFrom(chainId),
            HashHelper.ComputeFrom(symbol));
        var tokenVirtualAddress = Context.ConvertVirtualAddressToContractAddress(tokenVirtualHash);
        return tokenVirtualAddress;
    }

    private bool IsAddressValid(Address input)
    {
        return input != null && !input.Value.IsNullOrEmpty();
    }

    private bool IsStringValid(string input)
    {
        return input != null && !string.IsNullOrWhiteSpace(input);
    }

    private bool IsInitialized()
    {
        return State.IsInitialized.Value;
    }
}