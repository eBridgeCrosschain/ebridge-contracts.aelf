using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using EBridge.Contracts.Regiment;
using Shouldly;
using Xunit;
using CreateRegimentInput = EBridge.Contracts.Oracle.CreateRegimentInput;

namespace EBridge.Contracts.Report;

public class ReportContractTests : ReportContractTestBase
{
    [Fact]
    public async Task WithDrawTest()
    {
        await InitialOracleContractAsync();
        await InitialReportContractAsync();
        var regimentAddress = await GetRegimentAddress();

        await ReportContractStub.ApplyObserver.SendAsync(new ApplyObserverInput()
        {
            RegimentAddressList = { regimentAddress }
        });
        var observerList = await ReportContractStub.GetObserverList.CallAsync(regimentAddress);
        observerList.Value.ShouldContain(DefaultSenderAddress);
        
        var executeResult = await ReportContractStub.WithdrawTokens.SendWithExceptionAsync(new WithdrawTokensInput
        {
            Regiment = regimentAddress,
            Amount = 1
        });
        executeResult.TransactionResult.Error.ShouldContain("Sender is an observer for regiment");
    }

    private async Task<Address> GetRegimentAddress()
    {
        var executionResult = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput()
        {
            Manager = DefaultSenderAddress,
            IsApproveToJoin = true
        });
        var regimentAddress = RegimentCreated.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        return regimentAddress;
    }

}