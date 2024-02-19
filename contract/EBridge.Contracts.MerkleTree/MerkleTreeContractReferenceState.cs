using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;
using EBridge.Contracts.Regiment;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContractState
{
    internal RegimentContractContainer.RegimentContractReferenceState RegimentContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
}