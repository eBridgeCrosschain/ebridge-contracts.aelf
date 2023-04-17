using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace EBridge.Contracts.StringAggregator
{
    public partial class StringAggregatorContractState : ContractState
    {
        public SingletonState<Address> Owner { get; set; }
        // public MappedState<string, MethodFees> TransactionFees { get; set; }
        public SingletonState<AuthorityInfo> MethodFeeController { get; set; }
    }
}