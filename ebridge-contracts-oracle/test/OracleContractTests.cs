using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.Oracle
{
    public partial class OracleContractTests : TestBase
    {
        [Fact]
        public async Task PlayTests()
        {
            // Prepare awards.
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                To = DAppContractAddress,
                Symbol = "ELF",
                Amount = 100
            });

            var result = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = DefaultAddress,
                Symbol = "ELF"
            });
            result.Balance.ShouldNotBe(100);
        }
    }
    
}