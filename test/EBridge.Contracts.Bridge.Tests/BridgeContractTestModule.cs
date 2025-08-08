using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using AetherLink.Contracts.Ramp;
using EBridge.Contracts.Bridge.ContractInitializationProvider;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using MainChainDAppContractTestDeploymentListProvider = EBridge.Contracts.Bridge.ContractInitializationProvider.MainChainDAppContractTestDeploymentListProvider;

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
                new RampContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(RampContract).Assembly.Location)
            }
        };
        contractCodeProvider.Codes = contractCodes;
    }
}