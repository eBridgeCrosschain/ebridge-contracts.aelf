using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using AElf;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Association;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Kernel.Proposal;
using AElf.Standards.ACS0;
using AElf.Standards.ACS3;
using AElf.Types;
using EBridge.Contracts.MerkleTreeContract;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.Report;
using EBridge.Contracts.TestContract.ReceiptMaker;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Threading;
using AddAdminsInput = EBridge.Contracts.Oracle.AddAdminsInput;
using CreateOrganizationInput = AElf.Contracts.Referendum.CreateOrganizationInput;
using CreateRegimentInput = EBridge.Contracts.Oracle.CreateRegimentInput;

namespace EBridge.Contracts.Bridge;

public class BridgeContractTestBase : DAppContractTestBase<BridgeContractTestModule>
{
    protected Address DefaultSenderAddress { get; set; }
    protected ECKeyPair DefaultKeypair => SampleAccount.Accounts.First().KeyPair;

    internal List<Account> Transmitters => SampleAccount.Accounts.Skip(1).Take(5).ToList();

    internal List<Account> Receivers => SampleAccount.Accounts.Skip(6).Take(5).ToList();

    internal List<Account> Lockers => SampleAccount.Accounts.Skip(11).Take(3).ToList();

    internal Address TransactionFeeRatioAddress { get; set; }
    internal ECKeyPair TransactionFeeRatio => SampleAccount.Accounts[14].KeyPair;

    internal List<Account> RestartNodes => SampleAccount.Accounts.Skip(15).Take(5).ToList();

    protected IBlockTimeProvider BlockTimeProvider =>
        Application.ServiceProvider.GetRequiredService<IBlockTimeProvider>();

    internal AssociationContractContainer.AssociationContractStub AssociationContractStub { get; set; }

    internal AssociationContractImplContainer.AssociationContractImplStub AssociationContractImplStub { get; set; }

    internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
    internal BridgeContractContainer.BridgeContractStub BridgeContractStub { get; set; }

    internal BridgeContractImplContainer.BridgeContractImplStub BridgeContractImplStub { get; set; }

    internal BridgeContractImplContainer.BridgeContractImplStub BridgeContractImplUserStub { get; set; }


    internal BridgeContractContainer.BridgeContractStub BridgeContractSetFeeRatioStub { get; set; }

    internal ReportContractContainer.ReportContractStub ReportContractStub { get; set; }
    internal OracleContractContainer.OracleContractStub OracleContractStub { get; set; }

    internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
    internal TokenContractContainer.TokenContractStub TokenContractStub2 { get; set; }
    internal RegimentContractContainer.RegimentContractStub RegimentContractStub { get; set; }

    internal ParliamentContractContainer.ParliamentContractStub ParliamentContractStub { get; set; }

    internal MerkleTreeContractContainer.MerkleTreeContractStub MerkleTreeContractStub { get; set; }

    internal List<OracleContractContainer.OracleContractStub> TransmittersOracleContractStubs { get; set; } =
        new List<OracleContractContainer.OracleContractStub>();

    internal List<ReportContractContainer.ReportContractStub> TransmittersReportContractStubs { get; set; } =
        new List<ReportContractContainer.ReportContractStub>();


    internal List<BridgeContractContainer.BridgeContractStub> ReceiverBridgeContractStubs { get; set; } =
        new List<BridgeContractContainer.BridgeContractStub>();

    internal List<BridgeContractContainer.BridgeContractStub> LockBridgeContractStubs { get; set; } =
        new List<BridgeContractContainer.BridgeContractStub>();

    internal List<AssociationContractImplContainer.AssociationContractImplStub>
        AssociationContractImplStubs { get; set; } =
        new List<AssociationContractImplContainer.AssociationContractImplStub>();

    internal ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub ReceiptMakerContractImplStub { get; set; }
    protected Address BridgeContractAddress { get; set; }
    public Address ReportContractAddress { get; set; }
    protected Address OracleContractAddress { get; set; }

    internal Address StringAggregatorContractAddress =>
        GetAddress(StringAggregatorSmartContractAddressNameProvider.StringName);
    protected Address MerkleTreeContractAddress { get; set; }

    internal Address RegimentContractAddress =>
        GetAddress(RegimentSmartContractAddressNameProvider.StringName);
    internal Address ParliamentContractAddress =>
        GetAddress(ParliamentSmartContractAddressNameProvider.StringName);

