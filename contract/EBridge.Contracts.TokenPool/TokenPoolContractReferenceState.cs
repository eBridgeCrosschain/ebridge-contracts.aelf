using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;
using EBridge.Contracts.Bridge;

namespace EBridge.Contracts.TokenPool;

public partial class TokenPoolContractState
{
    internal BridgeContractImplContainer.BridgeContractImplReferenceState BridgeContract { get; set; }
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    
}