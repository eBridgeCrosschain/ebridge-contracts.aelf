using System.Collections.Generic;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using EBridge.Contracts.Bridge.SmartContractNameProviders;
using Volo.Abp.DependencyInjection;

namespace EBridge.Contracts.Bridge.ContractInitializationProvider
{
    public class OracleContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = OracleSmartContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "EBridge.Contracts.Oracle";
    }
}