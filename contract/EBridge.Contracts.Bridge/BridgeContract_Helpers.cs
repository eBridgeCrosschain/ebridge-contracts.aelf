using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using EBridge.Contracts.TokenPool;
using Google.Protobuf;
using LockInput = EBridge.Contracts.TokenPool.LockInput;

namespace EBridge.Contracts.Bridge
{
    public partial class BridgeContract
    {
        private bool IsAddressValid(Address input)
        {
            return input != null && !input.Value.IsNullOrEmpty();
        }

        private TokenInfo GetTokenInfo(string symbol)
        {
            RequireTokenContractStateSet();
            return State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput { Symbol = symbol });
        }

        private void TransferToken(string symbol, long amount, Address to, string fromChainId)
        {
            if (amount <= 0)
            {
                return;
            }

            RequireTokenContractStateSet();
            if (State.TokenPoolContract.Value == null)
            {
                State.TokenContract.Transfer.Send(new TransferInput
                {
                    Amount = amount,
                    Symbol = symbol,
                    To = to,
                    Memo = "Token swap."
                });
            }
            else
            {
                State.TokenPoolContract.Release.Send(new ReleaseInput
                {
                    FromChainId = fromChainId,
                    Amount = amount,
                    Receiver = to,
                    TargetTokenSymbol = symbol
                });
            }
        }

        private void RequireTokenContractStateSet()
        {
            if (State.TokenContract.Value != null)
                return;

            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        }

        private SwapInfo GetTokenSwapInfo(Hash swapId)
        {
            var swapInfo = State.SwapInfo[swapId];
            Assert(swapInfo != null, "Token swap pair not found.");
            return swapInfo;
        }


        private bool ValidateSwapRatio(SwapRatio swapRatio)
        {
            return swapRatio.OriginShare > 0 && swapRatio.TargetShare > 0;
        }


        private bool TryGetOriginTokenAmount(string amountInString, out decimal amount)
        {
            return decimal.TryParse(amountInString, out amount);
        }

        private void TryGetNativeToken(decimal amount, long priceRatio, out decimal nativeAmount)
        {
            var priceRatioDecimal = (decimal)priceRatio / 100000000;
            nativeAmount = amount * priceRatioDecimal;
        }


        private bool TryGetReceiptIndex(string receiptId, out long receiptIndex)
        {
            return long.TryParse(receiptId.Split(".").Last(), out receiptIndex);
        }

        private bool IsValidAmount(string amountInString)
        {
            return !string.IsNullOrEmpty(amountInString) && amountInString.First() != '0' &&
                   amountInString.All(character => character >= '0' && character <= '9');
        }

        private void ValidateSwapTokenInput(SwapTokenInput swapTokenInput)
        {
            GetTokenSwapInfo(swapTokenInput.SwapId);
            var amountInString = swapTokenInput.OriginAmount;
            var validationResult = amountInString.Length > 0 && IsValidAmount(swapTokenInput.OriginAmount);
            Assert(validationResult, "Invalid token swap input.");
            Assert(State.Ledger[swapTokenInput.SwapId][swapTokenInput.ReceiptId] == null, "Already claimed.");
        }

        private Hash GetHashFromAddressData(Address receiverAddress)
        {
            return HashHelper.ComputeFrom(receiverAddress.ToBase58());
        }

        private Hash ComputeLeafHash(decimal amount, Address receiverAddress, string receiptId)
        {
            var amountHash = HashHelper.ComputeFrom(amount.ToString());
            var receiptIdHash = HashHelper.ComputeFrom(receiptId);
            var targetAddressHash = GetHashFromAddressData(receiverAddress);
            return HashHelper.ConcatAndCompute(amountHash, targetAddressHash, receiptIdHash);
        }

        private long GetTargetTokenAmount(decimal amount, SwapRatio swapRatio)
        {
            var expected = amount * swapRatio.TargetShare / swapRatio.OriginShare;
            var targetTokenAmount = decimal.ToInt64(expected);
            return targetTokenAmount;
        }


