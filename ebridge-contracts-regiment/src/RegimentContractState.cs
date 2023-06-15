using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace EBridge.Contracts.Regiment;

public partial class RegimentContractState : ContractState
{
    // public MappedState<string, MethodFees> TransactionFees { get; set; }
    public SingletonState<AuthorityInfo> MethodFeeController { get; set; }
    public BoolState IsInitialized { get; set; }
    public SingletonState<Address> Controller { get; set; }
    public SingletonState<int> MemberJoinLimit { get; set; }
    public SingletonState<int> RegimentLimit { get; set; }
    public SingletonState<int> MaximumAdminsCount { get; set; }

    public MappedState<Address, RegimentInfo> RegimentInfoMap { get; set; }
    public MappedState<Address, RegimentMemberList> RegimentMemberListMap { get; set; }

    public MappedState<Hash, Address> RegimentIdAddressMap { get; set; }

    public MappedState<Address, Hash> RegimentAddressIdMap { get; set; }
}