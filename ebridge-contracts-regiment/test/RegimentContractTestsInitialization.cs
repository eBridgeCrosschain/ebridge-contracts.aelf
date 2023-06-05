using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Token;
using AElf.Types;

namespace EBridge.Contracts.Regiment
{
    public partial class RegimentContractTests
    {
        // private readonly ECKeyPair KeyPair;
        private readonly RegimentContractContainer.RegimentContractStub RegimentContractStub;
        private readonly TokenContractContainer.TokenContractStub TokenContractStub;
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        public RegimentContractTests()
        {
            // KeyPair = SampleAccount.Accounts.First().KeyPair;
            RegimentContractStub = GetContractStub<RegimentContractContainer.RegimentContractStub>(DefaultKeyPair);
            TokenContractStub = GetTester<TokenContractContainer.TokenContractStub>(
                GetAddress(TokenSmartContractAddressNameProvider.StringName), DefaultKeyPair);
        }
    }
    
}