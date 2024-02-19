using System.IO;
using System.Linq;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Association;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.TestContract.ReceiptMaker;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace EBridge.Contracts.MerkleTreeContract;

public class MerkleTreeContractTestBase : DAppContractTestBase<MerkleTreeContractTestModule>
{
    protected Address DefaultSenderAddress { get; set; }
    protected ECKeyPair DefaultKeypair => SampleAccount.Accounts.First().KeyPair;

    protected ECKeyPair UserKeyPair => SampleAccount.Accounts[1].KeyPair;

    protected Address UserAddress => SampleAccount.Accounts[1].Address;
    protected Address UserAddress2 => SampleAccount.Accounts[2].Address;
    internal RegimentContractContainer.RegimentContractStub RegimentContractStub { get; set; }

    internal MerkleTreeContractContainer.MerkleTreeContractStub MerkleTreeContractStub { get; set; }

    internal MerkleTreeContractContainer.MerkleTreeContractStub UserMerkleTreeContractStub { get; set; }

    internal ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub ReceiptMakerContractImplStub { get; set; }
    internal AssociationContractContainer.AssociationContractStub AssociationContractStub { get; set; }

    internal AssociationContractImplContainer.AssociationContractImplStub AssociationContractImplStub { get; set; }
    protected Address MerkleTreeContractAddress { get; set; }
    
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }

    internal Address RegimentContractAddress =>
        GetAddress(RegimentSmartContractAddressNameProvider.StringName);

    internal Address ReceiptMakerContractAddress =>
        GetAddress(ReceiptMakerSmartContractAddressNameProvider.StringName);


    public MerkleTreeContractTestBase()
    {
        ZeroContractStub = GetContractZeroTester(DefaultKeypair);
        var result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(MerkleTreeContract).Assembly.Location))
        }));
        MerkleTreeContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        DefaultSenderAddress = SampleAccount.Accounts.First().Address;
        MerkleTreeContractStub = GetMerkleTreeContractStub(DefaultKeypair);
        UserMerkleTreeContractStub = GetMerkleTreeContractStub(UserKeyPair);
        RegimentContractStub = GetRegimentContractStub(DefaultKeypair);
        ReceiptMakerContractImplStub = GetReceiptMakerContractStub(DefaultKeypair);
        AssociationContractStub = GetAssociationContractStub(DefaultKeypair);
        AssociationContractImplStub = GetAssociationContractImplStub(DefaultKeypair);
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

    internal MerkleTreeContractContainer.MerkleTreeContractStub
        GetMerkleTreeContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<MerkleTreeContractContainer.MerkleTreeContractStub>(
            MerkleTreeContractAddress,
            senderKeyPair);
    }

    internal RegimentContractContainer.RegimentContractStub
        GetRegimentContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<RegimentContractContainer.RegimentContractStub>(
            RegimentContractAddress,
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
    
    internal ACS0Container.ACS0Stub GetContractZeroTester(
        ECKeyPair keyPair)
    {
        return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress,
            keyPair);
    }
}