    internal Address ReceiptMakerContractAddress =>
        GetAddress(ReceiptMakerSmartContractAddressNameProvider.StringName);
    
    internal readonly Address _regimentAddress =
        Address.FromBase58("2Myxs3YTFEcDN5VQDECBgmBda1NXJT1bdRQYSkdbZL74aKxEW3");
    
    internal Dictionary<string, Hash> _receiptDictionary;
    
    internal Hash _swapHashOfElf;
    internal Hash _swapHashOfUsdt;
    internal Hash _swapOfElfSpaceId;
    internal Hash _swapOfUsdtSpaceId;
    
    protected IBlockTimeProvider blockTimeProvider =>
        Application.ServiceProvider.GetRequiredService<IBlockTimeProvider>();
    
    public BridgeContractTestBase()
    {
        DefaultSenderAddress = SampleAccount.Accounts.First().Address;
        TransactionFeeRatioAddress = SampleAccount.Accounts[14].Address;
        
        ZeroContractStub = GetContractZeroTester(DefaultKeypair);
        var result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(BridgeContract).Assembly.Location))
        }));
        BridgeContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        
        result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(MerkleTreeContract.MerkleTreeContract).Assembly.Location))
        }));
        MerkleTreeContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        
        result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(OracleContract).Assembly.Location))
        }));
        OracleContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        
        result = AsyncHelper.RunSync(async () =>await ZeroContractStub.DeploySmartContract.SendAsync(new ContractDeploymentInput
        {   
            Category = KernelConstants.CodeCoverageRunnerCategory,
            Code = ByteString.CopyFrom(
                File.ReadAllBytes(typeof(ReportContract).Assembly.Location))
        }));
        ReportContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
        
        BridgeContractStub = GetBridgeContractStub(DefaultKeypair);
        BridgeContractImplStub = GetBridgeContractImplStub(DefaultKeypair);
        BridgeContractImplUserStub = GetTester<BridgeContractImplContainer.BridgeContractImplStub>(
            BridgeContractAddress,
            TransactionFeeRatio);
        BridgeContractSetFeeRatioStub = GetBridgeContractStub(TransactionFeeRatio);
        ReportContractStub = GetReportContractStub(DefaultKeypair);
        OracleContractStub = GetOracleContractStub(DefaultKeypair);
        TokenContractStub = GetTokenContractStub(DefaultKeypair);
        TokenContractStub2 = GetTokenContractStub(Lockers[0].KeyPair);
        MerkleTreeContractStub = GetMerkleTreeContractStub(DefaultKeypair);
        RegimentContractStub = GetRegimentContractStub(DefaultKeypair);
        ParliamentContractStub = GetParliamentContractStub(DefaultKeypair);
        ReceiptMakerContractImplStub = GetReceiptMakerContractStub(DefaultKeypair);
        AssociationContractStub = GetAssociationContractStub(DefaultKeypair);
        AssociationContractImplStub = GetAssociationContractImplStub(DefaultKeypair);

        foreach (var transmitter in Transmitters)
        {
            TransmittersOracleContractStubs.Add(GetOracleContractStub(transmitter.KeyPair));
        }

        foreach (var transmitter in Transmitters)
        {
            TransmittersReportContractStubs.Add(GetReportContractStub(transmitter.KeyPair));
        }

        foreach (var receiver in Receivers)
        {
            ReceiverBridgeContractStubs.Add(GetBridgeContractStub(receiver.KeyPair));
        }

        foreach (var locker in Lockers)
        {
            LockBridgeContractStubs.Add(GetBridgeContractStub(locker.KeyPair));
        }

        foreach (var node in RestartNodes)
        {
            AssociationContractImplStubs.Add(GetAssociationContractImplStub(node.KeyPair));
        }
    }

    internal MerkleTreeContractContainer.MerkleTreeContractStub
        GetMerkleTreeContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<MerkleTreeContractContainer.MerkleTreeContractStub>(
            MerkleTreeContractAddress,
            senderKeyPair);
    }

    internal BridgeContractContainer.BridgeContractStub
        GetBridgeContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<BridgeContractContainer.BridgeContractStub>(
            BridgeContractAddress,
            senderKeyPair);
    }

    internal BridgeContractImplContainer.BridgeContractImplStub
        GetBridgeContractImplStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<BridgeContractImplContainer.BridgeContractImplStub>(
            BridgeContractAddress,
            senderKeyPair);
    }

    internal ReportContractContainer.ReportContractStub
        GetReportContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<ReportContractContainer.ReportContractStub>(
            ReportContractAddress,
            senderKeyPair);
    }

    internal OracleContractContainer.OracleContractStub
        GetOracleContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<OracleContractContainer.OracleContractStub>(
            OracleContractAddress,
            senderKeyPair);
    }

    internal TokenContractContainer.TokenContractStub
        GetTokenContractStub(
            ECKeyPair senderKeyPair)
    {
        return GetTester<TokenContractContainer.TokenContractStub>(
            TokenContractAddress,
            senderKeyPair);
    }

    internal RegimentContractContainer.RegimentContractStub
        GetRegimentContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<RegimentContractContainer.RegimentContractStub>(
            RegimentContractAddress,
            senderKeyPair);
    }

    internal ParliamentContractContainer.ParliamentContractStub
        GetParliamentContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<ParliamentContractContainer.ParliamentContractStub>(
            ParliamentContractAddress,
            senderKeyPair);
    }

    internal AssociationContractContainer.AssociationContractStub
        GetAssociationContractStub(ECKeyPair senderKeyPair)
    {
        return GetTester<AssociationContractContainer.AssociationContractStub>(
            AssociationContractAddress,
            senderKeyPair);
    }

    internal AssociationContractImplContainer.AssociationContractImplStub
        GetAssociationContractImplStub(ECKeyPair senderKeyPair)
    {
        return GetTester<AssociationContractImplContainer.AssociationContractImplStub>(
            AssociationContractAddress,
            senderKeyPair);
    }

    internal ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub GetReceiptMakerContractStub(
        ECKeyPair senderKeyPair)
    {
        return GetTester<ReceiptMakerContractImplContainer.ReceiptMakerContractImplStub>(
            ReceiptMakerContractAddress,
            senderKeyPair
        );
    }
    
    internal async Task InitialOracleContractAsync()
    {
        await OracleContractStub.Initialize.SendAsync(new Oracle.InitializeInput
        {
            RegimentContractAddress = RegimentContractAddress
        });
    }

    internal async Task<(Address, Address)> InitialBridgeContractAsync()
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

    internal async Task InitialMerkleTreeContractAsync()
    {
        await MerkleTreeContractStub.Initialize.SendAsync(new MerkleTreeContract.InitializeInput
        {
            Owner = BridgeContractAddress,
            RegimentContractAddress = RegimentContractAddress
        });
    }

    internal async Task InitialReportContractAsync()
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

    internal async Task CreateAndIssueUSDTAsync()
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

    internal async Task PortTokenCreate()
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
            Amount = 5_00000000_00000000,
            Symbol = "PORT"
        });
        // Issue PORT token.
        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            To = DefaultSenderAddress,
            Amount = 5_00000000_00000000,
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
        // Approve Report Contract.
        var senderTokenContractStub = GetTokenContractStub(DefaultKeypair);
        await senderTokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = "PORT",
            Amount = 5_00000000_00000000,
            Spender = ReportContractAddress
        });
    }

    internal async Task InitialSetGas()
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

    internal async Task CheckBalanceAsync(Address ownerAddress, string symbol, long supposedBalance)
    {
        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = ownerAddress,
            Symbol = symbol
        })).Balance;
        balance.ShouldBe(supposedBalance);
    }
    
    internal async Task<Hash> ProposalToRestartContract((Address, Address) organizationAddress)
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
    
    internal async Task CreateRegimentTest()
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
    
    internal async Task<(Address, Address)> CreateOrganizationTest()
    {
        var executionResult = await AssociationContractStub.CreateOrganization.SendAsync(new AElf.Contracts.Association.CreateOrganizationInput
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
        var executionResult1 = await AssociationContractStub.CreateOrganization.SendAsync(new AElf.Contracts.Association.CreateOrganizationInput
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

    internal async Task<(Address, Address)> CreateOrganizationSecondTest()
    {
        var executionResult = await AssociationContractStub.CreateOrganization.SendAsync(new AElf.Contracts.Association.CreateOrganizationInput
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
        var executionResult1 = await AssociationContractStub.CreateOrganization.SendAsync(new AElf.Contracts.Association.CreateOrganizationInput
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

    

    internal async Task<Address> CreateRegiment_Use_NotAdmin()
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
    
    internal ACS0Container.ACS0Stub GetContractZeroTester(
        ECKeyPair keyPair)
    {
        return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress,
            keyPair);
    }
}