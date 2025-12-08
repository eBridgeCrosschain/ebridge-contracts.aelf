// using System.Linq;
// using System.Threading.Tasks;
// using AElf.Standards.ACS1;
// using Google.Protobuf.WellKnownTypes;
// using Shouldly;
// using Xunit;
//
// namespace EBridge.Contracts.Bridge;
//
// public partial class BridgeContractTests
// {
//     [Fact]
//     public async Task SetMethodFeeTest()
//     {
//         await InitialBridgeContractAsync();
//         await BridgeContractImplStub.SetMethodFee.SendAsync(new MethodFees
//         {
//             MethodName = nameof(BridgeContractStub.CreateReceipt),
//             IsSizeFeeFree = false,
//             Fees =
//             {
//                 new MethodFee
//                 {
//                     Symbol = "ELF",
//                     BasicFee = 5_00000000
//                 }
//             }
//         });
//         var fee = await BridgeContractImplStub.GetMethodFee.CallAsync(new StringValue
//         {
//             Value = nameof(BridgeContractStub.CreateReceipt)
//         });
//         fee.Fees.First().Symbol.ShouldBe("ELF");
//         fee.Fees.First().BasicFee.ShouldBe(5_00000000);
//         fee.MethodName.ShouldBe("CreateReceipt");
//     }
//
//     [Fact]
//     public async Task SetMethodFeeTest_Failed_InvalidToken()
//     {
//         await InitialBridgeContractAsync();
//         {
//             var executionResult = await BridgeContractImplStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
//             {
//                 MethodName = nameof(BridgeContractStub.CreateReceipt),
//                 IsSizeFeeFree = false,
//                 Fees =
//                 {
//                     new MethodFee
//                     {
//                         Symbol = "ELF",
//                         BasicFee = -1
//                     }
//                 }
//             });
//             executionResult.TransactionResult.Error.ShouldContain("Invalid amount.");
//         }
//         {
//             var executionResult = await BridgeContractImplStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
//             {
//                 MethodName = nameof(BridgeContractStub.CreateReceipt),
//                 IsSizeFeeFree = false,
//                 Fees =
//                 {
//                     new MethodFee
//                     {
//                         Symbol = "TEST",
//                         BasicFee = 2
//                     }
//                 }
//             });
//             executionResult.TransactionResult.Error.ShouldContain("Token is not found.");
//         }
//         {
//             var executionResult = await BridgeContractImplUserStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
//             {
//                 MethodName = nameof(BridgeContractStub.CreateReceipt),
//                 IsSizeFeeFree = false,
//                 Fees =
//                 {
//                     new MethodFee
//                     {
//                         Symbol = "ELF",
//                         BasicFee = 2
//                     }
//                 }
//             });
//             executionResult.TransactionResult.Error.ShouldContain("Unauthorized to set method fee.");
//         }
//     }
//
//     [Fact]
//     public async Task ChangeMethodFeeControllerTest()
//     {
//         var organizationAddress = await InitialBridgeContractAsync();
//         {
//             var executionResult = await BridgeContractImplStub.ChangeMethodFeeController.SendWithExceptionAsync(
//                 new AuthorityInfo
//                 {
//                     ContractAddress = AssociationContractAddress,
//                     OwnerAddress = DefaultSenderAddress
//                 });
//             executionResult.TransactionResult.Error.ShouldContain("Invalid authority input.");
//         }
//         await BridgeContractImplStub.ChangeMethodFeeController.SendAsync(new AuthorityInfo
//         {
//             ContractAddress = AssociationContractAddress,
//             OwnerAddress = organizationAddress.Item1
//         });
//         var controller = await BridgeContractImplStub.GetMethodFeeController.CallAsync(new Empty());
//         controller.OwnerAddress.ShouldBe(organizationAddress.Item1);
//         {
//             var executionResult = await BridgeContractImplStub.ChangeMethodFeeController.SendWithExceptionAsync(
//                 new AuthorityInfo
//                 {
//                     ContractAddress = AssociationContractAddress,
//                     OwnerAddress = organizationAddress.Item1
//                 });
//             executionResult.TransactionResult.Error.ShouldContain("No permission.");
//         }
//     }
// }