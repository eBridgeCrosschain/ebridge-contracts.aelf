using AElf.Contracts.MultiToken;
using EBridge.Contracts.Regiment;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContractState
{
    internal RegimentContractContainer.RegimentContractReferenceState RegimentContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

}