using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace EBridge.Contracts.MerkleTreeContract.ContractInitializationProvider;

public class ReceiptMakerContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
{
    public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
    {
        return new List<ContractInitializationMethodCall>();
    }

    public Hash SystemSmartContractName => ReceiptMakerSmartContractAddressNameProvider.Name;
    public string ContractCodeName => "EBridge.Contracts.TestContract.ReceiptMaker";
}