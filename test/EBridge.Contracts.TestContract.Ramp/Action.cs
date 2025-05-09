using Google.Protobuf.WellKnownTypes;

namespace AetherLink.Contracts.Ramp;

public class RampContract : RampContractContainer.RampContractBase
{
    public override Empty Send(SendInput input)
    {
        return new Empty();
    }
}