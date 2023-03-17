using AElf.Contracts.Association;
using AElf.Contracts.MultiToken;

namespace AElf.Contracts.Regiment;

public partial class RegimentContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

    internal AssociationContractContainer.AssociationContractReferenceState AssociationContract { get; set; }
}