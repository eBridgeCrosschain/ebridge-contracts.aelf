using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.TestContract.ReceiptMaker
{
    public partial class ReceiptMakerContractTests : TestBase
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
                Symbol = "CARD",
                Owner = DefaultAddress
            });
            result.Balance.ShouldNotBe(100);
        }
    }
    
}