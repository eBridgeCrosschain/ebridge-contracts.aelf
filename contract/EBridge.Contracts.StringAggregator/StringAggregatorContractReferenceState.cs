using AElf.Contracts.MultiToken;

namespace EBridge.Contracts.StringAggregator;

public partial class StringAggregatorContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
}