        private void TransferDepositFrom(string symbol, long amount, Address address)
        {
            RequireTokenContractStateSet();
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                Amount = amount,
                From = address,
                To = Context.Self,
                Symbol = symbol,
                Memo = "Token swap contract deposit."
            });
        }

        private void WithdrawDepositTo(string symbol, long amount, Address address)
        {
            RequireTokenContractStateSet();
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Amount = amount,
                To = address,
                Symbol = symbol,
                Memo = "Token swap withdraw deposit."
            });
        }

        private void AssertSwapTargetToken(string symbol)
        {
            var tokenInfo = GetTokenInfo(symbol);
            Assert(tokenInfo != null && !string.IsNullOrEmpty(tokenInfo.Symbol), "Token not found.");
        }

        private void AssertPriceRatioFluctuation(string chainId)
        {
            var priceRatioDif =
                (decimal)Math.Abs(State.PriceRatio[chainId].Sub(State.PrePriceRatio[chainId])) / 100000000;
            if (priceRatioDif == 0)
            {
                return;
            }

            Assert(State.PriceFluctuationRatio[chainId] > 0, "Not set price fluctuation ratio.");
            var priceFluctuation = State.PriceFluctuationRatio[chainId];
            var priceRatioMax = (decimal)Math.Max(State.PriceRatio[chainId], State.PrePriceRatio[chainId]) / 100000000;
            var fluctuation = priceRatioDif / priceRatioMax;
            Assert(fluctuation <= (decimal)priceFluctuation / 100,
                $"Price fluctuation higher than {priceFluctuation} percent.");
        }

        #region LockToken

        private void DoTransferFee(string symbol, long amount, Address to)
        {
            RequireTokenContractStateSet();
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Amount = amount,
                To = to,
                Symbol = symbol,
                Memo = "Withdraw transaction fee."
            });
        }

        private long CalculateTransactionFee(long gasFee, long gasPrice, long priceRatio, decimal feeRatio)
        {
            var gasPriceDecimal = (decimal)gasPrice / 1000000000;
            var transactionFee = gasFee * gasPriceDecimal;
            var priceRatioDecimal = (decimal)priceRatio / 100000000;
            var fee = decimal.Round((transactionFee / 1000000000) * priceRatioDecimal * feeRatio, PriceDecimals);
            return (long)decimal.Ceiling(fee) * 100000000;
        }
        
        private long CalculateTransactionFeeForTon(long priceRatio,long tonFee)
        {
            var priceRatioDecimal = (decimal)priceRatio / 100000000;
            var fee = decimal.Round(((decimal)tonFee / 1000000000) * priceRatioDecimal, PriceDecimals);
            return (long)(fee * 100000000);
        }

        private Hash CalculateReceiptHash(string receiptId, long amount, string targetAddress)
        {
            var addressHash = HashHelper.ComputeFrom(ByteArrayHelper.HexStringToByteArray(targetAddress));
            var amountEthereum = ConvertLong(amount);
            var amountHash = HashHelper.ComputeFrom(amountEthereum.ToArray());
            var receiptIdHash = HashHelper.ComputeFrom(receiptId);
            return HashHelper.ConcatAndCompute(receiptIdHash, amountHash, addressHash);
        }

        private Hash CalculateReceiptHashForTon(Hash receiptIdToken, long amount, string targetAddress,
            long receiptIndex)
        {
            var addressHash = HashHelper.ComputeFrom(ByteString.FromBase64(targetAddress).ToByteArray());
            var amountTon = ConvertLong(amount);
            var amountHash = HashHelper.ComputeFrom(amountTon.ToArray());
            var receiptIndexTon = ConvertLong(receiptIndex);
            var receiptIndexHash = HashHelper.ComputeFrom(receiptIndexTon.ToArray());
            var receiptIdHash = HashHelper.ConcatAndCompute(receiptIdToken, receiptIndexHash);
            return HashHelper.ConcatAndCompute(receiptIdHash, amountHash, addressHash);
        }

        private IEnumerable<byte> ConvertLong(long data,int byteSize = 32)
        {
            var b = data.ToBytes();

            if (b.Length == byteSize)
                return b;
            var diffCount = byteSize.Sub(b.Length);
            var longDataBytes = GetByteListWithCapacity(byteSize);
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

        private void TransferDepositTo(string symbol, long amount, Address from, string targetChainId)
        {
            Assert(amount > 0, $"Insufficient lock amount {amount}.");

            RequireTokenContractStateSet();
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = from,
                Amount = amount,
                Symbol = symbol,
                To = Context.Self,
                Memo = "Token Lock."
            });
            if (State.TokenPoolContract.Value == null) return;
            State.TokenContract.Approve.Send(new ApproveInput
            {
                Spender = State.TokenPoolContract.Value,
                Symbol = symbol,
                Amount = amount
            });
            State.TokenPoolContract.Lock.Send(new LockInput
            {
                TargetChainId = targetChainId,
                TargetTokenSymbol = symbol,
                Amount = amount,
                Sender = from
            });
        }

        private void TransferFee(string symbol, long amount, Address from, Address to)
        {
            if (amount <= 0)
            {
                return;
            }

            RequireTokenContractStateSet();
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = from,
                Amount = amount,
                Symbol = symbol,
                To = to,
                Memo = "Transaction fee."
            });
        }

        #endregion

        #region limiter

        private DailyLimitTokenInfo GetDailyLimit(DailyLimitTokenInfo dailyLimit)
        {
            if (dailyLimit == null)
            {
                return new DailyLimitTokenInfo();
            }

            var lastRefreshTime = dailyLimit.RefreshTime;
            var count = (Context.CurrentBlockTime - lastRefreshTime).Seconds.Div(DefaultDailyRefreshTime);
            if (count > 0)
            {
                lastRefreshTime = lastRefreshTime.AddSeconds(DefaultDailyRefreshTime.Mul(count));
                dailyLimit.RefreshTime = lastRefreshTime;
                dailyLimit.TokenAmount = dailyLimit.DefaultTokenAmount;
            }

            return dailyLimit;
        }

        private TokenBucket GetTokenBucketAmount(TokenBucket bucket)
        {
            if (bucket == null || !bucket.IsEnable)
            {
                return null;
            }

            var timeDiff = (Context.CurrentBlockTime - bucket.LastUpdatedTime).Seconds;
            if (timeDiff != 0)
            {
                Assert(bucket.CurrentTokenAmount <= bucket.TokenCapacity, "Token bucket overfilled.");
                bucket.CurrentTokenAmount =
                    CalculateRefill(bucket.TokenCapacity, bucket.CurrentTokenAmount, timeDiff, bucket.Rate);
                bucket.LastUpdatedTime = Context.CurrentBlockTime;
            }

            return bucket;
        }

        private void ConsumeTokenAmount(DailyLimitTokenInfo dailyLimitTokenInfo, TokenBucket tokenBucket, long amount)
        {
            Assert(amount <= dailyLimitTokenInfo.TokenAmount,
                $"Amount exceeds daily limit amount. Current daily limit is {dailyLimitTokenInfo.TokenAmount}");
            dailyLimitTokenInfo.TokenAmount = dailyLimitTokenInfo.TokenAmount.Sub(amount);

            if (tokenBucket != null)
            {
                Assert(amount <= tokenBucket.TokenCapacity, "Amount exceeds token max capacity.");
                if (amount > tokenBucket.CurrentTokenAmount)
                {
                    var minWaitInSeconds = amount.Sub(tokenBucket.CurrentTokenAmount).Add(tokenBucket.Rate.Sub(1))
                        .Div(tokenBucket.Rate);
                    throw new AssertionException(
                        $"Amount exceeds current token amount, the minimum wait time is {minWaitInSeconds}s");
                }

                tokenBucket.CurrentTokenAmount = tokenBucket.CurrentTokenAmount.Sub(amount);
            }
        }

        private long CalculateRefill(long capacity, long currentTokenAmount, long timeDiff, long rate)
        {
            return Math.Min(capacity, currentTokenAmount.Add(rate.Mul(timeDiff)));
        }

        #endregion
    }
}