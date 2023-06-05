using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Token;
using AElf.Types;

namespace EBridge.Contracts.StringAggregator
{
    public partial class StringAggregatorContractTests
    {
        // private readonly ECKeyPair KeyPair;
        private readonly StringAggregatorContractContainer.StringAggregatorContractStub StringAggregatorContractStub;
        private readonly TokenContractContainer.TokenContractStub TokenContractStub;
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        public StringAggregatorContractTests()
        {
            // KeyPair = SampleAccount.Accounts.First().KeyPair;
            StringAggregatorContractStub = GetContractStub<StringAggregatorContractContainer.StringAggregatorContractStub>(DefaultKeyPair);
            TokenContractStub = GetTester<TokenContractContainer.TokenContractStub>(
                GetAddress(TokenSmartContractAddressNameProvider.StringName), DefaultKeyPair);
        }
    }
    
}