using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace AElf.Boilerplate.TestBase
{
    public class SideChainDAppContractTestDeploymentListProvider : SideChainContractDeploymentListProvider, IContractDeploymentListProvider
    {
        public List<Hash> GetDeployContractNameList()
        {
            var list = base.GetDeployContractNameList();
            list.Add(DAppSmartContractAddressNameProvider.Name);
            list.Add(MerkleTreeSmartContractAddressNameProvider.Name);
            list.Add(RegimentSmartContractAddressNameProvider.Name);
            list.Add(ReceiptMakerSmartContractAddressNameProvider.Name);
            list.Add(BridgeSmartContractAddressNameProvider.Name);
            list.Add(OracleSmartContractAddressNameProvider.Name);
            list.Add(ReportSmartContractAddressNameProvider.Name);
            return list;
        }
    }
    
    public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider, IContractDeploymentListProvider
    {
        public List<Hash> GetDeployContractNameList()
        {
            var list = base.GetDeployContractNameList();
            list.Add(DAppSmartContractAddressNameProvider.Name);
            list.Add(MerkleTreeSmartContractAddressNameProvider.Name);
            list.Add(RegimentSmartContractAddressNameProvider.Name);
            list.Add(ReceiptMakerSmartContractAddressNameProvider.Name);
            list.Add(BridgeSmartContractAddressNameProvider.Name);
            list.Add(OracleSmartContractAddressNameProvider.Name);
            list.Add(ReportSmartContractAddressNameProvider.Name);
            return list;
        }
    }
}