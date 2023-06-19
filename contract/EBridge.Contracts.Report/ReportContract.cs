using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Standards.ACS13;
using AElf.Types;
using EBridge.Contracts.Oracle;
using EBridge.Contracts.Regiment;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Report
{
    public partial class ReportContract : ReportContractContainer.ReportContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.IsInitialized.Value, "Already initialized.");
            // State.GensisContract.Value = Context.GetZeroSmartContractAddress();
            // var author = State.GensisContract.GetContractAuthor.Call(Context.Self);
            // Assert(Context.Sender == author, "No permission.");
            State.Owner.Value = input.OwnerAddress;
            State.OracleContract.Value = input.OracleContractAddress;
            State.RegimentContract.Value = input.RegimentContractAddress;
            State.OracleTokenSymbol.Value = State.OracleContract.GetOracleTokenSymbol.Call(new Empty()).Value;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.ParliamentContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);
            State.ReportFee.Value = input.ReportFee;
            State.ApplyObserverFee.Value = input.ApplyObserverFee;
            State.TokenContract.Approve.Send(new ApproveInput
            {
                Spender = State.OracleContract.Value,
                Symbol = State.OracleTokenSymbol.Value,
                Amount = long.MaxValue
            });
            foreach (var address in input.InitialRegisterWhiteList)
            {
                State.RegisterWhiteListMap[address] = true;
            }

            State.IsInitialized.Value = true;
            return new Empty();
        }

        public override Hash QueryOracle(QueryOracleInput input)
        {
            var token = string.IsNullOrEmpty(input.Token) ? State.TargetChainAddressMap[input.ChainId] : input.Token;
            Assert(!string.IsNullOrEmpty(token), "Token is null. ChainId:{input.ChainId}");
            var offChainAggregationInfo = State.OffChainAggregationInfoMap[input.ChainId][token];
            Assert(offChainAggregationInfo != null, "Off chain aggregation info not exists.");

            // Pay oracle tokens to this contract, amount: report fee + oracle nodes payment.
            PayToTheContract(input.Payment);
            var queryInfo = GetOffChainQueryInfo(input.QueryInfo, input.NodeIndex, offChainAggregationInfo);

            //Title -> data on chain.
            var predicate = IfDataOnChain;
            if (predicate(queryInfo))
            {
                ProposeReport(input.ChainId, token, queryInfo, offChainAggregationInfo);
                return Hash.Empty;
            }

            var regimentAddress =
                State.RegimentContract.GetRegimentAddress.Call(offChainAggregationInfo.RegimentId);
            var queryInput = new QueryInput
            {
                Payment = input.Payment,
                AggregateThreshold = Math.Max(offChainAggregationInfo.AggregateThreshold, input.AggregateThreshold),
                // DO NOT FILL THIS FILED.
                // AggregatorContractAddress = null,
                DesignatedNodeList = new AddressList
                {
                    Value = {regimentAddress}
                },
                QueryInfo = new QueryInfo
                {
                    Title = queryInfo.Title,
                    Options = {queryInfo.Options}
                },
                CallbackInfo = new CallbackInfo
                {
                    ContractAddress = Context.Self,
                    MethodName = nameof(ProposeReport)
                },
                Token = input.Token
            };
            State.OracleContract.Query.Send(queryInput);
            var queryId = Context.GenerateId(State.OracleContract.Value, HashHelper.ComputeFrom(queryInput));
            State.ReportQueryRecordMap[queryId] = new ReportQueryRecord
            {
                OriginQuerySender = Context.Sender,
                // Record current report fee in case it changes before cancelling this query.
                PaidReportFee = State.ReportFee.Value,
                Payment = input.Payment,
                TargetChainId = input.ChainId
            };
            return queryId;
        }

        private void PayToTheContract(long payment)
        {
            var totalPayment = State.ReportFee.Value.Add(payment);
            if (totalPayment > 0)
            {
                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    From = Context.Sender,
                    To = Context.Self,
                    Symbol = State.OracleTokenSymbol.Value,
                    Amount = totalPayment
                });
            }
        }

        private OffChainQueryInfo GetOffChainQueryInfo(OffChainQueryInfo queryInfo, int nodeIndex,
            OffChainAggregationInfo offChainAggregationInfo)
        {
            var result = new OffChainQueryInfo();
            if (queryInfo == null)
            {
                Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count > nodeIndex,
                    "Invalid node index.");
                Assert(offChainAggregationInfo.RoundIds[nodeIndex] != -1,
                    $"Query info of index {nodeIndex} already removed.");
                result.Title = offChainAggregationInfo.OffChainQueryInfoList.Value[nodeIndex].Title;
                result.Options.Add(
                    offChainAggregationInfo.OffChainQueryInfoList.Value[nodeIndex].Options);
            }
            else
            {
                result.Title = queryInfo.Title;
                result.Options.Add(queryInfo.Options);
            }

            return result;
        }

        public override Empty CancelQueryOracle(Hash input)
        {
            var reportQueryRecord = State.ReportQueryRecordMap[input];
            Assert(reportQueryRecord != null, "Query not exists or not delegated by Report Contract.");
            Assert(reportQueryRecord.OriginQuerySender == Context.Sender, "No permission.");

            // Return report fee and payment
            var needToReturn = reportQueryRecord.PaidReportFee.Add(reportQueryRecord.Payment);
            if (needToReturn > 0)
            {
                State.TokenContract.Transfer.Send(new TransferInput
                {
                    To = reportQueryRecord.OriginQuerySender,
                    Symbol = State.OracleTokenSymbol.Value,
                    Amount = needToReturn
                });
            }

            State.OracleContract.CancelQuery.Send(input);
            return new Empty();
        }

        public override Report ProposeReport(CallbackInput input)
        {
            Assert(Context.Sender == State.OracleContract.Value,
                "Only Oracle Contract can propose report.");
            Assert(State.ReportQueryRecordMap[input.QueryId] != null,
                "This query is not initialed by Report Contract.");

            var plainResult = new PlainResult();
            plainResult.MergeFrom(input.Result);

            var queryRecord = State.ReportQueryRecordMap[input.QueryId];

            var currentRoundId = State.CurrentRoundIdMap[queryRecord.TargetChainId][plainResult.Token];

            var offChainAggregationInfo =
                State.OffChainAggregationInfoMap[queryRecord.TargetChainId][plainResult.Token];

            Report report;
            if (offChainAggregationInfo.OffChainQueryInfoList.Value.Count == 1)
            {
                report = ProposeReportWithNormalStyle(plainResult, input.QueryId, currentRoundId,
                    offChainAggregationInfo, queryRecord.TargetChainId);
            }
            else
            {
                report = ProposeReportWithMerkleStyle(plainResult, input.QueryId, offChainAggregationInfo,
                    currentRoundId, queryRecord.TargetChainId);
            }

            return report;
        }

        private Report ProposeReportWithNormalStyle(PlainResult plainResult, Hash queryId, long currentRoundId,
            OffChainAggregationInfo offChainAggregationInfo, string targetChainId)
        {
            var originObservations = new Observations
            {
                Value =
                {
                    plainResult.DataRecords.Value.Select(d => new Observation
                    {
                        Key = d.Address.ToByteArray().ToHex(),
                        Data = d.Data
                    })
                }
            };
            var report = new Report
            {
                QueryId = queryId,
                RoundId = currentRoundId,
                Observations = originObservations,
                AggregatedData =
                    ByteString.CopyFrom(GetAggregatedData(offChainAggregationInfo, plainResult).GetBytes())
            };
            State.ReportMap[targetChainId][plainResult.Token][currentRoundId] = report;
            State.CurrentRoundIdMap[targetChainId][plainResult.Token] = currentRoundId.Add(1);
            var regimentId = State.RegimentContract.GetRegimentId.Call(plainResult.RegimentAddress);
            Context.Fire(new ReportProposed
            {
                RegimentId = regimentId,
                Token = plainResult.Token,
                RoundId = currentRoundId,
                RawReport = GenerateEvmRawReport(report)
            });
            return report;
        }

        private Report ProposeReportWithMerkleStyle(PlainResult plainResult, Hash queryId,
            OffChainAggregationInfo offChainAggregationInfo, long currentRoundId, string targetChainId)
        {
            var offChainQueryInfo = new OffChainQueryInfo
            {
                Title = plainResult.QueryInfo.Title,
                Options = {plainResult.QueryInfo.Options}
            };
            var nodeIndex = offChainAggregationInfo.OffChainQueryInfoList.Value.IndexOf(offChainQueryInfo);
            var nodeRoundId = offChainAggregationInfo.RoundIds[nodeIndex];
            Assert(nodeRoundId != -1, $"Query info of index {nodeIndex} already removed.");
            Assert(nodeRoundId.Add(1) == currentRoundId,
                $"Data of {offChainQueryInfo} already revealed.{nodeIndex}\n{offChainAggregationInfo}");
            offChainAggregationInfo.RoundIds[nodeIndex] = nodeRoundId.Add(1);
            var aggregatedData = GetAggregatedData(offChainAggregationInfo, plainResult);
            var report = State.ReportMap[targetChainId][plainResult.Token][currentRoundId] ?? new Report
            {
                QueryId = queryId,
                RoundId = currentRoundId,
                Observations = new Observations()
            };
            report.Observations.Value.Add(new Observation
            {
                Key = nodeIndex.ToString(),
                Data = aggregatedData
            });
            State.NodeObserverListMap[plainResult.Token][currentRoundId][nodeIndex] = new ObserverList
            {
                Value = {plainResult.DataRecords.Value.Select(d => d.Address)}
            };
            Context.Fire(new MerkleReportNodeAdded
            {
                Token = plainResult.Token,
                NodeIndex = nodeIndex,
                NodeRoundId = nodeRoundId,
                AggregatedData = aggregatedData
            });
            if (offChainAggregationInfo.RoundIds.All(i => i >= currentRoundId || i == -1))
            {
                // Time to generate merkle tree.
                report.AggregatedData =
                    GenerateMerkleTree(report, offChainAggregationInfo, plainResult.Token, currentRoundId);
                for (var i = 0; i < offChainAggregationInfo.OffChainQueryInfoList.Value.Count; i++)
                {
                    State.NodeObserverListMap[plainResult.Token][currentRoundId].Remove(i);
                }

                var regimentId = State.RegimentContract.GetRegimentId.Call(plainResult.RegimentAddress);
                Context.Fire(new ReportProposed
                {
                    RegimentId = regimentId,
                    Token = plainResult.Token,
                    RoundId = currentRoundId,
                    RawReport = GenerateEvmRawReport(report)
                });
                State.CurrentRoundIdMap[targetChainId][plainResult.Token] = currentRoundId.Add(1);
            }

            State.ReportMap[targetChainId][plainResult.Token][currentRoundId] = report;
            return report;
        }

        private ByteString GenerateMerkleTree(Report report, OffChainAggregationInfo offChainAggregationInfo,
            string token, long currentRoundId)
        {
            var merkleNodes = new List<Hash>();
            for (var i = 0; i < offChainAggregationInfo.OffChainQueryInfoList.Value.Count; i++)
            {
                var node = report.Observations.Value.FirstOrDefault(o => o.Key == i.ToString());
                var nodeHash = node == null ? Hash.Empty : HashHelper.ComputeFrom(node.Data);
                merkleNodes.Add(nodeHash);
            }

            var merkleTree = BinaryMerkleTree.FromLeafNodes(merkleNodes);
            State.BinaryMerkleTreeMap[token][currentRoundId] = merkleTree;
            return merkleTree.Root.Value;
        }

        private Report ProposeReport(string chainId, string token, OffChainQueryInfo queryInfo,
            OffChainAggregationInfo info)
        {
            var currentRoundId = State.CurrentRoundIdMap[chainId][token];
            var report = new Report
            {
                RoundId = currentRoundId,
                Observations = new Observations
                {
                    Value =
                    {
                        new Observation
                        {
                            //Key -> get receipt id.
                            Key = queryInfo.Title.Split("_").Last(),
                            //Data -> receipt hash.
                            Data = queryInfo.Options.First()
                        }
                    }
                }
            };
            State.ReportMap[chainId][token][currentRoundId] = report;
            State.CurrentRoundIdMap[chainId][token] = currentRoundId.Add(1);
            Context.Fire(new ReportProposed
            {
                RegimentId = info.RegimentId,
                Token = token,
                RoundId = currentRoundId,
                RawReport = GenerateEvmRawReport(report),
                QueryInfo = new OffChainQueryInfo
                {
                    Title = queryInfo.Title,
                    Options = {queryInfo.Options.First()}
                },
                TargetChainId = info.ChainId
            });
            State.ReportRecordMap[chainId][token][currentRoundId] = new ReportQueryRecord
            {
                PaidReportFee = State.ReportFee.Value
            };
            return report;
        }

        private string GetAggregatedData(OffChainAggregationInfo offChainAggregationInfo,
            PlainResult plainResult)
        {
            var aggregatorContractAddress = offChainAggregationInfo.AggregatorContractAddress;
            if (aggregatorContractAddress == null)
            {
                // If user didn't fill in aggregator contract address, just return the majority result;
                // Or return the first plain result if the majority result not exists.
                return GetMajorityResult(plainResult);
            }

            State.AggregatorContract.Value = aggregatorContractAddress;
            var aggregateInput = new AggregateInput {AggregateOption = offChainAggregationInfo.AggregateOption};
            foreach (var nodeData in plainResult.DataRecords.Value)
            {
                aggregateInput.Results.Add(nodeData.Data);
                aggregateInput.Frequencies.Add(1);
            }

            // Use an ACS13 Contract to aggregate a data.
            return State.AggregatorContract.Aggregate.Call(aggregateInput).Value;
        }

        private string GetMajorityResult(PlainResult plainResult)
        {
            var results = plainResult.DataRecords.Value.Select(r => r.Data);
            var countDict = new Dictionary<string, int>();
            foreach (var result in results)
            {
                if (countDict.ContainsKey(result))
                {
                    countDict[result] += 1;
                }
                else
                {
                    countDict.Add(result, 1);
                }
            }

            return countDict.OrderByDescending(d => d.Value).Select(d => d.Key).ToList().First();
        }

        public override Empty ConfirmReport(ConfirmReportInput input)
        {
            // Assert Sender is from certain Observer Association.
            var offChainAggregationInfo = State.OffChainAggregationInfoMap[input.ChainId][input.Token];
            Assert(offChainAggregationInfo != null, "Off chain aggregation info not exists.");
            var report = State.ReportMap[input.ChainId][input.Token][input.RoundId];
            Assert(report != null, $"Report of round {input.RoundId} not proposed.");

            var reportRecord = report.QueryId == null
                ? State.ReportRecordMap[input.ChainId][input.Token][input.RoundId]
                : State.ReportQueryRecordMap[report.QueryId];

            Assert(!reportRecord.IsRejected, "This report is already rejected.");
            Assert(!reportRecord.IsAllNodeConfirmed, "This report is already confirmed by all nodes.");


            var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(offChainAggregationInfo.RegimentId);
            var memberList = State.OracleContract.GetRegimentMemberList
                .Call(regimentAddress).Value;

            Assert(IsRegimentMember(Context.Sender, regimentAddress),
                "Sender isn't a member of certain regiment.");

            var skipList = State.SkipMemberListMap[input.ChainId][input.Token]?.Value;
            Assert(skipList != null && !skipList.Contains(Context.Sender), "Sender is in the skip list.");

            State.ObserverSignatureMap[input.ChainId][input.Token][input.RoundId][Context.Sender] =
                input.Signature;
            if (!reportRecord.ConfirmedNodeList.Contains(Context.Sender))
            {
                reportRecord.ConfirmedNodeList.Add(Context.Sender);
            }

            if (reportRecord.ConfirmedNodeList.Count == memberList.Count.Sub(skipList?.Count ?? 0))
            {
                reportRecord.IsAllNodeConfirmed = true;
            }

            if (report.QueryId == null)
            {
                State.ReportRecordMap[input.ChainId][input.Token][input.RoundId] = reportRecord;
            }
            else
            {
                State.ReportQueryRecordMap[report.QueryId] = reportRecord;
            }

            Context.Fire(new ReportConfirmed
            {
                Token = input.Token,
                RoundId = input.RoundId,
                Signature = input.Signature,
                RegimentId = offChainAggregationInfo.RegimentId,
                IsAllNodeConfirmed = reportRecord.IsAllNodeConfirmed,
                TargetChainId = offChainAggregationInfo.ChainId
            });
            return new Empty();
        }

        public override Empty RejectReport(RejectReportInput input)
        {
            var offChainAggregationInfo = State.OffChainAggregationInfoMap[input.ChainId][input.Token];
            Assert(offChainAggregationInfo != null, "Off chain aggregation info not exists.");
            var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(offChainAggregationInfo.RegimentId);

            Assert(offChainAggregationInfo.OffChainQueryInfoList.Value.Count == 1,
                "Merkle tree style aggregation doesn't support rejection.");

            Assert(State.ObserverSignatureMap[input.ChainId][input.Token][input.RoundId][Context.Sender] == null,
                "Sender already confirmed this report.");
            Assert(IsRegimentMember(Context.Sender, regimentAddress),
                "Sender isn't a member of certain Observer Association.");
            foreach (var accusingNode in input.AccusingNodes)
            {
                Assert(IsRegimentMember(accusingNode, regimentAddress),
                    "Accusing node isn't a member of certain Observer Association.");
            }

            var report = State.ReportMap[input.ChainId][input.Token][input.RoundId];
            var senderData = report.Observations.Value
                .FirstOrDefault(o => o.Key == Context.Sender.ToByteArray().ToHex())?.Data;
            foreach (var accusingNode in input.AccusingNodes)
            {
                var accusedNodeData = report.Observations.Value.First(o => o.Key == accusingNode.ToByteArray().ToHex())
                    .Data;
                Assert(senderData == null || !senderData.Equals(accusedNodeData), "Invalid accuse.");
                // Fine.
                State.ObserverMortgagedTokensMap[accusingNode] = State.ObserverMortgagedTokensMap[accusingNode]
                    .Sub(GetAmercementAmount(regimentAddress));
            }

            var reportQueryRecord = State.ReportQueryRecordMap[report.QueryId];
            reportQueryRecord.IsRejected = true;
            State.ReportQueryRecordMap[report.QueryId] = reportQueryRecord;
            return new Empty();
        }

        private bool IsRegimentMember(Address address, Address regimentAddress)
        {
            return State.RegimentContract.IsRegimentMember.Call(new IsRegimentMemberInput
            {
                RegimentAddress = regimentAddress,
                Address = address
            }).Value;
        }

        public override Empty ChangeOracleContractAddress(Address input)
        {
            Assert(Context.Sender == State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
                "No permission.");
            State.OracleContract.Value = input;
            return new Empty();
        }

        public override Empty SetSkipMemberList(SetSkipMemberListInput input)
        {
            var regimentId = State.OffChainAggregationInfoMap[input.ChainId][input.Token].RegimentId;
            var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(regimentId);
            var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
            Assert(Context.Sender == regimentManager, "No permission.");
            var memberList = State.SkipMemberListMap[input.ChainId][input.Token] ?? new MemberList();
            memberList.Value.AddRange(input.Value.Value);
            State.SkipMemberListMap[input.ChainId][input.Token] = memberList;
            return new Empty();
        }
    }
}