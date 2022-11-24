using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.Bridge;
using AElf.Contracts.MerkleTreeContract;
using AElf.Contracts.Oracle;
using AElf.Contracts.Regiment;
using AElf.Contracts.StringAggregator;
using AElf.ContractTestBase;
using AElf.ContractTestKit;
using AElf.Kernel.SmartContract.Application;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace AElf.Contracts.Report;


[DependsOn(typeof(MainChainDAppContractTestModule))]
public class ReportContractTestModule : MainChainDAppContractTestModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IBlockTimeProvider, BlockTimeProvider>();
        context.Services
            .AddSingleton<IContractDeploymentListProvider, MainChainDAppContractTestDeploymentListProvider>();
    }

    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        var contractCodeProvider = context.ServiceProvider.GetService<IContractCodeProvider>() ??
                                   new ContractCodeProvider();
        var contractCodes = new Dictionary<string, byte[]>(contractCodeProvider.Codes)
        {
            {
                new ReportContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(ReportContract).Assembly.Location)
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