using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace EBridge.Contracts.MerkleTreeContract.ContractInitializationProvider;

public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider,
    IContractDeploymentListProvider
{
    public new List<Hash> GetDeployContractNameList()
    {
        var list = base.GetDeployContractNameList();
        list.Add(ReceiptMakerSmartContractAddressNameProvider.Name);
        return list;
    }
}