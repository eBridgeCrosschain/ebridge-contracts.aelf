using AElf.Sdk.CSharp.State;
using AElf.Standards.ACS1;
using AElf.Types;

namespace AElf.Contracts.StringAggregator
{
    public partial class StringAggregatorContractState : ContractState
    {
        public SingletonState<Address> Owner { get; set; }
        public MappedState<string, MethodFees> TransactionFees { get; set; }
        public SingletonState<AuthorityInfo> MethodFeeController { get; set; }
    }
}