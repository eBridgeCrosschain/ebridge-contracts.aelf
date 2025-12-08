using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Standards.ACS0;
using AetherLink.Contracts.Ramp;
using EBridge.Contracts.TokenPool;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractState
{
    
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }

    internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }

    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }

    internal TokenPoolContractContainer.TokenPoolContractReferenceState TokenPoolContract { get; set; }
    
    internal RampContractContainer.RampContractReferenceState RampContract { get; set; }
}