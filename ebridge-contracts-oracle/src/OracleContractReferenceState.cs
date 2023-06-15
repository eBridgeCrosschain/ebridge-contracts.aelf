using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Contracts.Profit;
using AElf.Standards.ACS13;
using EBridge.Contracts.Regiment;

namespace EBridge.Contracts.Oracle;

public partial class OracleContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

    internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }

    internal AEDPoSContractContainer.AEDPoSContractReferenceState ConsensusContract { get; set; }

    internal ProfitContractContainer.ProfitContractReferenceState ProfitContract { get; set; }

    internal RegimentContractContainer.RegimentContractReferenceState RegimentContract { get; set; }

    internal OracleAggregatorContractContainer.OracleAggregatorContractReferenceState OracleAggregatorContract { get; set; }
}