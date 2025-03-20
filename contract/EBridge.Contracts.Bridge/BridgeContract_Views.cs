using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    #region Permission

    public override Address GetContractController(Empty input)
    {
        return State.Controller.Value;
    }

    public override Address GetContractAdmin(Empty input)
    {
        return State.Admin.Value;
    }

    public override BoolValue IsContractPause(Empty input)
    {
        return new BoolValue
        {
            Value = State.IsContractPause.Value
        };
    }

    public override Address GetPauseController(Empty input)
    {
        return State.PauseController.Value;
    }

    public override Address GetRestartOrganization(Empty input)
    {
        return State.RestartOrganizationAddress.Value;
    }

    public override AuthorityInfo GetTransactionFeeRatioController(Empty input)
    {
        return State.FeeRatioController.Value;
    }

    #endregion


    #region Others to aelf

    public override SwapInfo GetSwapInfo(Hash input)
    {
        return State.SwapInfo[input];
    }

    public override SwapAmounts GetSwapAmounts(GetSwapAmountsInput input)
    {
        return State.Ledger[input.SwapId][input.ReceiptId];
    }

    public override SwappedReceiptInfo GetSwappedReceiptInfo(GetSwappedReceiptInfoInput input)
    {
        return State.RecorderReceiptInfoMap[input.SwapId][input.ReceiptId];
    }

    public override Hash GetSpaceIdBySwapId(Hash input)
    {
        return State.SwapSpaceIdMap[input];
    }

    public override Hash GetSwapIdByToken(GetSwapIdByTokenInput input)
    {
        return State.ChainTokenSwapIdMap[input.ChainId][input.Symbol];
    }

    public override SwapPairInfo GetSwapPairInfo(GetSwapPairInfoInput input)
    {
        return State.SwapPairInfoMap[input.SwapId][input.Symbol];
    }

    public override TokenSymbolList GetTokenWhitelist(StringValue input)
    {
        return State.ChainTokenWhitelist[input.Value] ?? new TokenSymbolList();
    }

    #endregion

    #region Transaction Fee

    public override Int64Value GetGasLimit(StringValue input)
    {
        return new Int64Value
        {
            Value = State.GasLimit[input.Value]
        };
    }

    public override Int64Value GetGasPrice(StringValue input)
    {
        return new Int64Value
        {
            Value = State.GasPrice[input.Value]
        };
    }

    public override Int64Value GetPriceRatio(StringValue input)
    {
        return new Int64Value
        {
            Value = State.PriceRatio[input.Value]
        };
    }

    public override StringValue GetFeeFloatingRatio(StringValue input)
    {
        return new StringValue
        {
            Value = State.FeeFloatingRatio[input.Value]
        };
    }

    public override Int64Value GetFeeByChainId(StringValue input)
    {
        if (!decimal.TryParse(State.FeeFloatingRatio[input.Value], out var floatingRatio))
        {
            floatingRatio = 1;
        }

        var fee = 0L;
        if (State.GasLimit[input.Value] == 0)
        {
            var tonFee = State.CrossChainConfigMap[input.Value]?.Fee;
            fee = CalculateTransactionFeeForTon(State.PriceRatio[input.Value], tonFee ?? 0);
        }
        else
        {
            fee = CalculateTransactionFee(State.GasLimit[input.Value], State.GasPrice[input.Value],
                State.PriceRatio[input.Value], floatingRatio);
        }

        return new Int64Value
        {
            Value = fee
        };
    }

    public override Int32Value GetPriceFluctuationRatio(StringValue input)
    {
        return new Int32Value { Value = State.PriceFluctuationRatio[input.Value] };
    }

    public override Int64Value GetCurrentTransactionFee(Empty input)
    {
        return new Int64Value { Value = State.TransactionFee.Value };
    }

    #endregion

    #region AElf to others

    public override Receipt GetReceiptInfo(StringValue input)
    {
        return State.ReceiptMap[input.Value];
    }

    public override ReceiptIdInfo GetReceiptIdInfo(Hash input)
    {
        return State.ReceiptIdInfoMap[input];
    }

    #endregion
}