using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace EBridge.Contracts.TokenPool.ContractInitializationProvider;

public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider,
    IContractDeploymentListProvider
{
    public new List<Hash> GetDeployContractNameList()
    {
        var list = base.GetDeployContractNameList();
        list.Add(RegimentSmartContractAddressNameProvider.Name);
        list.Add(ReceiptMakerSmartContractAddressNameProvider.Name);
        list.Add(StringAggregatorSmartContractAddressNameProvider.Name);
        return list;
    }
}