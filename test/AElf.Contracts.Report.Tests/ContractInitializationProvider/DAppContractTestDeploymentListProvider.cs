using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace AElf.Contracts.Report;

public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider,
    IContractDeploymentListProvider
{
    public new List<Hash> GetDeployContractNameList()
    {
        var list = base.GetDeployContractNameList();
        list.Add(RegimentSmartContractAddressNameProvider.Name);
        list.Add(ReportSmartContractAddressNameProvider.Name);
        list.Add(OracleSmartContractAddressNameProvider.Name);
        list.Add(RegimentSmartContractAddressNameProvider.Name);
        return list;
    }
}