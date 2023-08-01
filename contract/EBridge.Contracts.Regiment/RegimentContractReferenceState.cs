using AElf.Contracts.Association;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace EBridge.Contracts.Regiment;

public partial class RegimentContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

    internal AssociationContractContainer.AssociationContractReferenceState AssociationContract { get; set; }
    
    internal ACS0Container.ACS0ReferenceState GensisContract { get; set; }
}