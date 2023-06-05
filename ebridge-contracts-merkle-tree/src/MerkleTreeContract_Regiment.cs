using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContract
{
    public override Empty CreateRegiment(CreateRegimentInput input)
    {
        State.RegimentContract.CreateRegiment.Send(new Regiment.CreateRegimentInput
        {
            InitialMemberList = {input.InitialMemberList},
            IsApproveToJoin = input.IsApproveToJoin,
            Manager = Context.Sender
        });
        return new Empty();
    }

    public override Empty JoinRegiment(JoinRegimentInput input)
    {
        State.RegimentContract.JoinRegiment.Send(new Regiment.JoinRegimentInput
        {
            RegimentAddress = input.RegimentAddress,
            NewMemberAddress = input.NewMemberAddress,
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }

    public override Empty LeaveRegiment(LeaveRegimentInput input)
    {
        Assert(input.LeaveMemberAddress == Context.Sender, "No permission.");
        State.RegimentContract.LeaveRegiment.Send(new Regiment.LeaveRegimentInput
        {
            RegimentAddress = input.RegimentAddress,
            LeaveMemberAddress = input.LeaveMemberAddress,
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }

    public override Empty AddRegimentMember(AddRegimentMemberInput input)
    {
        State.RegimentContract.AddRegimentMember.Send(new Regiment.AddRegimentMemberInput
        {
            RegimentAddress = input.RegimentAddress,
            NewMemberAddress = input.NewMemberAddress,
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }

    public override Empty DeleteRegimentMember(DeleteRegimentMemberInput input)
    {
        State.RegimentContract.DeleteRegimentMember.Send(new Regiment.DeleteRegimentMemberInput
        {
            RegimentAddress = input.RegimentAddress,
            DeleteMemberAddress = input.DeleteMemberAddress,
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }

    public override Empty TransferRegimentOwnership(TransferRegimentOwnershipInput input)
    {
        State.RegimentContract.TransferRegimentOwnership.Send(new Regiment.TransferRegimentOwnershipInput
        {
            RegimentAddress = input.RegimentAddress,
            NewManagerAddress = input.NewManagerAddress,
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }

    public override Empty AddAdmins(AddAdminsInput input)
    {
        State.RegimentContract.AddAdmins.Send(new Regiment.AddAdminsInput
        {
            RegimentAddress = input.RegimentAddress,
            NewAdmins = {input.NewAdmins},
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }

    public override Empty DeleteAdmins(DeleteAdminsInput input)
    {
        State.RegimentContract.DeleteAdmins.Send(new Regiment.DeleteAdminsInput
        {
            RegimentAddress = input.RegimentAddress,
            DeleteAdmins = {input.DeleteAdmins},
            OriginSenderAddress = Context.Sender
        });
        return new Empty();
    }
}