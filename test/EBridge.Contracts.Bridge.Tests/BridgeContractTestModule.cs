using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using EBridge.Contracts.Bridge.ContractInitializationProvider;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.StringAggregator;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using MainChainDAppContractTestDeploymentListProvider = EBridge.Contracts.Bridge.ContractInitializationProvider.MainChainDAppContractTestDeploymentListProvider;
using ReportContract = EBridge.Contracts.Report.ReportContract;

namespace EBridge.Contracts.Bridge;

[DependsOn(typeof(MainChainDAppContractTestModule))]
public class BridgeContractTestModule : MainChainDAppContractTestModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IBlockTimeProvider, BlockTimeProvider>();
        context.Services
            .AddSingleton<IContractDeploymentListProvider, MainChainDAppContractTestDeploymentListProvider>();
        Configure<ContractOptions>(o => o.ContractDeploymentAuthorityRequired = false);
    }

    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        var contractCodeProvider = context.ServiceProvider.GetService<IContractCodeProvider>() ??
                                   new ContractCodeProvider();
        var contractCodes = new Dictionary<string, byte[]>(contractCodeProvider.Codes)
        {
            {
                new BridgeContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(EBridge.Contracts.Bridge.BridgeContract).Assembly.Location)
            },
            {
                new ReportContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(ReportContract).Assembly.Location)
            },
            {
                new MerkleTreeContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(EBridge.Contracts.MerkleTreeContract.MerkleTreeContract).Assembly.Location)
            },
            {
                new RegimentContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(RegimentContract).Assembly.Location)
            },
            {
                new ReceiptMakerContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(TestContract.ReceiptMaker.ReceiptMakerContract).Assembly.Location)
            },
            {
                new OracleContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(OracleContract).Assembly.Location)
            },
            {
                new StringAggregatorContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(StringAggregatorContract).Assembly.Location)
            }
        };
        contractCodeProvider.Codes = contractCodes;
    }
}