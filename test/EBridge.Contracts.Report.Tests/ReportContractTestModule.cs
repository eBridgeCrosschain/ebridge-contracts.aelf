using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.ContractTestKit;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using EBridge.Contracts.Bridge.ContractInitializationProvider;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.StringAggregator;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using MainChainDAppContractTestDeploymentListProvider = EBridge.Contracts.Report.ContractInitializationProvider.MainChainDAppContractTestDeploymentListProvider;
using OracleContractInitializationProvider = EBridge.Contracts.Report.ContractInitializationProvider.OracleContractInitializationProvider;
using RegimentContractInitializationProvider = EBridge.Contracts.Report.ContractInitializationProvider.RegimentContractInitializationProvider;
using ReportContractInitializationProvider = EBridge.Contracts.Report.ContractInitializationProvider.ReportContractInitializationProvider;

namespace EBridge.Contracts.Report;

[DependsOn(typeof(MainChainDAppContractTestModule))]
public class ReportContractTestModule : MainChainDAppContractTestModule
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
                new ReportContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(EBridge.Contracts.Report.ReportContract).Assembly.Location)
            },
            {
                new RegimentContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(RegimentContract).Assembly.Location)
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