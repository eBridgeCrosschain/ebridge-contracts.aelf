namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    private const int DefaultMaximalLeafCount = 1024;
    private const long QueryPayment = 0;
    private const string DefaultFeeSymbol = "ELF";
    private const int PriceDecimals = 8;
    private const long DefaultDailyRefreshTime = 86400;
}