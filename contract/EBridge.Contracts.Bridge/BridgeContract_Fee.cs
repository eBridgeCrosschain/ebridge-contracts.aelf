using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract
{
    public override Empty SetFeeFloatingRatio(SetRatioInput input)
    {
        Assert(State.FeeRatioController.Value != null,"Controller not set.");
        Assert(Context.Sender == State.FeeRatioController.Value.OwnerAddress, "No permission.");
        foreach (var feeRatio in input.Value)
        {
            Assert(feeRatio.Ratio_ > 0, $"Incorrect fee floating ratio.{feeRatio.Ratio_}");
            State.FeeFloatingRatio[feeRatio.ChainId] = ((decimal) feeRatio.Ratio_ / 100 + 1).ToString();
        }

        return new Empty();
    }

    public override Empty SetGasLimit(SetGasLimitInput input)
    {
        Assert(Context.Sender == State.Controller.Value, $"No permission. {Context.Sender}");
        foreach (var gasLimit in input.GasLimitList)
        {
            Assert(gasLimit.GasLimit_ > 0, $"Incorrect gas limit.{gasLimit.GasLimit_}");
            State.GasLimit[gasLimit.ChainId] = gasLimit.GasLimit_;
        }

        return new Empty();
    }

    public override Empty SetGasPrice(SetGasPriceInput input)
    {
        Assert(Context.Sender == State.Controller.Value, $"No permission. {Context.Sender}");
        foreach (var gasPrice in input.GasPriceList)
        {
            Assert(gasPrice.GasPrice_ > 0, $"Incorrect gas price.{gasPrice.GasPrice_}");
            State.GasPrice[gasPrice.ChainId] = gasPrice.GasPrice_;
        }

        return new Empty();
    }

    public override Empty SetPriceRatio(SetRatioInput input)
    {
        Assert(Context.Sender == State.Controller.Value, $"No Permission. {Context.Sender}");
        foreach (var priceRatio in input.Value)
        {
            Assert(priceRatio.Ratio_ > 0, $"Incorrect price ratio.{priceRatio.Ratio_}");
            if (State.PrePriceRatio[priceRatio.ChainId] == 0)
            {
                State.PriceRatio[priceRatio.ChainId] = priceRatio.Ratio_;
                State.PrePriceRatio[priceRatio.ChainId] = priceRatio.Ratio_;
            }
            else
            {
                State.PrePriceRatio[priceRatio.ChainId] = State.PriceRatio[priceRatio.ChainId];
                State.PriceRatio[priceRatio.ChainId] = priceRatio.Ratio_;
            }
        }

        return new Empty();
    }

    public override Empty SetPriceFluctuationRatio(SetRatioInput input)
    {
        Assert(Context.Sender == State.Admin.Value, $"No Permission. {Context.Sender}");
        foreach (var ratio in input.Value)
        {
            Assert(ratio.Ratio_ is > 0 and <= 100, "Incorrect fluctuation.");
            State.PriceFluctuationRatio[ratio.ChainId] = (int)ratio.Ratio_;
        }
        return new Empty();
    }
}