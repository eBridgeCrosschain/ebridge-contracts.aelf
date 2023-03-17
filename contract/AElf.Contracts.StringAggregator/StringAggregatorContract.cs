using AElf.Standards.ACS13;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.StringAggregator
{
    public partial class StringAggregatorContract : StringAggregatorContractContainer.StringAggregatorContractBase
    {
        public override StringValue Aggregate(AggregateInput input)
        {
            //var indexOfMax = input.Frequencies.IndexOf(input.Frequencies.Min());

            return new StringValue
            {
                Value = input.Results[0]
            };
        }
    }
}