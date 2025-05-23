using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProviders;

public class RampContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("AetherLink.Contracts.Ramp");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}