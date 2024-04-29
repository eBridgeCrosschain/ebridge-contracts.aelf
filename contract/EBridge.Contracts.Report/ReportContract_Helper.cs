using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Types;

namespace EBridge.Contracts.Report
{
    public partial class ReportContract
    {
        public const string ArraySuffix = "[]";
        public const string Bytes32 = "bytes32";
        public const string Bytes32Array = Bytes32 + ArraySuffix;
        public const string Uint256 = "uint256";
        public const int SlotByteSize = 32;
        public const int DigestFixedLength = 16;

        private string GenerateEvmRawReport(Report report)
        {
            var data = new List<object>();
            GenerateObservation(report, out var observations);
            data.Add(observations);

            //Byte32[]: the concrete answer(observation data)
            var result = SerializeReport(data, Bytes32Array).ToArray()
                .ToHex();
            return result;
        }

        // TODO: A report generator selector is needed.

        private List<byte> GenerateConfigText(Report report)
        {
            var round = report.RoundId;
            var validBytesCount = (byte) report.AggregatedData.Length;
            Assert(round >= 0, "Invalid round.");

            // configText consists of:
            // 6-byte zero padding
            // 16-byte configDigest
            // 8-byte round id
            // 1-byte observer count
            // 1-byte valid byte count (aggregated answer)
            var configText = GetByteListWithCapacity(SlotByteSize);
            var roundBytes = round.ToBytes();
            BytesCopy(roundBytes, 0, configText, 22, 8);
            configText[SlotByteSize.Sub(1)] = validBytesCount;
            return configText;
        }

        private List<byte> FillObservationBytes(byte[] result)
        {
            if (result.Length == 0)
                return GetByteListWithCapacity(SlotByteSize);
            var totalBytesLength = result.Length.Sub(1).Div(SlotByteSize).Add(1);
            var ret = GetByteListWithCapacity(totalBytesLength.Mul(SlotByteSize));
            // Pad with zeros in front until less than 32 bytes.
            BytesCopy(result, 0, ret, SlotByteSize-result.Length , result.Length);
            return ret;
        }


        private void GenerateObservation(Report report, out List<byte> observations)
        {
            observations = new List<byte>();
            if (report.Observations.Value.Count > 0)
            {
                observations.AddRange(
                    FillObservationBytes(ConvertLong(GetReceiptIndex(report.Observations.Value.First().Key)).ToArray()));
                observations.AddRange(FillObservationBytes(ByteStringHelper
                    .FromHexString(report.Observations.Value.First().Data).ToByteArray()));
            }
            if (report.Observations.Value.Count <= 1) return;
            for (var i = 1; i < report.Observations.Value.Count; i++)
            {
                if (report.Observations.Value[i].Key == DefaultReceiptInfoKey)
                {
                    // first value is amount
                    var valueArray = report.Observations.Value[i].Data.Split("-");
                    Assert(long.TryParse(valueArray.First(), out var value), "Failed to parse.");
                    observations.AddRange(FillObservationBytes(ConvertLong(value).ToArray()));
                    for (var j = 1; j < valueArray.Length; j++)
                    {
                        observations.AddRange(FillObservationBytes(ByteStringHelper
                            .FromHexString(valueArray[j]).ToByteArray()));
                    }
                }
                else
                {
                    observations.AddRange(FillObservationBytes(ByteStringHelper
                        .FromHexString(report.Observations.Value[i].Data).ToByteArray()));
                }
            }
        }

        private long GetReceiptIndex(string receipt)
        {
            Assert(long.TryParse(receipt.Split(".").Last(), out var receiptIndex), "Incorrect receipt index.");
            return receiptIndex;
        }


        private void GenerateMultipleObservation(Report report, out List<byte> observerOrder,
            out List<byte> observationsLength,
            out List<byte> observations)
        {
            observerOrder = GetByteListWithCapacity(SlotByteSize);
            observationsLength = GetByteListWithCapacity(SlotByteSize);
            observations = new List<byte>();
            if (report.Observations.Value.Any() && !int.TryParse(report.Observations.Value[0].Key, out _))
            {
                return;
            }

            int i = 0;
            foreach (var observation in report.Observations.Value)
            {
                Assert(int.TryParse(observation.Key, out var order), $"invalid observation key : {observation.Key}");
                observerOrder[i] = (byte) order;
                observation.Data = observation.Data;
                observationsLength[i] = (byte) observation.Data.Length;
                observations.AddRange(FillObservationBytes(observation.Data.GetBytes()));
                i++;
            }
        }

