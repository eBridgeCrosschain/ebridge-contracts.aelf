using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace EBridge.Contracts.Bridge;

public partial class BridgeContractState : ContractState
{
    /// <summary>
    /// Is initialized
    /// </summary>
    public BoolState IsInitialized { get; set; }

    /// <summary>
    /// Contract Controller.
    /// </summary>
    public SingletonState<Address> Controller { get; set; }

    /// <summary>
    /// Contract admin.
    /// </summary>
    public SingletonState<Address> Admin { get; set; }
    
    /// <summary>
    /// Method fee controller.
    /// </summary>
    public SingletonState<AuthorityInfo> MethodFeeController { get; set; }

    /// <summary>
    /// Controller who can approve transfer.
    /// </summary>
    public SingletonState<Address> ApproveTransferController { get; set; }
    
    /// <summary>
    /// Is contract pause (true->pause/false=>start).
    /// </summary>
    public SingletonState<bool> IsContractPause { get; set; }
    
    /// <summary>
    /// Organization address who can restart contract.
    /// </summary>
    public SingletonState<Address> RestartOrganizationAddress { get; set; }
    
    /// <summary>
    /// Controller who can pause the contract.
    /// </summary>
    public SingletonState<Address> PauseController { get; set; }
    
    /// <summary>
    /// The maximum amount of transfers per token.
    /// </summary>
    public MappedState<string, long> TokenMaximumAmount { get; set; }

    /// <summary>
    /// Contract method name -> MethodFees
    /// </summary>
    // internal MappedState<string, MethodFees> TransactionFees { get; set; }

    #region Others to AElf.

    /// <summary>
    /// Space Id -> Receipt Id -> Receipt Hash
    /// </summary>
    public MappedState<Hash, long, Hash> RecorderReceiptHashMap { get; set; }

    /// <summary>
    /// Space Id -> Receipt Count
    /// </summary>
    public MappedState<Hash,long> SpaceReceiptCountMap { get; set; }
    
    /// <summary>
    /// Swap Id -> Space Id
    /// </summary>
    public MappedState<Hash, Hash> SwapSpaceIdMap { get; set; }

    /// <summary>
    /// Chain id -> symbol -> swapId
    /// </summary>
    public MappedState<string, string, Hash> ChainTokenSwapIdMap { get; set; }

    /// <summary>
    /// Swap Id -> Swap Info
    /// </summary>
    public MappedState<Hash,SwapInfo> SwapInfo { get; set; } 
    
    /// <summary>
    /// Swap Id -> Receipt Id -> SwapAmounts(receiver + received amount)
    /// </summary>
    public MappedState<Hash, string, SwapAmounts> Ledger { get; set; }
    
    
    /// <summary>
    /// Swap Id -> Receipt Id -> Receipt Info
    /// </summary>
    public MappedState<Hash, string, SwappedReceiptInfo> RecorderReceiptInfoMap { get; set; }

    /// <summary>
    /// Swap Id -> Symbol -> SwapPairInfo
    /// </summary>
    public MappedState<Hash, string, SwapPairInfo> SwapPairInfoMap { get; set; }
    
    /// <summary>
    /// Swap Id -> Tree index
    /// </summary>
    public MappedState<Hash, long> RecordedTreeLeafIndex { get; set; }
    
    /// <summary>
    /// Receipt Id -> true/false(whether the receipt can be received)
    /// </summary>
    public MappedState<string, bool> ApproveTransfer { get; set; }

    #endregion

    #region AElf to others.

    /// <summary>
    /// payment
    /// </summary>
    public SingletonState<long> QueryPayment { get; set; }
    
    /// <summary>
    /// chain id -> token white list
    /// </summary>
    public MappedState<string,TokenSymbolList> ChainTokenWhitelist { get; set; }

    /// <summary>
    /// ReceiptId Token(chainId + symbol) -> Receipt count
    /// </summary>
    public MappedState<Hash,long> ReceiptCountMap { get; set; }

    /// <summary>
    /// Receipt Id -> Receipt
    /// </summary>
    public MappedState<string , Receipt> ReceiptMap { get; set; }

    /// <summary>
    /// ReceiptId(token) -> {chain_id + symbol} 
    /// </summary>
    public MappedState<Hash, ReceiptIdInfo> ReceiptIdInfoMap { get; set; }
    
    
    // To Others Chain Fee.
    /// <summary>
    /// The controller can change fee floating ratio.
    /// </summary>
    public SingletonState<AuthorityInfo> FeeRatioController { get; set; }
    /// <summary>
    /// ChainId -> Gas Fee
    /// </summary>
    public MappedState<string,long> GasLimit { get; set; }
    /// <summary>
    ///ChainId -> Gas Price
    /// </summary>
    public MappedState<string,long> GasPrice { get; set; }
    
    /// <summary>
    /// ChainId -> PriceRatio （ETH/ELF）
    /// </summary>
    public MappedState<string, long> PriceRatio { get; set; }
    
    /// <summary>
    /// ChainId -> Last PriceRatio（ETH/ELF）
    /// </summary>
    public MappedState<string,long> PrePriceRatio { get; set; }
    
    /// <summary>
    /// ChainId -> Fee floating ratio (1 -> default / 1.2 -> 20% extra fee)
    /// </summary>
    public MappedState<string, string> FeeFloatingRatio { get; set; }

    /// <summary>
    /// ChainId -> 10 (fluctuation：10%)
    /// </summary>
    public MappedState<string, int> PriceFluctuationRatio { get; set; }

    public SingletonState<long> TransactionFee { get; set; }

    #endregion
    
    /// <summary>
    /// Daily receipt limit per token.Refresh daily at 0:00
    /// token symbol -> target chain -> { amount,refresh time }
    /// </summary>
    public MappedState<string, string, DailyLimitTokenInfo> ReceiptDailyLimit { get; set; }
    
    /// <summary>
    /// Daily swap limit per token.Refresh daily at 0:00
    /// swap id -> { amount,refresh time }
    /// </summary>
    public MappedState<Hash, DailyLimitTokenInfo> SwapDailyLimit { get; set; }

    /// <summary>
    /// token symbol -> target chain -> token bucket
    /// </summary>
    public MappedState<string, string, TokenBucket> ReceiptTokenBucketInfo { get; set; }
    
    /// <summary>
    /// swap id -> token bucket
    /// </summary>
    public MappedState<Hash, TokenBucket> SwapTokenBucketInfo { get; set; }

    public SingletonState<long> DailyLimitRefreshTime { get; set; }

}