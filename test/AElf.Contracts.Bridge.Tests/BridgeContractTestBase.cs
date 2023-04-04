using System.Collections.Generic;
using System.Linq;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Association;
using AElf.Contracts.MerkleTreeContract;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Oracle;
using AElf.Contracts.Parliament;
using AElf.Contracts.Regiment;
using AElf.Contracts.Report;
using AElf.Contracts.TestContract.ReceiptMaker;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Proposal;
using AElf.Types;
using Microsoft.Extensions.DependencyInjection;

namespace AElf.Contracts.Bridge;

public class BridgeContractTestBase : DAppContractTestBase<BridgeContractTestModule>
{
    protected Address DefaultSenderAddress { get; set; }
    protected ECKeyPair DefaultKeypair => SampleAccount.Accounts.First().KeyPair;

    internal List<Account> Transmitters => SampleAccount.Accounts.Skip(1).Take(5).ToList();

    internal List<Account> Receivers => SampleAccount.Accounts.Skip(6).Take(5).ToList();

    internal List<Account> Lockers => SampleAccount.Accounts.Skip(11).Take(3).ToList();

    internal Address TransactionFeeRatioAddress { get; set; }
    internal ECKeyPair TransactionFeeRatio => SampleAccount.Accounts[14].KeyPair;

    internal List<Account> RestartNodes => SampleAccount.Accounts.Skip(15).Take(5).ToList();

    protected IBlockTimeProvider BlockTimeProvider =>
        Application.ServiceProvider.GetRequiredService<IBlockTimeProvider>();

    internal AssociationContractContainer.AssociationContractStub AssociationContractStub { get; set; }

    internal AssociationContractImplContainer.AssociationContractImplStub AssociationContractImplStub { get; set; }

    internal BridgeContractContainer.BridgeContractStub BridgeContractStub { get; set; }

    internal BridgeContractImplContainer.BridgeContractImplStub BridgeContractImplStub { get; set; }

    internal BridgeContractImplContainer.BridgeContractImplStub BridgeContractImplUserStub { get; set; }


    internal BridgeContractContainer.BridgeContractStub BridgeContractSetFeeRatioStub { get; set; }

    internal ReportContractContainer.ReportContractStub ReportContractStub { get; set; }
    internal OracleContractContainer.OracleContractStub OracleContractStub { get; set; }

    internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
    internal TokenContractContainer.TokenContractStub TokenContractStub2 { get; set; }
    internal RegimentContractContainer.RegimentContractStub RegimentContractStub { get; set; }

    internal ParliamentContractContainer.ParliamentContractStub ParliamentContractStub { get; set; }

    internal MerkleTreeContractContainer.MerkleTreeContractStub MerkleTreeContractStub { get; set; }

    internal List<OracleContractContainer.OracleContractStub> TransmittersOracleContractStubs { get; set; } =
        new List<OracleContractContainer.OracleContractStub>();

    internal List<ReportContractContainer.ReportContractStub> TransmittersReportContractStubs { get; set; } =
        new List<ReportContractContainer.ReportContractStub>();


    internal List<BridgeContractContainer.BridgeContractStub> ReceiverBridgeContractStubs { get; set; } =
        new List<BridgeContractContainer.BridgeContractStub>();

    internal List<BridgeContractContainer.BridgeContractStub> LockBridgeContractStubs { get; set; } =
        new List<BridgeContractContainer.BridgeContractStub>();

    internal List<AssociationContractImplContainer.AssociationContractImplStub>
        AssociationContractImplStubs { get; set; } =
        new List<AssociationContractImplContainer.AssociationContractImplStub>();

    internal ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub ReceiptMakerContractImplStub { get; set; }

    internal Address BridgeContractAddress => GetAddress(BridgeSmartContractAddressNameProvider.StringName);

    internal Address ReportContractAddress => GetAddress(ReportSmartContractAddressNameProvider.StringName);

    internal Address OracleContractAddress => GetAddress(OracleSmartContractAddressNameProvider.StringName);

    internal Address StringAggregatorContractAddress =>
        GetAddress(StringAggregatorSmartContractAddressNameProvider.StringName);

    internal Address MerkleTreeContractAddress =>
        GetAddress(MerkleTreeSmartContractAddressNameProvider.StringName);

    internal Address RegimentContractAddress =>
        GetAddress(RegimentSmartContractAddressNameProvider.StringName);

