using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Kernel;
using AElf.Types;
using EBridge.Contracts.TokenPool;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Ramp;
using Shouldly;
using Xunit;


namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests
{
    [Fact]
    public async Task<(Address, Address)> InitialSwapAsync()
    {
        var organization = await InitialBridgeContractAsync();
        await CreateAndIssueUSDTAsync();

        await BridgeContractStub.AddToken.SendAsync(new AddTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                },
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "USDT"
                },
                new ChainToken
                {
                    ChainId = "Polygon",
                    Symbol = "USDT"
                }
            }
        });
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Amount = long.MaxValue,
            Spender = BridgeContractAddress,
            Symbol = "ELF"
        });

        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Amount = long.MaxValue,
            Spender = BridgeContractAddress,
            Symbol = "USDT"
        });
        return organization;
    }
    
    
    private async Task<DateTime> SetSwapLimit()
    {
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<SwapDailyLimitInfo>
        {
            new SwapDailyLimitInfo
            {
                SwapId = _swapHashOfElf,
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new SwapDailyLimitInfo
            {
                SwapId = _swapHashOfUsdt,
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };

        await BridgeContractImplStub.SetSwapDailyLimit.SendAsync(new SetSwapDailyLimitInput
        {
            SwapDailyLimitInfos = { input }
        });

        var input1 = new List<SwapTokenBucketConfig>()
        {
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfElf,
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 1_00000000
            },
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfUsdt,
                IsEnable = true,
                TokenCapacity = 2_0000_00000000,
                Rate = 100_00000000
            }
        };
        await BridgeContractImplStub.ConfigSwapTokenBucket.SendAsync(new ConfigSwapTokenBucketInput
        {
            SwapTokenBucketConfigs = { input1 }
        });
        return time;
    }

    #region Swap

    [Fact]
    public async Task<(Address, Address)> CreateSwapTestAsync()
    {
        var organization = await InitialSwapAsync();
        // Create swap.
        var createSwapResult = await BridgeContractStub.CreateSwap.SendAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    Symbol = "ELF",
                    FromChainId = "Ethereum",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 10000000000,
                        TargetShare = 1
                    }
                }
        });
        _swapHashOfElf = createSwapResult.Output;
        var swapId = await BridgeContractStub.GetSwapIdByToken.CallAsync(new GetSwapIdByTokenInput
        {
            ChainId = "Ethereum",
            Symbol = "ELF"
        });
        swapId.ShouldBe(_swapHashOfElf);
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = "ELF",
            Amount = 10_0000_00000000,
            Spender = TokenPoolContractAddress
        });
        await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
        {
            TokenSymbol = "ELF",
            Amount = 10_0000_00000000
        });
        {
            var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
            {
                TokenSymbol = "ELF"
            });
            tokenPoolInfo.Liquidity.ShouldBe(10_0000_00000000);
        }

        // Create another swap.
        createSwapResult = await BridgeContractStub.CreateSwap.SendAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    Symbol = "USDT",
                    FromChainId = "Polygon",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 10000000000,
                        TargetShare = 1
                    }
                }
        });
        _swapHashOfUsdt = createSwapResult.Output;
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Symbol = "USDT",
            Amount = 10_0000_00000000,
            Spender = TokenPoolContractAddress
        });
        await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
        {
            TokenSymbol = "USDT",
            Amount = 10_0000_00000000
        });
        {
            var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
            {
                TokenSymbol = "USDT"
            });
            tokenPoolInfo.Liquidity.ShouldBe(10_0000_00000000);
        }
        return organization;
    }

    [Fact]
    public async Task CreateSwapTest_NoPermission()
    {
        await InitialSwapAsync();
        var executionResult = await LockBridgeContractStubs[0].CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    FromChainId = "Ethereum",
                    Symbol = "ELF",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 10000000000,
                        TargetShare = 1
                    }
                }
        });
        executionResult.TransactionResult.Error.ShouldContain("Only contract admin can create swap.");
    }

    [Fact]
    public async Task CreateSwapTest_Repeat()
    {
        await CreateSwapTestAsync();
        var result = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    Symbol = "ELF",
                    FromChainId = "Ethereum",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 10000000000,
                        TargetShare = 1
                    }
                }
        });
        result.TransactionResult.Error.ShouldContain("Swap already created.");
    }

    [Fact]
    public async Task CreateSwapTest_SymbolNotExist()
    {
        await InitialSwapAsync();
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    FromChainId = "Ethereum",
                    Symbol = "TEST",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 10000000000,
                        TargetShare = 1
                    }
                }
        });
        executionResult.TransactionResult.Error.ShouldContain("Token not found.");
    }

    [Fact]
    public async Task CreateSwapTest_InvalidSwapRatio()
    {
        await InitialSwapAsync();
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    FromChainId = "Ethereum",
                    Symbol = "ELF",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 0,
                        TargetShare = 1
                    }
                }
        });
        executionResult.TransactionResult.Error.ShouldContain("Invalidate swap ratio.");
    }

    [Fact]
    public async Task CreateSwapTest_NotSetMerkleTreeContract()
    {
        await BridgeContractStub.Initialize.SendAsync(new InitializeInput
        {
            Admin = DefaultSenderAddress,
            Controller = DefaultSenderAddress
        });
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            SwapTargetToken =
                new SwapTargetToken
                {
                    FromChainId = "Ethereum",
                    Symbol = "ELF",
                    SwapRatio = new SwapRatio
                    {
                        OriginShare = 0,
                        TargetShare = 1
                    }
                }
        });
        executionResult.TransactionResult.Error.ShouldContain("Invalidate swap ratio.");
    }

    [Fact]
    public async Task GetSwapPairInfo()
    {
        await CreateSwapTestAsync();
        {
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.SwappedTimes.ShouldBe(0);
            result.SwappedAmount.ShouldBe(0);
        }
        {
            var result1 = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = new Hash(),
                Symbol = "XXX"
            });
            result1.SwappedTimes.ShouldBe(0);
        }
    }

    [Fact]
    public async Task ChangeSwapRatioTest()
    {
        await CreateSwapTestAsync();
        {
            var swapInfo = await BridgeContractStub.GetSwapInfo.CallAsync(_swapHashOfElf);
            swapInfo.SwapTargetToken.SwapRatio.OriginShare.ShouldBe(10000000000);
            swapInfo.SwapTargetToken.SwapRatio.TargetShare.ShouldBe(1);
        }
        await BridgeContractStub.ChangeSwapRatio.SendAsync(new ChangeSwapRatioInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "ELF",
            SwapRatio = new SwapRatio
            {
                OriginShare = 50000000000,
                TargetShare = 2
            }
        });
        {
            var swapInfo = await BridgeContractStub.GetSwapInfo.CallAsync(_swapHashOfElf);
            swapInfo.SwapTargetToken.SwapRatio.OriginShare.ShouldBe(50000000000);
            swapInfo.SwapTargetToken.SwapRatio.TargetShare.ShouldBe(2);
        }
        var executionResult = await BridgeContractStub.ChangeSwapRatio.SendWithExceptionAsync(new ChangeSwapRatioInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "ELF",
            SwapRatio = new SwapRatio
            {
                OriginShare = -1,
                TargetShare = 2
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("SwapRatio originShare or TargetShare is invalid");
        executionResult = await BridgeContractStub.ChangeSwapRatio.SendWithExceptionAsync(new ChangeSwapRatioInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "ELF",
            SwapRatio = new SwapRatio
            {
                OriginShare = 50000000000,
                TargetShare = -1
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("SwapRatio originShare or TargetShare is invalid");
    }

    [Fact]
    public async Task ChangeSwapRatioTest_NoPermission()
    {
        await CreateSwapTestAsync();
        var executionResult = await LockBridgeContractStubs[0].ChangeSwapRatio.SendWithExceptionAsync(
            new ChangeSwapRatioInput
            {
                SwapId = _swapHashOfElf,
                TargetTokenSymbol = "ELF",
                SwapRatio = new SwapRatio
                {
                    OriginShare = 50000000000,
                    TargetShare = 2
                }
            });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task ChangeSwapRatioTest_SwapIdNotExist()
    {
        await CreateSwapTestAsync();
        var executionResult = await BridgeContractStub.ChangeSwapRatio.SendWithExceptionAsync(new ChangeSwapRatioInput
        {
            SwapId = new Hash(),
            TargetTokenSymbol = "ELF",
            SwapRatio = new SwapRatio
            {
                OriginShare = 50000000000,
                TargetShare = 2
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Token swap pair not found.");
    }

    [Fact]
    public async Task ChangeSwapRatioTest_SymbolNotExist()
    {
        await CreateSwapTestAsync();
        var executionResult = await BridgeContractStub.ChangeSwapRatio.SendWithExceptionAsync(new ChangeSwapRatioInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "BNB",
            SwapRatio = new SwapRatio
            {
                OriginShare = 50000000000,
                TargetShare = 2
            }
        });
        executionResult.TransactionResult.Error.ShouldContain($"Swap target token {_swapHashOfElf}-BNB is not exist.");
    }

    [Fact]
    public async Task<(Address, Address)> ToAElfTest_Pause()
    {
        var organization = await CreateSwapTestAsync();

        await BridgeContractStub.Pause.SendAsync(new Empty());

        return organization;
    }

    [Fact]
    public async Task ToAElfTest_Restart()
    {
        var organization = await ToAElfTest_Pause();

        var proposalId = await ProposalToRestartContract(organization);
        await AssociationContractImplStub.Release.SendAsync(proposalId);
        var state = await BridgeContractStub.IsContractPause.CallAsync(new Empty());
        state.Value.ShouldBe(false);
    }
    
    #endregion

}