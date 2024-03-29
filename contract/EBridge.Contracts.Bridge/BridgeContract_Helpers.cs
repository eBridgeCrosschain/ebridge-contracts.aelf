using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;

namespace EBridge.Contracts.Bridge
{
    public partial class BridgeContract
    {
        private TokenInfo GetTokenInfo(string symbol)
        {
            RequireTokenContractStateSet();
            return State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput {Symbol = symbol});
        }

        private void TransferToken(string symbol, long amount, Address to)
        {
            if (amount <= 0)
            {
                return;
            }

            RequireTokenContractStateSet();
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Amount = amount,
                Symbol = symbol,
                To = to,
                Memo = "Token swap."
            });
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
            var priceRatioDecimal = (decimal) priceRatio / 100000000;
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
                (decimal) Math.Abs(State.PriceRatio[chainId].Sub(State.PrePriceRatio[chainId])) / 100000000;
            if (priceRatioDif == 0)
            {
                return;
            }

            Assert(State.PriceFluctuationRatio[chainId] > 0, "Not set price fluctuation ratio.");
            var priceFluctuation = State.PriceFluctuationRatio[chainId];
            var priceRatioMax = (decimal) Math.Max(State.PriceRatio[chainId], State.PrePriceRatio[chainId]) / 100000000;
            var fluctuation = priceRatioDif / priceRatioMax;
            Assert(fluctuation <= (decimal) priceFluctuation / 100,
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
            var gasPriceDecimal = (decimal) gasPrice / 1000000000;
            var transactionFee = gasFee * gasPriceDecimal;
            var priceRatioDecimal = (decimal) priceRatio / 100000000;
            var fee = decimal.Round((transactionFee / 1000000000) * priceRatioDecimal * feeRatio, PriceDecimals);
            return (long) decimal.Ceiling(fee) * 100000000;
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

        private void TransferDepositTo(string symbol, long amount, Address from)
        {
            Assert(amount > 0,$"Insufficient lock amount {amount}.");

            RequireTokenContractStateSet();
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = from,
                Amount = amount,
                Symbol = symbol,
                To = Context.Self,
                Memo = "Token Lock."
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
    }
}