using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using EBridge.Contracts.Bridge;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.TokenPool;

public class TokenPoolContractTest : TokenPoolContractTestBase
{
    [Fact]
    public async Task Initialize_Test()
    {
        await TokenPoolContractStub.Initialize.SendAsync(new InitializeInput
        {
            BridgeContractAddress = DefaultSenderAddress,
            Admin = DefaultSenderAddress
        });
        var result = await TokenPoolContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            BridgeContractAddress = DefaultSenderAddress,
            Admin = DefaultSenderAddress
        });
        result.TransactionResult.Error.ShouldContain("Already initialized.");
        var admin = await TokenPoolContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(DefaultSenderAddress);
    }

    [Fact]
    public async Task SetAdmin_Test()
    {
        await Initialize_Test();
        await TokenPoolContractStub.SetAdmin.SendAsync(User1Address);
        var admin = await TokenPoolContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(User1Address);
        var result = await TokenPoolContractStub.SetAdmin.SendWithExceptionAsync(User1Address);
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task Lock_Test_Success()
    {
        await Initialize_Test();
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Spender = TokenPoolContractAddress,
            Symbol = "ELF",
            Amount = 2000000000
        });

        var balanceBefore = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        })).Balance;

        var result = await TokenPoolContractStub.Lock.SendAsync(new LockInput
        {
            TargetChainId = "Sepolia",
            Amount = 1000000000,
            Sender = User1Address,
            TargetTokenSymbol = "ELF"
        });
        var lockEvent = Locked.Parser.ParseFrom(result.TransactionResult.Logs
            .FirstOrDefault(l => l.Name == nameof(Locked))
            ?.NonIndexed);
        lockEvent.FromChainId.ShouldBe("AELF");
        lockEvent.ToChainId.ShouldBe("Sepolia");
        lockEvent.Amount.ShouldBe(1000000000);
        lockEvent.Sender.ShouldBe(User1Address);

        var balanceAfter = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        })).Balance;
        (balanceBefore - balanceAfter).ShouldBe(1000000000);
        var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
        {
            TokenSymbol = "ELF"
        });
        tokenPoolInfo.Liquidity.ShouldBe(1000000000);
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = tokenPoolInfo.TokenVirtualAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(1000000000);
        }
        await TokenPoolContractStub.Lock.SendAsync(new LockInput
        {
            TargetChainId = "Bsc",
            Amount = 1000000000,
            Sender = User1Address,
            TargetTokenSymbol = "ELF"
        });
        var tokenPoolInfo1 = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
        {
            TokenSymbol = "ELF"
        });
        tokenPoolInfo1.Liquidity.ShouldBe(2000000000);
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = tokenPoolInfo.TokenVirtualAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(2000000000);
        }
    }

    [Fact]
    public async Task Lock_Test_Failed()
    {
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = 1000000000,
                Sender = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Contract has not been initialized.");
        }
        await Initialize_Test();
        {
            var result = await TokenPoolContractStub1.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = 1000000000,
                Sender = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                Amount = 1000000000,
                Sender = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid chain id.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "",
                Amount = 1000000000,
                Sender = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid chain id.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = 1000000000,
                Sender = User1Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = 1000000000,
                Sender = User1Address,
                TargetTokenSymbol = ""
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = 1000000000,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid sender.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = 0,
                TargetTokenSymbol = "ELF",
                Sender = User1Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.Lock.SendWithExceptionAsync(new LockInput
            {
                TargetChainId = "Sepolia",
                Amount = -1,
                TargetTokenSymbol = "ELF",
                Sender = User1Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
    }

    [Fact]
    public async Task Release_Test_Success()
    {
        await Lock_Test_Success();
        var balanceBefore = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = User1Address,
            Symbol = "ELF"
        })).Balance;
        var result = await TokenPoolContractStub.Release.SendAsync(new ReleaseInput
        {
            FromChainId = "Sepolia",
            Amount = 500000000,
            Receiver = User1Address,
            TargetTokenSymbol = "ELF"
        });
        var releaseEvent = Released.Parser.ParseFrom(result.TransactionResult.Logs
            .FirstOrDefault(l => l.Name == nameof(Released))?.NonIndexed);
        releaseEvent.Amount.ShouldBe(500000000);
        releaseEvent.FromChainId.ShouldBe("Sepolia");
        releaseEvent.ToChainId.ShouldBe("AELF");
        releaseEvent.Receiver.ShouldBe(User1Address);
        releaseEvent.TargetTokenSymbol.ShouldBe("ELF");
        var balanceAfter = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = User1Address,
            Symbol = "ELF"
        })).Balance;
        (balanceAfter - balanceBefore).ShouldBe(500000000);
        var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
        {
            TokenSymbol = "ELF"
        });
        tokenPoolInfo.Liquidity.ShouldBe(1500000000);
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = tokenPoolInfo.TokenVirtualAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(1500000000);
        }
    }

    [Fact]
    public async Task Release_Test_Failed()
    {
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 500000000,
                Receiver = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Contract has not been initialized.");
        }
        await Lock_Test_Success();
        {
            var result = await TokenPoolContractStub1.Release.SendWithExceptionAsync(new ReleaseInput

            {
                FromChainId = "Sepolia",
                Amount = 1000000000,
                Receiver = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput

            {
                FromChainId = "",
                Amount = 1000000000,
                Receiver = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid chain id.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput

            {
                Amount = 1000000000,
                Receiver = User1Address,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid chain id.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 1000000000,
                Receiver = User1Address,
                TargetTokenSymbol = ""
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 1000000000,
                Receiver = User1Address,
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 1000000000,
                TargetTokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid receiver.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 0,
                TargetTokenSymbol = "ELF",
                Receiver = User1Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = -1,
                TargetTokenSymbol = "ELF",
                Receiver = User1Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.Release.SendWithExceptionAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 10000000000,
                TargetTokenSymbol = "ELF",
                Receiver = User1Address
            });
            result.TransactionResult.Error.ShouldContain("Pool liquidity is not enough.");
        }
    }

    [Fact]
    public async Task AddLiquidity_Test_Success()
    {
        await TokenPoolContractStub.Initialize.SendAsync(new InitializeInput
        {
            BridgeContractAddress = BridgeContractAddress,
            Admin = DefaultSenderAddress
        });
        await InitializeBridgeContractAndAddToken();
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Spender = TokenPoolContractAddress,
            Symbol = "ELF",
            Amount = 10000000000
        });
        var balanceBefore = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        })).Balance;
        // 1. add liquidity
        var result = await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
        {
            TokenSymbol = "ELF",
            Amount = 2000000000
        });
        var events = LiquidityAdded.Parser.ParseFrom(result.TransactionResult.Logs
            .FirstOrDefault(l => l.Name == nameof(LiquidityAdded))?.NonIndexed);
        events.TokenSymbol.ShouldBe("ELF");
        events.Amount.ShouldBe(2000000000);
        events.Provider.ShouldBe(DefaultSenderAddress);
        var balanceAfter = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        })).Balance;
        (balanceBefore - balanceAfter).ShouldBe(2000000000);
        {
            var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
            {
                TokenSymbol = "ELF"
            });
            tokenPoolInfo.Liquidity.ShouldBe(2000000000);
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = tokenPoolInfo.TokenVirtualAddress,
                    Symbol = "ELF"
                });
                balance.Balance.ShouldBe(2000000000);
            }
            {
                var liquidityInfo = await TokenPoolContractStub.GetLiquidity.CallAsync(new GetLiquidityInput
                {
                    Provider = DefaultSenderAddress,
                    TokenSymbol = "ELF"
                });
                liquidityInfo.Value.ShouldBe(2000000000);
                var removableLiquidity = await TokenPoolContractStub.GetRemovableLiquidity.CallAsync(
                    new GetLiquidityInput
                    {
                        Provider = DefaultSenderAddress,
                        TokenSymbol = "ELF"
                    });
                removableLiquidity.Value.ShouldBe(2000000000);
            }
        }
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Spender = TokenPoolContractAddress,
            Symbol = "ELF",
            Amount = 1000000000
        });
        // 2. lock
        {
            await TokenPoolContractStub.SetBridgeContract.SendAsync(DefaultSenderAddress);
        }
        await TokenPoolContractStub.Lock.SendAsync(new LockInput
        {
            TargetChainId = "Sepolia",
            Amount = 1000000000,
            Sender = User1Address,
            TargetTokenSymbol = "ELF"
        });
        {
            var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
            {
                TokenSymbol = "ELF"
            });
            tokenPoolInfo.Liquidity.ShouldBe(3000000000);
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = tokenPoolInfo.TokenVirtualAddress,
                    Symbol = "ELF"
                });
                balance.Balance.ShouldBe(3000000000);
            }
        }
        {
            var liquidityInfo = await TokenPoolContractStub.GetLiquidity.CallAsync(new GetLiquidityInput
            {
                Provider = DefaultSenderAddress,
                TokenSymbol = "ELF"
            });
            liquidityInfo.Value.ShouldBe(2000000000);
        }
        // 3. release
        {
            await TokenPoolContractStub.Release.SendAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 2500000000,
                Receiver = User1Address,
                TargetTokenSymbol = "ELF"
            });
        }
        {
            var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
            {
                TokenSymbol = "ELF"
            });
            tokenPoolInfo.Liquidity.ShouldBe(500000000);
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Owner = tokenPoolInfo.TokenVirtualAddress,
                    Symbol = "ELF"
                });
                balance.Balance.ShouldBe(500000000);
            }
        }
        {
            var liquidityInfo = await TokenPoolContractStub.GetLiquidity.CallAsync(new GetLiquidityInput
            {
                Provider = DefaultSenderAddress,
                TokenSymbol = "ELF"
            });
            liquidityInfo.Value.ShouldBe(2000000000);
            var removableLiquidity = await TokenPoolContractStub.GetRemovableLiquidity.CallAsync(new GetLiquidityInput
            {
                Provider = DefaultSenderAddress,
                TokenSymbol = "ELF"
            });
            removableLiquidity.Value.ShouldBe(500000000);
        }
    }

    [Fact]
    public async Task AddLiquidity_Test_Failed()
    {
        await TokenPoolContractStub.Initialize.SendAsync(new InitializeInput
        {
            BridgeContractAddress = BridgeContractAddress,
            Admin = DefaultSenderAddress
        });
        {
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = TokenPoolContractAddress,
                Symbol = "ELF",
                Amount = 10000000000
            });
        }
        await InitializeBridgeContractAndAddToken();
        {
            var result = await TokenPoolContractStub.AddLiquidity.SendWithExceptionAsync(new AddLiquidityInput
            {
                Amount = 2000000000
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.AddLiquidity.SendWithExceptionAsync(new AddLiquidityInput
            {
                TokenSymbol = "",
                Amount = 2000000000
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.AddLiquidity.SendWithExceptionAsync(new AddLiquidityInput
            {
                Amount = 0,
                TokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.AddLiquidity.SendWithExceptionAsync(new AddLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.AddLiquidity.SendWithExceptionAsync(new AddLiquidityInput
            {
                TokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
    }

    [Fact]
    public async Task RemoveLiquidity_Test_Success()
    {
        await TokenPoolContractStub.Initialize.SendAsync(new InitializeInput
        {
            BridgeContractAddress = BridgeContractAddress,
            Admin = DefaultSenderAddress
        });
        await InitializeBridgeContractAndAddToken();
        await TokenContractStub.Approve.SendAsync(new ApproveInput
        {
            Spender = TokenPoolContractAddress,
            Symbol = "ELF",
            Amount = 10000000000
        });
        await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
        {
            TokenSymbol = "ELF",
            Amount = 2000000000
        });
        var balanceBefore = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        })).Balance;
        var result = await TokenPoolContractStub.RemoveLiquidity.SendAsync(new RemoveLiquidityInput
        {
            TokenSymbol = "ELF",
            Amount = 1500000000
        });
        var events = LiquidityRemoved.Parser.ParseFrom(result.TransactionResult.Logs
            .FirstOrDefault(l => l.Name == nameof(LiquidityRemoved))?.NonIndexed);
        events.TokenSymbol.ShouldBe("ELF");
        events.Provider.ShouldBe(DefaultSenderAddress);
        events.Amount.ShouldBe(1500000000);
        var balanceAfter = (await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultSenderAddress,
            Symbol = "ELF"
        })).Balance;
        (balanceAfter - balanceBefore).ShouldBe(1500000000);
        var tokenPoolInfo = await TokenPoolContractStub.GetTokenPoolInfo.CallAsync(new GetTokenPoolInfoInput
        {
            TokenSymbol = "ELF"
        });
        tokenPoolInfo.Liquidity.ShouldBe(500000000);
        {
            var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = tokenPoolInfo.TokenVirtualAddress,
                Symbol = "ELF"
            });
            balance.Balance.ShouldBe(500000000);
        }
        {
            var liquidityInfo = await TokenPoolContractStub.GetLiquidity.CallAsync(new GetLiquidityInput
            {
                Provider = DefaultSenderAddress,
                TokenSymbol = "ELF"
            });
            liquidityInfo.Value.ShouldBe(500000000);
            var removableLiquidity = await TokenPoolContractStub.GetRemovableLiquidity.CallAsync(
                new GetLiquidityInput
                {
                    Provider = DefaultSenderAddress,
                    TokenSymbol = "ELF"
                });
            removableLiquidity.Value.ShouldBe(500000000);
        }
    }
     [Fact]
    public async Task RemoveLiquidity_Test_Failed()
    {
        await TokenPoolContractStub.Initialize.SendAsync(new InitializeInput
        {
            BridgeContractAddress = BridgeContractAddress,
            Admin = DefaultSenderAddress
        });
        await InitializeBridgeContractAndAddToken();
        {
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                Amount = 2000000000
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                TokenSymbol = "",
                Amount = 2000000000
            });
            result.TransactionResult.Error.ShouldContain("Invalid symbol.");
        }
        {
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                Amount = 0,
                TokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                TokenSymbol = "ELF"
            });
            result.TransactionResult.Error.ShouldContain("Invalid amount.");
        }
        {
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = 4000000000
            });
            result.TransactionResult.Error.ShouldContain("Not enough liquidity to remove.");
        }
        {
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = TokenPoolContractAddress,
                Symbol = "ELF",
                Amount = 10000000000
            });
            await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = 2000000000
            });
        }
        {
            await TokenPoolContractStub.SetBridgeContract.SendAsync(DefaultSenderAddress);
            await TokenPoolContractStub.Release.SendAsync(new ReleaseInput
            {
                FromChainId = "Sepolia",
                Amount = 700000000,
                TargetTokenSymbol = "ELF",
                Receiver = User1Address
            });
        }
        {
            await TokenPoolContractStub.SetBridgeContract.SendAsync(BridgeContractAddress);
            var result = await TokenPoolContractStub.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = 2000000000
            });
            result.TransactionResult.Error.ShouldContain("Not enough liquidity to remove.");
        }
        {
            var result = await TokenPoolContractStub1.RemoveLiquidity.SendWithExceptionAsync(new RemoveLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = 500000000
            });
            result.TransactionResult.Error.ShouldContain("Not enough liquidity to remove.");
        }
    }

    private async Task InitializeBridgeContractAndAddToken()
    {
        await BridgeContractStub.Initialize.SendAsync(new Bridge.InitializeInput
        {
            MerkleTreeContractAddress = DefaultSenderAddress,
            OracleContractAddress = DefaultSenderAddress,
            RegimentContractAddress = DefaultSenderAddress,
            ReportContractAddress = DefaultSenderAddress,
            Admin = DefaultSenderAddress,
            Controller = DefaultSenderAddress,
            OrganizationAddress = DefaultSenderAddress,
            PauseController = DefaultSenderAddress,
            ApproveTransferController = DefaultSenderAddress
        });
        await BridgeContractStub.AddToken.SendAsync(new AddTokenInput
        {
            Value =
            {
                new ChainToken
                {
                    ChainId = "Sepolia",
                    Symbol = "ELF"
                }
            }
        });
    }
}