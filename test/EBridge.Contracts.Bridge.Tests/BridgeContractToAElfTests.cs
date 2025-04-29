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

    [Fact]
    public async Task ToAElfPipelineTest()
    {
        await CreateSwapTestAsync();
        var time = await SetSwapLimit();
        
        {
            var swapTime = TimestampHelper.GetUtcNow().ToDateTime();
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(swapTime.AddHours(1)));
            var executionResult = await ReceiverBridgeContractStubs.First().SwapToken.SendAsync(new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[0].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId,
                SwapId = _swapHashOfElf
            });
            var log = TokenSwapped.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(TokenSwapped)).NonIndexed);
            log.FromChainId.ShouldBe("Ethereum");
            await CheckBalanceAsync(Receivers.First().Address, "ELF", 10000000L);
            {
                var log1 = SwapLimitChanged.Parser.ParseFrom(executionResult.TransactionResult.Logs
                    .FirstOrDefault(l => l.Name == nameof(SwapLimitChanged))?.NonIndexed);
                log1.Symbol.ShouldBe("ELF");
                log1.FromChainId.ShouldBe("Ethereum");
                log1.CurrentSwapDailyLimitAmount.ShouldBe(10_0000_00000000 - 10000000);
                log1.CurrentSwapBucketTokenAmount.ShouldBe(5_0000_00000000 - 10000000);
                log1.SwapDailyLimitRefreshTime.ShouldBe(Timestamp.FromDateTime(time.Date));
                log1.SwapBucketUpdateTime.ShouldBeLessThanOrEqualTo(Timestamp.FromDateTime(swapTime.AddHours(1)));
            }
            {
                var dailyLimit = await BridgeContractImplStub.GetSwapDailyLimit.CallAsync(_swapHashOfElf);
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000 - 10000000);
                blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(swapTime.AddHours(1).AddMinutes(1)));
                var bucket = await BridgeContractImplStub.GetCurrentSwapTokenBucketState.CallAsync(_swapHashOfElf);
                bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000); 
            }
            var swapAmount = await ReceiverBridgeContractStubs.First().GetSwapAmounts.CallAsync(new GetSwapAmountsInput
            {
                SwapId = _swapHashOfElf,
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId
            });
            swapAmount.Receiver.ShouldBe(Receivers.First().Address);
            swapAmount.ReceivedAmounts["ELF"].ShouldBe(10000000L);

            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.SwappedTimes.ShouldBe(1);
            result.SwappedAmount.ShouldBe(10000000L);
        }
        {
            // Swap
            await BridgeContractStub.SwapToken.SendAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[1].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[1].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[1].ReceiptId,
                SwapId = _swapHashOfElf
            });
            await CheckBalanceAsync(Receivers[1].Address, "ELF", 20000000L);
            await CheckBalanceAsync(Receivers[1].Address, "USDT", 0);
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.SwappedTimes.ShouldBe(2);
            result.SwappedAmount.ShouldBe(30000000L);
        }
        {
            // Swap
            await BridgeContractStub.SwapToken.SendAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[4].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[4].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId,
                SwapId = _swapHashOfElf
            });
            await CheckBalanceAsync(Receivers[4].Address, "ELF", 50000000L);
            await CheckBalanceAsync(Receivers[4].Address, "USDT", 0);
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.SwappedTimes.ShouldBe(3);
            result.SwappedAmount.ShouldBe(80000000L);
            var receiptInfo = await BridgeContractStub.GetSwappedReceiptInfo.CallAsync(
                new GetSwappedReceiptInfoInput
                {
                    SwapId = _swapHashOfElf,
                    ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId
                });
            receiptInfo.ReceiptId.ShouldBe(SampleSwapInfo.SwapInfos[4].ReceiptId);
            receiptInfo.AmountMap["ELF"].ShouldBe(50000000L);
        }
        {
            // Swap
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[0].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[5].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[5].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Merkle proof failed.");
        }
        {
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = TokenPoolContractAddress,
                Symbol = "USDT",
                Amount = 1000000000000
            });
            await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
            {
                TokenSymbol = "USDT",
                Amount = 1000000000000
            });
        }
        {
            // Swap
            await ReceiverBridgeContractStubs.First().SwapToken.SendAsync(new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[5].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[5].ReceiptId,
                SwapId = _swapHashOfUsdt
            });
            await CheckBalanceAsync(Receivers.First().Address, "ELF", 10000000L);
            await CheckBalanceAsync(Receivers[0].Address, "USDT", 10000000L);
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfUsdt,
                Symbol = "USDT"
            });
            result.SwappedTimes.ShouldBe(1);
            result.SwappedAmount.ShouldBe(10000000L);
        }

        {
            // Swap
            await ReceiverBridgeContractStubs[1].SwapToken.SendAsync(new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[6].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[6].ReceiptId,
                SwapId = _swapHashOfUsdt
            });
            await CheckBalanceAsync(Receivers[1].Address, "ELF", 20000000L);
            await CheckBalanceAsync(Receivers[1].Address, "USDT", 20000000L);
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfUsdt,
                Symbol = "USDT"
            });
            result.SwappedTimes.ShouldBe(2);
            result.SwappedAmount.ShouldBe(30000000L);
        }
        {
            // Swap
            await ReceiverBridgeContractStubs[4].SwapToken.SendAsync(new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[9].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[9].ReceiptId,
                SwapId = _swapHashOfUsdt
            });
            await CheckBalanceAsync(Receivers[4].Address, "ELF", 50000000L);
            await CheckBalanceAsync(Receivers[4].Address, "USDT", 50000000L);
        }
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
        executionResult.TransactionResult.Error.ShouldContain("Only regiment manager can create swap.");
    }

    [Fact]
    public async Task CreateSwapTest_RegimentIdIsNull()
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
                        OriginShare = 10000000000,
                        TargetShare = 1
                    }
                }
        });
        executionResult.TransactionResult.Error.ShouldContain("Regiment id cannot be null.");
    }

    [Fact]
    public async Task CreateSwapTest_NotAdmin()
    {
        await InitialBridgeContractAsync();
        await CreateAndIssueUSDTAsync();

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
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
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
        executionResult.TransactionResult.Error.ShouldContain("Bridge Contract is not the admin of regiment.");
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
        executionResult.TransactionResult.Error.ShouldContain("MerkleTree contract is not initialized.");
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
    

    [Fact]
    public async Task ToAElfTest_ExceedDailyLimit()
    {
        await CreateSwapTestAsync();
        
    }

    [Fact]
    public async Task ToAElfTest_ExceedBucketLimit()
    {
        await CreateSwapTestAsync();
    }

    [Fact]
    public async Task ToAElfTest_DuplicateCommit()
    {
        await CreateSwapTestAsync();
    }

    [Fact]
    public async Task ToAElfTest_SwapIdIsNull()
    {
        await CreateSwapTestAsync();
    }

    [Fact]
    public async Task ToAElfTest_SpaceIdIsNull()
    {
        await CreateSwapTestAsync();
    }

    [Fact]
    public async Task<(Address, Address)> SwapTokenTest_Pause()
    {
        var organization = await CreateSwapTestAsync();
        await BridgeContractStub.Pause.SendAsync(new Empty());
        var executionResult = await ReceiverBridgeContractStubs.First().SwapToken.SendWithExceptionAsync(
            new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[0].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId,
                SwapId = _swapHashOfElf
            });
        executionResult.TransactionResult.Error.ShouldContain("Contract is paused.");
        return organization;
    }

    [Fact]
    public async Task SwapTokenTest_Restart()
    {
        var organization = await SwapTokenTest_Pause();
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
        var proposalId = await ProposalToRestartContract(organization);
        await AssociationContractImplStub.Release.SendAsync(proposalId);
        var bridgeBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = BridgeContractAddress
        });
        {
            var executionResult = await ReceiverBridgeContractStubs.First().SwapToken.SendAsync(new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[0].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId,
                SwapId = _swapHashOfElf
            });
            var log = TokenSwapped.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(TokenSwapped)).NonIndexed);
            log.FromChainId.ShouldBe("Ethereum");
            await CheckBalanceAsync(Receivers.First().Address, "ELF", 10000000L);
            var swapAmount = await ReceiverBridgeContractStubs.First().GetSwapAmounts.CallAsync(new GetSwapAmountsInput
            {
                SwapId = _swapHashOfElf,
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId
            });
            swapAmount.Receiver.ShouldBe(Receivers.First().Address);
            swapAmount.ReceivedAmounts["ELF"].ShouldBe(10000000L);

            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.SwappedTimes.ShouldBe(1);
            result.SwappedAmount.ShouldBe(10000000L);
            var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
            {
                TokenSymbol = "ELF"
            });
            tokenPoolInfo.Liquidity.ShouldBe(9999990000000);
        }
    }

    [Fact]
    public async Task SwapTokenTest_NoDeposit()
    {
        await InitialSwapAsync();
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
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<SwapDailyLimitInfo>
        {
            new SwapDailyLimitInfo
            {
                SwapId = _swapHashOfElf,
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetSwapDailyLimit.SendAsync(new SetSwapDailyLimitInput
        {
            SwapDailyLimitInfos = { input }
        });
       
        {
            // Swap
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[2].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[2].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[2].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Pool liquidity is not enough.");
        }
    }

    [Fact]
    public async Task SwapTokenTest_ProofFail_IncorrectReceiptId()
    {
        await CreateSwapTestAsync();
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
        var result = await BridgeContractImplStub.SetSwapDailyLimit.SendAsync(new SetSwapDailyLimitInput
        {
            SwapDailyLimitInfos = { input }
        });
        
        {
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[2].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[2].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[1].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Merkle proof failed.");
        }
    }

    [Fact]
    public async Task SwapTokenTest_SwapIdIsNull()
    {
        await CreateSwapTestAsync();
        
        {
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[2].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[2].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[1].ReceiptId,
                SwapId = new Hash()
            });
            executionResult.TransactionResult.Error.ShouldContain("Token swap pair not found.");
        }
    }

    [Fact]
    public async Task SwapTokenTest_ProofFail_AmountIsZero()
    {
        await CreateSwapTestAsync();
        
        {
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[2].Address,
                OriginAmount = "0",
                ReceiptId = SampleSwapInfo.SwapInfos[2].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid token swap input.");
        }
    }

    [Fact]
    public async Task SwapTokenTest_IncorrectAmount()
    {
        await CreateSwapTestAsync();
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
        var result = await BridgeContractImplStub.SetSwapDailyLimit.SendAsync(new SetSwapDailyLimitInput
        {
            SwapDailyLimitInfos = { input }
        });
        
        {
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[2].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[1].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[2].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Merkle proof failed.");
        }
    }

    [Fact]
    public async Task SwapTokenTest_Duplicate()
    {
        await ToAElfPipelineTest();
        var executionResult = await ReceiverBridgeContractStubs.First().SwapToken.SendWithExceptionAsync(
            new SwapTokenInput
            {
                OriginAmount = SampleSwapInfo.SwapInfos[0].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId,
                SwapId = _swapHashOfElf
            });
        executionResult.TransactionResult.Error.ShouldContain("Already claimed.");
    }

    #endregion

    // #region Deposit
    //
    // [Fact]
    // public async Task DepositTest_NoPermission()
    // {
    //     await CreateSwapTestAsync();
    //     var executionResult = await LockBridgeContractStubs[0].Deposit.SendWithExceptionAsync(new DepositInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         Amount = 1000_00000000,
    //         TargetTokenSymbol = "ELF"
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("No permission.");
    // }
    //
    // [Fact]
    // public async Task DepositTest_SwapIdIsNull()
    // {
    //     await CreateSwapTestAsync();
    //     var executionResult = await LockBridgeContractStubs[0].Deposit.SendWithExceptionAsync(new DepositInput
    //     {
    //         SwapId = new Hash(),
    //         Amount = 1000_00000000,
    //         TargetTokenSymbol = "ELF"
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("Token swap pair not found.");
    // }
    //
    // [Fact]
    // public async Task DepositTest_SymbolNotExist()
    // {
    //     await CreateSwapTestAsync();
    //     var executionResult = await BridgeContractStub.Deposit.SendWithExceptionAsync(new DepositInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         Amount = 1000_00000000,
    //         TargetTokenSymbol = "BNB"
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain($"Swap pair {_swapHashOfElf}-BNB is not exist.");
    // }
    //
    // [Fact]
    // public async Task DepositTest_InvalidAmount()
    // {
    //     await CreateSwapTestAsync();
    //     var executionResult = await BridgeContractStub.Deposit.SendWithExceptionAsync(new DepositInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         Amount = 0,
    //         TargetTokenSymbol = "ELF"
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("Invalid deposit amount.");
    // }
    //
    // [Fact]
    // public async Task WithdrawTest()
    // {
    //     await CreateSwapTestAsync();
    //
    //     var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //     {
    //         Symbol = "ELF",
    //         Owner = DefaultSenderAddress
    //     });
    //
    //     await BridgeContractStub.Withdraw.SendAsync(new WithdrawInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         TargetTokenSymbol = "ELF",
    //         Amount = 30000_00000000
    //     });
    //     {
    //         var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //         {
    //             Symbol = "ELF",
    //             Owner = BridgeContractAddress
    //         });
    //         balance.Balance.ShouldBe(70000_00000000);
    //     }
    //     {
    //         var userBalanceWithdraw = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //         {
    //             Symbol = "ELF",
    //             Owner = DefaultSenderAddress
    //         });
    //         userBalanceWithdraw.Balance.ShouldBe(userBalance.Balance + 30000_00000000);
    //     }
    //     {
    //         var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
    //         {
    //             SwapId = _swapHashOfElf,
    //             Symbol = "ELF"
    //         });
    //         result.DepositAmount.ShouldBe(7_0000_00000000);
    //     }
    // }
    //
    // [Fact]
    // public async Task WithdrawTest_NoPermission()
    // {
    //     await CreateSwapTestAsync();
    //
    //     var executionResult = await LockBridgeContractStubs[0].Withdraw.SendWithExceptionAsync(new WithdrawInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         TargetTokenSymbol = "ELF",
    //         Amount = 30000_00000000
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("No permission.");
    // }
    //
    // [Fact]
    // public async Task WithdrawTest_SwapIdIsNull()
    // {
    //     await CreateSwapTestAsync();
    //
    //     var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
    //     {
    //         SwapId = new Hash(),
    //         TargetTokenSymbol = "ELF",
    //         Amount = 30000_00000000
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("Token swap pair not found.");
    // }
    //
    // [Fact]
    // public async Task WithdrawTest_SymbolNotExist()
    // {
    //     await CreateSwapTestAsync();
    //
    //     var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         TargetTokenSymbol = "BNB",
    //         Amount = 30000_00000000
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain($"Swap pair {_swapHashOfElf}-BNB is not exist.");
    // }
    //
    // [Fact]
    // public async Task WithdrawTest_InvalidAmount()
    // {
    //     await CreateSwapTestAsync();
    //     var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         Amount = 0,
    //         TargetTokenSymbol = "ELF"
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("Invalid withdraw amount.");
    // }
    //
    // [Fact]
    // public async Task WithdrawTest_DepositNotEnough()
    // {
    //     await WithdrawTest();
    //     var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
    //     {
    //         SwapId = _swapHashOfElf,
    //         Amount = 8_0000_00000000,
    //         TargetTokenSymbol = "ELF"
    //     });
    //     executionResult.TransactionResult.Error.ShouldContain("Contract balance not enough");
    // }
    //
    // #endregion
}