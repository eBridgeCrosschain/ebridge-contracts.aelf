using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContract
{
    public override Address GetContractOwner(Empty input)
    {
        return State.Owner.Value;
    }

    public override MerkleTreeList ConstructMerkleTree(ConstructMerkleTreeInput input)
    {
        var spaceInfo = State.SpaceInfoMap[input.SpaceId];
        Assert(spaceInfo != null,$"Incorrect space id.{input.SpaceId}.");
        var lastTreeIndex = State.LastRecordedMerkleTreeIndex[input.SpaceId];
        var lastLeafIndex = State.LastRecordedLeafIndex[input.SpaceId];
        var spaceIsEmpty = lastLeafIndex == -2;
        var lastTreeIsFull = true;
        var merkleTreeList = new MerkleTreeList();
        if (!spaceIsEmpty)
        {
            lastTreeIsFull = State.SpaceMerkleTreeIndex[input.SpaceId][lastTreeIndex].IsFullTree;
        }

        var leafNodeList = input.LeafNodeHash.ToList();
        if (!lastTreeIsFull)
        {
            var oldTreeIndex = State.LastRecordedMerkleTreeIndex[input.SpaceId];
            var nodeCount = (int) (input.LeafNodeHash.Count > (spaceInfo.MaxLeafCount - oldTreeIndex)
                ? spaceInfo.MaxLeafCount - oldTreeIndex
                : input.LeafNodeHash.Count);
            var updateNodeHashList = leafNodeList.GetRange(0, nodeCount);
            merkleTreeList.Value.Add(UpdateConstructMerkleTree(input.SpaceId, oldTreeIndex, updateNodeHashList));
            if (nodeCount >= input.LeafNodeHash.Count) return merkleTreeList;
            var remainNodeList = leafNodeList.GetRange(nodeCount, leafNodeList.Count.Sub(nodeCount));
            merkleTreeList.Value.AddRange(ConstructNewMerkleTree(remainNodeList, input.SpaceId, spaceInfo,
                lastTreeIndex, out var nodeList).Value);
        }
        else
        {
            merkleTreeList =
                ConstructNewMerkleTree(leafNodeList, input.SpaceId, spaceInfo, lastTreeIndex, out var nodeList);
        }

        return merkleTreeList;
    }

    public override MerklePath GetMerklePath(GetMerklePathInput input)
    {
        var spaceInfo = State.SpaceInfoMap[input.SpaceId];
        Assert(State.LastRecordedLeafIndex[input.SpaceId] >= 0 ,$"No leaf in the space.{input.SpaceId}");
        Assert(input.LeafNodeIndex <= State.LastRecordedLeafIndex[input.SpaceId],
            $"Incorrect leaf index.Last leaf index:{State.LastRecordedLeafIndex[input.SpaceId]}");
        var merkleTreeIndex = input.LeafNodeIndex / spaceInfo.MaxLeafCount;
        var merkleTree = State.SpaceMerkleTreeIndex[input.SpaceId][merkleTreeIndex];
        var receiptHashList = GetReceiptHashList(input.ReceiptMaker, merkleTree.FirstLeafIndex,
            merkleTree.LastLeafIndex, input.SpaceId);
        var binaryMerkleTree = GenerateMerkleTree(receiptHashList);
        var path = binaryMerkleTree.GenerateMerklePath((int) ((int) input.LeafNodeIndex % spaceInfo.MaxLeafCount));
        return path;
    }

    public override BoolValue MerkleProof(MerkleProofInput input)
    {
        var spaceInfo = State.SpaceInfoMap[input.SpaceId];
        Assert(State.LastRecordedLeafIndex[input.SpaceId] >= 0 ,$"No leaf in the space.{input.SpaceId}");
        Assert(input.LastLeafIndex <= State.LastRecordedLeafIndex[input.SpaceId],
            $"Incorrect last leaf index. Last leaf index:{State.LastRecordedLeafIndex[input.SpaceId]}");
        var merkleTreeIndex = input.LastLeafIndex / spaceInfo.MaxLeafCount;
        var merkleTree = State.SpaceMerkleTreeIndex[input.SpaceId][merkleTreeIndex];
        var root = input.MerklePath.ComputeRootWithLeafNode(input.LeafNode);
        return new BoolValue
        {
            Value = merkleTree.MerkleTreeRoot == root
        };
    }

    public override Int64Value GetRegimentSpaceCount(Hash input)
    {
        return new Int64Value
        {
            Value = State.RegimentSpaceIndexMap[input]
        };
    }
    
    public override SpaceInfo GetSpaceInfo(Hash input)
    {
        return State.SpaceInfoMap[input];
    }

    public override Int64Value GetMerkleTreeCountBySpace(Hash input)
    {
        return new Int64Value
        {
            Value = State.LastRecordedMerkleTreeIndex[input] == -2 ? 0 : State.LastRecordedMerkleTreeIndex[input].Add(1)
        };
    }

    public override MerkleTree GetMerkleTreeByIndex(GetMerkleTreeByIndexInput input)
    {
        return State.SpaceMerkleTreeIndex[input.SpaceId][input.MerkleTreeIndex];
    }

    public override Int64Value GetLastLeafIndex(GetLastLeafIndexInput input)
    {
        var result = State.SpaceInfoMap[input.SpaceId] == null ? -1 : State.LastRecordedLeafIndex[input.SpaceId];
        return new Int64Value
        {
            Value = result
        };
    }

    public override Int64Value GetFullTreeCount(Hash input)
    {
        return new Int64Value
        {
            Value = State.FullMerkleTreeCountMap[input]
        };
    }

    public override Int64Value GetRemainLeafCount(Hash input)
    {
        var lastLeafIndex = State.LastRecordedLeafIndex[input] == -2 ? 0 : State.LastRecordedLeafIndex[input]+1;
        var merkleTreeCount = GetMerkleTreeCountBySpace(input).Value == 0 ? 1 : GetMerkleTreeCountBySpace(input).Value;
        var maxLeafCount = merkleTreeCount.Mul(GetSpaceInfo(input).MaxLeafCount);
        var remainCount = maxLeafCount.Sub(lastLeafIndex);
        return new Int64Value
        {
            Value = remainCount
        };
    }

    public override GetLeafLocatedMerkleTreeOutput GetLeafLocatedMerkleTree(GetLeafLocatedMerkleTreeInput input)
    {
        var spaceInfo = State.SpaceInfoMap[input.SpaceId];
        var merkleTreeIndex = input.LeafIndex / spaceInfo.MaxLeafCount;
        var merkleTree = State.SpaceMerkleTreeIndex[input.SpaceId][merkleTreeIndex];
        return new GetLeafLocatedMerkleTreeOutput
        {
            SpaceId = input.SpaceId,
            FirstLeafIndex = merkleTree.FirstLeafIndex,
            LastLeafIndex = merkleTree.LastLeafIndex,
            MerkleTreeIndex = merkleTreeIndex
        };
    }

    public override Int64Value GetLastMerkleTreeIndex(Hash input)
    {
        return new Int64Value
        {
            Value = State.LastRecordedMerkleTreeIndex[input]
        };
    }
}