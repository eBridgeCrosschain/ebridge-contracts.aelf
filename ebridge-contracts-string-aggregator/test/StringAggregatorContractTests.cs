using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.StringAggregator
{
    public partial class StringAggregatorContractTests : TestBase
    {
        [Fact]
        public async Task PlayTests()
        {
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                To = DAppContractAddress,
                Symbol = "ELF",
                Amount = 100
            });

            var result = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = "CARD",
                Owner = DefaultAddress
            });
            result.Balance.ShouldNotBe(100);
        }
    }
    
}