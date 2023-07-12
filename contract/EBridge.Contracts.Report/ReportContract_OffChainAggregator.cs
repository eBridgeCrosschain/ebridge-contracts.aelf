using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Report
{
    public partial class ReportContract
    {
        public override OffChainAggregationInfo RegisterOffChainAggregation(
            RegisterOffChainAggregationInput input)
        {
            Assert(State.RegisterWhiteListMap[Context.Sender], "Sender not in register white list.");
            Assert(State.OffChainAggregationInfoMap[input.ChainId][input.Token] == null,
                $"Off chain aggregation info of {input.Token} already registered.");

            var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(input.RegimentId);
            var regimentInfo = State.RegimentContract.GetRegimentInfo.Call(regimentAddress);
            Assert(regimentInfo.Manager != null, "Regiment not exists.");

            var offChainAggregationInfo = new OffChainAggregationInfo
            {
                Token = input.Token,
                OffChainQueryInfoList = input.OffChainQueryInfoList,
                ConfigDigest = input.ConfigDigest,
                RegimentId = input.RegimentId,
                AggregateThreshold = input.AggregateThreshold,
                AggregatorContractAddress = input.AggregatorContractAddress,
                ChainId = input.ChainId,
                Register = Context.Sender,
                AggregateOption = input.AggregateOption
            };
            if (input.OffChainQueryInfoList != null)
            {
                Assert(input.OffChainQueryInfoList.Value.Count >= 1, "At least 1 off-chain info.");
                Assert(input.OffChainQueryInfoList.Value.Count <= MaximumOffChainQueryInfoCount,
                    $"Maximum off chain query info count: {MaximumOffChainQueryInfoCount}");
                if (input.OffChainQueryInfoList?.Value.Count > 1)
                {
                    Assert(input.AggregatorContractAddress != null,
                        "Merkle tree style aggregator must set aggregator contract address.");
                }

                for (var i = 0; i < input.OffChainQueryInfoList?.Value.Count; i++)
                {
                    offChainAggregationInfo.RoundIds.Add(0);
                }
            }

            State.TargetChainAddressMap[input.ChainId] = input.Token;

            State.OffChainAggregationInfoMap[input.ChainId][input.Token] = offChainAggregationInfo;
            State.CurrentRoundIdMap[input.ChainId][input.Token] = 1;

            Context.Fire(new OffChainAggregationRegistered
            {
                Token = offChainAggregationInfo.Token,
                OffChainQueryInfoList = offChainAggregationInfo.OffChainQueryInfoList,
                ConfigDigest = offChainAggregationInfo.ConfigDigest,
                RegimentId = offChainAggregationInfo.RegimentId,
                AggregateThreshold = offChainAggregationInfo.AggregateThreshold,
                AggregatorContractAddress = offChainAggregationInfo.AggregatorContractAddress,
                ChainId = offChainAggregationInfo.ChainId,
                Register = offChainAggregationInfo.Register,
                AggregateOption = offChainAggregationInfo.AggregateOption
            });

            return offChainAggregationInfo;
        }

        public override Empty AddOffChainQueryInfo(AddOffChainQueryInfoInput input)
        {
            var offChainAggregationInfo = State.OffChainAggregationInfoMap[input.ChainId][input.Token];
            Assert(offChainAggregationInfo != null, $"Token {input.Token} not registered.");
            Assert(offChainAggregationInfo.Register == Context.Sender, "No permission.");
            Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count > 1,
                "Only merkle style aggregation can manage off chain query info.");
            offChainAggregationInfo.OffChainQueryInfoList.Value.Add(input.OffChainQueryInfo);
            offChainAggregationInfo.RoundIds.Add(State.CurrentRoundIdMap[input.ChainId][input.Token].Sub(1));
            Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count <= MaximumOffChainQueryInfoCount,
                $"Maximum off chain query info count: {MaximumOffChainQueryInfoCount}");
            State.OffChainAggregationInfoMap[input.ChainId][input.Token] = offChainAggregationInfo;
            Context.Fire(new OffChainQueryInfoAdded()
            {
                Sender = Context.Sender,
                Token = input.Token,
                ChainId = input.ChainId,
                OffChainQueryInfo = input.OffChainQueryInfo
            });
            return new Empty();
        }

        public override Empty RemoveOffChainQueryInfo(RemoveOffChainQueryInfoInput input)
        {
            var offChainAggregationInfo = State.OffChainAggregationInfoMap[input.ChainId][input.Token];
            Assert(offChainAggregationInfo != null, $"Token {input.Token} not registered.");
            Assert(offChainAggregationInfo.Register == Context.Sender, "No permission.");
            Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count > 1,
                "Only merkle style aggregation can manage off chain query info.");
            Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count > input.RemoveNodeIndex, "Invalid index.");
            offChainAggregationInfo.OffChainQueryInfoList.Value[input.RemoveNodeIndex] =
                new OffChainQueryInfo
                {
                    Title = "invalid"
                };
            offChainAggregationInfo.RoundIds[input.RemoveNodeIndex] = -1;
            State.OffChainAggregationInfoMap[input.ChainId][input.Token] = offChainAggregationInfo;
            Context.Fire(new OffChainQueryInfoRemoved()
            {
                Sender = Context.Sender,
                Token = input.Token,
                ChainId = input.ChainId,
                RemoveNodeIndex = input.RemoveNodeIndex
            });
            return new Empty();
        }

        public override Empty ChangeOffChainQueryInfo(ChangeOffChainQueryInfoInput input)
        {
            var offChainAggregationInfo = State.OffChainAggregationInfoMap[input.ChainId][input.Token];
            Assert(offChainAggregationInfo != null, $"Token {input.Token} not registered.");
            Assert(offChainAggregationInfo.Register == Context.Sender, "No permission.");
            Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count == 1,
                "Only single style aggregation can change off chain query info.");
            offChainAggregationInfo.OffChainQueryInfoList.Value[0] = input.NewOffChainQueryInfo;
            State.OffChainAggregationInfoMap[input.ChainId][input.Token] = offChainAggregationInfo;
            Context.Fire(new OffChainQueryInfoChanged()
            {
                Sender = Context.Sender,
                Token = input.Token,
                ChainId = input.ChainId,
                NewOffChainQueryInfo = input.NewOffChainQueryInfo
            });
            return new Empty();
        }

        public override Empty AddRegisterWhiteList(Address input)
        {
            Assert(Context.Sender == State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
                "No permission.");
            Assert(!State.RegisterWhiteListMap[input], $"{input} already in register white list.");
            State.RegisterWhiteListMap[input] = true;
            Context.Fire(new RegisterWhiteListAdded()
            {
                Sender = Context.Sender,
                AddAddress = input
            });
            return new Empty();
        }

        public override Empty RemoveFromRegisterWhiteList(Address input)
        {
            Assert(Context.Sender == State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
                "No permission.");
            Assert(State.RegisterWhiteListMap[input], $"{input} is not in register white list.");
            State.RegisterWhiteListMap[input] = false;
            Context.Fire(new RegisterWhiteListRemoved()
            {
                Sender = Context.Sender,
                RemoveAddress = input
            });
            return new Empty();
        }
    }
}