        private List<byte> SerializeReport(List<object> data, params string[] dataType)
        {
            var dataLength = (long) dataType.Length;
            Assert(dataLength == data.Count, "Invalid data length.");
            var result = new List<byte>();
            var currentIndex = dataLength;
            var lazyData = new List<byte>();
            for (var i = 0; i < dataLength; i++)
            {
                var typeStrLen = dataType[i].Length;
                if (string.CompareOrdinal(dataType[i].Substring(typeStrLen.Sub(2), 2), ArraySuffix) == 0)
                {
                    var typePrefix = dataType[i].Substring(0, typeStrLen.Sub(2));
                    long dataPosition;
                    if (data[i] is IEnumerable<long> dataList)
                    {
                        long arrayLength = dataList.Count();
                        dataPosition = currentIndex.Mul(SlotByteSize);
                        result.AddRange(ConvertLong(dataPosition));
                        currentIndex = currentIndex.Add(arrayLength).Add(1);
                        lazyData.AddRange(ConvertLong(arrayLength));
                        lazyData.AddRange(ConvertLongArray(typePrefix, dataList));
                        continue;
                    }

                    var bytesList = data[i] as List<byte>;
                    Assert(bytesList != null, "invalid observations");
                    var bytes32Count = bytesList.Count % SlotByteSize == 0
                        ? bytesList.Count.Div(SlotByteSize)
                        : bytesList.Count.Div(SlotByteSize).Add(1);
                    dataPosition = currentIndex.Mul(SlotByteSize);
                    result.AddRange(ConvertLong(dataPosition));
                    currentIndex = currentIndex.Add(bytes32Count).Add(1);
                    lazyData.AddRange(ConvertLong(bytes32Count));
                    lazyData.AddRange(ConvertBytes32Array(bytesList, bytes32Count.Mul(SlotByteSize)));
                    continue;
                }

                if (dataType[i] == Bytes32)
                {
                    result.AddRange(ConvertBytes32(data[i]));
                }
                else if (dataType[i] == Uint256)
                {
                    result.AddRange(ConvertLong((long) data[i]));
                }
            }

            result.AddRange(lazyData);
            return result;
        }

        private IEnumerable<byte> ConvertLong(long data)
        {
            var b = data.ToBytes();
            if (b.Length == SlotByteSize)
                return b;
            var diffCount = SlotByteSize.Sub(b.Length);
            var longDataBytes = GetByteListWithCapacity(SlotByteSize);
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

        private List<byte> ConvertLongArray(string dataType, IEnumerable<long> dataList)
        {
            if (dataType != Uint256)
                return null;
            var dataBytes = new List<byte>();
            foreach (var data in dataList)
            {
                dataBytes.AddRange(ConvertLong(data));
            }

            return dataBytes;
        }

        private List<byte> ConvertBytes32Array(List<byte> data, int dataSize)
        {
            if (dataSize == 0)
            {
                return new List<byte>();
            }

            var target = GetByteListWithCapacity(dataSize);
            BytesCopy(data, 0, target, 0, data.Count);
            return target;
        }

        private List<byte> ConvertBytes32(object data)
        {
            var dataBytes = data as List<byte>;
            Assert(dataBytes.Count == SlotByteSize, "Invalid bytes32 data.");

            return dataBytes;
        }

        private void BytesCopy(IReadOnlyList<byte> src, int srcOffset, List<byte> dst, int dstOffset, int count)
        {
            for (var i = srcOffset; i < srcOffset + count; i++)
            {
                dst[dstOffset] = src[i];
                dstOffset++;
            }
        }

        private List<byte> GetByteListWithCapacity(int count)
        {
            var list = new List<byte>();
            list.AddRange(Enumerable.Repeat((byte) 0, count));
            return list;
        }

        private long GetAmercementAmount(Address associationAddress = null)
        {
            return associationAddress == null
                ? MinimumAmercementAmount
                : Math.Max(State.AmercementAmountMap[associationAddress], MinimumAmercementAmount);
        }

        private bool IfDataOnChain(OffChainQueryInfo info)
        {
            return info.Title.StartsWith("lock_token");
        }
    }
}