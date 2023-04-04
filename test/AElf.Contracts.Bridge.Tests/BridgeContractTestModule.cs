using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.MerkleTreeContract;
using AElf.Contracts.Oracle;
using AElf.Contracts.Regiment;
using AElf.Contracts.Report;
using AElf.Contracts.StringAggregator;
using AElf.ContractTestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.SmartContract.Application;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace AElf.Contracts.Bridge;

[DependsOn(typeof(MainChainDAppContractTestModule))]
public class BridgeContractTestModule : MainChainDAppContractTestModule
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
                new BridgeContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(BridgeContract).Assembly.Location)
            },
            {
                new ReportContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(ReportContract).Assembly.Location)
            },
            {
                new MerkleTreeContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(MerkleTreeContract.MerkleTreeContract).Assembly.Location)
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