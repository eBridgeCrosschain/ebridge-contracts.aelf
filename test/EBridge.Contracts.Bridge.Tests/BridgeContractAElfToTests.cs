using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CSharp.Core;
using AElf.Kernel;
using AElf.Types;
using EBridge.Contracts.Bridge.Helpers;
using EBridge.Contracts.TokenPool;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests : BridgeContractTestBase
{
    [Fact]
    public async Task<(Address, Address)> InitialAElfTo()
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
                    ChainId = "Sepolia",
                    Symbol = "ELF"
                },
                new ChainToken
                {
                    ChainId = "Kovan",
                    Symbol = "USDT"
                },
                new ChainToken
                {
                    ChainId = "Ton",
                    Symbol = "ELF"
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
        await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);
        await BridgeContractImplStub.SetCrossChainConfig.SendAsync(new()
        {
            ChainId = "Ton",
            ContractAddress = "kQDS511tzowt2x1xyIDgpglhaz6wG9uVP2t4BixFTViYQoM/",
            ChainIdNumber = 1100,
            ChainType = ChainType.Tvm,
            ContractAddressForReceive = "kQDS511tzowt2x1xyIDgpglhaz6wG9uVP2t4BixFTViYQoM/"
        });
        await BridgeContractImplStub.SetCrossChainConfig.SendAsync(new()
        {
            ChainId = "Ethereum",
            ContractAddress = "0x8243C4927257ef20dbF360b012C9f72f9A6427c3",
            ChainIdNumber = 11155111,
            ChainType = ChainType.Evm,
            ContractAddressForReceive = "0x3c37E0A09eAFEaA7eFB57107802De1B28A6f5F07/"
        });
        return organization;
    }

    private async Task<DateTime> SetLimit()
    {
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ton",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
        };

        await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });

        var input1 = new List<ReceiptTokenBucketConfig>()
        {
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 1_00000000
            },
            new ReceiptTokenBucketConfig
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 2_0000_00000000,
                Rate = 100_00000000
            },
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "Ton",
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 1_00000000
            }
        };
        await BridgeContractImplStub.ConfigReceiptTokenBucket.SendAsync(new ConfigReceiptTokenBucketInput
        {
            ReceiptTokenBucketConfigs = { input1 }
        });
        return time;
    }

    [Fact]
    public async Task AElfToPipelineTest()
    {
        await InitialAElfTo();
        var time = await SetLimit();
        var creatReceiptTime = TimestampHelper.GetUtcNow().ToDateTime();
        ;
        await InitialSetGas();
        await InitRampConfig();
        {
            var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = "ELF",
                Owner = DefaultSenderAddress
            })).Balance;

            blockTimeProvider.SetBlockTime(creatReceiptTime.AddMilliseconds(5).ToTimestamp());
            var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 100_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            {
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000 - 100_00000000);
                blockTimeProvider.SetBlockTime(creatReceiptTime.AddMilliseconds(5).AddMinutes(1).AddSeconds(1)
                    .ToTimestamp());
                var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                    new GetCurrentReceiptTokenBucketStateInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                bucket.CurrentTokenAmount.ShouldBeLessThanOrEqualTo(5_0000_00000000 - 100_00000000 + 65_00000000);
            }

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
            await CheckBalanceAsync(BridgeContractAddress, "ELF", actualFee);
            await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000 - actualFee);

            // var reportProposed = ReportProposed.Parser.ParseFrom(executionResult.TransactionResult.Logs
            //     .First(l => l.Name == nameof(ReportProposed)).NonIndexed);
            // reportProposed.TargetChainId.ShouldBe("Ethereum");
            // var title = $"lock_token_{receiptId}";
            // reportProposed.QueryInfo.Title.ShouldBe(title);
            // reportProposed.QueryInfo.Options.Count.ShouldBe(2);

            // var rawReport = await ReportContractStub.GetRawReport.CallAsync(new GetRawReportInput
            // {
            //     ChainId = "Ethereum",
            //     Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            //     RoundId = 1
            // });

            // _receiptDictionary = new Dictionary<string, Hash>();
            // _receiptDictionary[receiptId] =
            //     CalculateReceiptHash(receiptId, 100_00000000, "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04");
            // var result = ByteArrayHelper.HexStringToByteArray(rawReport.Value);
            // result.Length.ShouldBe(224);
            //
            // var data = new List<byte>();
            // data.AddRange(FillObservationBytes(ByteStringHelper
            //     .FromHexString(_receiptDictionary[receiptId].ToHex()).ToByteArray()));
            // data.ShouldBe(result.ToList().GetRange(96, 32));
            //
            // var data1 = new List<byte>();
            // data1.AddRange(FillObservationBytes(ConvertLong(10000000000).ToArray()));
            // data1.ShouldBe(result.ToList().GetRange(128, 32));
            //
            // var data2 = new List<byte>();
            // data2.AddRange(FillObservationBytes(ByteStringHelper
            //     .FromHexString("0x643C7DCAd9321b36de85FEaC19763BE492dB5a04").ToByteArray()));
            // data2.ShouldBe(result.ToList().GetRange(160, 32));
            //
            // var data3 = new List<byte>();
            // data3.AddRange(FillObservationBytes(ByteStringHelper
            //     .FromHexString(receiptId.Split(".").First()).ToByteArray()));
            // data3.ShouldBe(result.ToList().GetRange(192, 32));
            //
            // var regimentInfo = await RegimentContractStub.GetRegimentInfo.CallAsync(_regimentAddress);
            // var skipList = new MemberList();
            // foreach (var admin in regimentInfo.Admins)
            // {
            //     skipList.Value.Add(admin);
            // }
            //
            // skipList.Value.Add(regimentInfo.Manager);
            // await ReportContractStub.SetSkipMemberList.SendAsync(new SetSkipMemberListInput
            // {
            //     ChainId = "Ethereum",
            //     Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            //     Value = skipList
            // });
            // foreach (var account in Transmitters.SkipLast(1))
            // {
            //     var stub = GetReportContractStub(account.KeyPair);
            //     await stub.ConfirmReport.SendAsync(new ConfirmReportInput
            //     {
            //         ChainId = "Ethereum",
            //         Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            //         RoundId = 1,
            //         Signature = SignHelper.GetSignature(rawReport.Value, account.KeyPair.PrivateKey).RecoverInfo
            //     });
            // }
        }
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddMinutes(1)));
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

            {
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000 - 100_00000000 - 50_00000000);
                blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddMinutes(1).AddSeconds(1)));
                var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                    new GetCurrentReceiptTokenBucketStateInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                bucket.CurrentTokenAmount.ShouldBeLessThanOrEqualTo(5_0000_00000000 - 100_00000000 - 50_00000000 +
                                                                    61_00000000);
            }

            // var reportProposed = ReportProposed.Parser.ParseFrom(executionResult.TransactionResult.Logs
            //     .First(l => l.Name == nameof(ReportProposed)).NonIndexed);
            // reportProposed.TargetChainId.ShouldBe("Ethereum");
            // var title = $"lock_token_{receiptId}";
            // reportProposed.QueryInfo.Title.ShouldBe(title);
            //
            // var rawReport = await ReportContractStub.GetRawReport.CallAsync(new GetRawReportInput
            // {
            //     ChainId = "Ethereum",
            //     Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            //     RoundId = 2
            // });
            //
            // var report = await ReportContractStub.GetReport.CallAsync(new GetReportInput
            // {
            //     ChainId = "Ethereum",
            //     Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            //     RoundId = 2
            // });
            // var receiptIdTokenHash = report.Observations.Value.First().Key.Split(".").First();
            // var receiptIdInfo =
            //     await BridgeContractStub.GetReceiptIdInfo.CallAsync(Hash.LoadFromHex(receiptIdTokenHash));
            // receiptIdInfo.Symbol.ShouldBe("ELF");
            //
            //
            // foreach (var account in Transmitters.SkipLast(1))
            // {
            //     var stub = GetReportContractStub(account.KeyPair);
            //     await stub.ConfirmReport.SendAsync(new ConfirmReportInput
            //     {
            //         ChainId = "Ethereum",
            //         Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
            //         RoundId = 2,
            //         Signature = SignHelper.GetSignature(rawReport.Value, account.KeyPair.PrivateKey).RecoverInfo
            //     });
            // }
        }
        {
            await CheckBalanceAsync(BridgeContractAddress, "ELF", 62_00000000);
        }
        // exceed max amount
        {
            blockTimeProvider.SetBlockTime(
                Timestamp.FromDateTime(creatReceiptTime.AddMinutes(1).AddSeconds(1).AddMinutes(1)));
            var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 50000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            executionResult.TransactionResult.Error.ShouldContain(
                "Amount exceeds current token amount, the minimum wait time is");
            blockTimeProvider.SetBlockTime(
                Timestamp.FromDateTime(creatReceiptTime.AddMinutes(1).AddSeconds(1).AddMinutes(1).AddSeconds(30)));
            var executionResult1 = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 40000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            var log = ReceiptLimitChanged.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(ReceiptLimitChanged))?.NonIndexed);
            log.Symbol.ShouldBe("ELF");
            log.TargetChainId.ShouldBe("Ethereum");
            log.ReceiptDailyLimitRefreshTime.ShouldBe(Timestamp.FromDateTime(time.Date));
            log.CurrentReceiptDailyLimitAmount.ShouldBe(10_0000_00000000 - 100_00000000 - 50_00000000 - 40000_00000000);
            log.CurrentReceiptBucketTokenAmount.ShouldBe(5_0000_00000000 - 40000_00000000);
            log.ReceiptBucketUpdateTime.ShouldBe(
                Timestamp.FromDateTime(creatReceiptTime.AddMinutes(1).AddSeconds(1).AddMinutes(1).AddSeconds(30)));
            {
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000 - 100_00000000 - 50_00000000 - 40000_00000000);
                blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddMinutes(1).AddSeconds(1)
                    .AddMinutes(1).AddSeconds(30).AddSeconds(1)));
                var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                    new GetCurrentReceiptTokenBucketStateInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000 - 100_00000000 - 50_00000000 + 61_00000000 +
                    60_00000000 + 29_00000000 - 40000_00000000 + 1_00000000);
            }
        }
    }

    [Fact]
    public async Task AElfToTonPipeline()
    {
        await InitialAElfTo();
        var time = await SetLimit();
        var creatReceiptTime = TimestampHelper.GetUtcNow().ToDateTime();
        await InitialSetGas();
        await BridgeContractImplStub.SetCrossChainConfig.SendAsync(new()
        {
            ChainId = "Ton",
            ContractAddress = "kQDS511tzowt2x1xyIDgpglhaz6wG9uVP2t4BixFTViYQoM/",
            ChainIdNumber = 1100,
            ChainType = ChainType.Tvm,
            ContractAddressForReceive = "kQDS511tzowt2x1xyIDgpglhaz6wG9uVP2t4BixFTViYQoM/"
        });
        var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 1000000000000,
            TargetAddress = "EQBvA4zKQaQOjwu7HbyHiWJU7xQyzV4hre1YXq2PzVR2UTyT",
            TargetChainId = "Ton",
            TargetChainType = 1
        });
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 3000000000000,
            TargetAddress = "EQBvA4zKQaQOjwu7HbyHiWJU7xQyzV4hre1YXq2PzVR2UTyT",
            TargetChainId = "Ton",
            TargetChainType = 1
        });
    }

    [Fact]
    public async Task AElfToPipeline_DailyLimit_Test()
    {
        await InitialAElfTo();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };

        await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });

        var creatReceiptTime = TimestampHelper.GetUtcNow().ToDateTime();
        await InitialSetGas();
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime));
            var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 20_0000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            executionResult.TransactionResult.Error.ShouldContain(
                "Amount exceeds daily limit amount. Current daily limit is 10000000000000");
        }
        await InitRampConfig();
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddHours(1)));
            var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 8_0000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            {
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000 - 8_0000_00000000);
            }
            {
                blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddDays(2)));
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000);
            }
        }
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddHours(1)));
            var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 3_0000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            executionResult.TransactionResult.Error.ShouldContain(
                "Amount exceeds daily limit amount. Current daily limit is 2000000000000");
        }
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddDays(1).AddHours(1)));
            var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 3_0000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            {
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(10_0000_00000000 - 3_0000_00000000);
                dailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time.AddDays(1)));
            }
            var log = ReceiptLimitChanged.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(ReceiptLimitChanged))?.NonIndexed);
            log.Symbol.ShouldBe("ELF");
            log.TargetChainId.ShouldBe("Ethereum");
            log.ReceiptBucketUpdateTime.ShouldBeNull();
            log.ReceiptDailyLimitRefreshTime.ShouldBe(Timestamp.FromDateTime(time.AddDays(1)));
            log.CurrentReceiptDailyLimitAmount.ShouldBe(7_0000_00000000);
            log.CurrentReceiptBucketTokenAmount.ShouldBe(long.MaxValue);
        }
        {
            var input1 = new List<ReceiptDailyLimitInfo>
            {
                new ReceiptDailyLimitInfo
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 2_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time.AddDays(1))
                },
                new ReceiptDailyLimitInfo
                {
                    Symbol = "USDT",
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 5_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time.AddDays(1))
                }
            };

            var executionResult = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(
                new SetReceiptDailyLimitInput
                {
                    ReceiptDailyLimitInfos = { input1 }
                });
            {
                var limitLogList = (from log in executionResult.TransactionResult.Logs
                    where log.Name == nameof(ReceiptDailyLimitSet)
                    select ReceiptDailyLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
                limitLogList[0].Symbol.ShouldBe("ELF");
                limitLogList[0].TargetChainId.ShouldBe("Ethereum");
                limitLogList[0].ReceiptDailyLimit.ShouldBe(2_0000_00000000);
                limitLogList[0].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time.AddDays(1)));
                limitLogList[0].CurrentReceiptDailyLimit.ShouldBe(0);
            }

            {
                var dailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                    new GetReceiptDailyLimitInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                dailyLimit.TokenAmount.ShouldBe(0);
                dailyLimit.DefaultTokenAmount.ShouldBe(2_0000_00000000);
                dailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time.AddDays(1)));
            }
        }
    }

    [Fact]
    public async Task AElfToPipeline_BucketLimit_Test()
    {
        await InitialAElfTo();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };

        await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
        var input1 = new List<ReceiptTokenBucketConfig>()
        {
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 1_00000000
            },
            new ReceiptTokenBucketConfig
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 2_0000_00000000,
                Rate = 100_00000000
            }
        };
        await BridgeContractImplStub.ConfigReceiptTokenBucket.SendAsync(new ConfigReceiptTokenBucketInput
        {
            ReceiptTokenBucketConfigs = { input1 }
        });

        var creatReceiptTime = TimestampHelper.GetUtcNow().ToDateTime();
        blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime));
        var result = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 8_0000_00000000,
            TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
            TargetChainId = "Ethereum"
        });
        result.TransactionResult.Error.ShouldContain("Amount exceeds token max capacity.");
        await InitialSetGas();
        await InitRampConfig();
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddSeconds(5)));
            var executionResult = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 5_0000_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            {
                blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddSeconds(10)));
                var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                    new GetCurrentReceiptTokenBucketStateInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000 - 5_0000_00000000 + 5_00000000);
            }
        }
        {
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddSeconds(5).AddHours(1)));
            var executionResult = await BridgeContractStub.CreateReceipt.SendWithExceptionAsync(new CreateReceiptInput
            {
                Symbol = "ELF",
                Amount = 3700_00000000,
                TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                TargetChainId = "Ethereum"
            });
            {
                var minWait = await BridgeContractImplStub.GetReceiptMinWaitTimeInSeconds.CallAsync(
                    new GetReceiptMinWaitTimeInSecondsInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum",
                        TokenAmount = 3700_00000000
                    });
                minWait.Value.ShouldBe(100);
            }
            executionResult.TransactionResult.Error.ShouldContain(
                "Amount exceeds current token amount, the minimum wait time is 100s");
            {
                blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(creatReceiptTime.AddSeconds(5).AddHours(2)));
                var executionResult1 = await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
                {
                    Symbol = "ELF",
                    Amount = 7000_00000000,
                    TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
                    TargetChainId = "Ethereum"
                });

                blockTimeProvider.SetBlockTime(
                    Timestamp.FromDateTime(creatReceiptTime.AddSeconds(5).AddHours(2).AddSeconds(1)));
                var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                    new GetCurrentReceiptTokenBucketStateInput
                    {
                        Symbol = "ELF",
                        TargetChain = "Ethereum"
                    });
                bucket.CurrentTokenAmount.ShouldBe(201_00000000);
            }
        }
    }

    // [Fact]
    //  public async Task RejectReportTest()
    // {
    //     await InitialAElfTo();
    //     await InitialSetGas();
    //     var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
    //     var input = new List<ReceiptDailyLimitInfo>
    //     {
    //         new ReceiptDailyLimitInfo
    //         {
    //             Symbol = "ELF",
    //             TargetChain = "Ethereum",
    //             DefaultTokenAmount = 10_0000_00000000,
    //             StartTime = Timestamp.FromDateTime(time)
    //         },
    //         new ReceiptDailyLimitInfo
    //         {
    //             Symbol = "USDT",
    //             TargetChain = "Ethereum",
    //             DefaultTokenAmount = 5_0000_00000000,
    //             StartTime = Timestamp.FromDateTime(time)
    //         },
    //         new ReceiptDailyLimitInfo
    //         {
    //             Symbol = "ELF",
    //             TargetChain = "BSC",
    //             DefaultTokenAmount = 10_0000_00000000,
    //             StartTime = Timestamp.FromDateTime(time)
    //         },
    //         new ReceiptDailyLimitInfo
    //         {
    //             Symbol = "USDT",
    //             TargetChain = "BSC",
    //             DefaultTokenAmount = 5_0000_00000000,
    //             StartTime = Timestamp.FromDateTime(time)
    //         }
    //     };
    //     await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
    //     {
    //         ReceiptDailyLimitInfos = { input }
    //     });
    //     {
    //         await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
    //         {
    //             Symbol = "ELF",
    //             Amount = 100_00000000,
    //             TargetAddress = "0x643C7DCAd9321b36de85FEaC19763BE492dB5a04",
    //             TargetChainId = "Ethereum"
    //         });
    //
    //         var rawReport = await ReportContractStub.GetRawReport.CallAsync(new GetRawReportInput
    //         {
    //             ChainId = "Ethereum",
    //             Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
    //             RoundId = 1
    //         });
    //         var regimentInfo = await RegimentContractStub.GetRegimentInfo.CallAsync(_regimentAddress);
    //         var skipList = new MemberList();
    //         foreach (var admin in regimentInfo.Admins)
    //         {
    //             skipList.Value.Add(admin);
    //         }
    //
    //         skipList.Value.Add(regimentInfo.Manager);
    //         await ReportContractStub.SetSkipMemberList.SendAsync(new SetSkipMemberListInput
    //         {
    //             ChainId = "Ethereum",
    //             Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
    //             Value = skipList
    //         });
    //         foreach (var transmitter in Transmitters)
    //         {
    //             await TokenContractStub.Transfer.SendAsync(new TransferInput
    //             {
    //                 Symbol = "PORT",
    //                 To = transmitter.Address,
    //                 Amount = 300_00000000
    //             });
    //             var stub = GetTokenContractStub(transmitter.KeyPair);
    //             await stub.Approve.SendAsync(new ApproveInput
    //             {
    //                 Symbol = "PORT",
    //                 Spender = ReportContractAddress,
    //                 Amount = 300_00000000
    //             });
    //         }
    //         foreach (var reportContractStub in TransmittersReportContractStubs)
    //         {
    //             await reportContractStub.ApplyObserver.SendAsync(new ApplyObserverInput
    //             {
    //                 RegimentAddressList = { _regimentAddress }
    //             });
    //         }
    //
    //         for (var i = 0; i < Transmitters.Count; i++)
    //         {
    //             var mortgagedToken = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
    //                 new GetObserverMortgagedTokenByRegimentInput
    //                 {
    //                     RegimentAddress = _regimentAddress,
    //                     ObserverAddress = Transmitters[i].Address
    //                 });
    //             mortgagedToken.Value.ShouldBe(200_00000000);
    //             var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //             {
    //                 Owner = Transmitters[i].Address,
    //                 Symbol = "PORT"
    //             })).Balance;
    //             if (i == 0)
    //             {
    //                 balance.ShouldBe(500000100_00000000);
    //             }
    //             else
    //             {
    //                 balance.ShouldBe(100_00000000);
    //             }
    //         }
    //         
    //
    //         for (var i = 0; i < Transmitters.Count - 1; i++)
    //         {
    //             var stub = GetReportContractStub(Transmitters[i].KeyPair);
    //             await stub.ConfirmReport.SendAsync(new ConfirmReportInput
    //             {
    //                 ChainId = "Ethereum",
    //                 Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
    //                 RoundId = 1,
    //                 Signature = SignHelper.GetSignature(rawReport.Value, Transmitters[i].KeyPair.PrivateKey).RecoverInfo
    //             });
    //             var amount = await stub.GetMortgagedTokenAmount.CallAsync(Transmitters[i].Address);
    //             amount.Value.ShouldBe(200_00000000);
    //         }
    //
    //         var lastStub = GetReportContractStub(Transmitters.Last().KeyPair);
    //         await lastStub.RejectReport.SendAsync(new RejectReportInput
    //         {
    //             ChainId = "Ethereum",
    //             Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
    //             RoundId = 1,
    //             AccusingNodes = { Transmitters.Last().Address }
    //         });
    //         var mortgagedTokenLast = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
    //             new GetObserverMortgagedTokenByRegimentInput
    //             {
    //                 RegimentAddress = _regimentAddress,
    //                 ObserverAddress = Transmitters.Last().Address
    //             });
    //         mortgagedTokenLast.Value.ShouldBe(100_00000000);
    //         var amount1 = await TransmittersReportContractStubs.Last().GetMortgagedTokenAmount.CallAsync(Transmitters.Last().Address);
    //         amount1.Value.ShouldBe(200_00000000);
    //         var mortgagedToken2 = await ReportContractStub.GetObserverMortgagedTokenByRegiment.CallAsync(
    //             new GetObserverMortgagedTokenByRegimentInput
    //             {
    //                 RegimentAddress = _regimentAddress,
    //                 ObserverAddress = Transmitters[1].Address
    //             });
    //         mortgagedToken2.Value.ShouldBe(200_00000000);
    //         var result = await TransmittersReportContractStubs.Last().ConfirmReport.SendWithExceptionAsync(new ConfirmReportInput
    //         {
    //             ChainId = "Ethereum",
    //             Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
    //             RoundId = 1,
    //             Signature = SignHelper.GetSignature(rawReport.Value, Transmitters.Last().KeyPair.PrivateKey).RecoverInfo
    //         });
    //         result.TransactionResult.Error.ShouldContain("This report is already rejected.");
    //         var amount2Before = await TransmittersReportContractStubs.Last().GetMortgagedTokenAmount.CallAsync(Transmitters.Last().Address);
    //         amount2Before.Value.ShouldBe(200_00000000);
    //         var amountNode = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //         {
    //             Owner = ReportContractAddress,
    //             Symbol = "PORT"
    //         });
    //         amountNode.Balance.ShouldBe(0);
    //         await TransmittersReportContractStubs.Last().QuitObserver.SendAsync(new QuitObserverInput
    //         {
    //             RegimentAddressList = { _regimentAddress }
    //         });
    //         var amount2After = await TransmittersReportContractStubs.Last().GetMortgagedTokenAmount.CallAsync(Transmitters.Last().Address);
    //         amount2After.Value.ShouldBe(0);
    //         var amountNodeAfter = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //         {
    //             Owner = ReportContractAddress,
    //             Symbol = "PORT"
    //         });
    //         amountNodeAfter.Balance.ShouldBe(100_00000000);
    //         var amountReport = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
    //         {
    //             Owner = ReportContractAddress,
    //             Symbol = "PORT"
    //         });
    //         amountReport.Balance.ShouldBe(100_00000000);
    //
    //     }
    // }

    #region Token whitelist

    private async Task AddTokenTest_Initialize()
    {
        await InitialBridgeContractAsync();
        await CreateAndIssueUSDTAsync();
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
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
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
        await InitRampConfig();
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        var transactionFee = gasFee.Value * (decimal)gasPrice.Value / 1000000000 * (decimal)priceRatio.Value /
            100000000 * decimal.Parse(floatingRatio.Value);
        var fee = decimal.Round(transactionFee / 1000000000, 8);
        var actualFee = (long)decimal.Ceiling(fee) * 100000000;
        await CheckBalanceAsync(BridgeContractAddress, "ELF", actualFee);
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
        transactionFee.Value.ShouldBe(93_00000000);
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
        transactionFeeAfter.Value.ShouldBe(63_00000000);
    }

    [Fact]
    public async Task WithdrawTransactionFee_Test_InsufficientAmount()
    {
        await AElfToPipelineTest();
        var transactionFee = await BridgeContractStub.GetCurrentTransactionFee.CallAsync(new Empty());
        transactionFee.Value.ShouldBe(93_00000000);
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
        transactionFeeAfter.Value.ShouldBe(93_00000000);
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
        transactionFee.Value.ShouldBe(93_00000000);
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
        transactionFeeAfter.Value.ShouldBe(31_00000000);
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
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
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
            await InitRampConfig();
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
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
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
        await InitRampConfig();
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

    // [Fact]
    // public async Task ConfirmReport_Duplicate()
    // {
    //     await AElfToPipelineTest();
    //     foreach (var account in Transmitters)
    //     {
    //         var stub = GetReportContractStub(account.KeyPair);
    //         var rawTest = new StringValue();
    //         var executionResult = await stub.ConfirmReport.SendWithExceptionAsync(new ConfirmReportInput
    //         {
    //             ChainId = "Ethereum",
    //             Token = "0xf8F862Aaeb9cb101383d27044202aBbe3a057eCC",
    //             RoundId = 1,
    //             Signature = SignHelper.GetSignature(rawTest.Value, account.KeyPair.PrivateKey).RecoverInfo
    //         });
    //         executionResult.TransactionResult.Error.ShouldContain("This report is already confirmed by all nodes.");
    //     }
    // }

    [Fact]
    public async Task AElfToSetFeeTest_NotSetPriceRatio()
    {
        await InitialAElfTo();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
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
        var balance = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Symbol = "ELF",
            Owner = DefaultSenderAddress
        })).Balance;
        await InitRampConfig();
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000);
    }

    [Fact]
    public async Task AElfToSetFeeTest_GasLimitIsZero()
    {
        await InitialAElfTo();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
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
        await InitRampConfig();
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        // var getFee = await BridgeContractStub.GetFeeByChainId.CallAsync(new StringValue
        // {
        //     Value = "Ethereum"
        // });
        // getFee.Value.ShouldBe(0);
        await CheckBalanceAsync(DefaultSenderAddress, "ELF", balance - 100_00000000);
    }

    [Fact]
    public async Task AElfToSetFeeTest_GasPriceIsZero()
    {
        await InitialAElfTo();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new ReceiptDailyLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
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
        await InitRampConfig();
        await BridgeContractStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            Symbol = "ELF",
            Amount = 100_00000000,
            TargetAddress = "0x6b123105e9a4c56f1Ee2eB012Bda74664ec63515",
            TargetChainId = "Ethereum"
        });
        var transactionFee = gasFee.Value * (decimal)(gasPrice.Value) / 1000000000 * (decimal)priceRatio.Value /
                             100000000;
        var fee = decimal.Round(transactionFee / 1000000000, 8);
        var actualFee = (long)decimal.Ceiling(fee);
        var getFee = await BridgeContractStub.GetFeeByChainId.CallAsync(new StringValue
        {
            Value = "Ethereum"
        });
        actualFee.ShouldBe(getFee.Value);
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
        list.AddRange(Enumerable.Repeat((byte)0, count));
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

    private List<byte> FillObservationBytes(byte[] result)
    {
        if (result.Length == 0)
            return GetByteListWithCapacity(32);
        var totalBytesLength = result.Length.Sub(1).Div(32).Add(1);
        var ret = GetByteListWithCapacity(totalBytesLength.Mul(32));
        // Pad with zeros in front until less than 32 bytes.
        BytesCopy(result, 0, ret, 32 - result.Length, result.Length);
        return ret;
    }

    private async Task InitRampConfig()
    {
        await BridgeContractImplStub.SetCrossChainConfig.SendAsync(new()
        {
            ChainId = "Sepolia",
            ContractAddress = "0x3c37E0A09eAFEaA7eFB57107802De1B28A6f5F07",
            ChainIdNumber = 11155111,
            ChainType = ChainType.Evm,
            ContractAddressForReceive = "0x8243C4927257ef20dbF360b012C9f72f9A6427c3"
        });
    }

    #endregion
}