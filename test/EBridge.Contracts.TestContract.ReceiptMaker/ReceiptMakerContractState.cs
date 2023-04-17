using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace EBridge.Contracts.TestContract.ReceiptMaker;

public class ReceiptMakerContractState : ContractState
{
    public SingletonState<ReceiptList> ReceiptList { get; set; }

    public MappedState<Hash, ReceiptIdArray> ReceiptIdListMap { get; set; }

    /// <summary>
    /// Recorder Id -> Receipt Count
    /// </summary>
    public MappedState<Hash, long> ReceiptCountMap { get; set; }

    /// <summary>
    /// Recorder Id -> Receipt Id -> Receipt Hash
    /// </summary>
    public MappedState<Hash, long, Hash> RecorderReceiptHashMap { get; set; }
}