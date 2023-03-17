using AElf.Contracts.MultiToken;
using AElf.Contracts.Regiment;

namespace AElf.Contracts.MerkleTreeContract;

public partial class MerkleTreeContractState
{
    internal RegimentContractContainer.RegimentContractReferenceState RegimentContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

}