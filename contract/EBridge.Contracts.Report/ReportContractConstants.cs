namespace EBridge.Contracts.Report
{
    public partial class ReportContract
    {
        private const long MinimumAmercementAmount = 100_00000000;
        private const int MaximumOffChainQueryInfoCount = 1023;
        private const long DefaultApplyObserverFee = 200_00000000;
        private const string DefaultReceiptInfoKey = "ReceiptInfoKey";
    }
}