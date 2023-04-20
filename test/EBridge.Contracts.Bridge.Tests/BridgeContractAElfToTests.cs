using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CSharp.Core;
using AElf.Types;
using EBridge.Contracts.Bridge.Helpers;
using EBridge.Contracts.Report;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests : BridgeContractTestBase
{
    [Fact]
    public async Task<(Address, Address)> InitialAElfTo()
    {
        await InitialOracleContractAsync();
        var organization = await InitialBridgeContractAsync();
        await InitialReportContractAsync();
        await InitialMerkleTreeContractAsync();
        await CreateAndIssueUSDTAsync();
        await CreateRegimentTest();

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
                    ChainId = "Kovan",
                    Symbol = "USDT"
                }
            }
        });
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            Amount = 10000_00000000,
            Symbol = "ELF",
            To = Lockers[0].Address,
            Memo = "test"
        });

        await CheckBalanceAsync(Lockers[0].Address, "ELF", 10000_00000000);
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Amount = 10000_00000000,
            Symbol = "ELF",
            Spender = Lockers[0].Address
        });

        var allowance = await TokenContractStub2.GetAllowance.CallAsync(new GetAllowanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress,
            Spender = Lockers[0].Address
        });
        allowance.Allowance.ShouldBe(10000_00000000);

        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Amount = long.MaxValue,
            Spender = BridgeContractAddress,
            Symbol = "ELF"
        });
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Amount = long.MaxValue,
            Spender = ReportContractAddress,
            Symbol = "ELF"
        });
        var regimentId = HashHelper.ComputeFrom(_regimentAddress);

        await ReportContractStub.RegisterOffChainAggregation.SendAsync(new RegisterOffChainAggregationInput
        {
            Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            RegimentId = regimentId,
            ChainId = "Ethereum"
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
        return organization;
    }

    [Fact]
    public async Task AElfToPipelineTest()
    {
        await InitialAElfTo();
        await InitialSetGas();
        {
            var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = "ELF",
                Owner = DefaultSenderAddress
            })).Balance;

            var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 100_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });

            var receiptCreated = ReceiptCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(ReceiptCreated)).NonIndexed);
            var receiptId = receiptCreated.ReceiptId;

            {
                var receiptInfo = await BridgeContractStub.GetReceiptInfo.CallAsync(new StringValue
                {
                    Value = receiptId
                });
                receiptInfo.Owner.ShouldBe(DefaultSenderAddress);
                receiptInfo.Symbol.ShouldBe("ELF");
            }

            var actualFee = (await BridgeContractStub.GetFeeByChainId.CallAsync(new StringValue
            {
                Value = "Ethereum"
            })).Value;
            actualFee.ShouldBe(31_00000000);
            await CheckBalanceAsync(BridgeContractAddress, "ELF", 100_00000000 + actualFee);
            await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000 - actualFee);

            var reportProposed = ReportProposed.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(ReportProposed)).NonIndexed);
            reportProposed.TargetChainId.ShouldBe("Ethereum");
            var title = $"lock_token_{receiptId}";
            reportProposed.QueryInfo.Title.ShouldBe(title);

            var rawReport = await ReportContractStub.GetRawReport.CallAsync(new GetRawReportInput
            {
                ChainId = "Ethereum",
                Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                RoundId = 1
            });

            _receiptDictionary = new Dictionary<string, Hash>();
            _receiptDictionary[receiptId] =
                CalculateReceiptHash(receiptId, 100_00000000, "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04");
            //var result = ByteArrayHelper.HexStringToByteArray(rawReport.Value);

            // var data = new List<byte>();
            // data.AddRange(FillObservationBytes(_receiptDictionary[receiptId].ToHex().GetBytes()));
            //data.ShouldBe(result.ToList().GetRange(96,32));


            var regimentInfo = await RegimentContractStub.GetRegimentInfo.CallAsync(_regimentAddress);
            var skipList = new MemberList();
            foreach (var admin in regimentInfo.Admins)
            {
                skipList.Value.Add(admin);
            }

            skipList.Value.Add(regimentInfo.Manager);
            await ReportContractStub.SetSkipMemberList.SendAsync(new SetSkipMemberListInput
            {
                ChainId = "Ethereum",
                Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                Value = skipList
            });
            foreach (var account in Transmitters)
            {
                var stub = GetReportContractStub(account.KeyPair);
                await stub.ConfirmReport.SendAsync(new ConfirmReportInput
                {
                    ChainId = "Ethereum",
                    Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                    RoundId = 1,
                    Signature = SignHelper.GetSignature(rawReport.Value, account.KeyPair.PrivateKey).RecoverInfo
                });
            }
        }
        {
            var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 50_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            var receiptCreated = ReceiptCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(ReceiptCreated)).NonIndexed);
            var receiptId = receiptCreated.ReceiptId;

            var reportProposed = ReportProposed.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(ReportProposed)).NonIndexed);
            reportProposed.TargetChainId.ShouldBe("Ethereum");
            var title = $"lock_token_{receiptId}";
            reportProposed.QueryInfo.Title.ShouldBe(title);

            var rawReport = await ReportContractStub.GetRawReport.CallAsync(new GetRawReportInput
            {
                ChainId = "Ethereum",
                Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                RoundId = 2
            });

            var report = await ReportContractStub.GetReport.CallAsync(new GetReportInput
            {
                ChainId = "Ethereum",
                Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                RoundId = 2
            });
            var receiptIdTokenHash = report.Observations.Value.First().Key.Split(".").First();
            var receiptIdInfo =
                await BridgeContractStub.GetReceiptIdInfo.CallAsync(Hash.LoadFromHex(receiptIdTokenHash));
            receiptIdInfo.Symbol.ShouldBe("ELF");


            foreach (var account in Transmitters)
            {
                var stub = GetReportContractStub(account.KeyPair);
                await stub.ConfirmReport.SendAsync(new ConfirmReportInput
                {
                    ChainId = "Ethereum",
                    Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                    RoundId = 2,
                    Signature = SignHelper.GetSignature(rawReport.Value, account.KeyPair.PrivateKey).RecoverInfo
                });
            }
        }
        {
            await CheckBalanceAsync(BridgeContractAddress, "ELF",  150_00000000 + 62_00000000);
        }
    }

    [Fact]
    public async Task SwapTokenWithoutDeposit()
    {
        await AElfToPipelineTest();
        {
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
            {
                // Query
                var queryId = await MakeQueryAsync(_swapHashOfElf.ToString(), 1, 3);

                // Commit
                await CommitAndRevealAsync(queryId, _swapHashOfElf, "Ethereum", "ELF", 1, 3);
            }
            await CheckBalanceAsync(BridgeContractAddress, "ELF",  150_00000000 + 62_00000000);
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
            await CheckBalanceAsync(BridgeContractAddress, "ELF",  150_00000000 + 62_00000000 - 10000000L);
            var swapPairInfo = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            swapPairInfo.DepositAmount.ShouldBe(0);
            {
                await BridgeContractStub.Deposit.SendAsync(new DepositInput
                {
                    SwapId = _swapHashOfElf,
                    TargetTokenSymbol = "ELF",
                    Amount = 10_0000_00000000
                });
            }
            var swapPairInfo1 = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            swapPairInfo1.DepositAmount.ShouldBe(10_0000_00000000);
            await BridgeContractStub.SwapToken.SendAsync(new SwapTokenInput
            {
                ReceiverAddress = Receivers[1].Address,
                OriginAmount = SampleSwapInfo.SwapInfos[1].OriginAmount,
                ReceiptId = SampleSwapInfo.SwapInfos[1].ReceiptId,
                SwapId = _swapHashOfElf
            });
            await CheckBalanceAsync(Receivers[1].Address, "ELF", 20000000L);
            var swapPairInfo2 = await BridgeContractStub.GetSwapPairInfo.CallAsync(new GetSwapPairInfoInput
            {
                SwapId = _swapHashOfElf,
                Symbol = "ELF"
            });
            swapPairInfo2.DepositAmount.ShouldBe(10_0000_00000000);
            await CheckBalanceAsync(BridgeContractAddress, "ELF",  150_00000000 + 62_00000000 - 10000000L - 20000000L + 10_0000_00000000);
        }
    }

    #region Token whitelist

    private async Task AddTokenTest_Initialize()
    {
        await InitialOracleContractAsync();
        await InitialBridgeContractAsync();
        await InitialReportContractAsync();
        await CreateAndIssueUSDTAsync();
        await CreateRegimentTest();
    }

    [Fact]
    public async Task AddTokenTest_NoPermission()
    {
        await AddTokenTest_Initialize();
        var executionResult = await LockBridgeContractStubs[0].AddToken.SendWithExceptionAsync(new AddTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                }
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task AddTokenTest_SameTokenOnce()
    {
        await AddTokenTest_Initialize();
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
                    Symbol = "ELF"
                }
            }
        });
        var symbolList = await BridgeContractStub.GetTokenWhitelist.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        symbolList.Symbol.Count.ShouldBe(1);
        symbolList.Symbol[0].ShouldBe("ELF");
    }

    [Fact]
    public async Task AddTokenTest_SameToken()
    {
        await AddTokenTest_Initialize();
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
                    ChainId = "Kovan",
                    Symbol = "ELF"
                }
            }
        });
        await BridgeContractStub.AddToken.SendAsync(new AddTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                }
            }
        });

        var symbolList = await BridgeContractStub.GetTokenWhitelist.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        symbolList.Symbol.Count.ShouldBe(1);
        symbolList.Symbol[0].ShouldBe("ELF");
    }

    [Fact]
    public async Task AddTokenTest_SymbolNotExist()
    {
        await AddTokenTest_Initialize();
        var executionResult = await BridgeContractStub.AddToken.SendWithExceptionAsync(new AddTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "BNB"
                }
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Token BNB info is not exist.");
    }

    [Fact]
    public async Task RemoveTokenTest()
    {
        await AddTokenTest_SameTokenOnce();
        await BridgeContractStub.RemoveToken.SendAsync(new RemoveTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                }
            }
        });
        {
            var tokenWhitelist = await BridgeContractStub.GetTokenWhitelist.CallAsync(new StringValue
            {
                Value = "Ethereum"
            });
            tokenWhitelist.Symbol.Count.ShouldBe(0);
        }
    }

    [Fact]
    public async Task RemoveTokenTest_NoPermission()
    {
        await AddTokenTest_Initialize();
        var executionResult = await LockBridgeContractStubs[0].RemoveToken.SendWithExceptionAsync(new RemoveTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                }
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task RemoveTokenTest_SameTokenOnce()
    {
        await AddTokenTest_SameToken();
        await BridgeContractStub.RemoveToken.SendAsync(new RemoveTokenInput
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
                    Symbol = "ELF"
                }
            }
        });
        {
            var symbol = await BridgeContractStub.GetTokenWhitelist.CallAsync(new StringValue
            {
                Value = "Ethereum"
            });
            symbol.Symbol.Count.ShouldBe(0);
        }
    }

    [Fact]
    public async Task RemoveTokenTest_SameToken()
    {
        await AddTokenTest_SameToken();
        await BridgeContractStub.RemoveToken.SendAsync(new RemoveTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                }
            }
        });
        {
            var symbolList = await BridgeContractStub.GetTokenWhitelist.CallAsync(new StringValue
            {
                Value = "Ethereum"
            });
            symbolList.Symbol.Count.ShouldBe(0);
        }
        {
            var symbolList = await BridgeContractStub.GetTokenWhitelist.CallAsync(new StringValue
            {
                Value = "Kovan"
            });
            symbolList.Symbol.Count.ShouldBe(1);
            symbolList.Symbol[0].ShouldBe("ELF");
        }
        var executionResult = await BridgeContractStub.RemoveToken.SendWithExceptionAsync(new RemoveTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "ELF"
                }
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Token ELF is not in whitelist.");
    }

    [Fact]
    public async Task RemoveTokenTest_ChainIdNotExist()
    {
        await AddTokenTest_SameTokenOnce();
        var executionResult = await BridgeContractStub.RemoveToken.SendWithExceptionAsync(new RemoveTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ploygon",
                    Symbol = "ELF"
                }
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Incorrect chain id Ploygon.");
    }

    [Fact]
    public async Task RemoveTokenTest_SymbolNotExist()
    {
        await AddTokenTest_SameTokenOnce();
        var executionResult = await BridgeContractStub.RemoveToken.SendWithExceptionAsync(new RemoveTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Ethereum",
                    Symbol = "USDT"
                }
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Token USDT is not in whitelist.");
    }

    #endregion

    #region Transaction fee

    [Fact]
    public async Task SetGasLimit_NoPermission()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeController.SendAsync(SampleAccount.Accounts[5].Address);
        var controller = await BridgeContractStub.GetContractController.CallAsync(new Empty());
        controller.ShouldBe(SampleAccount.Accounts[5].Address);
        {
            var result = await LockBridgeContractStubs[0].SetGasLimit.SendWithExceptionAsync(new SetGasLimitInput
            {
                GasLimitList =
                {
                    new GasLimit()
                    {
                        ChainId = "Ethereum",
                        GasLimit_ = 293414
                    }
                }
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
    }

    [Fact]
    public async Task SetGasLimit_Incorrect()
    {
        await InitialBridgeContractAsync();
        {
            var result = await BridgeContractStub.SetGasLimit.SendWithExceptionAsync(new SetGasLimitInput
            {
                GasLimitList =
                {
                    new GasLimit()
                    {
                        ChainId = "Ethereum",
                        GasLimit_ = -1
                    }
                }
            });
            result.TransactionResult.Error.ShouldContain("Incorrect gas limit.");
        }
    }

    [Fact]
    public async Task SetGasPrice_NoPermission()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeController.SendAsync(SampleAccount.Accounts[5].Address);
        var controller = await BridgeContractStub.GetContractController.CallAsync(new Empty());
        controller.ShouldBe(SampleAccount.Accounts[5].Address);
        {
            var result = await LockBridgeContractStubs[0].SetGasPrice.SendWithExceptionAsync(new SetGasPriceInput
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
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
    }

    [Fact]
    public async Task SetGasPrice_Incorrect()
    {
        await InitialBridgeContractAsync();
        {
            var result = await BridgeContractStub.SetGasPrice.SendWithExceptionAsync(new SetGasPriceInput
            {
                GasPriceList =
                {
                    new GasPrice
                    {
                        ChainId = "Ethereum",
                        GasPrice_ = -1
                    }
                }
            });
            result.TransactionResult.Error.ShouldContain("Incorrect gas price.");
        }
    }

    [Fact]
    public async Task SetPriceRatio_NoPermission()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeController.SendAsync(SampleAccount.Accounts[5].Address);
        var controller = await BridgeContractStub.GetContractController.CallAsync(new Empty());
        controller.ShouldBe(SampleAccount.Accounts[5].Address);
        {
            var result = await LockBridgeContractStubs[0].SetPriceRatio.SendWithExceptionAsync(new SetRatioInput
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
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
    }

    [Fact]
    public async Task SetPriceRatio_Incorrect()
    {
        await InitialBridgeContractAsync();
        {
            var result = await BridgeContractStub.SetPriceRatio.SendWithExceptionAsync(new SetRatioInput
            {
                Value =
                {
                    new Ratio
                    {
                        ChainId = "Ethereum",
                        Ratio_ = -1
                    }
                }
            });
            result.TransactionResult.Error.ShouldContain("Incorrect price ratio.");
        }
    }

    [Fact]
    public async Task SetFeeFloatingRatio_NoPermission()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.ChangeController.SendAsync(SampleAccount.Accounts[5].Address);
        var controller = await BridgeContractStub.GetContractController.CallAsync(new Empty());
        controller.ShouldBe(SampleAccount.Accounts[5].Address);
        {
            var result = await LockBridgeContractStubs[0].SetFeeFloatingRatio.SendWithExceptionAsync(
                new SetRatioInput
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
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
    }

    [Fact]
    public async Task SetFeeFloatingRatio_Incorrect()
    {
        await InitialBridgeContractAsync();
        {
            var result = await BridgeContractStub.SetFeeFloatingRatio.SendWithExceptionAsync(
                new SetRatioInput
                {
                    Value =
                    {
                        new Ratio
                        {
                            ChainId = "Ethereum",
                            Ratio_ = -1
                        }
                    }
                });
            result.TransactionResult.Error.ShouldContain("Incorrect fee floating ratio.");
        }
    }


    [Fact]
    public async Task AElfToSetFeeTest()
    {
        await InitialAElfTo();
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
        var gasFee = await BridgeContractStub.GetGasLimit.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        gasFee.Value.ShouldBe(293414);
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
        var gasPrice = await BridgeContractStub.GetGasPrice.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        gasPrice.Value.ShouldBe(8245816000);
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
        var priceRatio = await BridgeContractStub.GetPriceRatio.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        priceRatio.Value.ShouldBe(1052631578947);

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
        var floatingRatio = await BridgeContractStub.GetFeeFloatingRatio.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        floatingRatio.Value.ShouldBe("1.2");

        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress
        })).Balance;
        // var controller = await ParliamentContractStub.GetDefaultOrganizationAddress.CallAsync(new Empty());
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        var transactionFee = gasFee.Value * (decimal) gasPrice.Value / 1000000000 * (decimal) priceRatio.Value /
            100000000 * decimal.Parse(floatingRatio.Value);
        var fee = decimal.Round(transactionFee / 1000000000, 8);
        var actualFee = (long) decimal.Ceiling(fee) * 100000000;
        await CheckBalanceAsync(BridgeContractAddress, "ELF", 100_00000000 + actualFee);
        await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000 - actualFee);

        var getFee = await BridgeContractStub.GetFeeByChainId.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        getFee.Value.ShouldBe(actualFee);
    }

    [Fact]
    public async Task WithdrawTransactionFee_Test()
    {
        await AElfToPipelineTest();
        var transactionFee = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFee.Value.ShouldBe(62_00000000);
        var balanceContract = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = BridgeContractAddress,
            Symbol = "ELF"
        });
        var balanceAdmin = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        });
        await BridgeContractStub.WithdrawTransactionFee.SendAsync(new Int64Value
        {
            Value = 30_00000000
        });
        var balanceContractNew = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = BridgeContractAddress,
            Symbol = "ELF"
        });
        var balanceAdminNew = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        });
        balanceContractNew.Balance.ShouldBe(balanceContract.Balance - 30_00000000);
        balanceAdminNew.Balance.ShouldBe(balanceAdmin.Balance + 30_00000000);
        var transactionFeeAfter = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFeeAfter.Value.ShouldBe(32_00000000);
    }

    [Fact]
    public async Task WithdrawTransactionFee_Test_InsufficientAmount()
    {
        await AElfToPipelineTest();
        var transactionFee = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFee.Value.ShouldBe(62_00000000);
        var balanceContract = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = BridgeContractAddress,
            Symbol = "ELF"
        });
        var balanceAdmin = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        });
        var executionResult = await BridgeContractStub.WithdrawTransactionFee.SendWithExceptionAsync(new Int64Value
        {
            Value = 1000_00000000
        });
        executionResult.TransactionResult.Error.ShouldContain("Insufficient amount.");
        var transactionFeeAfter = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFeeAfter.Value.ShouldBe(62_00000000);
    }

    [Fact]
    public async Task WithdrawTransactionFee_Test_InvalidAmount()
    {
        await AElfToPipelineTest();
        var executionResult = await BridgeContractStub.WithdrawTransactionFee.SendWithExceptionAsync(new Int64Value
        {
            Value = -1
        });
        executionResult.TransactionResult.Error.ShouldContain("Invalid withdraw amount.");
    }

    [Fact]
    public async Task WithdrawTransactionFee_Test_All()
    {
        await AElfToPipelineTest();
        var transactionFee = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFee.Value.ShouldBe(62_00000000);
        var balanceContract = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = BridgeContractAddress,
            Symbol = "ELF"
        });
        var balanceAdmin = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        });
        await BridgeContractStub.WithdrawTransactionFee.SendAsync(new Int64Value
        {
            Value = 62_00000000
        });
        var balanceContractNew = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = BridgeContractAddress,
            Symbol = "ELF"
        });
        var balanceAdminNew = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        });
        balanceContractNew.Balance.ShouldBe(balanceContract.Balance - 62_00000000);
        balanceAdminNew.Balance.ShouldBe(balanceAdmin.Balance + 62_00000000);
        var transactionFeeAfter = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFeeAfter.Value.ShouldBe(0);
    }

    [Fact]
    public async Task SetPriceFluctuationRatio_Test()
    {
        await InitialBridgeContractAsync();
        await BridgeContractStub.SetPriceFluctuationRatio.SendAsync(new SetRatioInput
        {
            Value =
            {
                new Ratio
                {
                    ChainId = "Ethereum",
                    Ratio_ = 5
                }
            }
        });
        var fluctuation = await BridgeContractStub.GetPriceFluctuationRatio.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        fluctuation.Value.ShouldBe(5);
    }

    [Fact]
    public async Task SetPriceFluctuationRatio_Test_NoPermission()
    {
        await InitialBridgeContractAsync();
        var executionResult = await BridgeContractSetFeeRatioStub.SetPriceFluctuationRatio.SendWithExceptionAsync(
            new SetRatioInput
            {
                Value =
                {
                    new Ratio
                    {
                        ChainId = "Ethereum",
                        Ratio_ = 5
                    }
                }
            });
        executionResult.TransactionResult.Error.ShouldContain("No Permission.");
    }

    [Fact]
    public async Task SetPriceFluctuationRatio_Test_Invalid()
    {
        await InitialBridgeContractAsync();
        {
            var executionResult = await BridgeContractStub.SetPriceFluctuationRatio.SendWithExceptionAsync(
                new SetRatioInput
                {
                    Value =
                    {
                        new Ratio
                        {
                            ChainId = "Ethereum",
                            Ratio_ = -1
                        }
                    }
                });
            executionResult.TransactionResult.Error.ShouldContain("Incorrect fluctuation.");
        }
        {
            var executionResult = await BridgeContractStub.SetPriceFluctuationRatio.SendWithExceptionAsync(
                new SetRatioInput
                {
                    Value =
                    {
                        new Ratio
                        {
                            ChainId = "Ethereum",
                            Ratio_ = 101
                        }
                    }
                });
            executionResult.TransactionResult.Error.ShouldContain("Incorrect fluctuation.");
        }
    }

    #endregion

    #region Lock

    [Fact]
    public async Task CreateReceiptTest_PriceRatioFluctuation()
    {
        await InitialAElfTo();
        await InitialSetGas();
        {
            await BridgeContractStub.SetPriceRatio.SendAsync(new SetRatioInput
            {
                Value =
                {
                    new Ratio
                    {
                        ChainId = "Ethereum",
                        Ratio_ = 2140350880000
                    }
                }
            });
            await BridgeContractStub.SetPriceFluctuationRatio.SendAsync(new SetRatioInput
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
            {
                var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(
                    new CreateReceiptInput
                    {
                        Symbol = "ELF",
                        Amount = 100_00000000,
                        TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                        TargetChainId = "Ethereum"
                    });
                executionResult.TransactionResult.Error.ShouldContain("Price fluctuation higher than 20 percent.");
            }
            {
                await BridgeContractStub.SetPriceFluctuationRatio.SendAsync(new SetRatioInput
                {
                    Value =
                    {
                        new Ratio
                        {
                            ChainId = "Ethereum",
                            Ratio_ = 51
                        }
                    }
                });
            }
            {
                await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
                {
                    Symbol = "ELF",
                    Amount = 100_00000000,
                    TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                    TargetChainId = "Ethereum"
                });
            }
        }
    }

    [Fact]
    public async Task CreateReceiptTest_LockAmountIsZero()
    {
        await InitialAElfTo();
        await InitialSetGas();
        {
            var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 0,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            executionResult.TransactionResult.Error.ShouldContain("Insufficient lock amount ");
        }
    }

    [Fact]
    public async Task CreateReceiptTest_TokenIsNotInWhitelist()
    {
        await InitialAElfTo();
        var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
        {
            Symbol = "xxx",
            Amount = 100_00000000,
            TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
            TargetChainId = "Ethereum"
        });
        executionResult.TransactionResult.Error.ShouldContain("Token xxx is not in whitelist.");
    }

    [Fact]
    public async Task CreateReceiptTest_NotExistTokenWhitelist()
    {
        await InitialAElfTo();
        var executionResult1 = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
        {
            Symbol = "xxx",
            Amount = 100_00000000,
            TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
            TargetChainId = "Ploygon"
        });
        executionResult1.TransactionResult.Error.ShouldContain("No symbol list under the chain id Ploygon.");
    }

    [Fact]
    public async Task<(Address, Address)> CreateReceiptTest_Pause()
    {
        var organization = await InitialBridgeContractAsync();
        await BridgeContractStub.Pause.SendAsync(new Empty());
        var state = await BridgeContractStub.IsContractPause.CallAsync(new Empty());
        state.Value.ShouldBe(true);
        var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
            TargetChainId = "Ethereum"
        });
        executionResult.TransactionResult.Error.ShouldContain("Contract is paused.");
        return organization;
    }

    [Fact]
    public async Task CreateReceiptTest_Restart()
    {
        var organization = await InitialAElfTo();
        await InitialSetGas();
        {
            await BridgeContractStub.Pause.SendAsync(new Empty());
            var state = await BridgeContractStub.IsContractPause.CallAsync(new Empty());
            state.Value.ShouldBe(true);
        }
        var proposalId = await ProposalToRestartContract(organization);
        await AssociationContractImplStub.Release.SendAsync(proposalId);
        {
            var state = await BridgeContractStub.IsContractPause.CallAsync(new Empty());
            state.Value.ShouldBe(false);
        }
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
            TargetChainId = "Ethereum"
        });
    }

    [Fact]
    public async Task QueryOracle_NotRegisterOffChainInfo()
    {
        await InitialAElfTo();
        await InitialSetGas();
        var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
            TargetChainId = "Ploygon"
        });
        executionResult.TransactionResult.Error.ShouldContain("No symbol list under the chain id Ploygon.");
    }

    [Fact]
    public async Task ConfirmReport_NotProposed()
    {
        await InitialAElfTo();
        foreach (var account in Transmitters)
        {
            var stub = GetReportContractStub(account.KeyPair);
            var rawTest = new StringValue();
            var executionResult = await stub.ConfirmReport.SendWithExceptionAsync(new ConfirmReportInput
            {
                ChainId = "Ethereum",
                Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                RoundId = 1,
                Signature = SignHelper.GetSignature(rawTest.Value, account.KeyPair.PrivateKey).RecoverInfo
            });
            executionResult.TransactionResult.Error.ShouldContain("Report of round 1 not proposed.");
        }
    }

    [Fact]
    public async Task ConfirmReport_Duplicate()
    {
        await AElfToPipelineTest();
        foreach (var account in Transmitters)
        {
            var stub = GetReportContractStub(account.KeyPair);
            var rawTest = new StringValue();
            var executionResult = await stub.ConfirmReport.SendWithExceptionAsync(new ConfirmReportInput
            {
                ChainId = "Ethereum",
                Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
                RoundId = 1,
                Signature = SignHelper.GetSignature(rawTest.Value, account.KeyPair.PrivateKey).RecoverInfo
            });
            executionResult.TransactionResult.Error.ShouldContain("This report is already confirmed by all nodes.");
        }
    }

    [Fact]
    public async Task AElfToSetFeeTest_NotSetPriceRatio()
    {
        await InitialAElfTo();
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
        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress
        })).Balance;
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        await CheckBalanceAsync(BridgeContractAddress, "ELF", 100_00000000);
        await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000);
    }

    [Fact]
    public async Task AElfToSetFeeTest_GasLimitIsZero()
    {
        await InitialAElfTo();
        var gasFee = await BridgeContractStub.GetGasLimit.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        gasFee.Value.ShouldBe(0);
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
        await BridgeContractStub.SetPriceRatio.SendAsync(new SetRatioInput
        {
            Value =
            {
                new Ratio
                {
                    ChainId = "Ethereum",
                    Ratio_ = 10526315789474
                }
            }
        });

        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress
        })).Balance;
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        var getFee = await BridgeContractStub.GetFeeByChainId.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        getFee.Value.ShouldBe(0);
        await CheckBalanceAsync(BridgeContractAddress, "ELF", 100_00000000);
        await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000);
    }

    [Fact]
    public async Task AElfToSetFeeTest_GasPriceIsZero()
    {
        await InitialAElfTo();
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
        var gasFee = await BridgeContractStub.GetGasLimit.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        gasFee.Value.ShouldBe(293414);
        var gasPrice = await BridgeContractStub.GetGasPrice.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        gasPrice.Value.ShouldBe(0);
        //var priceRatioReal = 1 / 0.000095;
        await BridgeContractStub.SetPriceRatio.SendAsync(new SetRatioInput
        {
            Value =
            {
                new Ratio
                {
                    ChainId = "Ethereum",
                    Ratio_ = 10526315789474
                }
            }
        });
        var priceRatio = await BridgeContractStub.GetPriceRatio.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        priceRatio.Value.ShouldBe(10526315789474);

        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress
        })).Balance;
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        var transactionFee = gasFee.Value * (decimal) (gasPrice.Value) / 1000000000 * (decimal) priceRatio.Value /
                             100000000;
        var fee = decimal.Round(transactionFee / 1000000000, 8);
        var actualFee = (long) decimal.Ceiling(fee);
        var getFee = await BridgeContractStub.GetFeeByChainId.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        actualFee.ShouldBe(getFee.Value);
        await CheckBalanceAsync(BridgeContractAddress, "ELF", 100_00000000);
        await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000);
    }

    [Fact]
    public Task LeafReceiptHashTest()
    {
        var targetAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        var receiptId = "0x5e3f08e85db1c78f544e826f9417e034fd34010df16edf6391c866fd9f0659f9.1234";
        var amount = 100;
        var leafHash = CalculateReceiptHash(receiptId, amount, targetAddress);
        leafHash.ShouldBe(Hash.LoadFromHex("0xf3d4041e74fb7fc669591b5e3ca659760acb173b3486dd71090140a920a9ccbb"));
        return Task.CompletedTask;
    }

    private Hash CalculateReceiptHash(string receiptId, long amount, string targetAddress)
    {
        var addressHash = HashHelper.ComputeFrom(ByteArrayHelper.HexStringToByteArray(targetAddress));
        var amountEthereum = ConvertLong(amount);
        var amountHash = HashHelper.ComputeFrom(amountEthereum.ToArray());
        var receiptIdHash = HashHelper.ComputeFrom(receiptId);
        return HashHelper.ConcatAndCompute(receiptIdHash, amountHash, addressHash);
    }

    private IEnumerable<byte> ConvertLong(long data)
    {
        var b = data.ToBytes();
        if (b.Length == 32)
            return b;
        var diffCount = 32.Sub(b.Length);
        var longDataBytes = GetByteListWithCapacity(32);
        byte c = 0;
        if (data < 0)
        {
            c = 0xff;
        }

        for (var j = 0; j < diffCount; j++)
        {
            longDataBytes[j] = c;
        }

        BytesCopy(b, 0, longDataBytes, diffCount, b.Length);
        return longDataBytes;
    }

    private List<byte> GetByteListWithCapacity(int count)
    {
        var list = new List<byte>();
        list.AddRange(Enumerable.Repeat((byte) 0, count));
        return list;
    }

    private void BytesCopy(IReadOnlyList<byte> src, int srcOffset, List<byte> dst, int dstOffset, int count)
    {
        for (var i = srcOffset; i < srcOffset + count; i++)
        {
            dst[dstOffset] = src[i];
            dstOffset++;
        }
    }

    #endregion
}