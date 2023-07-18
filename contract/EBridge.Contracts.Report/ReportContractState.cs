using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace EBridge.Contracts.Report
{
    public partial class ReportContractState : ContractState
    {
        public SingletonState<Address> Owner { get; set; }

        // public MappedState<string, MethodFees> TransactionFees { get; set; }
        public SingletonState<AuthorityInfo> MethodFeeController { get; set; }
        public SingletonState<string> OracleTokenSymbol { get; set; }

        public SingletonState<string> ObserverMortgageTokenSymbol { get; set; }
        public SingletonState<long> ReportFee { get; set; }
        public SingletonState<long> ApplyObserverFee { get; set; }

        /// <summary>
        /// QueryId -> ReportQueryRecord
        /// </summary>
        public MappedState<Hash, ReportQueryRecord> ReportQueryRecordMap { get; set; }

        /// <summary>
        /// Chain id -> Token(Contract address) -> round Id -> ReportQueryRecord
        /// </summary>
        public MappedState<string, string, long, ReportQueryRecord> ReportRecordMap { get; set; }

        /// <summary>
        /// chain id -> token -> observer address -> signature
        /// </summary>
        public MappedState<string, string, long, Address, string> ObserverSignatureMap { get; set; }

        /// <summary>
        /// chain id -> token -> round id
        /// </summary>
        public MappedState<string, string, long> CurrentRoundIdMap { get; set; }

        /// <summary>
        /// ChainId -> Ethereum Contract Address -> Round Number (Round Id) -> Report.
        /// </summary>
        public MappedState<string, string, long, Report> ReportMap { get; set; }

        /// <summary>
        /// TargetChainId -> Target Contract address.
        /// </summary>
        /// <returns></returns>
        public MappedState<string, string> TargetChainAddressMap { get; set; }

        /// <summary>
        /// chainId -> token -> OffChainAggregationInfo
        /// </summary>
        public MappedState<string, string, OffChainAggregationInfo> OffChainAggregationInfoMap { get; set; }

        /// <summary>
        /// regiment address -> sender -> fee
        /// </summary>
        public MappedState<Address, Address, long> ObserverMortgagedTokensMap { get; set; }

        public MappedState<string, long, BinaryMerkleTree> BinaryMerkleTreeMap { get; set; }

        /// <summary>
        /// Off Chain Aggregation Token -> Round Id -> Node Index -> Observer List
        /// </summary>
        public MappedState<string, long, int, ObserverList> NodeObserverListMap { get; set; }

        public MappedState<Address, long> AmercementAmountMap { get; set; }

        public SingletonState<bool> IsInitialized { get; set; }

        public MappedState<Address, bool> RegisterWhiteListMap { get; set; }

        /// <summary>
        /// Regiment Address -> Observer List
        /// </summary>
        public MappedState<Address, ObserverList> ObserverListMap { get; set; }

        /// <summary>
        /// chain Id -> Token -> Skip confirm member list
        /// </summary>
        public MappedState<string, string, MemberList> SkipMemberListMap { get; set; }
    }
}