using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Token;
using AElf.Types;

namespace EBridge.Contracts.TestContract.ReceiptMaker
{
    public partial class ReceiptMakerContractTests
    {
        // private readonly ECKeyPair KeyPair;
        private readonly ReceiptMakerContractContainer.ReceiptMakerContractStub ReceiptMakerContractStub;
        private readonly TokenContractContainer.TokenContractStub TokenContractStub;
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        public ReceiptMakerContractTests()
        {
            // KeyPair = SampleAccount.Accounts.First().KeyPair;
            ReceiptMakerContractStub = GetContractStub<ReceiptMakerContractContainer.ReceiptMakerContractStub>(DefaultKeyPair);
            TokenContractStub = GetTester<TokenContractContainer.TokenContractStub>(
                GetAddress(TokenSmartContractAddressNameProvider.StringName), DefaultKeyPair);
        }
    }
    
}