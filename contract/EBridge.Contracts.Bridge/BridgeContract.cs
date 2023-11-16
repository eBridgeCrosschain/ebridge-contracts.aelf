using AElf.Sdk.CSharp;
using AElf.Standards.ACS3;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContract : BridgeContractImplContainer.BridgeContractImplBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(State.Controller.Value == null, "Already initialized.");
        Assert(State.IsInitialized.Value == false,"Already initialized.");
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        var author = State.GenesisContract.GetContractAuthor.Call(Context.Self);
        Assert(Context.Sender == author, "No permission.");
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        State.ParliamentContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);
        State.IsInitialized.Value = true;
        State.OracleContract.Value = input.OracleContractAddress;
        State.MerkleTreeContract.Value = input.MerkleTreeContractAddress;
        State.RegimentContract.Value = input.RegimentContractAddress;
        State.ReportContract.Value = input.ReportContractAddress;
        State.QueryPayment.Value = QueryPayment;

        State.Controller.Value = input.Controller;
        State.Admin.Value = input.Admin;
        State.FeeRatioController.Value = new AuthorityInfo
        {
            OwnerAddress = input.Admin,
            ContractAddress = State.ParliamentContract.Value
        };
        State.IsContractPause.Value = false;
        State.RestartOrganizationAddress.Value = input.OrganizationAddress;
        State.PauseController.Value = input.PauseController;
        return new Empty();
    }

    #region Permission

    public override Empty ChangeController(Address input)
    {
        Assert(Context.Sender == State.Admin.Value, $"No permission. Admin is {State.Admin.Value}. ");
        State.Controller.Value = input;
        return new Empty();
    }

    public override Empty ChangeAdmin(Address input)
    {
        Assert(Context.Sender == State.Admin.Value, $"No permission. Admin is {State.Admin.Value}. ");
        State.Admin.Value = input;
        return new Empty();
    }

    public override Empty ChangeTransactionFeeController(AuthorityInfo input)
    {
        Assert(State.FeeRatioController.Value != null,"Controller not set.");

        Assert(Context.Sender == State.Admin.Value, "No permission.");
        if (input.ContractAddress != null)
        {
            Assert(ValidateOrganizationExists(input.ContractAddress, input.OwnerAddress), "Invalid authority input.");
        }

        State.FeeRatioController.Value = input;
        return new Empty();
    }

    public override Empty ChangeRestartOrganization(Address input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(
            ValidateOrganizationExists(
                Context.GetContractAddressByName(SmartContractConstants.AssociationContractSystemName), input),
            "Organization is not exist.");
        State.RestartOrganizationAddress.Value = input;
        return new Empty();
    }

    public override Empty ChangePauseController(Address input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        State.PauseController.Value = input;
        return new Empty();
    }

    private bool ValidateOrganizationExists(Address contractAddress, Address ownerAddress)
    {
        return Context.Call<BoolValue>(contractAddress,
            nameof(AuthorizationContractContainer.AuthorizationContractReferenceState.ValidateOrganizationExist),
            ownerAddress).Value;
    }

    #endregion

    #region Pause/Restart

    public override Empty Pause(Empty input)
    {
        Assert(Context.Sender == State.PauseController.Value, "No permission.");
        Assert(!State.IsContractPause.Value, "Contract has already been paused.");
        State.IsContractPause.Value = true;
        Context.Fire(new Paused()
        {
            Sender = Context.Sender
        });
        return new Empty();
    }

    public override Empty Restart(Empty input)
    {
        Assert(Context.Sender == State.RestartOrganizationAddress.Value, "No permission.");
        Assert(State.IsContractPause.Value, "Contract has already been started.");
        State.IsContractPause.Value = false;
        Context.Fire(new Unpaused()
        {
            Sender = Context.Sender
        });
        return new Empty();
    }

    #endregion
    
}