using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.ContractTestKit;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractTests
{
    [Fact]
    public async Task SetDailyReceiptLimit_Success()
    {
        await InitialBridgeContractAsync();
        var time = DateTime.UtcNow.Date;
        var input = new List<DailyReceiptLimitInfo>
        {
            new DailyReceiptLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new DailyReceiptLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new DailyReceiptLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "BSC",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new DailyReceiptLimitInfo
            {
                Symbol = "USDT",
                TargetChain = "BSC",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendAsync(new SetDailyReceiptLimitInput
        {
            DailyReceiptLimitInfos = { input }
        });

        {
            var dailyReceiptLimit = await BridgeContractImplStub.GetDailyReceiptLimit.CallAsync(
                new GetDailyReceiptLimitInput
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum"
                });
            dailyReceiptLimit.DefaultTokenAmount.ShouldBe(10_0000_00000000);
            dailyReceiptLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailyReceiptLimit.TokenAmount.ShouldBe(10_0000_00000000);
        }
        {
            var dailyReceiptLimit = await BridgeContractImplStub.GetDailyReceiptLimit.CallAsync(
                new GetDailyReceiptLimitInput
                {
                    Symbol = "ELF",
                    TargetChain = "BSC"
                });
            dailyReceiptLimit.DefaultTokenAmount.ShouldBe(10_0000_00000000);
            dailyReceiptLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailyReceiptLimit.TokenAmount.ShouldBe(10_0000_00000000);
        }
        {
            var dailyReceiptLimit = await BridgeContractImplStub.GetDailyReceiptLimit.CallAsync(
                new GetDailyReceiptLimitInput
                {
                    Symbol = "USDT",
                    TargetChain = "Ethereum"
                });
            dailyReceiptLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
            dailyReceiptLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailyReceiptLimit.TokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var dailyReceiptLimit = await BridgeContractImplStub.GetDailyReceiptLimit.CallAsync(
                new GetDailyReceiptLimitInput
                {
                    Symbol = "USDT",
                    TargetChain = "BSC"
                });
            dailyReceiptLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
            dailyReceiptLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailyReceiptLimit.TokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(DailyReceiptLimitSet) select DailyReceiptLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].TargetChainId.ShouldBe("Ethereum");
            limitLogList[0].DailyReceiptLimit.ShouldBe(10_0000_00000000);
            limitLogList[0].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[1].Symbol.ShouldBe("USDT");
            limitLogList[1].TargetChainId.ShouldBe("Ethereum");
            limitLogList[1].DailyReceiptLimit.ShouldBe(5_0000_00000000);
            limitLogList[1].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[2].Symbol.ShouldBe("ELF");
            limitLogList[2].TargetChainId.ShouldBe("BSC");
            limitLogList[2].DailyReceiptLimit.ShouldBe(10_0000_00000000);
            limitLogList[2].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[3].Symbol.ShouldBe("USDT");
            limitLogList[3].TargetChainId.ShouldBe("BSC");
            limitLogList[3].DailyReceiptLimit.ShouldBe(5_0000_00000000);
            limitLogList[3].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));

        }

    }
    
    [Fact]
    public async Task SetDailyReceiptLimit_Success_Reset()
    {
        await SetDailyReceiptLimit_Success();
        var time = DateTime.UtcNow.AddHours(2).Date;
        var input = new List<DailyReceiptLimitInfo>
        {
            new DailyReceiptLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendAsync(new SetDailyReceiptLimitInput
        {
            DailyReceiptLimitInfos = { input }
        });
        {
            var dailyReceiptLimit = await BridgeContractImplStub.GetDailyReceiptLimit.CallAsync(
                new GetDailyReceiptLimitInput
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum"
                });
            dailyReceiptLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
            dailyReceiptLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailyReceiptLimit.TokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(DailyReceiptLimitSet) select DailyReceiptLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF");
            limitLogList[0].TargetChainId.ShouldBe("Ethereum");
            limitLogList[0].DailyReceiptLimit.ShouldBe(5_0000_00000000);
            limitLogList[0].ReceiptRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
        }

    }
    
    [Fact]
    public async Task SetDailyReceiptLimit_Failed_NoPermission()
    {
        await InitialBridgeContractAsync();
        var time = DateTime.UtcNow.Date;
        var input = new List<DailyReceiptLimitInfo>
        {
            new DailyReceiptLimitInfo
            {
                Symbol = "ELF",
                TargetChain = "Ethereum",
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplUserStub.SetDailyReceiptLimit.SendWithExceptionAsync(new SetDailyReceiptLimitInput
        {
            DailyReceiptLimitInfos = { input }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task SetDailyReceiptLimit_Failed_InvalidInput()
    {
        await InitialBridgeContractAsync();
        var time = DateTime.UtcNow.Date;
        {
            var input = new List<DailyReceiptLimitInfo>();
            var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendWithExceptionAsync(new SetDailyReceiptLimitInput
            {
                DailyReceiptLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new List<DailyReceiptLimitInfo>
            {
                new DailyReceiptLimitInfo
                {
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendWithExceptionAsync(new SetDailyReceiptLimitInput
            {
                DailyReceiptLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
        {
            var input = new List<DailyReceiptLimitInfo>
            {
                new DailyReceiptLimitInfo
                {
                    Symbol = "ELF",
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendWithExceptionAsync(new SetDailyReceiptLimitInput
            {
                DailyReceiptLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
        {
            var input = new List<DailyReceiptLimitInfo>
            {
                new DailyReceiptLimitInfo
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 0,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendWithExceptionAsync(new SetDailyReceiptLimitInput
            {
                DailyReceiptLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
        {
            var time1 = DateTime.UtcNow;
            var input = new List<DailyReceiptLimitInfo>
            {
                new DailyReceiptLimitInfo
                {
                    Symbol = "ELF",
                    TargetChain = "Ethereum",
                    DefaultTokenAmount = 0,
                    StartTime = Timestamp.FromDateTime(time1)
                }
            };
            var result = await BridgeContractImplStub.SetDailyReceiptLimit.SendWithExceptionAsync(new SetDailyReceiptLimitInput
            {
                DailyReceiptLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily receipt limit info.");
        }
    }

    [Fact]
    public async Task SetDailySwapLimit_Success()
    {
        await CreateSwapTestAsync();
        var time = DateTime.UtcNow.Date;
        var input = new List<DailySwapLimitInfo>
        {
            new DailySwapLimitInfo
            {
                SwapId = _swapHashOfElf,
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            },
            new DailySwapLimitInfo
            {
                SwapId = _swapHashOfUsdt,
                DefaultTokenAmount = 5_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetDailySwapLimit.SendAsync(new SetDailySwapLimitInput
        {
            DailySwapLimitInfos = { input }
        });
        {
            var dailySwapLimit = await BridgeContractImplStub.GetDailySwapLimit.CallAsync(_swapHashOfElf);
            dailySwapLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailySwapLimit.TokenAmount.ShouldBe(10_0000_00000000);
            dailySwapLimit.DefaultTokenAmount.ShouldBe(10_0000_00000000);
        }
        {
            var dailySwapLimit = await BridgeContractImplStub.GetDailySwapLimit.CallAsync(_swapHashOfUsdt);
            dailySwapLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailySwapLimit.TokenAmount.ShouldBe(5_0000_00000000);
            dailySwapLimit.DefaultTokenAmount.ShouldBe(5_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(DailySwapLimitSet) select DailySwapLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF"); 
            limitLogList[0].FromChainId.ShouldBe("Ethereum"); 
            limitLogList[0].DailySwapLimit.ShouldBe(10_0000_00000000);
            limitLogList[0].SwapRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            limitLogList[1].Symbol.ShouldBe("USDT"); 
            limitLogList[1].FromChainId.ShouldBe("Polygon"); 
            limitLogList[1].DailySwapLimit.ShouldBe(5_0000_00000000);
            limitLogList[1].SwapRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
        }

    }
    [Fact]
    public async Task SetDailySwapLimit_Success_Reset()
    {
        await SetDailySwapLimit_Success();
        var time = DateTime.UtcNow.AddHours(2).Date;
        var input = new List<DailySwapLimitInfo>
        {
            new DailySwapLimitInfo
            {
                SwapId = _swapHashOfElf,
                DefaultTokenAmount = 6_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplStub.SetDailySwapLimit.SendAsync(new SetDailySwapLimitInput
        {
            DailySwapLimitInfos = { input }
        });
        {
            var dailySwapLimit = await BridgeContractImplStub.GetDailySwapLimit.CallAsync(_swapHashOfElf);
            dailySwapLimit.RefreshTime.ShouldBe(Timestamp.FromDateTime(time));
            dailySwapLimit.TokenAmount.ShouldBe(6_0000_00000000);
            dailySwapLimit.DefaultTokenAmount.ShouldBe(6_0000_00000000);
        }
        {
            var limitLogList = (from log in result.TransactionResult.Logs where log.Name == nameof(DailySwapLimitSet) select DailySwapLimitSet.Parser.ParseFrom(log.NonIndexed)).ToList();
            limitLogList[0].Symbol.ShouldBe("ELF"); 
            limitLogList[0].FromChainId.ShouldBe("Ethereum"); 
            limitLogList[0].DailySwapLimit.ShouldBe(6_0000_00000000);
            limitLogList[0].SwapRefreshTime.ShouldBe(Timestamp.FromDateTime(time));
        }

    }
    
    [Fact]
    public async Task SetDailySwapLimit_Failed_NoPermission()
    {
        await InitialBridgeContractAsync();
        var time = DateTime.UtcNow.Date;
        var input = new List<DailySwapLimitInfo>
        {
            new DailySwapLimitInfo
            {
                SwapId = _swapHashOfElf,
                DefaultTokenAmount = 10_0000_00000000,
                StartTime = Timestamp.FromDateTime(time)
            }
        };
        var result = await BridgeContractImplUserStub.SetDailySwapLimit.SendWithExceptionAsync(new SetDailySwapLimitInput
        {
            DailySwapLimitInfos = { input }
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task SetDailySwapLimit_Failed_InvalidInput()
    {
        await InitialBridgeContractAsync();
        var time = DateTime.UtcNow.Date;
        {
            var input = new List<DailySwapLimitInfo>();
            var result = await BridgeContractImplStub.SetDailySwapLimit.SendWithExceptionAsync(new SetDailySwapLimitInput
            {
                DailySwapLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var input = new List<DailySwapLimitInfo>
            {
                new DailySwapLimitInfo
                {
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetDailySwapLimit.SendWithExceptionAsync(new SetDailySwapLimitInput
            {
                DailySwapLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily swap limit info.");
        }
        {
            var input = new List<DailySwapLimitInfo>
            {
                new DailySwapLimitInfo
                {
                    SwapId = _swapHashOfElf,
                    DefaultTokenAmount = 0,
                    StartTime = Timestamp.FromDateTime(time)
                }
            };
            var result = await BridgeContractImplStub.SetDailySwapLimit.SendWithExceptionAsync(new SetDailySwapLimitInput
            {
                DailySwapLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily swap limit info.");
        }
        {
            var time1 = DateTime.UtcNow;
            var input = new List<DailySwapLimitInfo>
            {
                new DailySwapLimitInfo
                {
                    SwapId = _swapHashOfElf,
                    DefaultTokenAmount = 10_0000_00000000,
                    StartTime = Timestamp.FromDateTime(time1)
                }
            };
            var result = await BridgeContractImplStub.SetDailySwapLimit.SendWithExceptionAsync(new SetDailySwapLimitInput
            {
                DailySwapLimitInfos = { input }
            });
            result.TransactionResult.Error.ShouldContain("Invalid input daily swap limit info.");
        }
    }

    [Fact]
    public async Task ConfigReceiptTokenBucket_Success()
    {
        await InitialBridgeContractAsync();
        var time = DateTime.UtcNow;
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
        var time = DateTime.UtcNow;
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
        var time = DateTime.UtcNow;
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
        var time = DateTime.UtcNow.Date;
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
        var time = DateTime.UtcNow;
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
        var time = DateTime.UtcNow;
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
        var time = DateTime.UtcNow;
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
        var time = DateTime.UtcNow.Date;
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