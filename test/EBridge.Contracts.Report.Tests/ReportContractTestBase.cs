using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using EBridge.Contracts.Bridge;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace EBridge.Contracts.Report;

public class ReportContractTestBase : DAppContractTestBase<ReportContractTestModule>
{
    protected Address DefaultSenderAddress { get; set; }
    protected ECKeyPair DefaultKeypair => SampleAccount.Accounts.First().KeyPair;

    internal List<Account> Transmitters => SampleAccount.Accounts.Skip(1).Take(5).ToList();


    internal OracleContractContainer.OracleContractStub OracleContractStub { get; set; }

    internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
    internal RegimentContractContainer.RegimentContractStub RegimentContractStub { get; set; }

    internal ReportContractContainer.ReportContractStub ReportContractStub { get; set; }

    internal List<OracleContractContainer.OracleContractStub> TransmittersOracleContractStubs { get; set; } =
        new List<OracleContractContainer.OracleContractStub>();


    public Address ReportContractAddress { get; set; }
    protected Address OracleContractAddress { get; set; }

    internal Address StringAggregatorContractAddress =>
        GetAddress(StringAggregatorSmartContractAddressNameProvider.StringName);

    internal Address RegimentContractAddress =>
        GetAddress(RegimentSmartContractAddressNameProvider.StringName);
    
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }

    internal readonly Address _regimentAddress =
        Address.FromBase58("2aT9rHLuFRFCHJ1cBSTDR8oD1EFFEBqqiw8fdXD8UuEKRj6Tfh");
    
    public ReportContractTestBase()
    {
        ZeroContractStub = GetContractZeroTester(DefaultKeypair);
        var result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(OracleContract).Assembly.Location))
        }));
        OracleContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        
        result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(ReportContract).Assembly.Location))
        }));
        ReportContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        DefaultSenderAddress = SampleAccount.Accounts.First().Address;
        OracleContractStub = GetOracleContractStub(DefaultKeypair);
        TokenContractStub = GetTokenContractStub(DefaultKeypair);
        RegimentContractStub = GetRegimentContractStub(DefaultKeypair);
        ReportContractStub = GetReportContractStub(DefaultKeypair);

        foreach (var transmitter in Transmitters)
        {
            TransmittersOracleContractStubs.Add(GetOracleContractStub(transmitter.KeyPair));
        }
    }


    internal OracleContractContainer.OracleContractStub
        GetOracleContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<OracleContractContainer.OracleContractStub>(
            OracleContractAddress,
            senderKeyPair);
    }

    internal TokenContractContainer.TokenContractStub
        GetTokenContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<TokenContractContainer.TokenContractStub>(
            TokenContractAddress,
            senderKeyPair);
    }

    internal RegimentContractContainer.RegimentContractStub
        GetRegimentContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<RegimentContractContainer.RegimentContractStub>(
            RegimentContractAddress,
            senderKeyPair);
    }

    internal ReportContractContainer.ReportContractStub GetReportContractStub(
        ECKeyPair senderKeyPair)
    {
        return GetTester<ReportContractContainer.ReportContractStub>(
            ReportContractAddress,
            senderKeyPair
        );
    }
    
    internal async Task InitialReportContractAsync()
    {
        await PortTokenCreate();
        await ReportContractStub.Initialize.SendAsync(new InitializeInput()
        {
            OracleContractAddress = OracleContractAddress,
            RegimentContractAddress = RegimentContractAddress,
            ReportFee = 0,
            ApplyObserverFee = 200_00000000,
            InitialRegisterWhiteList = {DefaultSenderAddress}
        });
    }
    
    internal async Task InitialOracleContractAsync()
    {
        await OracleContractStub.Initialize.SendAsync(new Oracle.InitializeInput
        {
            RegimentContractAddress = RegimentContractAddress
        });
    }
    
    internal async Task PortTokenCreate()
    {
        // Create PORT token.
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            TokenName = "Port Token",
            Decimals = 8,
            Issuer = DefaultSenderAddress,
            IsBurnable = true,
            Symbol = "PORT",
            TotalSupply = 10_00000000_00000000
        });
    
        // Issue PORT token.
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            To = Transmitters.First().Address,
            Amount = 5_00000000_00000000,
            Symbol = "PORT"
        });
        // Issue PORT token.
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            To = DefaultSenderAddress,
            Amount = 5_00000000_00000000,
            Symbol = "PORT"
        });
    
        // Approve Oracle Contract.
        var transmitterTokenContractStub = GetTokenContractStub(Transmitters.First().KeyPair);
        await transmitterTokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = "PORT",
            Amount = 5_00000000_00000000,
            Spender = OracleContractAddress
        });
        // Approve Report Contract.
        var senderTokenContractStub = GetTokenContractStub(DefaultKeypair);
        await senderTokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = "PORT",
            Amount = 5_00000000_00000000,
            Spender = ReportContractAddress
        });
    }
    
    internal ACS0Container.ACS0Stub GetContractZeroTester(
        ECKeyPair keyPair)
    {
        return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress,
            keyPair);
    }
}