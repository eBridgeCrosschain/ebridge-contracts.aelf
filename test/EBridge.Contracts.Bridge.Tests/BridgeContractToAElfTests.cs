using System;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using EBridge.Contracts.Oracle;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using CallbackInfo = EBridge.Contracts.Oracle.CallbackInfo;


namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests
{
    [Fact]
    public async Task<(Address, Address)> InitialSwapAsync()
    {
        await InitialOracleContractAsync();
        var organization = await InitialBridgeContractAsync();
        await InitialMerkleTreeContractAsync();
        await CreateAndIssueUSDTAsync();

        await CreateRegimentTest();

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
        await BridgeContractStub.SetTokenMaximumAmount.SendAsync(new SetMaximumAmountInput
        {
            Value =
            {
                new TokenMaximumAmount
                {
                    Symbol = "ELF",
                    MaximumAmount = 400000000
                },
                new TokenMaximumAmount
                {
                    Symbol = "USDT",
                    MaximumAmount = 400000000
                }
            }
        });
        {
            var tokenMaximumAmount = await BridgeContractStub.GetTokenMaximumAmount.CallAsync(new StringValue
            {
                Value = "ELF"
            });
            tokenMaximumAmount.Value.ShouldBe(400000000);
        }
        return organization;
    }

    [Fact]
    public async Task SetTokenMaximumAmount_NoPermission()
    {
        var executionResult = await BridgeContractSetFeeRatioStub.SetTokenMaximumAmount.SendWithExceptionAsync(
            new SetMaximumAmountInput
            {
                Value =
                {
                    new TokenMaximumAmount
                    {
                        Symbol = "ELF",
                        MaximumAmount = 4000000000_00000000
                    },
                    new TokenMaximumAmount
                    {
                        Symbol = "USDT",
                        MaximumAmount = 4000000000_00000000
                    }
                }
            });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task SetTokenMaximumAmount_Duplicate()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.SetTokenMaximumAmount.SendAsync(
            new SetMaximumAmountInput
            {
                Value =
                {
                    new TokenMaximumAmount
                    {
                        Symbol = "ELF",
                        MaximumAmount = 4000000000_00000000
                    },
                    new TokenMaximumAmount
                    {
                        Symbol = "ELF",
                        MaximumAmount = 5000000000_00000000
                    },
                    new TokenMaximumAmount
                    {
                        Symbol = "USDT",
                        MaximumAmount = 4000000000_00000000
                    }
                }
            });
        {
            var tokenMaximumAmount = await BridgeContractStub.GetTokenMaximumAmount.CallAsync(new StringValue
            {
                Value = "ELF"
            });
            tokenMaximumAmount.Value.ShouldBe(5000000000_00000000);
        }
    }
    
    [Fact]
    public async Task SetTokenMaximumAmount_Invalid()
    {
        await InitialBridgeContractAsync();
        var executionResult = await BridgeContractStub.SetTokenMaximumAmount.SendWithExceptionAsync(
            new SetMaximumAmountInput
            {
                Value =
                {
                    new TokenMaximumAmount
                    {
                        Symbol = "ELF",
                        MaximumAmount = 4000000000_00000000
                    },
                    new TokenMaximumAmount
                    {
                        Symbol = "ELF",
                        MaximumAmount = -1
                    },
                }
            });
        executionResult.TransactionResult.Error.ShouldContain("invalid MaximumAmount");
    }

    private async Task OracleQueryCommitAndReveal()
    {
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 4, 5);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 4, 5);
        }
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 6, 6);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 6, 6);
        }

        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfUsdt.ToString(), 1, 4);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfUsdt, "Ploygon", "USDT", 1, 4);
        }
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfUsdt.ToString(), 5, 5);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfUsdt, "Ploygon", "USDT", 5, 5);
        }
    }

    [Fact]
    public async Task ToAElfPipelineTest()
    {
        await CreateSwapTestAsync();
        await OracleQueryCommitAndReveal();
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
            result.DepositAmount.ShouldBe(10_0000_00000000);
            await CheckBalanceAsync(BridgeContractAddress,"ELF", bridgeBalance.Balance - 10000000L);
        }
        {
            {
                var executionResult = await BridgeContractStub.ApproveTransfer.SendAsync(new ApproveTransferInput
                {
                    ReceiptId = SampleSwapInfo.SwapInfos[1].ReceiptId
                });
                var log = ApproveTransfer.Parser.ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(ApproveTransfer)).NonIndexed);
                log.ReceiptId.ShouldBe(SampleSwapInfo.SwapInfos[1].ReceiptId);
                log.Sender.ShouldBe(DefaultSenderAddress);
            }
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
            result.DepositAmount.ShouldBe(10_0000_00000000);
        }
        {
            // Swap
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[4].Address,
                OriginAmount = (long.Parse(SampleSwapInfo.SwapInfos[4].OriginAmount) * 10).ToString(),
                ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Waiting for admin authorization");
            {
                var executionResult1 = await BridgeContractSetFeeRatioStub.ApproveTransfer.SendWithExceptionAsync(
                    new ApproveTransferInput
                    {
                        ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId
                    });
                executionResult1.TransactionResult.Error.ShouldContain("No permission.");
            }
            {
                await BridgeContractStub.ApproveTransfer.SendAsync(new ApproveTransferInput
                {
                    ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId
                });
            }
            {
                var executionResult1 = await BridgeContractStub.ApproveTransfer.SendWithExceptionAsync(
                    new ApproveTransferInput
                    {
                        ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId
                    });
                executionResult1.TransactionResult.Error.ShouldContain("The receipt has been approved");
            }
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
            result.DepositAmount.ShouldBe(10_0000_00000000);
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
        var bridgeUsdtBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "USDT",
            Owner = BridgeContractAddress
        });
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
            result.DepositAmount.ShouldBe(10_0000_00000000);
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
            result.DepositAmount.ShouldBe(10_0000_00000000);
        }
        {
            // Swap
            {
                var executionResult = await ReceiverBridgeContractStubs[4].SwapToken.SendWithExceptionAsync(
                    new SwapTokenInput
                    {
                        OriginAmount = (long.Parse(SampleSwapInfo.SwapInfos[9].OriginAmount)*10).ToString(),
                        ReceiptId = SampleSwapInfo.SwapInfos[9].ReceiptId,
                        SwapId = _swapHashOfUsdt
                    });
                executionResult.TransactionResult.Error.ShouldContain("Waiting for admin authorization");
                {
                    await BridgeContractStub.ApproveTransfer.SendAsync(new ApproveTransferInput
                    {
                        ReceiptId = SampleSwapInfo.SwapInfos[9].ReceiptId
                    });
                }
            }
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
        var regimentId = HashHelper.ComputeFrom(_regimentAddress);
        // Create swap.
        var createSwapResult = await BridgeContractStub.CreateSwap.SendAsync(new CreateSwapInput
        {
            RegimentId = regimentId,
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
        _swapOfElfSpaceId = await BridgeContractStub.GetSpaceIdBySwapId.CallAsync(_swapHashOfElf);
        var swapId = await BridgeContractStub.GetSwapIdByToken.CallAsync(new GetSwapIdByTokenInput
        {
            ChainId = "Ethereum",
            Symbol = "ELF"
        });
        swapId.ShouldBe(_swapHashOfElf);

        await BridgeContractStub.Deposit.SendAsync(new DepositInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "ELF",
            Amount = 10_0000_00000000
        });
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = BridgeContractAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(10_0000_00000000);
        }
        {
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.DepositAmount.ShouldBe(10_0000_00000000);
        }

        // Create another swap.
        createSwapResult = await BridgeContractStub.CreateSwap.SendAsync(new CreateSwapInput
        {
            RegimentId = regimentId,
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
        _swapOfUsdtSpaceId = await BridgeContractStub.GetSpaceIdBySwapId.CallAsync(_swapHashOfUsdt);
        await BridgeContractStub.Deposit.SendAsync(new DepositInput
        {
            SwapId = _swapHashOfUsdt,
            TargetTokenSymbol = "USDT",
            Amount = 10_0000_00000000
        });
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = BridgeContractAddress,
                Symbol = "USDT"
            });
            balance.Balance.ShouldBe(10_0000_00000000);
        }
        {
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfUsdt,
                Symbol = "USDT"
            });
            result.DepositAmount.ShouldBe(10_0000_00000000);
        }
        await PortTokenCreate();
        return organization;
    }

    [Fact]
    public async Task CreateSwapTest_NoPermission()
    {
        await InitialSwapAsync();
        var regimentId = await RegimentContractStub.GetRegimentId.CallAsync(_regimentAddress);
        var executionResult = await LockBridgeContractStubs[0].CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            MerkleTreeLeafLimit = 1024,
            RegimentId = regimentId,
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
            MerkleTreeLeafLimit = 1024,
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
        await InitialOracleContractAsync();
        await InitialBridgeContractAsync();
        await CreateAndIssueUSDTAsync();

        var regimentAddress = await CreateRegiment_Use_NotAdmin();

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
        var regimentId = await RegimentContractStub.GetRegimentId.CallAsync(regimentAddress);
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            MerkleTreeLeafLimit = 1024,
            RegimentId = regimentId,
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
        var regimentId = HashHelper.ComputeFrom(_regimentAddress);
        var result = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            RegimentId = regimentId,
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
        var regimentId = await RegimentContractStub.GetRegimentId.CallAsync(_regimentAddress);
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            RegimentId = regimentId,
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
        var regimentId = await RegimentContractStub.GetRegimentId.CallAsync(_regimentAddress);
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            MerkleTreeLeafLimit = 1024,
            RegimentId = regimentId,
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
            OracleContractAddress = OracleContractAddress,
            RegimentContractAddress = RegimentContractAddress,
            ReportContractAddress = ReportContractAddress,
            Admin = DefaultSenderAddress,
            Controller = DefaultSenderAddress
        });
        var regimentId = await RegimentContractStub.GetRegimentId.CallAsync(_regimentAddress);
        var executionResult = await BridgeContractStub.CreateSwap.SendWithExceptionAsync(new CreateSwapInput
        {
            MerkleTreeLeafLimit = 1024,
            RegimentId = regimentId,
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
        var bridgeBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = BridgeContractAddress
        });
        {
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.SwappedTimes.ShouldBe(0);
            result.SwappedAmount.ShouldBe(0);
            result.DepositAmount.ShouldBe(bridgeBalance.Balance);
        }
        {
            var result1 = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = new Hash(),
                Symbol = "XXX"
            });
            result1.SwappedTimes.ShouldBe(0);
            result1.DepositAmount.ShouldBe(0);
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

        // Query
        var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

        await BridgeContractStub.Pause.SendAsync(new Empty());

        // Commit
        await CommitAndReveal_Pause(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);

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
        // Query
        var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);
        await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
    }

    [Fact]
    public async Task ToAElfTest_RecordedIncorrectIndex()
    {
        await CreateSwapTestAsync();
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 2, 3);

            // Commit
            await CommitAndReveal_IncorrectReceiptIndex(queryId, _swapHashOfElf, "Ethereum", "ELF", 2, 3);
        }
    }
    
    [Fact]
    public async Task ToAElfTest_DuplicateCommit()
    {
        await CreateSwapTestAsync();
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 2);

            // Commit
            await Commit_Duplicate(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 2);
        }
    }

    [Fact]
    public async Task ToAElfTest_SwapIdIsNull()
    {
        await CreateSwapTestAsync();
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync_SwapIdIsNull(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
    }

    [Fact]
    public async Task ToAElfTest_SpaceIdIsNull()
    {
        await CreateSwapTestAsync();
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync_SpaceIdIsNull(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
    }

    [Fact]
    public async Task RecordReceiptHash_NoPermission()
    {
        var executionResult = await BridgeContractStub.FulfillQuery.SendWithExceptionAsync(new CallbackInput
        {
            QueryId = new Hash(),
            Result = new StringValue().ToByteString()
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task IsTransferCanReceive_Test()
    {
        await CreateSwapTestAsync();
        await OracleQueryCommitAndReveal();
        {
            var result = await BridgeContractStub.IsTransferCanReceive.CallAsync(new IsTransferCanReceiveInput
            {
                ReceiptId = SampleSwapInfo.SwapInfos[0].ReceiptId,
                Symbol = "ELF",
                Amount = (long.Parse(SampleSwapInfo.SwapInfos[0].OriginAmount)/10000000000).ToString()
            });
            result.Value.ShouldBe(true);
        }
        {
            var result = await BridgeContractStub.IsTransferCanReceive.CallAsync(new IsTransferCanReceiveInput
            {
                ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId,
                Symbol = "ELF",
                Amount = SampleSwapInfo.SwapInfos[4].OriginAmount
            });
            result.Value.ShouldBe(false);
        }
        await BridgeContractStub.ApproveTransfer.SendAsync(new ApproveTransferInput
        {
            ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId
        });
        {
            var result = await BridgeContractStub.IsTransferCanReceive.CallAsync(new IsTransferCanReceiveInput
            {
                ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId,
                Symbol = "ELF",
                Amount = SampleSwapInfo.SwapInfos[4].OriginAmount
            });
            result.Value.ShouldBe(true);
        }
    }

    [Fact]
    public async Task ApproveTransfer_Test_NoPermission()
    {
        await CreateSwapTestAsync();
        await OracleQueryCommitAndReveal();
        var execution = await BridgeContractSetFeeRatioStub.ApproveTransfer.SendWithExceptionAsync(
            new ApproveTransferInput
            {
                ReceiptId = SampleSwapInfo.SwapInfos[4].ReceiptId
            });
        execution.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task<(Address, Address)> SwapTokenTest_Pause()
    {
        var organization = await CreateSwapTestAsync();
        await OracleQueryCommitAndReveal();
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
            result.DepositAmount.ShouldBe(10_0000_00000000);
        }
    }

    [Fact]
    public async Task SwapTokenTest_NoDeposit()
    {
        await InitialSwapAsync();
        var regimentId = HashHelper.ComputeFrom(_regimentAddress);
        // Create swap.
        var createSwapResult = await BridgeContractStub.CreateSwap.SendAsync(new CreateSwapInput
        {
            RegimentId = regimentId,
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
        _swapOfElfSpaceId = await BridgeContractStub.GetSpaceIdBySwapId.CallAsync(_swapHashOfElf);
        await PortTokenCreate();
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
        {
            // Swap
            var executionResult = await BridgeContractStub.SwapToken.SendWithExceptionAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[2].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[2].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[2].ReceiptId,
                SwapId = _swapHashOfElf
            });
            executionResult.TransactionResult.Error.ShouldContain("Insufficient balance");
        }
    }

    [Fact]
    public async Task SwapTokenTest_ProofFail_IncorrectReceiptId()
    {
        await CreateSwapTestAsync();
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
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
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
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
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
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
        {
            // Query
            var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

            // Commit
            await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
        }
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

    #region Deposit

    [Fact]
    public async Task DepositTest_NoPermission()
    {
        await CreateSwapTestAsync();
        var executionResult = await LockBridgeContractStubs[0].Deposit.SendWithExceptionAsync(new DepositInput
        {
            SwapId = _swapHashOfElf,
            Amount = 1000_00000000,
            TargetTokenSymbol = "ELF"
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task DepositTest_SwapIdIsNull()
    {
        await CreateSwapTestAsync();
        var executionResult = await LockBridgeContractStubs[0].Deposit.SendWithExceptionAsync(new DepositInput
        {
            SwapId = new Hash(),
            Amount = 1000_00000000,
            TargetTokenSymbol = "ELF"
        });
        executionResult.TransactionResult.Error.ShouldContain("Token swap pair not found.");
    }

    [Fact]
    public async Task DepositTest_SymbolNotExist()
    {
        await CreateSwapTestAsync();
        var executionResult = await BridgeContractStub.Deposit.SendWithExceptionAsync(new DepositInput
        {
            SwapId = _swapHashOfElf,
            Amount = 1000_00000000,
            TargetTokenSymbol = "BNB"
        });
        executionResult.TransactionResult.Error.ShouldContain($"Swap pair {_swapHashOfElf}-BNB is not exist.");
    }

    [Fact]
    public async Task DepositTest_InvalidAmount()
    {
        await CreateSwapTestAsync();
        var executionResult = await BridgeContractStub.Deposit.SendWithExceptionAsync(new DepositInput
        {
            SwapId = _swapHashOfElf,
            Amount = 0,
            TargetTokenSymbol = "ELF"
        });
        executionResult.TransactionResult.Error.ShouldContain("Invalid deposit amount.");
    }

    [Fact]
    public async Task WithdrawTest()
    {
        await CreateSwapTestAsync();

        var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress
        });

        await BridgeContractStub.Withdraw.SendAsync(new WithdrawInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "ELF",
            Amount = 30000_00000000
        });
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = "ELF",
                Owner = BridgeContractAddress
            });
            balance.Balance.ShouldBe(70000_00000000);
        }
        {
            var userBalanceWithdraw = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = "ELF",
                Owner = DefaultSenderAddress
            });
            userBalanceWithdraw.Balance.ShouldBe(userBalance.Balance + 30000_00000000);
        }
        {
            var result = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            result.DepositAmount.ShouldBe(7_0000_00000000);
        }
    }

    [Fact]
    public async Task WithdrawTest_NoPermission()
    {
        await CreateSwapTestAsync();

        var executionResult = await LockBridgeContractStubs[0].Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "ELF",
            Amount = 30000_00000000
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task WithdrawTest_SwapIdIsNull()
    {
        await CreateSwapTestAsync();

        var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            SwapId = new Hash(),
            TargetTokenSymbol = "ELF",
            Amount = 30000_00000000
        });
        executionResult.TransactionResult.Error.ShouldContain("Token swap pair not found.");
    }

    [Fact]
    public async Task WithdrawTest_SymbolNotExist()
    {
        await CreateSwapTestAsync();

        var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            SwapId = _swapHashOfElf,
            TargetTokenSymbol = "BNB",
            Amount = 30000_00000000
        });
        executionResult.TransactionResult.Error.ShouldContain($"Swap pair {_swapHashOfElf}-BNB is not exist.");
    }

    [Fact]
    public async Task WithdrawTest_InvalidAmount()
    {
        await CreateSwapTestAsync();
        var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            SwapId = _swapHashOfElf,
            Amount = 0,
            TargetTokenSymbol = "ELF"
        });
        executionResult.TransactionResult.Error.ShouldContain("Invalid withdraw amount.");
    }

    [Fact]
    public async Task WithdrawTest_DepositNotEnough()
    {
        await WithdrawTest();
        var executionResult = await BridgeContractStub.Withdraw.SendWithExceptionAsync(new WithdrawInput
        {
            SwapId = _swapHashOfElf,
            Amount = 8_0000_00000000,
            TargetTokenSymbol = "ELF"
        });
        executionResult.TransactionResult.Error.ShouldContain("Contract balance not enough");
    }

    #endregion

    #region Helper

    private async Task<Hash> MakeQueryAsync(string swapId, long from, long end)
    {
        var queryInput = new QueryInput
        {
            Payment = 10000,
            QueryInfo = new QueryInfo
            {
                Title = $"record_receipts_{swapId}",
                Options = {$"{from}", $"{end}"}
            },
            AggregatorContractAddress = StringAggregatorContractAddress,
            CallbackInfo = new CallbackInfo
            {
                ContractAddress = BridgeContractAddress
            },
            DesignatedNodeList = new AddressList
            {
                Value = {_regimentAddress}
            }
        };
        var executionResult = await TransmittersOracleContractStubs.First().Query.SendAsync(queryInput);
        return executionResult.Output;
    }

    private async Task CommitAndRevealAsync(Hash queryId, Hash swapId, string chainId, string symbol, long from,
        long end)
    {
        var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(symbol));
        var receiptHashMap = new ReceiptHashMap
        {
            SwapId = swapId.ToHex()
        };
        for (var index = from; index <= end; index++)
        {
            var receiptId = $"{tokenId}.{index}";
            receiptHashMap.Value.Add(receiptId,
                chainId == "Ploygon"
                    ? SampleSwapInfo.SwapInfos[(int) index + 4].ReceiptHash.ToHex()
                    : SampleSwapInfo.SwapInfos[(int) index - 1].ReceiptHash.ToHex());
        }

        var salt = HashHelper.ComputeFrom("Salt");

        foreach (var account in Transmitters)
        {
            var stub = GetOracleContractStub(account.KeyPair);
            var dataHash = HashHelper.ComputeFrom(receiptHashMap.ToString());
            var commitInput = new CommitInput
            {
                QueryId = queryId,
                Commitment = HashHelper.ConcatAndCompute(
                    dataHash,
                    HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(account.Address.ToBase58())))
            };
            await stub.Commit.SendAsync(commitInput);
        }

        foreach (var stub in TransmittersOracleContractStubs.Take(3))
        {
            await stub.Reveal.SendAsync(new RevealInput
            {
                Data = receiptHashMap.ToString(),
                Salt = salt,
                QueryId = queryId
            });
        }
    }

    private async Task CommitAndReveal_Pause(Hash queryId, Hash swapId, string chainId, string symbol,
        long from, long end)
    {
        var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(symbol));
        var receiptHashMap = new ReceiptHashMap
        {
            SwapId = swapId.ToHex()
        };
        for (var index = from; index <= end; index++)
        {
            var receiptId = $"{tokenId}.{index}";
            receiptHashMap.Value.Add(receiptId,
                chainId == "Ploygon"
                    ? SampleSwapInfo.SwapInfos[(int) index + 4].ReceiptHash.ToHex()
                    : SampleSwapInfo.SwapInfos[(int) index - 1].ReceiptHash.ToHex());
        }

        var salt = HashHelper.ComputeFrom("Salt");

        foreach (var account in Transmitters)
        {
            var stub = GetOracleContractStub(account.KeyPair);
            var dataHash = HashHelper.ComputeFrom(receiptHashMap.ToString());
            var commitInput = new CommitInput
            {
                QueryId = queryId,
                Commitment = HashHelper.ConcatAndCompute(
                    dataHash,
                    HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(account.Address.ToBase58())))
            };
            await stub.Commit.SendAsync(commitInput);
        }

        foreach (var stub in TransmittersOracleContractStubs.Take(2))
        {
            await stub.Reveal.SendAsync(new RevealInput
            {
                Data = receiptHashMap.ToString(),
                Salt = salt,
                QueryId = queryId
            });
        }

        var executionResult = await TransmittersOracleContractStubs[2].Reveal.SendWithExceptionAsync(new RevealInput
        {
            Data = receiptHashMap.ToString(),
            Salt = salt,
            QueryId = queryId
        });
        executionResult.TransactionResult.Error.ShouldContain("Contract is paused.");
    }

    private async Task CommitAndReveal_IncorrectReceiptIndex(Hash queryId, Hash swapId, string chainId, string symbol,
        long from, long end)
    {
        var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(symbol));
        var receiptHashMap = new ReceiptHashMap
        {
            SwapId = swapId.ToHex()
        };
        for (var index = from; index <= end; index++)
        {
            var receiptId = $"{tokenId}.{index}";
            receiptHashMap.Value.Add(receiptId,
                chainId == "Ploygon"
                    ? SampleSwapInfo.SwapInfos[(int) index + 4].ReceiptHash.ToHex()
                    : SampleSwapInfo.SwapInfos[(int) index - 1].ReceiptHash.ToHex());
        }

        var salt = HashHelper.ComputeFrom("Salt");

        foreach (var account in Transmitters)
        {
            var stub = GetOracleContractStub(account.KeyPair);
            var dataHash = HashHelper.ComputeFrom(receiptHashMap.ToString());
            var commitInput = new CommitInput
            {
                QueryId = queryId,
                Commitment = HashHelper.ConcatAndCompute(
                    dataHash,
                    HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(account.Address.ToBase58())))
            };
            await stub.Commit.SendAsync(commitInput);
        }

        foreach (var stub in TransmittersOracleContractStubs.Take(2))
        {
            await stub.Reveal.SendAsync(new RevealInput
            {
                Data = receiptHashMap.ToString(),
                Salt = salt,
                QueryId = queryId
            });
        }

        var executionResult = await TransmittersOracleContractStubs[2].Reveal.SendWithExceptionAsync(new RevealInput
        {
            Data = receiptHashMap.ToString(),
            Salt = salt,
            QueryId = queryId
        });
        executionResult.TransactionResult.Error.ShouldContain("Incorrect receipt index.");
    }
    
    private async Task Commit_Duplicate(Hash queryId, Hash swapId, string chainId, string symbol,
        long from, long end)
    {
        var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(symbol));
        var receiptHashMap = new ReceiptHashMap
        {
            SwapId = swapId.ToHex()
        };
        for (var index = from; index <= end; index++)
        {
            var receiptId = $"{tokenId}.{index}";
            receiptHashMap.Value.Add(receiptId,
                chainId == "Ploygon"
                    ? SampleSwapInfo.SwapInfos[(int) index + 4].ReceiptHash.ToHex()
                    : SampleSwapInfo.SwapInfos[(int) index - 1].ReceiptHash.ToHex());
        }

        var salt = HashHelper.ComputeFrom("Salt");

        var account = Transmitters[0];
        var stub = GetOracleContractStub(account.KeyPair);
        var dataHash = HashHelper.ComputeFrom(receiptHashMap.ToString());
        var commitInput = new CommitInput
        {
            QueryId = queryId,
            Commitment = HashHelper.ConcatAndCompute(
                dataHash,
                HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(account.Address.ToBase58())))
        };
        await stub.Commit.SendAsync(commitInput);
        var executeResult = await stub.Commit.SendWithExceptionAsync(commitInput);
        executeResult.TransactionResult.Error.ShouldContain("already submit commitment");
    }

    private async Task CommitAndRevealAsync_SwapIdIsNull(Hash queryId, Hash swapId, string chainId, string symbol,
        long from, long end)
    {
        var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(symbol));
        var receiptHashMap = new ReceiptHashMap();
        for (var index = from; index <= end; index++)
        {
            var receiptId = $"{tokenId}.{index}";
            receiptHashMap.Value.Add(receiptId,
                chainId == "Ploygon"
                    ? SampleSwapInfo.SwapInfos[(int) index + 4].ReceiptHash.ToHex()
                    : SampleSwapInfo.SwapInfos[(int) index - 1].ReceiptHash.ToHex());
        }

        var salt = HashHelper.ComputeFrom("Salt");

        foreach (var account in Transmitters)
        {
            var stub = GetOracleContractStub(account.KeyPair);
            var dataHash = HashHelper.ComputeFrom(receiptHashMap.ToString());
            var commitInput = new CommitInput
            {
                QueryId = queryId,
                Commitment = HashHelper.ConcatAndCompute(
                    dataHash,
                    HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(account.Address.ToBase58())))
            };
            await stub.Commit.SendAsync(commitInput);
        }

        foreach (var stub in TransmittersOracleContractStubs.Take(2))
        {
            await stub.Reveal.SendAsync(new RevealInput
            {
                Data = receiptHashMap.ToString(),
                Salt = salt,
                QueryId = queryId
            });
        }

        var executionResult = await TransmittersOracleContractStubs[2].Reveal.SendWithExceptionAsync(new RevealInput
        {
            Data = receiptHashMap.ToString(),
            Salt = salt,
            QueryId = queryId
        });
        executionResult.TransactionResult.Error.ShouldContain("Swap id is null.");
    }

    private async Task CommitAndRevealAsync_SpaceIdIsNull(Hash queryId, Hash swapId, string chainId, string symbol,
        long from, long end)
    {
        var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(symbol));
        var receiptHashMap = new ReceiptHashMap
        {
            SwapId = HashHelper.ComputeFrom("111").ToHex()
        };
        for (var index = from; index <= end; index++)
        {
            var receiptId = $"{tokenId}.{index}";
            receiptHashMap.Value.Add(receiptId,
                chainId == "Ploygon"
                    ? SampleSwapInfo.SwapInfos[(int) index + 4].ReceiptHash.ToHex()
                    : SampleSwapInfo.SwapInfos[(int) index - 1].ReceiptHash.ToHex());
        }

        var salt = HashHelper.ComputeFrom("Salt");

        foreach (var account in Transmitters)
        {
            var stub = GetOracleContractStub(account.KeyPair);
            var dataHash = HashHelper.ComputeFrom(receiptHashMap.ToString());
            var commitInput = new CommitInput
            {
                QueryId = queryId,
                Commitment = HashHelper.ConcatAndCompute(
                    dataHash,
                    HashHelper.ConcatAndCompute(salt, HashHelper.ComputeFrom(account.Address.ToBase58())))
            };
            await stub.Commit.SendAsync(commitInput);
        }

        foreach (var stub in TransmittersOracleContractStubs.Take(2))
        {
            await stub.Reveal.SendAsync(new RevealInput
            {
                Data = receiptHashMap.ToString(),
                Salt = salt,
                QueryId = queryId
            });
        }

        var executionResult = await TransmittersOracleContractStubs[2].Reveal.SendWithExceptionAsync(new RevealInput
        {
            Data = receiptHashMap.ToString(),
            Salt = salt,
            QueryId = queryId
        });
        executionResult.TransactionResult.Error.ShouldContain("Space id is null.");
    }

    #endregion
}