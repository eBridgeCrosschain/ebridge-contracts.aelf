using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.Election;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using EBridge.Contracts.TokenPool;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using Ramp;
using TimestampHelper = AElf.Kernel.TimestampHelper;

namespace EBridge.Contracts.Bridge
{
    public class BridgeContractRampTests : BridgeContractTestBase
    {
        #region SetCrossChainConfig Tests

        [Fact]
        public async Task SetCrossChainConfig_Success()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TestChain",
                ContractAddress = "0xTestContractAddress",
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0xTestContractAddressForReceive"
            };

            // Act
            await BridgeContractImplStub.SetCrossChainConfig.SendAsync(input);

            // Assert
            var result =
                await BridgeContractImplStub.GetCrossChainConfig.CallAsync(new StringValue { Value = "TestChain" });
            result.ShouldNotBeNull();
            result.ChainId.ShouldBe(12345);
            result.ContractAddress.ShouldBe("0xTestContractAddress");
            result.ContractAddressForReceive.ShouldBe("0xTestContractAddressForReceive");
            result.ChainType.ShouldBe(ChainType.Evm);
            result.Fee.ShouldBe(0); // Fee should be 0 for Evm

            var chainIdResult = await BridgeContractImplStub.GetChainIdMap.CallAsync(new Int32Value { Value = 12345 });
            chainIdResult.Value.ShouldBe("TestChain");
        }

        [Fact]
        public async Task SetCrossChainConfig_Success_Tvm()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TvmChain",
                ContractAddress = "kQTestTvmContractAddress",
                ChainIdNumber = 54321,
                ChainType = ChainType.Tvm,
                ContractAddressForReceive = "kQTestTvmContractAddressForReceive",
                Fee = 100
            };

            // Act
            await BridgeContractImplStub.SetCrossChainConfig.SendAsync(input);

            // Assert
            var result =
                await BridgeContractImplStub.GetCrossChainConfig.CallAsync(new StringValue { Value = "TvmChain" });
            result.ShouldNotBeNull();
            result.ChainId.ShouldBe(54321);
            result.ContractAddress.ShouldBe("kQTestTvmContractAddress");
            result.ContractAddressForReceive.ShouldBe("kQTestTvmContractAddressForReceive");
            result.ChainType.ShouldBe(ChainType.Tvm);
            result.Fee.ShouldBe(100); // Fee should be set for Tvm
        }

        [Fact]
        public async Task SetCrossChainConfig_NoPermission()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TestChain",
                ContractAddress = "0xTestContractAddress",
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0xTestContractAddressForReceive"
            };

            // Act & Assert
            var result = await BridgeContractImplSetFeeRatioStub.SetCrossChainConfig.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task SetCrossChainConfig_InvalidChainId()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "", // Empty chain ID
                ContractAddress = "0xTestContractAddress",
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0xTestContractAddressForReceive"
            };

            // Act & Assert
            var result = await BridgeContractImplStub.SetCrossChainConfig.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("Invalid chain.");
        }

        [Fact]
        public async Task SetCrossChainConfig_InvalidContractAddress()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TestChain",
                ContractAddress = "", // Empty contract address
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0xTestContractAddressForReceive"
            };

            // Act & Assert
            var result = await BridgeContractImplStub.SetCrossChainConfig.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("Invalid contract address.");
        }

        [Fact]
        public async Task SetCrossChainConfig_InvalidContractAddressForReceive()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TestChain",
                ContractAddress = "0xTestContractAddress",
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "" // Empty contract address for receive
            };

            // Act & Assert
            var result = await BridgeContractImplStub.SetCrossChainConfig.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("Invalid contract address for receive.");
        }

        #endregion

        #region SetRampContract Tests

        [Fact]
        public async Task SetRampContract_Success()
        {
            // Arrange
            await InitialBridgeContractAsync();

            // Act
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);

            // Assert
            var result = await BridgeContractImplStub.GetRampContract.CallAsync(new Empty());
            result.ShouldBe(RampContractAddress);
        }

        [Fact]
        public async Task SetRampContract_NoPermission()
        {
            // Arrange
            await InitialBridgeContractAsync();

            // Act & Assert
            var result =
                await BridgeContractImplSetFeeRatioStub.SetRampContract.SendWithExceptionAsync(RampContractAddress);
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task SetRampContract_InvalidAddress()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var invalidAddress = new Address(); // Empty address

            // Act & Assert
            var result = await BridgeContractImplStub.SetRampContract.SendWithExceptionAsync(invalidAddress);
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }

        #endregion

        #region SetRampTokenSwapConfig Tests

        [Fact]
        public async Task SetRampTokenSwapConfig_NoPermission()
        {
            // Arrange
            await InitialBridgeContractAsync();
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);

            var input = new TokenSwapConfig
            {
                TokenSwapList = new TokenSwapList
                {
                    TokenSwapInfoList =
                    {
                        new TokenSwapInfo
                        {
                            TargetChainId = 1,
                            TokenAddress = "0xTokenAddress",
                            Symbol = "ELF",
                            ExtraData = ByteString.CopyFromUtf8("ExtraData"),
                            Receiver = "Receiver"
                        }
                    }
                }
            };

            // Act & Assert
            var result = await BridgeContractImplSetFeeRatioStub.SetRampTokenSwapConfig.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task SetRampTokenSwapConfig_InvalidInput_Null()
        {
            // Arrange
            await InitialBridgeContractAsync();
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);

            // Instead of sending null, send an empty object which should also be invalid
            var emptyInput = new TokenSwapConfig
            {
                TokenSwapList = null
            };

            // Act & Assert
            var result = await BridgeContractImplStub.SetRampTokenSwapConfig.SendWithExceptionAsync(emptyInput);
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }

        [Fact]
        public async Task SetRampTokenSwapConfig_InvalidInput_EmptyList()
        {
            // Arrange
            await InitialBridgeContractAsync();
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);

            var input = new TokenSwapConfig
            {
                TokenSwapList = new TokenSwapList()
            };

            // Act & Assert
            var result = await BridgeContractImplStub.SetRampTokenSwapConfig.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }

        #endregion

        #region ForwardMessage Tests

        // Note: ForwardMessage is a complex method that requires proper setup
        // These tests provide a basic structure that may need to be adapted based on your specific implementation

        [Fact]
        public async Task ForwardMessage_ContractPaused()
        {
            // Arrange
            await InitialBridgeContractAsync();
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);
            await BridgeContractStub.Pause.SendAsync(new Empty()); // Pause the contract

            var input = new ForwardMessageInput
            {
                Message = ByteString.CopyFromUtf8("TestMessage"),
                TargetChainId = 9992731,
                SourceChainId = 1,
                Receiver = BridgeContractAddress.ToByteString(),
                Sender = ByteString.CopyFromUtf8("Sender"),
                TokenTransferMetadata = new Ramp.TokenTransferMetadata
                {
                    ExtraData = ByteString.CopyFromUtf8("ExtraData"),
                    TargetChainId = 1,
                    TokenAddress = "0xTokenAddress",
                    Symbol = "ELF",
                    Amount = 100
                }
            };

            // Act & Assert
            var result = await BridgeContractImplStub.ForwardMessage.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("Contract is paused.");
        }

        [Fact]
        public async Task ForwardMessage_NoPermission()
        {
            // Arrange
            await InitialBridgeContractAsync();
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);

            var input = new ForwardMessageInput
            {
                Message = ByteString.CopyFromUtf8("TestMessage"),
                TargetChainId = 9992731,
                SourceChainId = 1,
                Receiver = BridgeContractAddress.ToByteString(),
                Sender = ByteString.CopyFromUtf8("Sender"),
                TokenTransferMetadata = new Ramp.TokenTransferMetadata
                {
                    ExtraData = ByteString.CopyFromUtf8("ExtraData"),
                    TargetChainId = 1,
                    TokenAddress = "0xTokenAddress",
                    Symbol = "ELF",
                    Amount = 100
                }
            };

            // Act & Assert - Using a different sender than RampContract
            var result = await BridgeContractImplStub.ForwardMessage.SendWithExceptionAsync(input);
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task ForwardMessage()
        {
            // 1. Initialize
            await InitialBridgeContractAsync();
            // 2. set ramp
            var tx = await BridgeContractImplStub.SetRampContract.SendAsync(DefaultSenderAddress);
            tx.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            // 3. set cross chain config
            var setConfig = await BridgeContractImplStub.SetCrossChainConfig.SendAsync(new SetCrossChainConfigInput
            {
                ChainId = "Ethereum",
                ContractAddress = "0x3c37E0A09eAFEaA7eFB57107802De1B28A6f5F07",
                ChainIdNumber = 1,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0x3c37E0A09eAFEaA7eFB57107802De1B28A6f5F07"
            });
            setConfig.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            // 4. create swap
            var swap = await BridgeContractStub.CreateSwap.SendAsync(new CreateSwapInput
            {
                SwapTargetToken = new SwapTargetToken
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
            swap.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var swapId = swap.Output;
            // 5. set limit
            var time = TimestampHelper.GetUtcNow().ToDateTime().Date;
            var limit = await BridgeContractImplStub.SetSwapDailyLimit.SendAsync(new SetSwapDailyLimitInput
            {
                SwapDailyLimitInfos =
                {
                    new SwapDailyLimitInfo
                    {
                        SwapId = swapId,
                        DefaultTokenAmount = 1000000_00000000,
                        StartTime = Timestamp.FromDateTime(time)
                    }
                }
            });
            limit.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var time1 = TimestampHelper.GetUtcNow().ToDateTime();
            var input = new List<SwapTokenBucketConfig>()
            {
                new SwapTokenBucketConfig
                {
                    SwapId = swapId,
                    IsEnable = true,
                    TokenCapacity = 100000_00000000,
                    Rate = 10000_00000000
                }
            };
            blockTimeProvider.SetBlockTime(Timestamp.FromDateTime(time));
            var result = await BridgeContractImplStub.ConfigSwapTokenBucket.SendAsync(new ConfigSwapTokenBucketInput
            {
                SwapTokenBucketConfigs = { input }
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            // 6. liquidity
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = TokenPoolContractAddress,
                Symbol = "ELF",
                Amount = 100000_00000000
            });
            var add = await TokenPoolContractStub.AddLiquidity.SendAsync(new AddLiquidityInput
            {
                TokenSymbol = "ELF",
                Amount = 100000_00000000,
            });
            add.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var message =
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAC0HC3b+c0/+BoxMRLtVSUwEqE7GUwFwKyeqxZ1ButdpwAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAVLQLH4Ur2gAAAMnrbsDIw/un3hykgXC11qJneV8BfCCorzyxcUVCFbO+AQXYGVggx3Vq/N0vVJN0moXLIapKArTluAUFzskLhgA==";
            var forwardMessageInput = new ForwardMessageInput
            {
                Message = ByteString.FromBase64(message),
                TargetChainId = 9992731,
                SourceChainId = 1,
                Receiver = BridgeContractAddress.ToByteString(),
                Sender = ByteStringHelper.FromHexString("0x3c37E0A09eAFEaA7eFB57107802De1B28A6f5F07"),
                TokenTransferMetadata = new Ramp.TokenTransferMetadata
                {
                    ExtraData = ByteStringHelper.FromHexString(swapId.ToHex()),
                    TargetChainId = 9992731,
                    TokenAddress = "0x8adD57b8aD6C291BC3E3ffF89F767fcA08e0E7Ab",
                    Symbol = "ELF",
                    Amount = 100000_00000000
                }
            };
            var res = await BridgeContractImplStub.ForwardMessage.SendAsync(forwardMessageInput);
            res.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }
        
        #endregion

        #region GetCrossChainConfig, GetChainIdMap, GetRampContract Tests

        [Fact]
        public async Task GetCrossChainConfig_Success()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TestChain",
                ContractAddress = "0xTestContractAddress",
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0xTestContractAddressForReceive"
            };
            await BridgeContractImplStub.SetCrossChainConfig.SendAsync(input);

            // Act
            var result =
                await BridgeContractImplStub.GetCrossChainConfig.CallAsync(new StringValue { Value = "TestChain" });

            // Assert
            result.ShouldNotBeNull();
            result.ChainId.ShouldBe(12345);
            result.ContractAddress.ShouldBe("0xTestContractAddress");
            result.ContractAddressForReceive.ShouldBe("0xTestContractAddressForReceive");
            result.ChainType.ShouldBe(ChainType.Evm);
        }

        [Fact]
        public async Task GetChainIdMap_Success()
        {
            // Arrange
            await InitialBridgeContractAsync();
            var input = new SetCrossChainConfigInput
            {
                ChainId = "TestChain",
                ContractAddress = "0xTestContractAddress",
                ChainIdNumber = 12345,
                ChainType = ChainType.Evm,
                ContractAddressForReceive = "0xTestContractAddressForReceive"
            };
            await BridgeContractImplStub.SetCrossChainConfig.SendAsync(input);

            // Act
            var result = await BridgeContractImplStub.GetChainIdMap.CallAsync(new Int32Value { Value = 12345 });

            // Assert
            result.Value.ShouldBe("TestChain");
        }

        [Fact]
        public async Task GetRampContract_Success()
        {
            // Arrange
            await InitialBridgeContractAsync();
            await BridgeContractImplStub.SetRampContract.SendAsync(RampContractAddress);

            // Act
            var result = await BridgeContractImplStub.GetRampContract.CallAsync(new Empty());

            // Assert
            result.ShouldBe(RampContractAddress);
        }

        #endregion
    }
}