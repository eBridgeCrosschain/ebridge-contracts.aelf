using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProviders
{
    public class RegimentSmartContractAddressNameProvider
    {
        public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.Regiment");

        public static readonly string StringName = Name.ToStorageKey();
        public Hash ContractName => Name;
        public string ContractStringName => StringName;
    }
}

