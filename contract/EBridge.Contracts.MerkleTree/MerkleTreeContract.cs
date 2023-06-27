using System;
using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContract : MerkleTreeContractContainer.MerkleTreeContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(State.Owner.Value == null, $"Already initialized.");
        Assert(State.IsInitialized.Value == false,"Already initialized.");
        // State.GensisContract.Value = Context.GetZeroSmartContractAddress();
        // var author = State.GensisContract.GetContractAuthor.Call(Context.Self);
        // Assert(Context.Sender == author, "No permission.");
        State.IsInitialized.Value = true;
        State.Owner.Value = input.Owner;
        State.RegimentContract.Value = input.RegimentContractAddress;
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        return new Empty();
    }

    public override Empty ChangeOwner(Address input)
    {
        Assert(Context.Sender == State.Owner.Value, "No permission.");
        State.Owner.Value = input;
        return new Empty();
    }


    public override Empty CreateSpace(CreateSpaceInput input)
    {
        Assert(input.Value.Operator != null, "Not set regiment address.");
        Assert(input.Value.MaxLeafCount > 0 && input.Value.MaxLeafCount < 2 << 20, $"Incorrect leaf count.{input.Value.MaxLeafCount}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(input.Value.Operator);
        Assert(!regimentAddress.Value.IsEmpty, "Regiment Address not exist.");
        var regimentInfo = State.RegimentContract.GetRegimentInfo.Call(regimentAddress);
        Assert(regimentInfo != null, "Regiment Info not exist.");
        Assert(regimentInfo.Admins.Contains(Context.Sender), "No permission.");
        var id = State.RegimentSpaceIndexMap[input.Value.Operator].Add(1);
        var spaceId =
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.Value.Operator), HashHelper.ComputeFrom(id));
        if (State.SpaceInfoMap[spaceId] != null)
        {
            spaceId = NextSpaceId(spaceId, id, input.Value.Operator);
        }
        Assert(State.SpaceInfoMap[spaceId] == null, "spaceId existed.");
        
        State.SpaceInfoMap[spaceId] = input.Value;
        State.RegimentSpaceIndexMap[input.Value.Operator] += 1;
        State.LastRecordedLeafIndex[spaceId] = -2;
        State.LastRecordedMerkleTreeIndex[spaceId] = -2;
        Context.Fire(new SpaceCreated
        {
            SpaceId = spaceId,
            RegimentId = input.Value.Operator,
            SpaceInfo = input.Value
        });
        return new Empty();
    }

    private Hash NextSpaceId(Hash spaceId, long id, Hash op)
    {
        long baseId = long.MaxValue >> 4;
        for (int i = 1; i <= 10; i++)
        {
            long nextId = baseId + 16 * (id - 1) + i;
            spaceId =
                HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(op), HashHelper.ComputeFrom(nextId));
            if (State.SpaceInfoMap[spaceId] == null)
            {
                break;
            }
        }
        return spaceId;
    }

    public override Empty RecordMerkleTree(RecordMerkleTreeInput input)
    {
        var spaceInfo = State.SpaceInfoMap[input.SpaceId];
        Assert(spaceInfo != null, $"Incorrect space id.{input.SpaceId}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(spaceInfo.Operator);
        var memberList = State.RegimentContract.GetRegimentMemberList.Call(regimentAddress);
        Assert(memberList.Value.Contains(Context.Sender), "No permission.");
        var lastTreeIndex = State.LastRecordedMerkleTreeIndex[input.SpaceId];
        var lastLeafIndex = State.LastRecordedLeafIndex[input.SpaceId];
        var lastTreeIsFull = true;
        if (lastTreeIndex != -2 && lastLeafIndex != -2)
        {
            lastTreeIsFull = State.SpaceMerkleTreeIndex[input.SpaceId][lastTreeIndex].IsFullTree;
        }

        var leafNodeList = input.LeafNodeHash.ToList();
        if (!lastTreeIsFull)
        {
            var oldTreeIndex = State.LastRecordedMerkleTreeIndex[input.SpaceId];
            var nodeCount =
                (int) spaceInfo.MaxLeafCount.Sub(State.NotFullTreeNodeList[input.SpaceId][oldTreeIndex].Value.Count);
            var updateNodeHashList = leafNodeList.GetRange(0, Math.Min(nodeCount, leafNodeList.Count));
            UpdateMerkleTree(input.SpaceId, oldTreeIndex, updateNodeHashList);
            if (nodeCount >= input.LeafNodeHash.Count) return new Empty();
            leafNodeList = leafNodeList.GetRange(nodeCount, leafNodeList.Count.Sub(nodeCount));
        }

        GenerateNewMerkleTree(leafNodeList, input.SpaceId, spaceInfo, lastTreeIndex);
        return new Empty();
    }
}