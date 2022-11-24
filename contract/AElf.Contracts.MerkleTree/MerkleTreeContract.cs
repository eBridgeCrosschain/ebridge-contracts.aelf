using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.MerkleTreeContract;

public partial class MerkleTreeContract : MerkleTreeContractContainer.MerkleTreeContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(State.Owner.Value == null, $"Already initialized.");
        State.Owner.Value = input.Owner;
        State.RegimentContract.Value = input.RegimentContractAddress;
        return new Empty();
    }

    public override Empty ChangeOwner(Address input)
    {
        Assert(Context.Sender == State.Owner.Value,"No permission.");
        State.Owner.Value = input;
        return new Empty();
    }


    public override Empty CreateSpace(CreateSpaceInput input)
    {
        Assert(input.Value.Operators != null, "Not set regiment address.");
        Assert(input.Value.MaxLeafCount > 0 ,$"Incorrect leaf count.{input.Value.MaxLeafCount}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(input.Value.Operators);
        if (regimentAddress.Value.IsEmpty)
        {
            throw new AssertionException("Regiment Address not exist.");
        }
        var regimentInfo = State.RegimentContract.GetRegimentInfo.Call(regimentAddress);
        if (regimentInfo == null)
        {
            throw new AssertionException("Regiment Info not exist.");
        }

        Assert(regimentInfo.Admins.Contains(Context.Sender), "No permission.");
        var id = State.RegimentSpaceIndexMap[input.Value.Operators].Add(1);
        var spaceId =
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.Value.Operators), HashHelper.ComputeFrom(id));
        State.SpaceInfoMap[spaceId] = input.Value;
        var spaceIdList = State.RegimentSpaceIdListMap[input.Value.Operators] ?? new HashList();
        spaceIdList.Value.Add(spaceId);
        State.RegimentSpaceIdListMap[input.Value.Operators] = spaceIdList;
        State.RegimentSpaceIndexMap[input.Value.Operators] += 1;
        State.LastRecordedLeafIndex[spaceId] = -2;
        State.LastRecordedMerkleTreeIndex[spaceId] = -2;
        Context.Fire(new SpaceCreated
        {
            SpaceId = spaceId,
            RegimentId = input.Value.Operators,
            SpaceInfo = input.Value
        });
        return new Empty();
    }

    public override Empty RecordMerkleTree(RecordMerkleTreeInput input)
    {
        var spaceInfo = State.SpaceInfoMap[input.SpaceId];
        if (spaceInfo == null)
        {
            throw new AssertionException($"Incorrect space id.{input.SpaceId}");
        }

        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(spaceInfo.Operators);
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
            var updateNodeHashList = leafNodeList.GetRange(0, Math.Min(nodeCount,leafNodeList.Count));
            UpdateMerkleTree(input.SpaceId, oldTreeIndex, updateNodeHashList);
            if (nodeCount >= input.LeafNodeHash.Count) return new Empty();
            var remainNodeList = leafNodeList.GetRange(nodeCount, leafNodeList.Count.Sub(nodeCount));
            GenerateNewMerkleTree(remainNodeList, input.SpaceId, spaceInfo, lastTreeIndex);
        }
        else
        {
            GenerateNewMerkleTree(leafNodeList, input.SpaceId, spaceInfo, lastTreeIndex);
        }

        return new Empty();
    }
}