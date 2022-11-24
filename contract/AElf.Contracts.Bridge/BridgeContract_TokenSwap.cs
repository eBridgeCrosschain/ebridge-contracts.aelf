using System;
using System.Linq;
using AElf.Contracts.MerkleTreeContract;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Bridge;

public partial class BridgeContract
{
    public override Hash CreateSwap(CreateSwapInput input)
    {
        Assert(input.RegimentId != null, "Regiment id cannot be null.");
        Assert(State.MerkleTreeContract.Value != null, "MerkleTree contract is not initialized.");
        var fromChainIdInput = input.SwapTargetTokenList.First().FromChainId;
        var symbolInput = input.SwapTargetTokenList.First().Symbol;
        Assert(State.ChainTokenSwapIdMap[fromChainIdInput][symbolInput] == null,
            $"Swap already created. Chain id: {fromChainIdInput} Symbol: {symbolInput}. ");
        var swapId = HashHelper.ConcatAndCompute(Context.TransactionId, HashHelper.ComputeFrom(input));
        Assert(State.SwapInfo[swapId] == null, "Swap already created.");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(input.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "Only regiment manager can create swap.");
        Assert(State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Admins.Contains(Context.Self),
            $"Bridge Contract is not the admin of regiment. Regiment id: {input.RegimentId}");
        State.MerkleTreeContract.CreateSpace.Send(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = input.MerkleTreeLeafLimit == 0 ? DefaultMaximalLeafCount : input.MerkleTreeLeafLimit,
                Operators = input.RegimentId
            }
        });
        var spaceFromId = State.MerkleTreeContract.GetRegimentSpaceCount.Call(input.RegimentId).Value.Add(1);
        var spaceId =
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.RegimentId), HashHelper.ComputeFrom(spaceFromId));
        State.SwapSpaceIdMap[swapId] = spaceId;
        State.SpaceRegimentIdMap[spaceId] = input.RegimentId;

        var swapInfo = new SwapInfo
        {
            SwapId = swapId,
            RegimentId = input.RegimentId,
            SpaceId = spaceId
        };

        foreach (var swapTargetToken in input.SwapTargetTokenList)
        {
            AssertSwapTargetToken(swapTargetToken.Symbol);
            Assert(ValidateSwapRatio(swapTargetToken.SwapRatio), "Invalidate swap ratio.");
            swapInfo.SwapTargetTokenList.Add(swapTargetToken);
            var swapPairInfo = new SwapPairInfo();
            State.SwapPairInfoMap[swapId][swapTargetToken.Symbol] = swapPairInfo;
        }

        var fromChainId = swapInfo.SwapTargetTokenList.First().FromChainId;
        var symbol = swapInfo.SwapTargetTokenList.First().Symbol;
        State.ChainTokenSwapIdMap[fromChainId][symbol] = swapId;

        State.SwapInfo[swapId] = swapInfo;
        Context.Fire(new SwapInfoAdded
        {
            SwapId = swapId,
            FromChainId = swapInfo.SwapTargetTokenList.First().FromChainId,
            Symbol = swapInfo.SwapTargetTokenList.First().Symbol
        });
        return swapId;
    }

    public override Empty Deposit(DepositInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        Assert(input.Amount > 0, $"Invalid deposit amount.{input.Amount}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(swapInfo.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "No permission.");
        var swapPairInfo = State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol];
        if (swapPairInfo == null)
        {
            throw new AssertionException($"Swap pair {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist.");
        }

        swapPairInfo.DepositAmount = swapPairInfo.DepositAmount.Add(input.Amount);
        State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol] = swapPairInfo;
        TransferDepositFrom(input.TargetTokenSymbol, input.Amount, Context.Sender);
        return new Empty();
    }

    public override Empty SwapToken(SwapTokenInput input)
    {
        Assert(!State.IsContractPause.Value, "Contract is paused.");
        var receiverAddress = input.ReceiverAddress ?? Context.Sender;
        ValidateSwapTokenInput(input);
        Assert(TryGetReceiptIndex(input.ReceiptId, out var receiptIndex), "Incorrect receipt index.");
        Assert(TryGetOriginTokenAmount(input.OriginAmount, out var amount), "Invalid amount.");
        AssertTokenAmount(input.SwapId, input.ReceiptId, amount);
        var leafHash = ComputeLeafHash(amount, receiverAddress, input.ReceiptId);

        var spaceId = State.SwapSpaceIdMap[input.SwapId];
        var lastRecordedLeafIndex = State.MerkleTreeContract.GetLastLeafIndex.Call(new GetLastLeafIndexInput
        {
            SpaceId = spaceId
        });
        var maximalLeafCount = State.MerkleTreeContract.GetSpaceInfo.Call(spaceId).MaxLeafCount;

        // To locate the tree of specific receipt id.
        var firstLeafIndex = receiptIndex.Sub(1).Div(maximalLeafCount).Mul(maximalLeafCount);
        var maxLastLeafIndex = firstLeafIndex.Add(maximalLeafCount).Sub(1);
        var lastLeafIndex = Math.Min(maxLastLeafIndex, lastRecordedLeafIndex.Value);
        var merklePath = State.MerkleTreeContract.GetMerklePath.Call(new GetMerklePathInput
        {
            ReceiptMaker = Context.Self,
            LeafNodeIndex = receiptIndex.Sub(1),
            SpaceId = spaceId
        });

        Assert(State.MerkleTreeContract.MerkleProof.Call(new MerkleProofInput
        {
            LastLeafIndex = lastLeafIndex,
            LeafNode = leafHash,
            MerklePath = merklePath,
            SpaceId = spaceId
        }).Value, "Merkle proof failed.");

        //Transfer token.
        PerformTransferToken(input.SwapId, receiverAddress, amount, input.ReceiptId);
        return new Empty();
    }

    private void AssertTokenAmount(Hash swapId, string receiptId, decimal amount)
    {
        var swapInfo = GetTokenSwapInfo(swapId);
        var tokenSymbol = swapInfo.SwapTargetTokenList.FirstOrDefault()?.Symbol;
        var maximumAmount = State.TokenMaximumAmount[tokenSymbol];
        if (amount <= maximumAmount) return;
        if (State.ApproveTransfer[receiptId]) return;
        throw new AssertionException(
            $"{tokenSymbol} swap amount higher than maximum amount. Waiting for admin authorization. ReceiptId:{receiptId}");
    }

    private void PerformTransferToken(Hash swapId, Address receiverAddress, decimal amount, string receiptId)
    {
        var swapInfo = GetTokenSwapInfo(swapId);
        var swapAmounts = new SwapAmounts
        {
            Receiver = receiverAddress
        };
        var chainId = swapInfo.SwapTargetTokenList.First().FromChainId;
        foreach (var swapTargetToken in swapInfo.SwapTargetTokenList)
        {
            var swapPairInfo = State.SwapPairInfoMap[swapInfo.SwapId][swapTargetToken.Symbol];
            if (swapPairInfo == null)
            {
                throw new AssertionException($"Swap pair {swapInfo.SwapId}-{swapTargetToken.Symbol} is not exist.");
            }
            var targetTokenAmount = GetTargetTokenAmount(amount, swapTargetToken.SwapRatio);
            Assert(targetTokenAmount <= swapPairInfo.DepositAmount,
                $"Deposit not enough. Deposit amount : {swapPairInfo.DepositAmount}");
            
            // Update swap pair and ledger
            swapPairInfo.SwappedAmount = swapPairInfo.SwappedAmount.Add(targetTokenAmount);
            swapPairInfo.SwappedTimes = swapPairInfo.SwappedTimes.Add(1);
            swapPairInfo.DepositAmount = swapPairInfo.DepositAmount.Sub(targetTokenAmount);

            State.SwapPairInfoMap[swapInfo.SwapId][swapTargetToken.Symbol] = swapPairInfo;

            // Do transfer
            TransferToken(swapTargetToken.Symbol, targetTokenAmount, receiverAddress);
            Context.Fire(new TokenSwapped
            {
                Amount = targetTokenAmount,
                Address = receiverAddress,
                Symbol = swapTargetToken.Symbol,
                ReceiptId = receiptId,
                FromChainId = chainId
            });

            swapAmounts.ReceivedAmounts[swapTargetToken.Symbol] = targetTokenAmount;
        }

        State.Ledger[swapId][receiptId] = swapAmounts;
        State.RecorderReceiptInfoMap[swapId][receiptId] = new ReceiptInfo
        {
            ReceiptId = receiptId,
            ReceivingTime = Context.CurrentBlockTime,
            ReceivingTxId = Context.TransactionId,
            AmountMap = {swapAmounts.ReceivedAmounts}
        };

        //TODO: Consider to optimize the data structure for too many records scenario.
        var swappedReceiptIdList =
            State.SwappedReceiptIdListMap[swapId][receiverAddress] ?? new ReceiptIdList();
        swappedReceiptIdList.Value.Add(receiptId);
        State.SwappedReceiptIdListMap[swapId][receiverAddress] = swappedReceiptIdList;
    }

    public override Empty Withdraw(WithdrawInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        Assert(input.Amount > 0, $"Invalid withdraw amount.{input.Amount}");
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(swapInfo.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "No permission.");
        var swapPairInfo = State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol];
        if (swapPairInfo == null)
        {
            throw new AssertionException($"Swap pair {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist.");
        }

        Assert(swapPairInfo.DepositAmount >= input.Amount,
            $"Deposits not enough. Deposit amount : {swapPairInfo.DepositAmount}");
        swapPairInfo.DepositAmount = swapPairInfo.DepositAmount.Sub(input.Amount);
        State.SwapPairInfoMap[swapInfo.SwapId][input.TargetTokenSymbol] = swapPairInfo;
        WithdrawDepositTo(input.TargetTokenSymbol, input.Amount, Context.Sender);
        return new Empty();
    }

    public override Empty ChangeSwapRatio(ChangeSwapRatioInput input)
    {
        var swapInfo = GetTokenSwapInfo(input.SwapId);
        var regimentAddress = State.RegimentContract.GetRegimentAddress.Call(swapInfo.RegimentId);
        var regimentManager = State.RegimentContract.GetRegimentInfo.Call(regimentAddress).Manager;
        Assert(Context.Sender == regimentManager, "No permission.");
        var swapTargetToken =
            swapInfo.SwapTargetTokenList.SingleOrDefault(token => token.Symbol == input.TargetTokenSymbol);
        if (swapTargetToken == null)
        {
            throw new AssertionException(
                $"Swap target token {swapInfo.SwapId}-{input.TargetTokenSymbol} is not exist. ");
        }

        swapTargetToken.SwapRatio = input.SwapRatio;
        State.SwapInfo[swapInfo.SwapId] = swapInfo;
        Context.Fire(new SwapRatioChanged
        {
            SwapId = input.SwapId,
            NewSwapRatio = input.SwapRatio,
            TargetTokenSymbol = input.TargetTokenSymbol
        });
        return new Empty();
    }
}