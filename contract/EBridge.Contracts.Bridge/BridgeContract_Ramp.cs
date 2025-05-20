using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Empty SetCrossChainConfig(SetCrossChainConfigInput input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(!string.IsNullOrEmpty(input.ChainId), "Invalid chain.");
        Assert(!string.IsNullOrEmpty(input.ContractAddress), "Invalid contract address.");
        Assert(!string.IsNullOrEmpty(input.ContractAddressForReceive), "Invalid contract address for receive.");

        State.CrossChainIdMap[input.ChainIdNumber] = input.ChainId;
        State.CrossChainConfigMap[input.ChainId] = new()
        {
            ContractAddress = input.ContractAddress,
            ChainId = input.ChainIdNumber,
            ChainType = input.ChainType,
            ContractAddressForReceive = input.ContractAddressForReceive,
            Fee = input.ChainType == ChainType.Tvm ? input.Fee : 0
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
                TokenAddress = swap.TokenAddress,
                Symbol = swap.Symbol,
                ExtraData = swap.ExtraData,
                Receiver = swap.Receiver,
                SourceChainId = swap.SourceChainId == 0 ? Context.ChainId : swap.SourceChainId
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
        ValidateCrossChainMetaData(input);

        var leafHashValue = EncodeMessageAndVerification(input.Message, out var amountInString, out var targetAddress,
            out var receiptIndex, out var receiptIdHash);
        Assert(decimal.TryParse(amountInString, out var amount), "Invalid amount.");
        var metadata = input.TokenTransferMetadata;
        var swapId = Hash.LoadFromByteArray(metadata.ExtraData.ToByteArray());
        ConsumeSwapAmount(swapId, amount);
        State.ReceiptHashRecordStatus[leafHashValue] = true;
        
        //Transfer token.
        var receiptId = $"{receiptIdHash.ToHex()}.{receiptIndex}";
        PerformTransferToken(swapId, targetAddress, amount, receiptId);

        return new Empty();
    }

    private void ValidateCrossChainMetaData(ForwardMessageInput input)
    {
        Assert(input.TargetChainId > 0 && input.TargetChainId == Context.ChainId, "Invalid target chain id.");
        var receiver = Address.Parser.ParseFrom(input.Receiver);
        Assert(receiver != null && receiver == Context.Self, "Invalid receiver.");
        Assert(input.Message != null, "Invalid message.");
        var chainConfigStr = State.CrossChainIdMap[(int)input.SourceChainId];
        var chainConfig = State.CrossChainConfigMap[chainConfigStr];
        Assert(chainConfig != null, "Not supported chain id.");
        Assert(input.SourceChainId > 0 && input.SourceChainId == chainConfig.ChainId, "Invalid source chain id.");
        var sender = chainConfig.ChainType switch
        {
            ChainType.Evm => input.Sender?.ToHex(true),
            ChainType.Tvm => input.Sender?.ToBase64(),
            _ => throw new AssertionException("Invalid chain type.")
        };
        Assert(sender != null && string.Compare(sender, chainConfig.ContractAddressForReceive, true) == 0,
            "Invalid sender.");
    }

    private Hash EncodeMessageAndVerification(ByteString message, out string amount, out Address targetAddress,
        out long receiptIndex, out Hash receiptIdTokenHash)
    {
        var messageByte = message.ToByteArray();
        var receiptIndexByte = messageByte.Skip(0).Take(32).ToArray();
        var receiptIdToken = messageByte.Skip(32).Take(32).ToArray();
        var amountByte = messageByte.Skip(64).Take(32).ToArray();
        var targetAddressByte = messageByte.Skip(96).Take(32).ToArray();
        var leafHash = messageByte.Skip(128).Take(32).ToArray();
        var leafHashValue = Hash.LoadFromHex(leafHash.ToHex());
        receiptIdTokenHash = Hash.LoadFromHex(receiptIdToken.ToHex());
        var amountHash = HashHelper.ComputeFrom(amountByte);
        var receiptIndexHash = HashHelper.ComputeFrom(receiptIndexByte);
        var receiptIdHash = HashHelper.ConcatAndCompute(receiptIdTokenHash, receiptIndexHash);
        var targetAddressByteHash = HashHelper.ComputeFrom(targetAddressByte);
        var computeHash = HashHelper.ConcatAndCompute(receiptIdHash, amountHash, targetAddressByteHash);
        Assert(leafHashValue == computeHash, "Invalid leaf hash.");
        Assert(State.ReceiptHashRecordStatus[leafHashValue] == false, "Leaf hash has been recorded.");
        amount = ParseHexToString(amountByte);
        targetAddress = Address.FromBytes(targetAddressByte);
        receiptIndex = ParseHexToLong(receiptIndexByte);
        return leafHashValue;
    }

    public override RateLimiterTokenBucket GetCurrentTokenSwapBucketState(GetCurrentTokenSwapBucketStateInput input)
    {
        var result = new RateLimiterTokenBucket();
        var amount = input.Amount;
        var swapId = input.SwapId;
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

    public override CrossChainConfig GetCrossChainConfig(StringValue input) =>
        State.CrossChainConfigMap[input.Value];

    public override StringValue GetChainIdMap(Int32Value input)
    {
        return new StringValue
        {
            Value = State.CrossChainIdMap[input.Value]
        };
    }

    public override Address GetRampContract(Empty input)
    {
        return State.RampContract.Value;
    }
}