using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.Association;
using AElf.Standards.ACS1;
using AElf.Standards.ACS3;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.MerkleTreeContract;

public partial class MerkleTreeContractTests
{
    [Fact]
    public async Task SetMethodFeeTest()
    {
        await InitializeMerkleTreeTest();
        await MerkleTreeContractStub.SetMethodFee.SendAsync(new MethodFees
        {
            MethodName = nameof(MerkleTreeContractStub.RecordMerkleTree),
            IsSizeFeeFree = false,
            Fees =
            {
                new MethodFee
                {
                    Symbol = "ELF",
                    BasicFee = 5_00000000
                }
            }
        });
        var fee = await MerkleTreeContractStub.GetMethodFee.CallAsync(new StringValue
        {
            Value = nameof(MerkleTreeContractStub.RecordMerkleTree)
        });
        fee.Fees.First().Symbol.ShouldBe("ELF");
        fee.Fees.First().BasicFee.ShouldBe(5_00000000);
        fee.MethodName.ShouldBe("RecordMerkleTree");
    }

    [Fact]
    public async Task SetMethodFeeTest_Failed_InvalidToken()
    {
        await InitializeMerkleTreeTest();
        {
            var executionResult = await MerkleTreeContractStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
            {
                MethodName = nameof(MerkleTreeContractStub.RecordMerkleTree),
                IsSizeFeeFree = false,
                Fees =
                {
                    new MethodFee
                    {
                        Symbol = "ELF",
                        BasicFee = -1
                    }
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var executionResult = await MerkleTreeContractStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
            {
                MethodName = nameof(MerkleTreeContractStub.RecordMerkleTree),
                IsSizeFeeFree = false,
                Fees =
                {
                    new MethodFee
                    {
                        Symbol = "TEST",
                        BasicFee = 2
                    }
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Token is not found.");
        }
        {
            var executionResult = await UserMerkleTreeContractStub.SetMethodFee.SendWithExceptionAsync(new MethodFees
            {
                MethodName = nameof(UserMerkleTreeContractStub.RecordMerkleTree),
                IsSizeFeeFree = false,
                Fees =
                {
                    new MethodFee
                    {
                        Symbol = "ELF",
                        BasicFee = 2
                    }
                }
            });
            executionResult.TransactionResult.Error.ShouldContain("Unauthorized to set method fee.");
        }
    }

    [Fact]
    public async Task ChangeMethodFeeControllerTest()
    {
        await InitializeMerkleTreeTest();
        var execution = await AssociationContractStub.CreateOrganization.SendAsync(new CreateOrganizationInput
        {
            OrganizationMemberList = new OrganizationMemberList
            {
                OrganizationMembers =
                {
                    UserAddress,
                    UserAddress2
                }
            },
            ProposalReleaseThreshold = new ProposalReleaseThreshold
            {
                MinimalApprovalThreshold = 2,
                MaximalRejectionThreshold = 0,
                MinimalVoteThreshold = 2
            },
            CreationToken = HashHelper.ComputeFrom("test"),
            ProposerWhiteList = new ProposerWhiteList
            {
                Proposers =
                {
                    UserAddress,
                    UserAddress2
                }
            }
        });
        var organizationAddress = (OrganizationCreated.Parser.ParseFrom(execution.TransactionResult.Logs
            .FirstOrDefault(e => e.Name == nameof(OrganizationCreated))?.NonIndexed)).OrganizationAddress;
        {
            var executionResult = await MerkleTreeContractStub.ChangeMethodFeeController.SendWithExceptionAsync(
                new AuthorityInfo
                {
                    ContractAddress = AssociationContractAddress,
                    OwnerAddress = DefaultSenderAddress
                });
            executionResult.TransactionResult.Error.ShouldContain("Invalid authority input.");
        }
        await MerkleTreeContractStub.ChangeMethodFeeController.SendAsync(new AuthorityInfo
        {
            ContractAddress = AssociationContractAddress,
            OwnerAddress = organizationAddress
        });
        var controller = await MerkleTreeContractStub.GetMethodFeeController.CallAsync(new Empty());
        controller.OwnerAddress.ShouldBe(organizationAddress);
        {
            var executionResult = await MerkleTreeContractStub.ChangeMethodFeeController.SendWithExceptionAsync(
                new AuthorityInfo
                {
                    ContractAddress = AssociationContractAddress,
                    OwnerAddress = organizationAddress
                });
            executionResult.TransactionResult.Error.ShouldContain("No permission.");
        }
    }
}