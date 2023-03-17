using AElf.Contracts.ReceiptMakerContract;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.TestContract.ReceiptMaker;

public class ReceiptMakerContract : ReceiptMakerContractImplContainer.ReceiptMakerContractImplBase
{
    public override Empty CreateReceipt(CreateReceiptInput input)
    {
        var receiptList = new ReceiptList();
        for (var i = 0; i < 5; i++)
        {
            var receipt = new Receipt
            {
                Sender = Context.Sender,
                TargetAddress = new Address(),
                Amount = i + 1
            };
            receiptList.Value.Add(receipt);
            State.ReceiptList.Value = State.ReceiptList.Value ?? new ReceiptList();
            State.ReceiptList.Value.Value.Add(receipt);
            var receiptId = State.ReceiptList.Value.Value.Count - 1;
            State.RecorderReceiptHashMap[input.RecorderId][receiptId] = HashHelper.ComputeFrom(receipt);
            var receiptIdList = State.ReceiptIdListMap[input.RecorderId] ?? new ReceiptIdArray();
            receiptIdList.ReceiptId.Add(receiptId);
            State.ReceiptIdListMap[input.RecorderId] = receiptIdList;
            State.ReceiptCountMap[input.RecorderId] = State.ReceiptIdListMap[input.RecorderId].ReceiptId.Count;
        }

        return new Empty();
    }

    public override Empty CreateReceiptMax(CreateReceiptInput input)
    {
        var receiptList = new ReceiptList();
        for (int i = 0; i < 1024; i++)
        {
            var receipt = new Receipt
            {
                Sender = Context.Sender,
                TargetAddress = new Address(),
                Amount = i + 1
            };
            receiptList.Value.Add(receipt);
            State.ReceiptList.Value = State.ReceiptList.Value ?? new ReceiptList();
            State.ReceiptList.Value.Value.Add(receipt);
            var receiptId = State.ReceiptList.Value.Value.Count - 1;
            State.RecorderReceiptHashMap[input.RecorderId][receiptId] = HashHelper.ComputeFrom(receipt);
            var receiptIdList = State.ReceiptIdListMap[input.RecorderId] ?? new ReceiptIdArray();
            receiptIdList.ReceiptId.Add(receiptId);
            State.ReceiptIdListMap[input.RecorderId] = receiptIdList;
            State.ReceiptCountMap[input.RecorderId] = State.ReceiptIdListMap[input.RecorderId].ReceiptId.Count;
        }

        return new Empty();
    }

    public override Empty CreateReceiptDiy(CreateReceiptDiyInput input)
    {
        var count = input.ReceiptHash.Value.Count;
        for (int i = 0; i < count; i++)
        {
            State.RecorderReceiptHashMap[input.RecorderId][i] = input.ReceiptHash.Value[i];
            var receiptIdList = State.ReceiptIdListMap[input.RecorderId] ?? new ReceiptIdArray();
            receiptIdList.ReceiptId.Add(i);
            State.ReceiptIdListMap[input.RecorderId] = receiptIdList;
            State.ReceiptCountMap[input.RecorderId] = State.ReceiptIdListMap[input.RecorderId].ReceiptId.Count;
        }

        return new Empty();
    }

    public override Int64Value GetReceiptCount(Hash input)
    {
        return new Int64Value {Value = State.ReceiptCountMap[input]};
    }

    public override Hash GetReceiptHash(GetReceiptHashInput input)
    {
        return State.RecorderReceiptHashMap[input.SpaceId][input.ReceiptIndex];
    }

    public override GetReceiptHashListOutput GetReceiptHashList(GetReceiptHashListInput input)
    {
        var output = new GetReceiptHashListOutput();
        for (var i = input.FirstLeafIndex; i <= input.LastLeafIndex; i++)
        {
            var receiptHash = GetReceiptHash(new GetReceiptHashInput
            {
                SpaceId = input.SpaceId,
                ReceiptIndex = i
            });
            Assert(receiptHash != null, $"Receipt hash of {i} is null.");
            output.ReceiptHashList.Add(receiptHash);
        }

        return output;
    }
}