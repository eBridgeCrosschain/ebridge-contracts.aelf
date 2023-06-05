using AElf;
using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace EBridge.Contracts.MerkleTreeContract.SmartContractNameProviders;

public class StringAggregatorSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.StringAggregator");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}