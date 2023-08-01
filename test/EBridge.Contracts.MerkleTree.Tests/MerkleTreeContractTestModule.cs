using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using EBridge.Contracts.MerkleTreeContract.ContractInitializationProvider;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.TestContract.ReceiptMaker;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using MainChainDAppContractTestDeploymentListProvider = EBridge.Contracts.MerkleTreeContract.ContractInitializationProvider.MainChainDAppContractTestDeploymentListProvider;

namespace EBridge.Contracts.MerkleTreeContract;

[DependsOn(typeof(MainChainDAppContractTestModule))]
public class MerkleTreeContractTestModule : MainChainDAppContractTestModule
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
                new MerkleTreeContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(MerkleTreeContract).Assembly.Location)
            },
            {
                new RegimentContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(RegimentContract).Assembly.Location)
            },
            {
                new ReceiptMakerContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(TestContract.ReceiptMaker.ReceiptMakerContract).Assembly.Location)
            }
        };
        contractCodeProvider.Codes = contractCodes;
    }
}