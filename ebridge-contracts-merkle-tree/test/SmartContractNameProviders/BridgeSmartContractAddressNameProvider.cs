using AElf;
using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace EBridge.Contracts.MerkleTreeContract.SmartContractNameProviders;

public class BridgeSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.Bridge");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}