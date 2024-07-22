using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProviders;

public class TokenPoolSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.TokenPool");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}