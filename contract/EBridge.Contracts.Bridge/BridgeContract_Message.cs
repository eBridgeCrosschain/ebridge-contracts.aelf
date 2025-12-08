using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public const int SlotByteSize = 32;
    public const int TonAddressByteSize = 36;
    public const int TimeStampByteSize = 8;


    private List<byte> GenerateMessage(Hash receiptIdToken, long amount, string targetAddress, long receiptIndex)
    {
        var timestamp = Context.CurrentBlockTime.Seconds;
        var targetAddressToBase64 = targetAddress.Replace('-', '+').Replace('_', '/');
        var receiptHash =
            CalculateReceiptHash(receiptIdToken, amount, targetAddressToBase64, receiptIndex,ChainType.Tvm);
        var lazyData = new List<byte>();
        lazyData.AddRange(FillObservationBytes(ConvertLong(receiptIndex).ToArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(receiptIdToken.ToByteArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(ConvertLong(amount).ToArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(receiptHash.ToByteArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(ByteString.FromBase64(targetAddressToBase64).ToByteArray(),
            TonAddressByteSize));
        lazyData.AddRange(FillObservationBytes(ConvertLong(timestamp,TimeStampByteSize).ToArray(), TimeStampByteSize));
        return lazyData;
    }

    private List<byte> GenerateEvmMessage(Hash receiptIdToken, long amount, string targetAddress, long receiptIndex)
    {
        var receiptHash =
            CalculateReceiptHash(receiptIdToken, amount, targetAddress, receiptIndex,ChainType.Evm);
        var lazyData = new List<byte>();
        lazyData.AddRange(FillObservationBytes(ConvertLong(receiptIndex).ToArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(receiptIdToken.ToByteArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(ConvertLong(amount).ToArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(receiptHash.ToByteArray(), SlotByteSize));
        lazyData.AddRange(FillObservationBytes(ByteStringHelper.FromHexString(targetAddress).ToByteArray(),SlotByteSize));
        return lazyData;
    }
    
    private List<byte> FillObservationBytes(byte[] result, int byteSize)
    {
        if (result.Length == 0)
            return GetByteListWithCapacity(byteSize);
        var totalBytesLength = result.Length.Sub(1).Div(byteSize).Add(1);
        var ret = GetByteListWithCapacity(totalBytesLength.Mul(byteSize));
        // Pad with zeros in front until less than 32 bytes.
        BytesCopy(result, 0, ret, byteSize - result.Length, result.Length);
        return ret;
    }

    private long ParseHexToLong(byte[] data)
    {
        var res = new List<byte>();
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] <= 0) continue;
            for (var j = i; j < data.Length; j++)
            {
                res.Add(data[j]);
            }

            break;
        }
        var hexString = res.ToArray().ToHex();
        long decimalValue = 0;
        foreach (var hexChar in hexString)
        {
            var hexValue = hexChar switch
            {
                >= '0' and <= '9' => hexChar - '0',
                >= 'A' and <= 'F' => hexChar - 'A' + 10,
                >= 'a' and <= 'f' => hexChar - 'a' + 10,
                _ => 0
            };

            decimalValue = decimalValue * 16 + hexValue;
        }

        return decimalValue;
    }
    private string ParseHexToString(byte[] data)
    {
        var res = new List<byte>();
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] <= 0) continue;
            for (var j = i; j < data.Length; j++)
            {
                res.Add(data[j]);
            }

            break;
        }
        var hexString = res.ToArray().ToHex();
        BigIntValue decimalValue = 0;
        foreach (var hexChar in hexString)
        {
            var hexValue = hexChar switch
            {
                >= '0' and <= '9' => hexChar - '0',
                >= 'A' and <= 'F' => hexChar - 'A' + 10,
                >= 'a' and <= 'f' => hexChar - 'a' + 10,
                _ => 0
            };

            decimalValue = decimalValue.Mul(16).Add(hexValue);
        }

        return decimalValue.Value;
    }
}