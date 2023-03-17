using System.Linq;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Association;
using AElf.Contracts.ReceiptMakerContract;
using AElf.Contracts.Regiment;
using AElf.Contracts.TestContract.ReceiptMaker;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Types;

namespace AElf.Contracts.MerkleTreeContract;

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
    

    internal Address MerkleTreeContractAddress =>
        GetAddress(MerkleTreeSmartContractAddressNameProvider.StringName);

    internal Address RegimentContractAddress =>
        GetAddress(RegimentSmartContractAddressNameProvider.StringName);

    internal Address ReceiptMakerContractAddress =>
        GetAddress(ReceiptMakerSmartContractAddressNameProvider.StringName);


    public MerkleTreeContractTestBase()
    {
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
}