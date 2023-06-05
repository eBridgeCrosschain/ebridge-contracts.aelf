using System.Collections.Generic;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using EBridge.Contracts.MerkleTreeContract.SmartContractNameProviders;

namespace EBridge.Contracts.MerkleTreeContract.ContractInitializationProvider;

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