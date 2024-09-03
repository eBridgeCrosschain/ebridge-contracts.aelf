using System.IO;
using System.Linq;
using AElf;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using EBridge.Contracts.Bridge;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace EBridge.Contracts.TokenPool;

public class TokenPoolContractTestBase: DAppContractTestBase<TokenPoolContractTestModule>
{
    protected Address DefaultSenderAddress { get; set; }
    protected Address User1Address { get; set; }

    protected ECKeyPair DefaultKeypair => SampleAccount.Accounts.First().KeyPair;
    protected ECKeyPair User1Keypair => SampleAccount.Accounts[1].KeyPair;

    
    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    internal BridgeContractContainer.BridgeContractStub BridgeContractStub { get; set; }
    internal BridgeContractImplContainer.BridgeContractImplStub BridgeContractImplStub { get; set; }
    internal TokenPoolContractContainer.TokenPoolContractStub TokenPoolContractStub { get; set; }
    internal TokenPoolContractContainer.TokenPoolContractStub TokenPoolContractStub1 { get; set; }
    internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }


    protected Address TokenPoolContractAddress { get; set; }
    protected Address BridgeContractAddress { get; set; }

    public TokenPoolContractTestBase()
        {
            DefaultSenderAddress = SampleAccount.Accounts.First().Address;
            User1Address = SampleAccount.Accounts[1].Address;
            ZeroContractStub = GetContractZeroTester(DefaultKeypair);
            var code = File.ReadAllBytes(typeof(TokenPoolContract).Assembly.Location);
            var contractOperation = new ContractOperation
            {
                ChainId = 9992731,
                CodeHash = HashHelper.ComputeFrom(code),
                Deployer = DefaultSenderAddress,
                Salt = HashHelper.ComputeFrom("tokenpool"),
                Version = 1
            };
            contractOperation.Signature = GenerateContractSignature(DefaultKeypair.PrivateKey, contractOperation);

            var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
                new ContractDeploymentInput
                {
                    Category = KernelConstants.CodeCoverageRunnerCategory,
                    Code = ByteString.CopyFrom(code),
                    ContractOperation = contractOperation
                }));
            TokenPoolContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
            result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
            {   
                Category = KernelConstants.CodeCoverageRunnerCategory,
                Code = ByteString.CopyFrom(
                    File.ReadAllBytes(typeof(BridgeContract).Assembly.Location))
            }));
            BridgeContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
            TokenPoolContractStub = GetTokenPoolContractStub(DefaultKeypair);
            TokenPoolContractStub1 = GetTokenPoolContractStub(User1Keypair);
            TokenContractStub = GetTokenContractStub(DefaultKeypair);
            BridgeContractStub = GetBridgeContractStub(DefaultKeypair);
        }
    
    internal ACS0Container.ACS0Stub GetContractZeroTester(
        ECKeyPair keyPair)
    {
        return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress,
            keyPair);
    }
    internal TokenPoolContractContainer.TokenPoolContractStub
        GetTokenPoolContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<TokenPoolContractContainer.TokenPoolContractStub>(
            TokenPoolContractAddress,
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
    
    private ByteString GenerateContractSignature(byte[] privateKey, ContractOperation contractOperation)
    {
        var dataHash = HashHelper.ComputeFrom(contractOperation);
        var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
        return ByteStringHelper.FromHexString(signature.ToHex());
    }
    
    internal BridgeContractContainer.BridgeContractStub
        GetBridgeContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<BridgeContractContainer.BridgeContractStub>(
            BridgeContractAddress,
            senderKeyPair);
    }
}