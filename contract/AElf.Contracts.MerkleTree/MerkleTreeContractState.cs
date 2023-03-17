using System.Collections.Generic;
using AElf.Sdk.CSharp.State;
using AElf.Standards.ACS1;
using AElf.Types;

namespace AElf.Contracts.MerkleTreeContract;

public partial class MerkleTreeContractState : ContractState
{
    public SingletonState<Address> Owner { get; set; }
    public MappedState<string, MethodFees> TransactionFees { get; set; }
    public SingletonState<AuthorityInfo> MethodFeeController { get; set; }

    /// <summary>
    /// regiment id -> space count
    /// </summary>
    public MappedState<Hash, long> RegimentSpaceIndexMap { get; set; }

    /// <summary>
    /// regiment id -> space id list
    /// </summary>
    //public MappedState<Hash, HashList> RegimentSpaceIdListMap { get; set; }

    /// <summary>
    /// Space id -> SpaceInfo
    /// </summary>
    public MappedState<Hash, SpaceInfo> SpaceInfoMap { get; set; }

    /// <summary>
    /// Space id -> full tree count
    /// </summary>
    public MappedState<Hash, long> FullMerkleTreeCountMap { get; set; }

    /// <summary>
    /// Space id -> MerkleTree index -> MerkleTree
    /// </summary>
    public MappedState<Hash, long, MerkleTree> SpaceMerkleTreeIndex { get; set; }


    /// <summary>
    /// Space id -> last recorded merkleTree index
    /// </summary>
    public MappedState<Hash, long> LastRecordedMerkleTreeIndex { get; set; }

    /// <summary>
    /// Space id -> last recorded leaf index
    /// </summary>
    public MappedState<Hash, long> LastRecordedLeafIndex { get; set; }

    /// <summary>
    /// Space id -> MerkleTree index -> leaf node list
    /// </summary>
    public MappedState<Hash, long, HashList> NotFullTreeNodeList { get; set; }
}