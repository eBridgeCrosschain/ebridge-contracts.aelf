using AElf.Contracts.MerkleTreeContract;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Oracle;
using AElf.Contracts.Parliament;
using AElf.Contracts.Regiment;
using AElf.Contracts.Report;

namespace AElf.Contracts.Bridge;

public partial class BridgeContractState
{
    internal MerkleTreeContractContainer.MerkleTreeContractReferenceState MerkleTreeContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal OracleContractContainer.OracleContractReferenceState OracleContract { get; set; }
    internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }
    internal RegimentContractContainer.RegimentContractReferenceState RegimentContract { get; set; }

    internal ReportContractContainer.ReportContractReferenceState ReportContract { get; set; }
    
}