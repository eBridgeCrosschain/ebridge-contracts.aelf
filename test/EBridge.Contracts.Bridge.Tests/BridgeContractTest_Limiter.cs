using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ContractTestKit;
using AElf.Kernel;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests
{
    [Fact]
    public async Task SetReceiptDailyLimit_Success()
    {
        await InitialBridgeContractAsync();
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
            var receiptDailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                new GetReceiptDailyLimitInput
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum"
                });
            receiptDailyLimit.DefaultTokenAmount.ShouldBe(10_0000_00000000);
            receiptDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            receiptDailyLimit.TokenAmount.ShouldBe(10_0000_00000000);
        }
        {
            var receiptDailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                new GetReceiptDailyLimitInput
                {
                    Symbol = "ELF",
                    TargetChain = "BSC"
                });
            receiptDailyLimit.DefaultTokenAmount.ShouldBe(10_0000_00000000);
            receiptDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            receiptDailyLimit.TokenAmount.ShouldBe(10_0000_00000000);
        }
        {
            var receiptDailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                new GetReceiptDailyLimitInput
                {
                    Symbol = "USDT",
                    TargetChain = "Ethereum"
                });
            receiptDailyLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
            receiptDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            receiptDailyLimit.TokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var receiptDailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                new GetReceiptDailyLimitInput
                {
                    Symbol = "USDT",
                    TargetChain = "BSC"
                });
            receiptDailyLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
            receiptDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            receiptDailyLimit.TokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(ReceiptDailyLimitSet) select ReceiptDailyLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].TargetChainId.ShouldBe("Ethereum");
            limitLogList[0].ReceiptDailyLimit.ShouldBe(10_0000_00000000);
            limitLogList[0].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[0].CurrentReceiptDailyLimit.ShouldBe(10_0000_00000000);
            limitLogList[1].Symbol.ShouldBe("USDT");
            limitLogList[1].TargetChainId.ShouldBe("Ethereum");
            limitLogList[1].ReceiptDailyLimit.ShouldBe(5_0000_00000000);
            limitLogList[1].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[1].CurrentReceiptDailyLimit.ShouldBe(5_0000_00000000);
            limitLogList[2].Symbol.ShouldBe("ELF");
            limitLogList[2].TargetChainId.ShouldBe("BSC");
            limitLogList[2].ReceiptDailyLimit.ShouldBe(10_0000_00000000);
            limitLogList[2].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[2].CurrentReceiptDailyLimit.ShouldBe(10_0000_00000000);
            limitLogList[3].Symbol.ShouldBe("USDT");
            limitLogList[3].TargetChainId.ShouldBe("BSC");
            limitLogList[3].ReceiptDailyLimit.ShouldBe(5_0000_00000000);
            limitLogList[3].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[3].CurrentReceiptDailyLimit.ShouldBe(5_0000_00000000);
        }

    }
    
    [Fact]
    public async Task SetReceiptDailyLimit_Success_Reset()
    {
        await SetReceiptDailyLimit_Success();
        var time = TimestampHelper.GetUtcNow().ToDateTime().AddHours(2).Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
        {
            var receiptDailyLimit = await BridgeContractImplStub.GetReceiptDailyLimit.CallAsync(
                new GetReceiptDailyLimitInput
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum"
                });
            receiptDailyLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
            receiptDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            receiptDailyLimit.TokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(ReceiptDailyLimitSet) select ReceiptDailyLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].TargetChainId.ShouldBe("Ethereum");
            limitLogList[0].ReceiptDailyLimit.ShouldBe(5_0000_00000000);
            limitLogList[0].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[0].CurrentReceiptDailyLimit.ShouldBe(5_0000_00000000);

        }

    }
    
    [Fact]
    public async Task SetReceiptDailyLimit_Success_Reset_Failed()
    {
        await SetReceiptDailyLimit_Success();
        var time = TimestampHelper.GetUtcNow().ToDateTime().AddDays(2).Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
        result.TransactionResult.Error.ShouldContain("Invalid time,current refresh time is");

    }
    
    [Fact]
    public async Task SetReceiptDailyLimit_Failed_NoPermission()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        var input = new List<ReceiptDailyLimitInfo>
        {
            new ReceiptDailyLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplUserStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
        {
            ReceiptDailyLimitInfos = { input }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task SetReceiptDailyLimit_Failed_InvalidInput()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        {
            var input = new List<ReceiptDailyLimitInfo>();
            var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
            {
                ReceiptDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new List<ReceiptDailyLimitInfo>
            {
                new ReceiptDailyLimitInfo
                {
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
            {
                ReceiptDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
        {
            var input = new List<ReceiptDailyLimitInfo>
            {
                new ReceiptDailyLimitInfo
                {
                    Symbol = "ELF",
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
            {
                ReceiptDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
        {
            var input = new List<ReceiptDailyLimitInfo>
            {
                new ReceiptDailyLimitInfo
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 0,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
            {
                ReceiptDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
        {
            var time1 = TimestampHelper.GetUtcNow().ToDateTime();
            var input = new List<ReceiptDailyLimitInfo>
            {
                new ReceiptDailyLimitInfo
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 100,
                    StartTime = Timestamp.FromDateTime(time1)
                }
            };
            var result = await BridgeContractImplStub.SetReceiptDailyLimit.SendWithExceptionAsync(new SetReceiptDailyLimitInput
            {
                ReceiptDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid refresh time.");
        }
    }

    [Fact]
    public async Task SetSwapDailyLimit_Success()
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
            var swapDailyLimit = await BridgeContractImplStub.GetSwapDailyLimit.CallAsync(_swapHashOfElf);
            swapDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            swapDailyLimit.TokenAmount.ShouldBe(10_0000_00000000);
            swapDailyLimit.DefaultTokenAmount.ShouldBe(10_0000_00000000);
        }
        {
            var swapDailyLimit = await BridgeContractImplStub.GetSwapDailyLimit.CallAsync(_swapHashOfUsdt);
            swapDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            swapDailyLimit.TokenAmount.ShouldBe(5_0000_00000000);
            swapDailyLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(SwapDailyLimitSet) select SwapDailyLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF"); 
            limitLogList[0].FromChainId.ShouldBe("Ethereum"); 
            limitLogList[0].SwapDailyLimit.ShouldBe(10_0000_00000000);
            limitLogList[0].SwapRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[0].CurrentSwapDailyLimit.ShouldBe(10_0000_00000000);
            limitLogList[1].Symbol.ShouldBe("USDT"); 
            limitLogList[1].FromChainId.ShouldBe("Polygon"); 
            limitLogList[1].SwapDailyLimit.ShouldBe(5_0000_00000000);
            limitLogList[1].SwapRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[1].CurrentSwapDailyLimit.ShouldBe(5_0000_00000000);
        }

    }
    [Fact]
    public async Task SetSwapDailyLimit_Success_Reset()
    {
        await SetSwapDailyLimit_Success();
        var time = TimestampHelper.GetUtcNow().ToDateTime().AddHours(2).Date;
        var input = new List<SwapDailyLimitInfo>
        {
            new SwapDailyLimitInfo
            {
                SwapId = _swapHashOfElf,
                DefaultTokenAmount = 6_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetSwapDailyLimit.SendAsync(new SetSwapDailyLimitInput
        {
            SwapDailyLimitInfos = { input }
        });
        {
            var swapDailyLimit = await BridgeContractImplStub.GetSwapDailyLimit.CallAsync(_swapHashOfElf);
            swapDailyLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            swapDailyLimit.TokenAmount.ShouldBe(6_0000_00000000);
            swapDailyLimit.DefaultTokenAmount.ShouldBe(6_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(SwapDailyLimitSet) select SwapDailyLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF"); 
            limitLogList[0].FromChainId.ShouldBe("Ethereum"); 
            limitLogList[0].SwapDailyLimit.ShouldBe(6_0000_00000000);
            limitLogList[0].SwapRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[0].CurrentSwapDailyLimit.ShouldBe(6_0000_00000000);
        }

    }
    
    [Fact]
    public async Task SetSwapDailyLimit_Failed_NoPermission()
    {
        await InitialBridgeContractAsync();
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
        var result = await BridgeContractImplUserStub.SetSwapDailyLimit.SendWithExceptionAsync(new SetSwapDailyLimitInput
        {
            SwapDailyLimitInfos = { input }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task SetSwapDailyLimit_Failed_InvalidInput()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        {
            var input = new List<SwapDailyLimitInfo>();
            var result = await BridgeContractImplStub.SetSwapDailyLimit.SendWithExceptionAsync(new SetSwapDailyLimitInput
            {
                SwapDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new List<SwapDailyLimitInfo>
            {
                new SwapDailyLimitInfo
                {
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetSwapDailyLimit.SendWithExceptionAsync(new SetSwapDailyLimitInput
            {
                SwapDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily swap limit info.");
        }
        {
            var input = new List<SwapDailyLimitInfo>
            {
                new SwapDailyLimitInfo
                {
                    SwapId = _swapHashOfElf,
                    DefaultTokenAmount = 0,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetSwapDailyLimit.SendWithExceptionAsync(new SetSwapDailyLimitInput
            {
                SwapDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily swap limit info.");
        }
        {
            var time1 = TimestampHelper.GetUtcNow().ToDateTime();
            var input = new List<SwapDailyLimitInfo>
            {
                new SwapDailyLimitInfo
                {
                    SwapId = _swapHashOfElf,
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time1)
                }
            };
            var result = await BridgeContractImplStub.SetSwapDailyLimit.SendWithExceptionAsync(new SetSwapDailyLimitInput
            {
                SwapDailyLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily swap limit info.");
        }
    }

    [Fact]
    public async Task ConfigReceiptTokenBucket_Success()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime();
        var input = new List<ReceiptTokenBucketConfig>()
        {
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 10_0000_00000000,
                Rate = 167
            },
            new ReceiptTokenBucketConfig
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 100
            },
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                IsEnable = true,
                TokenCapacity = 10_0000_00000000,
                Rate = 167
            },
            new ReceiptTokenBucketConfig
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 100
            },
        };
        blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(time));
        var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendAsync(new ConfigReceiptTokenBucketInput
        {
            ReceiptTokenBucketConfigs = { input }
        });
        {
            var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                new GetCurrentReceiptTokenBucketStateInput
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum"
                });
            bucket.TokenCapacity.ShouldBe(10_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(10_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(167);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                new GetCurrentReceiptTokenBucketStateInput
                {
                    Symbol = "USDT",
                    TargetChain = "Ethereum"
                });
            bucket.TokenCapacity.ShouldBe(5_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(100);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                new GetCurrentReceiptTokenBucketStateInput
                {
                    Symbol = "ELF",
                    TargetChain = "BSC"
                });
            bucket.TokenCapacity.ShouldBe(10_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(10_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(167);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                new GetCurrentReceiptTokenBucketStateInput
                {
                    Symbol = "USDT",
                    TargetChain = "BSC"
                });
            bucket.TokenCapacity.ShouldBe(5_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(100);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(ReceiptTokenBucketSet) select ReceiptTokenBucketSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].TargetChainId.ShouldBe("Ethereum");
            limitLogList[0].ReceiptCapacity.ShouldBe(10_0000_00000000);
            limitLogList[0].ReceiptBucketIsEnable.ShouldBeTrue();
            limitLogList[0].ReceiptRefillRate.ShouldBe(167);
            limitLogList[0].ReceiptBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));

            limitLogList[1].Symbol.ShouldBe("USDT");
            limitLogList[1].TargetChainId.ShouldBe("Ethereum");
            limitLogList[1].ReceiptCapacity.ShouldBe(5_0000_00000000);
            limitLogList[1].ReceiptBucketIsEnable.ShouldBeTrue();
            limitLogList[1].ReceiptRefillRate.ShouldBe(100);
            limitLogList[1].ReceiptBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));
            
            limitLogList[2].Symbol.ShouldBe("ELF");
            limitLogList[2].TargetChainId.ShouldBe("BSC");
            limitLogList[2].ReceiptCapacity.ShouldBe(10_0000_00000000);
            limitLogList[2].ReceiptBucketIsEnable.ShouldBeTrue();
            limitLogList[2].ReceiptRefillRate.ShouldBe(167);
            limitLogList[2].ReceiptBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));
            
            limitLogList[3].Symbol.ShouldBe("USDT");
            limitLogList[3].TargetChainId.ShouldBe("BSC");
            limitLogList[3].ReceiptCapacity.ShouldBe(5_0000_00000000);
            limitLogList[3].ReceiptBucketIsEnable.ShouldBeTrue();
            limitLogList[3].ReceiptRefillRate.ShouldBe(100);
            limitLogList[3].ReceiptBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));

        }
        
    }
    
    [Fact]
    public async Task ConfigReceiptTokenBucket_Success_Reset()
    {
        await ConfigReceiptTokenBucket_Success();
        var time = TimestampHelper.GetUtcNow().ToDateTime();
        var input = new List<ReceiptTokenBucketConfig>()
        {
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 200
            }
        };
        blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(time));
        var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendAsync(new ConfigReceiptTokenBucketInput
        {
            ReceiptTokenBucketConfigs = { input }
        });
        {
            var bucket = await BridgeContractImplStub.GetCurrentReceiptTokenBucketState.CallAsync(
                new GetCurrentReceiptTokenBucketStateInput
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum"
                });
            bucket.TokenCapacity.ShouldBe(5_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(200);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(ReceiptTokenBucketSet) select ReceiptTokenBucketSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].TargetChainId.ShouldBe("Ethereum");
            limitLogList[0].ReceiptCapacity.ShouldBe(5_0000_00000000);
            limitLogList[0].ReceiptBucketIsEnable.ShouldBeTrue();
            limitLogList[0].ReceiptRefillRate.ShouldBe(200);
            limitLogList[0].ReceiptBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));

        }
        
    }
    
    [Fact]
    public async Task ConfigReceiptTokenBucket_Failed_NoPermission()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime();
        var input = new List<ReceiptTokenBucketConfig>()
        {
            new ReceiptTokenBucketConfig
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                IsEnable = true,
                TokenCapacity = 10_0000_00000000,
                Rate = 167
            }
        };
        blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(time));
        var result = await BridgeContractImplUserStub.ConfigReceiptTokenBucket.SendWithExceptionAsync(new ConfigReceiptTokenBucketInput
        {
            ReceiptTokenBucketConfigs = { input }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task ConfigReceiptTokenBucket_Failed_InvalidInput()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        {
            var input = new List<ReceiptTokenBucketConfig>();
            var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendWithExceptionAsync(new ConfigReceiptTokenBucketInput
            {
                ReceiptTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new List<ReceiptTokenBucketConfig>
            {
                new ReceiptTokenBucketConfig
                {
                    TargetChain = "Ethereum",
                    IsEnable = true,
                    TokenCapacity = 10_0000_00000000,
                    Rate = 167
                }
            };
            var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendWithExceptionAsync(new ConfigReceiptTokenBucketInput
            {
                ReceiptTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid receipt bucket config input.");
        }
        {
            var input = new List<ReceiptTokenBucketConfig>
            {
                new ReceiptTokenBucketConfig
                {
                    Symbol = "ELF",
                    IsEnable = true,
                    TokenCapacity = 10_0000_00000000,
                    Rate = 167
                }
            };
            var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendWithExceptionAsync(new ConfigReceiptTokenBucketInput
            {
                ReceiptTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid receipt bucket config input.");
        }
        {
            var input = new List<ReceiptTokenBucketConfig>
            {
                new ReceiptTokenBucketConfig
                {
                    Symbol = "ELF",
                    TargetChain = "BSC",
                    IsEnable = true,
                    TokenCapacity = 0,
                    Rate = 167
                }
            };
            var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendWithExceptionAsync(new ConfigReceiptTokenBucketInput
            {
                ReceiptTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid receipt bucket config input.");
        }
        {
            var input = new List<ReceiptTokenBucketConfig>
            {
                new ReceiptTokenBucketConfig
                {
                    Symbol = "ELF",
                    TargetChain = "BSC",
                    IsEnable = true,
                    TokenCapacity = 10_0000_00000000,
                    Rate = 0
                }
            };
            var result = await BridgeContractImplStub.ConfigReceiptTokenBucket.SendWithExceptionAsync(new ConfigReceiptTokenBucketInput
            {
                ReceiptTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid receipt bucket config input.");
        }
    }
    
    [Fact]
    public async Task ConfigSwapTokenBucket_Success()
    {
        await CreateSwapTestAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime();
        var input = new List<SwapTokenBucketConfig>()
        {
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfElf,
                IsEnable = true,
                TokenCapacity = 10_0000_00000000,
                Rate = 167
            },
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfUsdt,
                IsEnable = true,
                TokenCapacity = 5_0000_00000000,
                Rate = 100
            }
        };
        blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(time));
        var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendAsync(new ConfigSwapTokenBucketInput
        {
            SwapTokenBucketConfigs = { input }
        });
        {
            var bucket = await BridgeContractImplStub.GetCurrentSwapTokenBucketState.CallAsync(_swapHashOfElf);
            bucket.TokenCapacity.ShouldBe(10_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(10_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(167);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var bucket = await BridgeContractImplStub.GetCurrentSwapTokenBucketState.CallAsync(_swapHashOfUsdt);
            bucket.TokenCapacity.ShouldBe(5_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(5_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(100);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time));
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(SwapTokenBucketSet) select SwapTokenBucketSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].FromChainId.ShouldBe("Ethereum");
            limitLogList[0].SwapCapacity.ShouldBe(10_0000_00000000);
            limitLogList[0].SwapBucketIsEnable.ShouldBeTrue();
            limitLogList[0].SwapRefillRate.ShouldBe(167);
            limitLogList[0].SwapBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));

            limitLogList[1].Symbol.ShouldBe("USDT");
            limitLogList[1].FromChainId.ShouldBe("Polygon");
            limitLogList[1].SwapCapacity.ShouldBe(5_0000_00000000);
            limitLogList[1].SwapBucketIsEnable.ShouldBeTrue();
            limitLogList[1].SwapRefillRate.ShouldBe(100);
            limitLogList[1].SwapBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time));
        }
    }
    
    [Fact]
    public async Task ConfigSwapTokenBucket_Success_Reset()
    {
        await ConfigSwapTokenBucket_Success();
        var time = TimestampHelper.GetUtcNow().ToDateTime();
        var input = new List<SwapTokenBucketConfig>()
        {
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfElf,
                IsEnable = true,
                TokenCapacity = 20_0000_00000000,
                Rate = 200
            },
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfUsdt,
                IsEnable = true,
                TokenCapacity = 3_0000_00000000,
                Rate = 60
            }
        };
        blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(time.AddHours(2)));
        var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendAsync(new ConfigSwapTokenBucketInput
        {
            SwapTokenBucketConfigs = { input }
        });
        {
            var bucket = await BridgeContractImplStub.GetCurrentSwapTokenBucketState.CallAsync(_swapHashOfElf);
            bucket.TokenCapacity.ShouldBe(20_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(10_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(200);
            bucket.LastUpdatedTime.ShouldBe(Timestamp.FromDateTime(time.AddHours(2)));
        }
        {
            var bucket = await BridgeContractImplStub.GetCurrentSwapTokenBucketState.CallAsync(_swapHashOfUsdt);
            bucket.TokenCapacity.ShouldBe(3_0000_00000000);
            bucket.CurrentTokenAmount.ShouldBe(3_0000_00000000);
            bucket.IsEnable.ShouldBe(true);
            bucket.Rate.ShouldBe(60);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(SwapTokenBucketSet) select SwapTokenBucketSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].FromChainId.ShouldBe("Ethereum");
            limitLogList[0].SwapCapacity.ShouldBe(20_0000_00000000);
            limitLogList[0].SwapBucketIsEnable.ShouldBeTrue();
            limitLogList[0].SwapRefillRate.ShouldBe(200);
            limitLogList[0].SwapBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time.AddHours(2)));
            
            limitLogList[1].Symbol.ShouldBe("USDT");
            limitLogList[1].FromChainId.ShouldBe("Polygon");
            limitLogList[1].SwapCapacity.ShouldBe(3_0000_00000000);
            limitLogList[1].SwapBucketIsEnable.ShouldBeTrue();
            limitLogList[1].SwapRefillRate.ShouldBe(60);
            limitLogList[1].SwapBucketUpdateTime.ShouldBe(Timestamp.FromDateTime(time.AddHours(2)));
        }
    }
    
    [Fact]
    public async Task ConfigSwapTokenBucket_Failed_NoPermission()
    {
        await InitialBridgeContractAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime();
        var input = new List<SwapTokenBucketConfig>()
        {
            new SwapTokenBucketConfig
            {
                SwapId = _swapHashOfElf,
                IsEnable = true,
                TokenCapacity = 20_0000_00000000,
                Rate = 200
            }
        };
        var result = await BridgeContractImplUserStub.ConfigSwapTokenBucket.SendWithExceptionAsync(new ConfigSwapTokenBucketInput
        {
            SwapTokenBucketConfigs = { input }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task ConfigSwapTokenBucket_Failed_InvalidInput()
    {
        await CreateSwapTestAsync();
        var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
        {
            var input = new List<SwapTokenBucketConfig>();
            var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendWithExceptionAsync(new ConfigSwapTokenBucketInput
            {
                SwapTokenBucketConfigs = {  input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new List<SwapTokenBucketConfig>
            {
                new SwapTokenBucketConfig
                {
                    IsEnable = true,
                    TokenCapacity = 10_0000_00000000,
                    Rate = 167
                }
            };
            var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendWithExceptionAsync(new ConfigSwapTokenBucketInput
            {
                SwapTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid swap bucket config input.");
        }
        {
            var input = new List<SwapTokenBucketConfig>
            {
                new SwapTokenBucketConfig
                {
                    SwapId = _swapOfElfSpaceId,
                    IsEnable = true,
                    TokenCapacity = 10_0000_00000000,
                    Rate = 167
                }
            };
            var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendWithExceptionAsync(new ConfigSwapTokenBucketInput
            {
                SwapTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Token swap pair not found.");
        }
        {
            var input = new List<SwapTokenBucketConfig>
            {
                new SwapTokenBucketConfig
                {
                    SwapId = _swapHashOfElf,
                    IsEnable = true,
                    TokenCapacity = 0,
                    Rate = 167
                }
            };
            var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendWithExceptionAsync(new ConfigSwapTokenBucketInput
            {
                SwapTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid swap bucket config input.");
        }
        {
            var input = new List<SwapTokenBucketConfig>
            {
                new SwapTokenBucketConfig
                {
                    SwapId = _swapHashOfElf,
                    IsEnable = true,
                    TokenCapacity = 10_0000_00000000,
                    Rate = 0
                }
            };
            var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendWithExceptionAsync(new ConfigSwapTokenBucketInput
            {
                SwapTokenBucketConfigs = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid swap bucket config input.");
        }
    }
}