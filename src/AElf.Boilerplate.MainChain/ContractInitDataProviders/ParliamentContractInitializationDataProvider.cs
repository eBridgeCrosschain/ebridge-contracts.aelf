using AElf.GovernmentSystem;
using Volo.Abp.DependencyInjection;

namespace AElf.Boilerplate.MainChain.ContractInitDataProviders
{
    public class ParliamentContractInitializationDataProvider : IParliamentContractInitializationDataProvider,
        ITransientDependency
    {
        public ParliamentContractInitializationData GetContractInitializationData()
        {
            return new ParliamentContractInitializationData();
        }
    }
}