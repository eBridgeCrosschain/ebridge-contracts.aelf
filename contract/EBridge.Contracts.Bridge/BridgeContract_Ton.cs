using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Empty SetTonConfig(SetTonConfigInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.TonChainId > 0 && input.TonContractAddress != null, "Invalid input.");
        State.TonConfig.Value = new TonConfig
        {
            TonChainId = input.TonChainId,
            TonContractAddress = input.TonContractAddress
        };
        return new Empty();
    }

    public override Empty SetRampContract(Address input)
    {
        Assert(IsAddressValid(input), "Invalid input.");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        State.RampContract.Value = input;
        return new Empty();
    }

    public override Empty SetRampTokenSwapConfig(TokenSwapConfig input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input != null && input.TokenSwapList != null && input.TokenSwapList.TokenSwapInfoList.Count > 0,
            "Invalid input.");
        var tokenSwapList = new List<AetherLink.Contracts.Ramp.TokenSwapInfo>();
        foreach (var swap in input.TokenSwapList.TokenSwapInfoList)
        {
            tokenSwapList.Add(new AetherLink.Contracts.Ramp.TokenSwapInfo
            {
                TargetChainId = swap.TargetChainId,
                TargetContractAddress = swap.TargetContractAddress,
                TokenAddress = swap.TokenAddress,
                OriginToken = swap.OriginToken,
                SwapId = swap.SwapId
            });
        }

        State.RampContract.SetTokenSwapConfig.Send(new AetherLink.Contracts.Ramp.TokenSwapConfig
        {
            TokenSwapList = new AetherLink.Contracts.Ramp.TokenSwapList
            {
                TokenSwapInfoList = { tokenSwapList }
            }
        });
        return new Empty();
    }

    public override Empty ForwardMessage(ForwardMessageInput input)
    {
        Assert(!State.IsContractPause.Value, "Contract is paused.");
        Assert(Context.Sender == State.RampContract.Value, "No permission.");
        var tonConfig = State.TonConfig.Value;
        Assert(input.SourceChainId > 0 && input.SourceChainId == tonConfig.TonChainId, "Invalid source chain id.");
        Assert(input.TargetChainId > 0 && input.TargetChainId == Context.ChainId, "Invalid target chain id.");
        var sender = input.Sender?.ToBase64();
        Assert(sender != null && sender == tonConfig.TonContractAddress, "Invalid sender.");
        var receiver = input.Receiver?.ToPlainBase58();
        Assert(receiver != null && Address.FromBase58(receiver) == Context.Self, "Invalid receiver.");
        Assert(input.Message != null, "Invalid message.");
        var leafHashValue = EncodeMessageAndVerification(input.Message, out var amount, out var targetAddress,
            out var receiptIndex, out var receiptIdHash);

        var tokenAmount = input.TokenAmount;
        var swapId = Hash.LoadFromHex(tokenAmount.SwapId);
        var targetContractAddress = tokenAmount.TargetContractAddress;
        Assert(targetContractAddress != null && Address.FromBase58(targetContractAddress) == Context.Self,
            "Invalid target contract address.");
        ConsumeSwapAmount(swapId, amount);
        State.ReceiptHashRecordStatus[leafHashValue] = true;

        //Transfer token.
        var receiptId = $"{receiptIdHash.ToHex()}.{receiptIndex}";
        PerformTransferToken(swapId, targetAddress, amount, receiptId);

        return new Empty();
    }

    private Hash EncodeMessageAndVerification(ByteString message, out long amount, out Address targetAddress,
        out long receiptIndex, out Hash receiptIndexHash)
    {
        var messageByte = message.ToByteArray();
        var receiptIndexByte = messageByte.Skip(0).Take(32).ToArray();
        var receiptIdToken = messageByte.Skip(32).Take(32).ToArray();
        var amountByte = messageByte.Skip(64).Take(32).ToArray();
        var targetAddressByte = messageByte.Skip(96).Take(32).ToArray();
        var leafHash = messageByte.Skip(128).Take(32).ToArray();
        var leafHashValue = Hash.LoadFromHex(leafHash.ToHex());
        var receiptIdTokenHash = Hash.LoadFromHex(receiptIdToken.ToHex());
        var amountHash = HashHelper.ComputeFrom(amountByte);
        receiptIndexHash = HashHelper.ComputeFrom(receiptIndexByte);
        var receiptIdHash = HashHelper.ConcatAndCompute(receiptIdTokenHash, receiptIndexHash);
        var targetAddressByteHash = HashHelper.ComputeFrom(targetAddressByte);
        var computeHash = HashHelper.ConcatAndCompute(receiptIdHash, amountHash, targetAddressByteHash);
        Assert(leafHashValue == computeHash, "Invalid leaf hash.");
        Assert(State.ReceiptHashRecordStatus[leafHashValue] == false, "Leaf hash has been recorded.");
        amount = ParseHexToLong(amountByte);
        targetAddress = Address.FromBytes(targetAddressByte);
        receiptIndex = ParseHexToLong(receiptIndexByte);
        return leafHashValue;
    }

    public override RateLimiterTokenBucket GetCurrentTokenBucketState(Hash input)
    {
        var result = new RateLimiterTokenBucket();
        var amount = 0;
        var swapId = input;
        var swapInfo = GetTokenSwapInfo(swapId);
        var actualAmount = GetTargetTokenAmount(amount, swapInfo.SwapTargetToken?.SwapRatio);
        var dailyLimit = State.SwapDailyLimit[swapId];
        dailyLimit = GetDailyLimit(dailyLimit);
        var currentBucket = State.SwapTokenBucketInfo[swapId];
        currentBucket = GetTokenBucketAmount(currentBucket);
        if (dailyLimit == null && currentBucket == null)
        {
            result.IsDailyLimitEnabled = true;
            result.IsTokenBucketEnabled = true;
            return result;
        }

        if (actualAmount > dailyLimit.TokenAmount)
        {
            result.IsDailyLimitEnabled = false;
        }

        if (actualAmount > currentBucket.TokenCapacity)
        {
            result.IsTokenBucketEnabled = false;
        }

        return result;
    }

    public override TonConfig GetTonConfig(Empty input)
    {
        return State.TonConfig.Value;
    }

    public override Address GetRampContract(Empty input)
    {
        return State.RampContract.Value;
    }
}