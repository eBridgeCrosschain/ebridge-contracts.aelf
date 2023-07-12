using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Standards.ACS0;
using AElf.Standards.ACS13;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;

namespace EBridge.Contracts.Report
{
    public partial class ReportContractState
    {
        internal ParliamentContractImplContainer.ParliamentContractImplReferenceState ParliamentContract { get; set; }

        internal OracleContractContainer.OracleContractReferenceState OracleContract { get; set; }

        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

        internal RegimentContractContainer.RegimentContractReferenceState RegimentContract
        {
            get;
            set;
        }

        internal OracleAggregatorContractContainer.OracleAggregatorContractReferenceState AggregatorContract
        {
            get;
            set;
        }
        
        internal ACS0Container.ACS0ReferenceState GensisContract { get; set; }
    }
}