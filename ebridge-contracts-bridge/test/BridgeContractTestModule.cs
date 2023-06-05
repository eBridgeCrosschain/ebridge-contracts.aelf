using System.Collections.Generic;
using System.IO;
using AElf.ContractTestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.SmartContract.Application;
using AElf.Testing.TestBase;
using EBridge.Contracts.Bridge.ContractInitializationProvider;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.StringAggregator;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using ContractCodeProvider = AElf.Testing.TestBase.ContractCodeProvider;
using MainChainDAppContractTestDeploymentListProvider = EBridge.Contracts.Bridge.ContractInitializationProvider.MainChainDAppContractTestDeploymentListProvider;
using MerkleTreeContractInitializationProvider = EBridge.Contracts.Bridge.ContractInitializationProvider.MerkleTreeContractInitializationProvider;
using ReceiptMakerContractInitializationProvider = EBridge.Contracts.Bridge.ContractInitializationProvider.ReceiptMakerContractInitializationProvider;
using RegimentContractInitializationProvider = EBridge.Contracts.Bridge.ContractInitializationProvider.RegimentContractInitializationProvider;
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
                File.ReadAllBytes(typeof(EBridge.Contracts.TestContract.ReceiptMaker.ReceiptMakerContract).Assembly.Location)
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