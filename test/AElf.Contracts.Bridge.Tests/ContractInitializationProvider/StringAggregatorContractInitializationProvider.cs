using System.Collections.Generic;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace AElf.Contracts.Bridge
{
    public class StringAggregatorContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = StringAggregatorSmartContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "AElf.Contracts.StringAggregator";
    }
}