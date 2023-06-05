using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Token;
using AElf.Types;

namespace EBridge.Contracts.Oracle
{
    public partial class OracleContractTests
    {
        // private readonly ECKeyPair KeyPair;
        private readonly OracleContractContainer.OracleContractStub OracleContractStub;
        private readonly TokenContractContainer.TokenContractStub TokenContractStub;
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        public OracleContractTests()
        {
            // KeyPair = SampleAccount.Accounts.First().KeyPair;
            OracleContractStub = GetContractStub<OracleContractContainer.OracleContractStub>(DefaultKeyPair);
            TokenContractStub = GetTester<TokenContractContainer.TokenContractStub>(
                GetAddress(TokenSmartContractAddressNameProvider.StringName), DefaultKeyPair);
        }
    }
    
}