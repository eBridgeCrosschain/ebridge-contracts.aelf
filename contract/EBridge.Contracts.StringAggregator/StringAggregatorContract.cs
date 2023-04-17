using System.Linq;
using AElf.Standards.ACS13;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.StringAggregator
{
    public partial class StringAggregatorContract : StringAggregatorContractContainer.StringAggregatorContractBase
    {
        public override StringValue Aggregate(AggregateInput input)
        {
            var indexOfMax = input.Frequencies.IndexOf(input.Frequencies.Max());

            return new StringValue
            {
                Value = input.Results[indexOfMax]
            };
        }
    }
}