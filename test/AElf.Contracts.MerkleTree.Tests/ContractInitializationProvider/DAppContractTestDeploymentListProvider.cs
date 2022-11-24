using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace AElf.Contracts.MerkleTreeContract;

public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider,
    IContractDeploymentListProvider
{
    public new List<Hash> GetDeployContractNameList()
    {
        var list = base.GetDeployContractNameList();
        list.Add(MerkleTreeSmartContractAddressNameProvider.Name);
        list.Add(RegimentSmartContractAddressNameProvider.Name);
        list.Add(ReceiptMakerSmartContractAddressNameProvider.Name);
        return list;
    }
}