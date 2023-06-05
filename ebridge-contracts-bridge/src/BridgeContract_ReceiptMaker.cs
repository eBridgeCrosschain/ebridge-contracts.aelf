using System.Linq;
using AElf.CSharp.Core;
using AElf.Types;
using EBridge.Contracts.MerkleTreeContract;
using EBridge.Contracts.ReceiptMakerContract;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Empty RecordReceiptHash(CallbackInput input)
    {
        Assert(!State.IsContractPause.Value, "Contract is paused.");
        Assert(Context.Sender == State.OracleContract.Value, "No permission.");
        var queryResult = new StringValue();
        queryResult.MergeFrom(input.Result);
        var receiptHashMap = JsonParser.Default.Parse<ReceiptHashMap>(queryResult.Value);
        Assert(!string.IsNullOrEmpty(receiptHashMap.SwapId), "Swap id is null.");
        var swapId = Hash.LoadFromHex(receiptHashMap.SwapId);
        var spaceId = GetSpaceIdBySwapId(swapId);
        Assert(spaceId != null, $"Space id is null.SwapId : {swapId}");

        foreach (var (receiptId, receiptHash) in receiptHashMap.Value)
        {
            var treeIndex = State.RecordedTreeLeafIndex[spaceId].Add(1);
            Assert(TryGetReceiptIndex(receiptId, out var receiptIndex), "Incorrect receipt index.");
            Assert(receiptIndex == treeIndex,
                $"Incorrect receipt index. Current leaf index: {treeIndex}, Receive receipt index: {receiptIndex}");
            State.RecorderReceiptHashMap[spaceId][receiptIndex.Sub(1)] = Hash.LoadFromHex(receiptHash);
            State.RecordedTreeLeafIndex[spaceId] += 1;
            State.ApproveTransfer[receiptId] = false;
        }

        //Get received first and last receipt index.
        Assert(TryGetReceiptIndex(receiptHashMap.Value.Last().Key, out var lastIndex),
            "Incorrect receipt index.");
        Assert(TryGetReceiptIndex(receiptHashMap.Value.First().Key, out var firstIndex),
            "Incorrect receipt index.");
        State.SpaceReceiptCountMap[spaceId] = lastIndex;

        //Receipt index start with 1, leaf index start with 0.
        var leafNodeList = GetReceiptHashList(new GetReceiptHashListInput
        {
            SpaceId = spaceId,
            FirstLeafIndex = firstIndex.Sub(1),
            LastLeafIndex = lastIndex.Sub(1)
        }).ReceiptHashList;

        State.MerkleTreeContract.RecordMerkleTree.Send(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash = {leafNodeList}
        });

        return new Empty();
    }

    #region View

    public override Int64Value GetReceiptCount(Hash input)
    {
        return new Int64Value {Value = State.SpaceReceiptCountMap[input]};
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

    #endregion
}