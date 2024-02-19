using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Standards.ACS0;
using EBridge.Contracts.MerkleTreeContract;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.Report;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractState
{
    internal MerkleTreeContractContainer.MerkleTreeContractReferenceState MerkleTreeContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal OracleContractContainer.OracleContractReferenceState OracleContract { get; set; }
    internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }
    internal RegimentContractContainer.RegimentContractReferenceState RegimentContract { get; set; }

    internal ReportContractContainer.ReportContractReferenceState ReportContract { get; set; }

    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
}