using System.Collections.Generic;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using EBridge.Contracts.MerkleTreeContract.SmartContractNameProviders;
using Volo.Abp.DependencyInjection;

namespace EBridge.Contracts.MerkleTreeContract.ContractInitializationProvider;

public class RegimentContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
{
    public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
    {
        return new List<ContractInitializationMethodCall>();
    }

    public Hash SystemSmartContractName => RegimentSmartContractAddressNameProvider.Name;
    public string ContractCodeName => "EBridge.Contracts.Regiment";
}