using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.Regiment
{
    public partial class RegimentContractTests : TestBase
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
                Owner = DefaultAddress,
                Symbol = "ELF",
            });
            result.Balance.ShouldNotBe(100);
        }
    }
    
}