using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace EBridge.Contracts.TokenPool;

public partial class TokenPoolContractState : ContractState
{
    public BoolState IsInitialized { get; set; }
    public SingletonState<Address> Admin { get; set; }

    // token virtual address => liquidity
    public MappedState<Address, long> TokenLiquidity { get; set; }

    // user address => token virtual address => liquidity
    public MappedState<Address, Address, long> LiquidityProviderBalances { get; set; }
}