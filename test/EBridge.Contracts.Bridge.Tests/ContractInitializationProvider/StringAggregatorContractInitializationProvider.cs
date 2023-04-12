using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace EBridge.Contracts.Bridge.ContractInitializationProvider
{
    public class StringAggregatorContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = StringAggregatorSmartContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "EBridge.Contracts.StringAggregator";
    }
}