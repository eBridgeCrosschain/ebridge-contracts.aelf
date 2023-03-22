using System.Collections.Generic;
using System.Linq;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Oracle;
using AElf.Contracts.Regiment;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Types;

namespace AElf.Contracts.Report;

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


    internal Address OracleContractAddress => GetAddress(OracleSmartContractAddressNameProvider.StringName);

    internal Address StringAggregatorContractAddress =>
        GetAddress(StringAggregatorSmartContractAddressNameProvider.StringName);

    internal Address RegimentContractAddress =>
        GetAddress(RegimentSmartContractAddressNameProvider.StringName);

    internal Address ReportContractAddress =>
        GetAddress(ReportSmartContractAddressNameProvider.StringName);


    public ReportContractTestBase()
    {
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
}