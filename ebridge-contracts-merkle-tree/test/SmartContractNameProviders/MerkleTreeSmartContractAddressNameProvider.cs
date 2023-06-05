using AElf;
using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace EBridge.Contracts.MerkleTreeContract.SmartContractNameProviders
{
    public class MerkleTreeSmartContractAddressNameProvider
    {
        public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.MerkleTree");

        public static readonly string StringName = Name.ToStorageKey();
        public Hash ContractName => Name;
        public string ContractStringName => StringName;
    }
    
}

