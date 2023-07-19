using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace EBridge.Contracts.Report
{
    public partial class ReportContract
    {
        public override Empty ApplyObserver(ApplyObserverInput input)
        {
            State.ObserverMortgageTokenSymbol.Value ??=
                State.OracleContract.GetOracleTokenSymbol.Call(new Empty()).Value;
            var fee = State.ApplyObserverFee.Value == 0 ? DefaultApplyObserverFee : State.ApplyObserverFee.Value;
            var regimentCount = input.RegimentAddressList.Count;
            var totalApplyFee = fee.Mul(regimentCount);
            TransferTokenToSenderVirtualAddress(State.ObserverMortgageTokenSymbol.Value, totalApplyFee);
            foreach (var regimentAddress in input.RegimentAddressList)
            {
                Assert(IsRegimentMember(Context.Sender, regimentAddress),
                    $"Sender is not a member of regiment {regimentAddress}");
                var observerList = State.ObserverListMap[regimentAddress] ?? new ObserverList();
                Assert(!observerList.Value.Contains(Context.Sender),
                    $"Sender is already an observer for regiment {regimentAddress}");
                observerList.Value.Add(Context.Sender);
                State.ObserverListMap[regimentAddress] = observerList;
                State.ObserverInRegimentMortgagedTokensMap[regimentAddress][Context.Sender] = fee;
            }

            return new Empty();
        }

        public override Empty QuitObserver(QuitObserverInput input)
        {
            var currentLockingAmount = GetSenderVirtualAddressBalance(State.ObserverMortgageTokenSymbol.Value);
            foreach (var regimentAssociationAddress in input.RegimentAddressList)
            {
                var observerList = State.ObserverListMap[regimentAssociationAddress] ?? new ObserverList();
                Assert(observerList.Value.Contains(Context.Sender),
                    $"Sender is not an observer for regiment {regimentAssociationAddress}");
                observerList.Value.Remove(Context.Sender);
                State.ObserverListMap[regimentAssociationAddress] = observerList;
                var shouldReturnAmount = State.ObserverInRegimentMortgagedTokensMap[regimentAssociationAddress][Context.Sender];
                Assert(currentLockingAmount > 0 && currentLockingAmount >= shouldReturnAmount, "Insufficient amount");
                TransferTokenFromSenderVirtualAddress(State.ObserverMortgageTokenSymbol.Value, shouldReturnAmount);
                State.ObserverInRegimentMortgagedTokensMap[regimentAssociationAddress][Context.Sender] = 0;
            }

            return new Empty();
        }

        public override Empty MortgageTokens(MortgageTokensInput input)
        {
            var observerList = State.ObserverListMap[input.RegimentAddress] ?? new ObserverList();
            Assert(observerList.Value.Contains(Context.Sender),
                $"Sender is not an observer for regiment {input.RegimentAddress}");
            State.ObserverInRegimentMortgagedTokensMap[input.RegimentAddress][Context.Sender] =
                State.ObserverInRegimentMortgagedTokensMap[input.RegimentAddress][Context.Sender].Add(input.Amount);
            TransferTokenToSenderVirtualAddress(State.ObserverMortgageTokenSymbol.Value, input.Amount);
            return new Empty();
        }

        public override Empty AdjustApplyObserverFee(Int64Value input)
        {
            Assert(Context.Sender == State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
                "No permission.");
            State.ApplyObserverFee.Value = input.Value;
            return new Empty();
        }

        private void TransferTokenToSenderVirtualAddress(string symbol, long amount)
        {
            if (amount <= 0) return;
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(HashHelper.ComputeFrom(Context.Sender));
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                To = virtualAddress,
                Symbol = symbol,
                Amount = amount
            });
        }

        private void TransferTokenFromSenderVirtualAddress(string symbol, long amount)
        {
            if (amount <= 0) return;
            Context.SendVirtualInline(HashHelper.ComputeFrom(Context.Sender), State.TokenContract.Value,
                nameof(State.TokenContract.Transfer), new TransferInput
                {
                    To = Context.Sender,
                    Symbol = symbol,
                    Amount = amount
                }.ToByteString());
        }

        private long GetSenderVirtualAddressBalance(string symbol)
        {
            return GetVirtualAddressBalance(symbol, Context.Sender);
        }

        private long GetVirtualAddressBalance(string symbol, Address address)
        {
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(HashHelper.ComputeFrom(address));
            return State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = virtualAddress,
                Symbol = symbol
            }).Balance;
        }

        public override Empty AdjustReportFee(Int64Value input)
        {
            Assert(Context.Sender == State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
                "No permission.");
            State.ReportFee.Value = input.Value;
            return new Empty();
        }

        public override Int64Value GetMortgagedTokenAmount(Address input)
        {
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(HashHelper.ComputeFrom(input));
            return new Int64Value
            {
                Value = State.TokenContract.GetBalance.Call(new GetBalanceInput
                {
                    Owner = virtualAddress,
                    Symbol = State.ObserverMortgageTokenSymbol.Value
                }).Balance
            };
        }

        public override Int64Value GetObserverMortgagedTokenByRegiment(GetObserverMortgagedTokenByRegimentInput input)
        {
            var mortgagedToken = State.ObserverInRegimentMortgagedTokensMap[input.RegimentAddress][input.ObserverAddress];
            return new Int64Value
            {
                Value = mortgagedToken
            };
        }
    }
}