    internal Address ParliamentContractAddress =>
        GetAddress(ParliamentSmartContractAddressNameProvider.StringName);

    internal Address ReceiptMakerContractAddress =>
        GetAddress(ReceiptMakerSmartContractAddressNameProvider.StringName);


    public BridgeContractTestBase()
    {
        DefaultSenderAddress = SampleAccount.Accounts.First().Address;
        TransactionFeeRatioAddress = SampleAccount.Accounts[14].Address;
        BridgeContractStub = GetBridgeContractStub(DefaultKeypair);
        BridgeContractImplStub = GetBridgeContractImplStub(DefaultKeypair);
        BridgeContractImplUserStub = GetTester<BridgeContractImplContainer.BridgeContractImplStub>(
            BridgeContractAddress,
            TransactionFeeRatio);
        BridgeContractSetFeeRatioStub = GetBridgeContractStub(TransactionFeeRatio);
        ReportContractStub = GetReportContractStub(DefaultKeypair);
        OracleContractStub = GetOracleContractStub(DefaultKeypair);
        TokenContractStub = GetTokenContractStub(DefaultKeypair);
        TokenContractStub2 = GetTokenContractStub(Lockers[0].KeyPair);
        MerkleTreeContractStub = GetMerkleTreeContractStub(DefaultKeypair);
        RegimentContractStub = GetRegimentContractStub(DefaultKeypair);
        ParliamentContractStub = GetParliamentContractStub(DefaultKeypair);
        ReceiptMakerContractImplStub = GetReceiptMakerContractStub(DefaultKeypair);
        AssociationContractStub = GetAssociationContractStub(DefaultKeypair);
        AssociationContractImplStub = GetAssociationContractImplStub(DefaultKeypair);

        foreach (var transmitter in Transmitters)
        {
            TransmittersOracleContractStubs.Add(GetOracleContractStub(transmitter.KeyPair));
        }

        foreach (var transmitter in Transmitters)
        {
            TransmittersReportContractStubs.Add(GetReportContractStub(transmitter.KeyPair));
        }

        foreach (var receiver in Receivers)
        {
            ReceiverBridgeContractStubs.Add(GetBridgeContractStub(receiver.KeyPair));
        }

        foreach (var locker in Lockers)
        {
            LockBridgeContractStubs.Add(GetBridgeContractStub(locker.KeyPair));
        }

        foreach (var node in RestartNodes)
        {
            AssociationContractImplStubs.Add(GetAssociationContractImplStub(node.KeyPair));
        }
    }

    internal MerkleTreeContractContainer.MerkleTreeContractStub
        GetMerkleTreeContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<MerkleTreeContractContainer.MerkleTreeContractStub>(
            MerkleTreeContractAddress,
            senderKeyPair);
    }

    internal BridgeContractContainer.BridgeContractStub
        GetBridgeContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<BridgeContractContainer.BridgeContractStub>(
            BridgeContractAddress,
            senderKeyPair);
    }

    internal BridgeContractImplContainer.BridgeContractImplStub
        GetBridgeContractImplStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<BridgeContractImplContainer.BridgeContractImplStub>(
            BridgeContractAddress,
            senderKeyPair);
    }

    internal ReportContractContainer.ReportContractStub
        GetReportContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<ReportContractContainer.ReportContractStub>(
            ReportContractAddress,
            senderKeyPair);
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

    internal ParliamentContractContainer.ParliamentContractStub
        GetParliamentContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<ParliamentContractContainer.ParliamentContractStub>(
            ParliamentContractAddress,
            senderKeyPair);
    }

    internal AssociationContractContainer.AssociationContractStub
        GetAssociationContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<AssociationContractContainer.AssociationContractStub>(
            AssociationContractAddress,
            senderKeyPair);
    }

    internal AssociationContractImplContainer.AssociationContractImplStub
        GetAssociationContractImplStub(ECKeyPair senderKeyPair)
    {
        return GetTester<AssociationContractImplContainer.AssociationContractImplStub>(
            AssociationContractAddress,
            senderKeyPair);
    }

    internal ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub GetReceiptMakerContractStub(
        ECKeyPair senderKeyPair)
    {
        return GetTester<ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub>(
            ReceiptMakerContractAddress,
            senderKeyPair
        );
    }
}