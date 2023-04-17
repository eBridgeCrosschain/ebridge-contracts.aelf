using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.Association;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CSharp.Core.Extension;
using AElf.Standards.ACS3;
using AElf.Types;
using EBridge.Contracts.Regiment;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using AddAdminsInput = EBridge.Contracts.Oracle.AddAdminsInput;
using CreateRegimentInput = EBridge.Contracts.Oracle.CreateRegimentInput;
using DeleteAdminsInput = EBridge.Contracts.Oracle.DeleteAdminsInput;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests : BridgeContractTestBase
{
    private readonly Address _regimentAddress =
        Address.FromBase58("2aT9rHLuFRFCHJ1cBSTDR8oD1EFFEBqqiw8fdXD8UuEKRj6Tfh");

    private Hash _swapHashOfElf;
    private Hash _swapHashOfUsdt;
    private Hash _swapOfElfSpaceId;
    private Hash _swapOfUsdtSpaceId;

    private Dictionary<string, Hash> _receiptDictionary;

    #region Permission

    [Fact]
    public async Task Initialize_Duplicate()
    {
        await InitialBridgeContractAsync();
        var executionResult = await BridgeContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            MerkleTreeContractAddress = MerkleTreeContractAddress,
            OracleContractAddress = OracleContractAddress,
            RegimentContractAddress = RegimentContractAddress,
            ReportContractAddress = ReportContractAddress,
            Admin = DefaultSenderAddress,
            Controller = DefaultSenderAddress
        });
        executionResult.TransactionResult.Error.ShouldContain("Already initialized.");
    }

    [Fact]
    public async Task ChangeControllerTest()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeController.SendAsync(SampleAccount.Accounts[5].Address);
        var controller = await BridgeContractStub.GetContractController.CallAsync(new Empty());
        controller.ShouldBe(SampleAccount.Accounts[5].Address);
    }

    [Fact]
    public async Task ChangeControllerTest_NoPermission()
    {
        await ChangeControllerTest();
        var result =
            await BridgeContractSetFeeRatioStub.ChangeController.SendWithExceptionAsync(SampleAccount.Accounts[5].Address);
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ChangeAdminTest()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeAdmin.SendAsync(SampleAccount.Accounts[5].Address);
        var admin = await BridgeContractStub.GetContractAdmin.CallAsync(new Empty());
        admin.ShouldBe(SampleAccount.Accounts[5].Address);
    }

    [Fact]
    public async Task ChangeAdminTest_NoPermission()
    {
        await ChangeAdminTest();
        var result = await BridgeContractStub.ChangeAdmin.SendWithExceptionAsync(SampleAccount.Accounts[5].Address);
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ChangeFeeFloatingController()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeTransactionFeeController.SendAsync(new AuthorityInfo
        {
            OwnerAddress = SampleAccount.Accounts[5].Address
        });
        var controller = await BridgeContractStub.GetTransactionFeeRatioController.CallAsync(new Empty());
        controller.OwnerAddress.ShouldBe(SampleAccount.Accounts[5].Address);
    }

    [Fact]
    public async Task ChangeFeeFloatingController_NoPermission()
    {
        await InitialBridgeContractAsync();
        var result = await BridgeContractSetFeeRatioStub.ChangeTransactionFeeController.SendWithExceptionAsync(
            new AuthorityInfo
            {
                OwnerAddress = SampleAccount.Accounts[5].Address
            });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ChangePauseController()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangePauseController.SendAsync(SampleAccount.Accounts[5].Address);
        var controller = await BridgeContractStub.GetPauseController.CallAsync(new Empty());
        controller.ShouldBe(SampleAccount.Accounts[5].Address);
    }

    [Fact]
    public async Task ChangePauseController_NoPermission()
    {
        await ChangePauseController();
        var executionResult =
            await BridgeContractStub.ChangePauseController.SendWithExceptionAsync(SampleAccount.Accounts[5].Address);
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ChangeRestartOrganization()
    {
        await InitialBridgeContractAsync();
        var organizationSecond = await CreateOrganizationSecondTest();
        await BridgeContractStub.ChangeRestartOrganization.SendAsync(organizationSecond.Item2);
        var controller = await BridgeContractStub.GetRestartOrganization.CallAsync(new Empty());
        controller.ShouldBe(organizationSecond.Item2);
    }

    [Fact]
    public async Task ChangeRestartOrganization_NotExist()
    {
        await InitialBridgeContractAsync();
        var executionResult =
            await BridgeContractStub.ChangeRestartOrganization.SendWithExceptionAsync(Transmitters[0].Address);
        executionResult.TransactionResult.Error.ShouldContain("Organization is not exist.");
    }

    [Fact]
    public async Task ChangeRestartOrganization_NoPermission()
    {
        await InitialBridgeContractAsync();
        var organizationSecond = await CreateOrganizationSecondTest();
        var execution =
            await BridgeContractSetFeeRatioStub.ChangeRestartOrganization.SendWithExceptionAsync(organizationSecond
                .Item2);
        execution.TransactionResult.Error.ShouldContain("No permission.");
    }

    #endregion

    #region Regiment

    [Fact]
    public async Task Regiment_AddAdminTest()
    {
        await InitialOracleContractAsync();
        await CreateRegimentTest();
        await OracleContractStub.AddAdmins.SendAsync(new AddAdminsInput
        {
            RegimentAddress = _regimentAddress,
            OriginSenderAddress = DefaultSenderAddress,
            NewAdmins = {Lockers[0].Address}
        });
        var memberList = await RegimentContractStub.GetRegimentMemberList.CallAsync(_regimentAddress);
        memberList.Value.Count.ShouldBe(8);
        memberList.Value.ShouldContain(Lockers[0].Address);
        var regimentInfo = await RegimentContractStub.GetRegimentInfo.CallAsync(_regimentAddress);
        regimentInfo.Admins.Count.ShouldBe(2);
        regimentInfo.Admins[1].ShouldBe(Lockers[0].Address);
    }

    [Fact]
    public async Task Regiment_RemoveAdminTest()
    {
        await Regiment_AddAdminTest();
        await OracleContractStub.DeleteAdmins.SendAsync(new DeleteAdminsInput
        {
            RegimentAddress = _regimentAddress,
            OriginSenderAddress = DefaultSenderAddress,
            DeleteAdmins = {Lockers[0].Address}
        });
        var memberList = await RegimentContractStub.GetRegimentMemberList.CallAsync(_regimentAddress);
        memberList.Value.Count.ShouldBe(7);
        memberList.Value.ShouldNotContain(Lockers[0].Address);
        var regimentInfo = await RegimentContractStub.GetRegimentInfo.CallAsync(_regimentAddress);
        regimentInfo.Admins.Count.ShouldBe(1);
        regimentInfo.Admins[0].ShouldBe(BridgeContractAddress);
    }

    #endregion

    #region Pause/Restart

    [Fact]
    public async Task<(Address, Address)> PauseContract_Test()
    {
        var organizationAddress = await InitialBridgeContractAsync();
        await BridgeContractStub.Pause.SendAsync(new Empty());
        var state = await BridgeContractStub.IsContractPause.CallAsync(new Empty());
        state.Value.ShouldBe(true);
        return organizationAddress;
    }

    [Fact]
    public async Task PauseContract_Test_AlreadyPaused()
    {
        await PauseContract_Test();
        var execution = await BridgeContractStub.Pause.SendWithExceptionAsync(new Empty());
        execution.TransactionResult.Error.ShouldContain("Contract has already been paused.");
    }

    [Fact]
    public async Task PauseContract_Test_NoPermission()
    {
        await InitialBridgeContractAsync();
        var execution = await BridgeContractSetFeeRatioStub.Pause.SendWithExceptionAsync(new Empty());
        execution.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task RestartContract_Test()
    {
        var organizationAddress = await PauseContract_Test();
        {
            var execution = await BridgeContractStub.Restart.SendWithExceptionAsync(new Empty());
            execution.TransactionResult.Error.ShouldContain("No Permission.");
        }
        var proposalId = await ProposalToRestartContract(organizationAddress);
        await AssociationContractImplStub.Release.SendAsync(proposalId);
        var state = await BridgeContractStub.IsContractPause.CallAsync(new Empty());
        state.Value.ShouldBe(false);
        {
            var proposalId1 = await ProposalToRestartContract(organizationAddress);
            var execution = await AssociationContractImplStub.Release.SendWithExceptionAsync(proposalId1);
            execution.TransactionResult.Error.ShouldContain("Contract has already been started.");
        }
    }

    private async Task<Hash> ProposalToRestartContract((Address, Address) organizationAddress)
    {
        var executionResult = await AssociationContractImplStub.CreateProposal.SendAsync(new CreateProposalInput
        {
            ContractMethodName = nameof(BridgeContractStub.Restart),
            ToAddress = BridgeContractAddress,
            OrganizationAddress = organizationAddress.Item2,
            ExpiredTime = BlockTimeProvider.GetBlockTime().AddDays(2)
        });
        var proposalId = (ProposalCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .FirstOrDefault(e => e.Name == nameof(ProposalCreated))?.NonIndexed)).ProposalId;
        {
            var executionResult1 = await AssociationContractImplStubs[0].CreateProposal.SendAsync(
                new CreateProposalInput
                {
                    ContractMethodName = nameof(AssociationContractImplStub.Approve),
                    ToAddress = AssociationContractAddress,
                    OrganizationAddress = organizationAddress.Item1,
                    Params = proposalId.ToByteString(),
                    ExpiredTime = BlockTimeProvider.GetBlockTime().AddDays(2)
                });
            var proposalId1 = (ProposalCreated.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                .FirstOrDefault(e => e.Name == nameof(ProposalCreated))?.NonIndexed)).ProposalId;
            foreach (var associationContractImplStub in AssociationContractImplStubs)
            {
                await associationContractImplStub.Approve.SendAsync(proposalId1);
            }

            await AssociationContractImplStubs[0].Release.SendAsync(proposalId1);
        }
        await AssociationContractImplStub.Approve.SendAsync(proposalId);
        return proposalId;
    }

    #endregion

    #region Helper

    private async Task CreateRegimentTest()
    {
        // Create regiment.
        var executionResult = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = DefaultSenderAddress,
            InitialMemberList = {Transmitters.Select(a => a.Address)}
        });

        var regimentAddress = RegimentCreated.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        regimentAddress.ShouldBe(_regimentAddress);
        var regimentInfo = await RegimentContractStub.GetRegimentInfo.CallAsync(_regimentAddress);
        var manager = regimentInfo.Manager;
        manager.ShouldBe(DefaultSenderAddress);

        await OracleContractStub.AddAdmins.SendAsync(new AddAdminsInput
        {
            RegimentAddress = _regimentAddress,
            OriginSenderAddress = manager,
            NewAdmins = {BridgeContractAddress}
        });
    }

    private async Task<(Address, Address)> CreateOrganizationTest()
    {
        var executionResult = await AssociationContractStub.CreateOrganization.SendAsync(new CreateOrganizationInput
        {
            OrganizationMemberList = new OrganizationMemberList
            {
                OrganizationMembers =
                {
                    RestartNodes[0].Address,
                    RestartNodes[1].Address,
                    RestartNodes[2].Address,
                    RestartNodes[3].Address,
                    RestartNodes[4].Address,
                }
            },
            ProposalReleaseThreshold = new ProposalReleaseThreshold
            {
                MinimalApprovalThreshold = 4,
                MaximalRejectionThreshold = 1,
                MinimalVoteThreshold = 4
            },
            CreationToken = HashHelper.ComputeFrom("restart"),
            ProposerWhiteList = new ProposerWhiteList
            {
                Proposers =
                {
                    RestartNodes[0].Address,
                    RestartNodes[1].Address,
                    RestartNodes[2].Address,
                    RestartNodes[3].Address,
                    RestartNodes[4].Address,
                }
            }
        });
        var organizationAddress = (OrganizationCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .FirstOrDefault(e => e.Name == nameof(OrganizationCreated))?.NonIndexed)).OrganizationAddress;
        var executionResult1 = await AssociationContractStub.CreateOrganization.SendAsync(new CreateOrganizationInput
        {
            OrganizationMemberList = new OrganizationMemberList
            {
                OrganizationMembers =
                {
                    DefaultAccount.Address,
                    organizationAddress
                }
            },
            ProposalReleaseThreshold = new ProposalReleaseThreshold
            {
                MinimalApprovalThreshold = 2,
                MaximalRejectionThreshold = 0,
                MinimalVoteThreshold = 2
            },
            CreationToken = HashHelper.ComputeFrom("restart"),
            ProposerWhiteList = new ProposerWhiteList
            {
                Proposers =
                {
                    DefaultAccount.Address
                }
            }
        });
        var organizationAddress1 = (OrganizationCreated.Parser.ParseFrom(executionResult1.TransactionResult.Logs
            .FirstOrDefault(e => e.Name == nameof(OrganizationCreated))?.NonIndexed)).OrganizationAddress;
        return (organizationAddress, organizationAddress1);
    }

    private async Task<(Address, Address)> CreateOrganizationSecondTest()
    {
        var executionResult = await AssociationContractStub.CreateOrganization.SendAsync(new CreateOrganizationInput
        {
            OrganizationMemberList = new OrganizationMemberList
            {
                OrganizationMembers =
                {
                    RestartNodes[0].Address,
                    RestartNodes[1].Address,
                    RestartNodes[2].Address,
                    RestartNodes[3].Address,
                    RestartNodes[4].Address,
                }
            },
            ProposalReleaseThreshold = new ProposalReleaseThreshold
            {
                MinimalApprovalThreshold = 4,
                MaximalRejectionThreshold = 1,
                MinimalVoteThreshold = 4
            },
            CreationToken = HashHelper.ComputeFrom("test"),
            ProposerWhiteList = new ProposerWhiteList
            {
                Proposers =
                {
                    RestartNodes[0].Address,
                    RestartNodes[1].Address,
                    RestartNodes[2].Address,
                    RestartNodes[3].Address,
                    RestartNodes[4].Address,
                }
            }
        });
        var organizationAddress = (OrganizationCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .FirstOrDefault(e => e.Name == nameof(OrganizationCreated))?.NonIndexed)).OrganizationAddress;
        var executionResult1 = await AssociationContractStub.CreateOrganization.SendAsync(new CreateOrganizationInput
        {
            OrganizationMemberList = new OrganizationMemberList
            {
                OrganizationMembers =
                {
                    DefaultAccount.Address,
                    organizationAddress
                }
            },
            ProposalReleaseThreshold = new ProposalReleaseThreshold
            {
                MinimalApprovalThreshold = 2,
                MaximalRejectionThreshold = 0,
                MinimalVoteThreshold = 2
            },
            CreationToken = HashHelper.ComputeFrom("ttt"),
            ProposerWhiteList = new ProposerWhiteList
            {
                Proposers =
                {
                    DefaultAccount.Address
                }
            }
        });
        var organizationAddress1 = (OrganizationCreated.Parser.ParseFrom(executionResult1.TransactionResult.Logs
            .FirstOrDefault(e => e.Name == nameof(OrganizationCreated))?.NonIndexed)).OrganizationAddress;
        return (organizationAddress, organizationAddress1);
    }

    private async Task InitialOracleContractAsync()
    {
        await OracleContractStub.Initialize.SendAsync(new Oracle.InitializeInput
        {
            RegimentContractAddress = RegimentContractAddress
        });
    }

    private async Task<(Address, Address)> InitialBridgeContractAsync()
    {
        var organizationAddress = await CreateOrganizationTest();
        await BridgeContractStub.Initialize.SendAsync(new InitializeInput
        {
            MerkleTreeContractAddress = MerkleTreeContractAddress,
            OracleContractAddress = OracleContractAddress,
            RegimentContractAddress = RegimentContractAddress,
            ReportContractAddress = ReportContractAddress,
            Admin = DefaultSenderAddress,
            Controller = DefaultSenderAddress,
            OrganizationAddress = organizationAddress.Item2,
            PauseController = DefaultSenderAddress,
            ApproveTransferController = DefaultSenderAddress
        });
        return organizationAddress;
    }

    private async Task InitialMerkleTreeContractAsync()
    {
        await MerkleTreeContractStub.Initialize.SendAsync(new MerkleTreeContract.InitializeInput
        {
            Owner = BridgeContractAddress,
            RegimentContractAddress = RegimentContractAddress
        });
    }

    private async Task InitialReportContractAsync()
    {
        await PortTokenCreate();
        await ReportContractStub.Initialize.SendAsync(new Report.InitializeInput
        {
            OracleContractAddress = OracleContractAddress,
            RegimentContractAddress = RegimentContractAddress,
            ReportFee = 0,
            InitialRegisterWhiteList = {DefaultSenderAddress}
        });
    }

    private async Task CreateAndIssueUSDTAsync()
    {
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            Symbol = "USDT",
            Decimals = 8,
            Issuer = DefaultSenderAddress,
            TokenName = "Stable coin",
            TotalSupply = 10_00000000_00000000,
            IsBurnable = true
        });

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = "USDT",
            Amount = 10_00000000_00000000,
            To = DefaultSenderAddress
        });
    }

    private async Task PortTokenCreate()
    {
        // Create PORT token.
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            TokenName = "Port Token",
            Decimals = 8,
            Issuer = DefaultSenderAddress,
            IsBurnable = true,
            Symbol = "PORT",
            TotalSupply = 10_00000000_00000000
        });

        // Issue PORT token.
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            To = Transmitters.First().Address,
            Amount = 10_00000000_00000000,
            Symbol = "PORT"
        });

        // Approve Oracle Contract.
        var transmitterTokenContractStub = GetTokenContractStub(Transmitters.First().KeyPair);
        await transmitterTokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = "PORT",
            Amount = 5_00000000_00000000,
            Spender = OracleContractAddress
        });
    }

    private async Task InitialSetGas()
    {
        await BridgeContractStub.SetGasLimit.SendAsync(new SetGasLimitInput
        {
            GasLimitList =
            {
                new GasLimit
                {
                    ChainId = "Ethereum",
                    GasLimit_ = 293414
                }
            }
        });
        await BridgeContractStub.SetGasPrice.SendAsync(new SetGasPriceInput
        {
            GasPriceList =
            {
                new GasPrice
                {
                    ChainId = "Ethereum",
                    GasPrice_ = 8245816000
                }
            }
        });
        //var priceRatioReal = 1 / 0.000095;
        await BridgeContractStub.SetPriceRatio.SendAsync(new SetRatioInput
        {
            Value =
            {
                new Ratio
                {
                    ChainId = "Ethereum",
                    Ratio_ = 1052631578947
                }
            }
        });
        await BridgeContractStub.SetFeeFloatingRatio.SendAsync(new SetRatioInput
        {
            Value =
            {
                new Ratio
                {
                    ChainId = "Ethereum",
                    Ratio_ = 20
                }
            }
        });
        await BridgeContractStub.SetGasLimit.SendAsync(new SetGasLimitInput
        {
            GasLimitList =
            {
                new GasLimit
                {
                    ChainId = "Ploygon",
                    GasLimit_ = 293414
                }
            }
        });
        await BridgeContractStub.SetGasPrice.SendAsync(new SetGasPriceInput
        {
            GasPriceList =
            {
                new GasPrice
                {
                    ChainId = "Ploygon",
                    GasPrice_ = 8245816000
                }
            }
        });
        await BridgeContractStub.SetPriceRatio.SendAsync(new SetRatioInput
        {
            Value =
            {
                new Ratio
                {
                    ChainId = "Ploygon",
                    Ratio_ = 1052631578947
                }
            }
        });
    }

    private async Task CheckBalanceAsync(Address ownerAddress, string symbol, long supposedBalance)
    {
        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = ownerAddress,
            Symbol = symbol
        })).Balance;
        balance.ShouldBe(supposedBalance);
    }

    private async Task<Address> CreateRegiment_Use_NotAdmin()
    {
        var executionResult = await OracleContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = DefaultSenderAddress,
            IsApproveToJoin = true
        });
        var regimentAddress = RegimentCreated.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        return regimentAddress;
    }

    #endregion
}