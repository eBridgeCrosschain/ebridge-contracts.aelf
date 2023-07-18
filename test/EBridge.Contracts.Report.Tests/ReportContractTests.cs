using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Types;
using EBridge.Contracts.Regiment;
using Shouldly;
using Xunit;
using CreateRegimentInput = EBridge.Contracts.Oracle.CreateRegimentInput;

namespace EBridge.Contracts.Report;

public class ReportContractTests : ReportContractTestBase
{
    [Fact]
    public async Task<(Address, Address, Address)> ApplyObserver_Test()
    {
        await InitialOracleContractAsync();
        await InitialReportContractAsync();
        var beforeBalance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "PORT"
        })).Balance;
        var (regimentAddress1, regimentAddress2, regimentAddress3) = await GetRegimentAddress();
        await ReportContractStub.ApplyObserver.SendAsync(new ApplyObserverInput
        {
            RegimentAddressList = { regimentAddress1, regimentAddress2, regimentAddress3 }
        });
        var fee = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
            new GetObserverMortgagedTokenByRegimentInput
            {
                RegimentAddress = regimentAddress1,
                ObserverAddress = DefaultSenderAddress
            });
        fee.Value.ShouldBe(200_00000000);
        var afterBalance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "PORT"
        })).Balance;
        afterBalance.ShouldBe(beforeBalance - 200_00000000 * 3);
        var totalFee = await ReportContractStub.GetMortgagedTokenAmount.CallAsync(DefaultSenderAddress);
        totalFee.Value.ShouldBe(200_00000000 * 3);
        return (regimentAddress1, regimentAddress2, regimentAddress3);
    }

    [Fact]
    public async Task QuitObserver_Test()
    {
        var (regimentAddress1, regimentAddress2, regimentAddress3) = await ApplyObserver_Test();
        var beforeBalance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "PORT"
        })).Balance;
        await ReportContractStub.QuitObserver.SendAsync(new QuitObserverInput
        {
            RegimentAddressList = { regimentAddress1 }
        });
        var afterBalance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "PORT"
        })).Balance;
        afterBalance.ShouldBe(beforeBalance + 200_00000000);
        var fee = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
            new GetObserverMortgagedTokenByRegimentInput
            {
                RegimentAddress = regimentAddress1,
                ObserverAddress = DefaultSenderAddress
            });
        fee.Value.ShouldBe(0);
        fee = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
            new GetObserverMortgagedTokenByRegimentInput
            {
                RegimentAddress = regimentAddress2,
                ObserverAddress = DefaultSenderAddress
            });
        fee.Value.ShouldBe(200_00000000);
        var totalFee = await ReportContractStub.GetMortgagedTokenAmount.CallAsync(DefaultSenderAddress);
        totalFee.Value.ShouldBe(200_00000000 * 2);
    }

    [Fact]
    public async Task MortgagedToken_Test()
    {
        var (regimentAddress1, regimentAddress2, regimentAddress3) = await ApplyObserver_Test();
        var beforeBalance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "PORT"
        })).Balance;
        await ReportContractStub.MortgageTokens.SendAsync(new MortgageTokensInput
        {
            RegimentAddress = regimentAddress1,
            Amount = 100_00000000
        });
        var afterBalance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "PORT"
        })).Balance;
        afterBalance.ShouldBe(beforeBalance - 100_00000000);
        var fee = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
            new GetObserverMortgagedTokenByRegimentInput
            {
                RegimentAddress = regimentAddress1,
                ObserverAddress = DefaultSenderAddress
            });
        fee.Value.ShouldBe(300_00000000);
        fee = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
            new GetObserverMortgagedTokenByRegimentInput
            {
                RegimentAddress = regimentAddress2,
                ObserverAddress = DefaultSenderAddress
            });
        fee.Value.ShouldBe(200_00000000);
        var totalFee = await ReportContractStub.GetMortgagedTokenAmount.CallAsync(DefaultSenderAddress);
        totalFee.Value.ShouldBe(200_00000000 * 3 + 100_00000000);
    }


    private async Task<(Address, Address, Address)> GetRegimentAddress()
    {
        var executionResult1 = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = DefaultSenderAddress,
            IsApproveToJoin = false,
            InitialMemberList = { Transmitters[0].Address, Transmitters[1].Address, Transmitters[2].Address }
        });
        var regimentAddress1 = RegimentCreated.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        var executionResult2 = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = Transmitters[0].Address,
            IsApproveToJoin = false,
            InitialMemberList = { DefaultSenderAddress, Transmitters[1].Address, Transmitters[3].Address }
        });
        var regimentAddress2 = RegimentCreated.Parser
            .ParseFrom(executionResult2.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        var executionResult3 = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = Transmitters[1].Address,
            IsApproveToJoin = false,
            InitialMemberList = { Transmitters[2].Address, Transmitters[3].Address, Transmitters[4].Address }
        });
        var regimentAddress3 = RegimentCreated.Parser
            .ParseFrom(executionResult3.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        return (regimentAddress1, regimentAddress2, regimentAddress3);
    }
}