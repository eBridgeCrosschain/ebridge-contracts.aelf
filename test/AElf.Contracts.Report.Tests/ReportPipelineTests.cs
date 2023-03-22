namespace AElf.Contracts.Report;

public class ReportPipelineTests : ReportContractTestBase
{
    // private static readonly List<Account> ObserverAccounts = SampleAccount.Accounts.Skip(10).Take(5).ToList();
    // private readonly List<Address> _observerAddresses = ObserverAccounts.Select(a => a.Address).ToList();
    //
    // private List<OracleContractContainer.OracleContractStub> ObserverOracleStubs => ObserverAccounts
    //     .Select(a => GetTester<OracleContractContainer.OracleContractStub>(OracleContractAddress, a.KeyPair)).ToList();
    //
    // private Address _regimentAddress;
    // private Hash _regimentId;
    //
    //
    // [Fact]
    // public async Task PipelineTest()
    // {
    //     await InitializeReport();
    //     
    // }
    //
    // [Fact]
    // public async Task InitializeReport()
    // {
    //     await InitializeOracleTest();
    //     await CreateRegimentTest();
    //     await ReportContractStub.Initialize.SendAsync(new InitializeInput
    //     {
    //         OracleContractAddress = OracleContractAddress,
    //         RegimentContractAddress = RegimentContractAddress,
    //         ReportFee = 1_00000000,
    //         InitialRegisterWhiteList = { DefaultSenderAddress }
    //     });
    //
    // }
    //
    // private async Task InitializeOracleTest()
    // {
    //     await OracleContractStub.InitializeAndCreateToken.SendAsync(new Oracle.InitializeInput
    //     {
    //         RegimentContractAddress = RegimentContractAddress,
    //         IsChargeFee = true,
    //         MinimumOracleNodesCount = 3,
    //         DefaultAggregateThreshold = 2,
    //         DefaultRevealThreshold = 3,
    //         DefaultExpirationSeconds = 300
    //     });
    // }
    //
    // private async Task CreateRegimentTest()
    // {
    //     var executionResult = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
    //     {
    //         InitialMemberList = {_observerAddresses}
    //     });
    //     var logEvent = RegimentCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated)).NonIndexed);
    //     _regimentAddress = logEvent.RegimentAddress;
    //     _regimentId = logEvent.RegimentId;
    // }
}