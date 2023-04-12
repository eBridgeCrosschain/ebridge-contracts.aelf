using System.Collections.Generic;
using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using EBridge.Contracts.ReceiptMakerContract;
using SmartContractBridgeContextExtensions = AElf.Sdk.CSharp.SmartContractBridgeContextExtensions;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContract
{
    private List<Hash> GetReceiptHashList(Address receiptMaker, long firstLeafIndex, long lastLeafIndex,
        Hash spaceId)
    {
        var receiptHashList = Context.Call<GetReceiptHashListOutput>(receiptMaker,
            nameof(ReceiptMakerContractContainer.ReceiptMakerContractReferenceState.GetReceiptHashList),
            new GetReceiptHashListInput
            {
                FirstLeafIndex = firstLeafIndex,
                LastLeafIndex = lastLeafIndex,
                SpaceId = spaceId
            });

        return receiptHashList.ReceiptHashList.ToList();
    }

    private void GenerateNewMerkleTree(List<Hash> leafNodeList, Hash spaceId, SpaceInfo spaceInfo, long lastTreeIndex)
    {
        var merkleTreeList = ConstructNewMerkleTree(leafNodeList, spaceId, spaceInfo, lastTreeIndex, out var nodeList);

        foreach (var merkleTree in merkleTreeList.Value)
        {
            State.SpaceMerkleTreeIndex[spaceId][merkleTree.MerkleTreeIndex] = merkleTree;
            if (merkleTree.IsFullTree)
            {
                State.FullMerkleTreeCountMap[spaceId] += 1;
            }
            else
            {
                State.NotFullTreeNodeList[spaceId][merkleTree.MerkleTreeIndex] = new HashList {Value = {nodeList}};
            }

            SmartContractBridgeContextExtensions.Fire(Context, new MerkleTreeRecorded
            {
                SpaceId = spaceId,
                MerkleTreeIndex = merkleTree.MerkleTreeIndex,
                LastLeafIndex = merkleTree.LastLeafIndex,
                RegimentMemberAddress = Context.Sender
            });
            State.LastRecordedMerkleTreeIndex[spaceId] = merkleTree.MerkleTreeIndex;
            State.LastRecordedLeafIndex[spaceId] =
                State.SpaceMerkleTreeIndex[spaceId][merkleTree.MerkleTreeIndex].LastLeafIndex;
        }
    }

    private MerkleTreeList ConstructNewMerkleTree(List<Hash> leafNodeList, Hash spaceId, SpaceInfo spaceInfo,
        long lastTreeIndex, out List<Hash> notFullNodeList)
    {
        notFullNodeList = new List<Hash>();
        var i = 0;
        var groupLeafNode = leafNodeList
            .GroupBy(s => i++ / spaceInfo.MaxLeafCount)
            .Select(a => a.ToList())
            .ToList();
        var merkleTreeList = new MerkleTreeList();
        long merkleTreeIndex;
        if (lastTreeIndex == -2)
        {
            merkleTreeIndex = 0;
        }
        else
        {
            merkleTreeIndex = State.SpaceMerkleTreeIndex[spaceId][lastTreeIndex].IsFullTree
                ? lastTreeIndex + 1
                : lastTreeIndex;
        }
        var lastLeafIndex = State.LastRecordedLeafIndex[spaceId];
        foreach (var nodeList in groupLeafNode)
        {
            var firstLeafIndex = lastLeafIndex == -2
                ? 0
                : lastLeafIndex.Add(1);
            lastLeafIndex = firstLeafIndex.Add(nodeList.Count).Sub(1);
            var binaryMerkleTree = GenerateMerkleTree(nodeList);
            var isFull = nodeList.Count == spaceInfo.MaxLeafCount;
            var newTree = new MerkleTree
            {
                SpaceId = spaceId,
                MerkleTreeIndex = merkleTreeIndex,
                FirstLeafIndex = firstLeafIndex,
                LastLeafIndex = lastLeafIndex,
                MerkleTreeRoot = binaryMerkleTree.Root,
                IsFullTree = isFull
            };
            merkleTreeList.Value.Add(newTree);
            lastTreeIndex += 1;
            if (!isFull)
            {
                notFullNodeList = nodeList;
            }
            merkleTreeIndex = merkleTreeIndex.Add(1);
        }

        return merkleTreeList;
    }

    private BinaryMerkleTree GenerateMerkleTree(List<Hash> leafNodeHash)
    {
        var binaryMerkleTree = BinaryMerkleTree.FromLeafNodes(leafNodeHash);
        return binaryMerkleTree;
    }

    private void UpdateMerkleTree(Hash spaceId, long merkleTreeIndex, List<Hash> nodelist)
    {
        var tree = UpdateConstructMerkleTree(spaceId, merkleTreeIndex, nodelist);
        State.SpaceMerkleTreeIndex[spaceId][merkleTreeIndex] = tree;
        State.LastRecordedLeafIndex[spaceId] = tree.LastLeafIndex;
        if (tree.IsFullTree)
        {
            State.NotFullTreeNodeList[spaceId].Remove(merkleTreeIndex);
        }
        else
        {
            State.NotFullTreeNodeList[spaceId][merkleTreeIndex].Value.Add(nodelist);
        }

        State.LastRecordedMerkleTreeIndex[spaceId] = tree.MerkleTreeIndex;
        SmartContractBridgeContextExtensions.Fire(Context, new MerkleTreeRecorded
        {
            SpaceId = spaceId,
            MerkleTreeIndex = merkleTreeIndex,
            LastLeafIndex = tree.LastLeafIndex,
            RegimentMemberAddress = Context.Sender
        });
    }

    private MerkleTree UpdateConstructMerkleTree(Hash spaceId, long merkleTreeIndex, List<Hash> nodelist)
    {
        var leafNode = State.NotFullTreeNodeList[spaceId][merkleTreeIndex].Clone();
        leafNode.Value.AddRange(nodelist);
        var binaryTree = GenerateMerkleTree(leafNode.Value.ToList());
        var tree = State.SpaceMerkleTreeIndex[spaceId][merkleTreeIndex];
        tree.MerkleTreeRoot = binaryTree.Root;
        tree.LastLeafIndex = tree.FirstLeafIndex.Add(leafNode.Value.Count).Sub(1);
        tree.IsFullTree = leafNode.Value.Count == State.SpaceInfoMap[spaceId].MaxLeafCount;
        return tree;
    }
}