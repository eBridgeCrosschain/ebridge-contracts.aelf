using System.Collections.Generic;
using System.Linq;
using AElf.Types;
using SampleAccount = AElf.ContractTestBase.ContractTestKit.SampleAccount;

namespace AElf.Contracts.Bridge
{
    public class SampleSwapInfo
    {
        public static List<SwapInfoTest> SwapInfos;

        static SampleSwapInfo()
        {
            SwapInfos = new List<SwapInfoTest>();
            for (var i = 1; i <= 5; i++)
            {
                var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom("Ethereum"), HashHelper.ComputeFrom("ELF"));
                SwapInfos.Add(new SwapInfoTest
                {
                    OriginAmount = (1000000000_00000000 * i).ToString(),
                    ReceiverAddress = Receivers[(i - 1) % 5],
                    ReceiptId = $"{tokenId}.{i}",
                    TargetChainId = "AELF"
                });
            }
            for (var i = 1; i <= 5; i++)
            {
                var tokenId = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom("BSC"), HashHelper.ComputeFrom("USDT"));
                SwapInfos.Add(new SwapInfoTest
                {
                    OriginAmount = (100000000000000000 * i).ToString(),
                    ReceiverAddress = Receivers[(i - 1) % 5],
                    ReceiptId = $"{tokenId}.{i}",
                    TargetChainId = "AELF"
                });
            }
        }

        private static readonly List<Address> Receivers =
            SampleAccount.Accounts.Skip(6).Take(5).Select(a => a.Address).ToList();
    }

    public class SwapInfoTest
    {
        public Hash ReceiptHash => CalculateReceiptHash();
        public string ReceiptId { get; set; }
        public Address ReceiverAddress { get; set; }
        public string OriginAmount { get; set; }

        public string TargetChainId { get; set; }

        private Hash CalculateReceiptHash()
        {
            var amountHash = HashHelper.ComputeFrom(OriginAmount);
            var receiptIdHash = HashHelper.ComputeFrom(ReceiptId);
            var targetAddressHash = GetHashFromAddressData(ReceiverAddress);
            return HashHelper.ConcatAndCompute(amountHash, targetAddressHash, receiptIdHash);
        }
        

        private Hash GetHashFromAddressData(Address receiverAddress)
        {
            return HashHelper.ComputeFrom(receiverAddress.ToBase58());
        }
    }
}