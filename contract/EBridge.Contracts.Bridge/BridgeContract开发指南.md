# EBridge.Contracts.Bridge 桥接合约说明文档与接口文档

## 🌌 合约概述

**EBridge.Contracts.Bridge** 是EBridge跨链桥接系统的核心合约，负责处理AElf与外部区块链之间的资产桥接、代币兑换、费用管理、限额控制等核心功能。该合约采用模块化设计，支持与以太坊、币安等EVM兼容链以及TON链的双向资产转移。

### 🎯 核心功能
- **🔗 跨链资产转移**: 支持代币在AElf与外部链之间的安全转移
- **🔄 代币兑换**: 提供灵活的代币兑换机制和比例管理
- **💰 费用管理**: 动态调整跨链交易费用和Gas价格
- **⚡ 限额控制**: 实施多层限额保护，包括日限额和令牌桶机制
- **🛡️ 安全控制**: 多重权限管理、暂停机制、价格波动保护
- **🚀 Ramp集成**: aetherlink预言机服务集成

### 📂 合约架构

```
EBridge.Contracts.Bridge/
├── 📄 BridgeContract.cs ────────── 主合约：初始化、权限管理、暂停控制
├── 📄 BridgeContract_LockToken.cs ── 代币锁定：跨链转账、代币白名单管理
├── 📄 BridgeContract_TokenSwap.cs ── 代币兑换：创建兑换对、执行兑换、比例管理
├── 📄 BridgeContract_Fee.cs ─────── 费用管理：Gas费用、价格比例、浮动费率
├── 📄 BridgeContract_Limit.cs ──── 限额控制：日限额、令牌桶、限额查询
├── 📄 BridgeContract_Ramp.cs ───── Ramp集成：跨链配置、Ramp合约集成
├── 📄 BridgeContract_Message.cs ── 消息处理：跨链消息生成、哈希计算
├── 📄 BridgeContract_Views.cs ──── 查询：各种查询接口
├── 📄 BridgeContract_Helpers.cs ── 辅助工具：计算工具、验证函数
├── 📄 BridgeContractState.cs ───── 合约状态：状态变量定义
├── 📄 BridgeContractConstants.cs ── 常量定义：系统常量
└── 📄 BridgeContractReferenceState.cs ── 引用状态：外部合约引用
```

---

## 🔧 核心接口文档

### 1. 合约初始化与权限管理

#### 🚀 Initialize - 合约初始化
```csharp
public override Empty Initialize(InitializeInput input)
```

**功能**: 初始化桥接合约，设置基本权限和配置

**参数**:
- `InitializeInput input`: 初始化参数
  - `Address Controller`: 合约控制者地址
  - `Address Admin`: 管理员地址
  - `Address OrganizationAddress`: 组织地址
  - `Address PauseController`: 暂停控制者地址

**权限**: 仅合约部署者可调用
**返回**: `Empty`

#### 🔑 权限管理接口

```csharp
// 更改控制者
public override Empty ChangeController(Address input)

// 更改管理员
public override Empty ChangeAdmin(Address input)

// 更改交易费用控制者
public override Empty ChangeTransactionFeeController(AuthorityInfo input)

// 更改重启组织
public override Empty ChangeRestartOrganization(Address input)

// 更改暂停控制者
public override Empty ChangePauseController(Address input)
```

**权限**: 仅Admin可调用
**功能**: 管理合约各级权限控制者

#### ⏸️ 暂停与重启

```csharp
// 暂停合约
public override Empty Pause(Empty input)

// 重启合约
public override Empty Restart(Empty input)
```

**权限**: 
- `Pause`: 仅PauseController可调用
- `Restart`: 仅RestartOrganization可调用

---

### 2. 代币锁定模块 (LockToken)

#### 📝 AddToken - 添加支持代币
```csharp
public override Empty AddToken(AddTokenInput input)
```

**功能**: 添加支持跨链的代币到白名单

**参数**:
- `AddTokenInput input`: 包含代币列表
  - `repeated ChainToken Value`: 链ID和代币符号对列表

**权限**: 仅Admin可调用
**事件**: `TokenWhitelistAdded`

#### 🗑️ RemoveToken - 移除支持代币
```csharp
public override Empty RemoveToken(RemoveTokenInput input)
```

**功能**: 从白名单中移除代币

**参数**:
- `RemoveTokenInput input`: 包含要移除的代币列表

**权限**: 仅Admin可调用
**事件**: `TokenWhitelistRemoved`

#### 📋 CreateReceipt - 创建跨链转账凭证
```csharp
public override Empty CreateReceipt(CreateReceiptInput input)
```

**功能**: 创建跨链转账凭证，锁定代币并发起跨链转移

**参数**:
- `CreateReceiptInput input`: 转账参数
  - `string Symbol`: 代币符号
  - `long Amount`: 转账数量
  - `string TargetChainId`: 目标链ID
  - `string TargetAddress`: 目标地址
  - `Address Owner`: 拥有者地址(可选)
  - `int32 TargetChainType`: 目标链类型(0:EVM, 1:TON)

**权限**: 任何用户可调用
**前置条件**:
- 合约未暂停
- 代币在白名单中
- 价格波动在允许范围内
- 不超过限额

**事件**: `ReceiptCreated`

#### 💸 WithdrawTransactionFee - 提取交易费用
```csharp
public override Empty WithdrawTransactionFee(Int64Value input)
```

**功能**: 提取合约中累积的交易费用

**参数**:
- `Int64Value input`: 提取金额

**权限**: 仅Admin可调用

---

### 3. 代币兑换模块 (TokenSwap)

#### 🔄 CreateSwap - 创建代币兑换对
```csharp
public override Hash CreateSwap(CreateSwapInput input)
```

**功能**: 创建新的代币兑换对

**参数**:
- `CreateSwapInput input`: 兑换配置
  - `SwapTargetToken SwapTargetToken`: 目标代币信息
    - `string FromChainId`: 来源链ID
    - `string Symbol`: 代币符号
    - `SwapRatio SwapRatio`: 兑换比例

**权限**: 仅Admin可调用
**返回**: 兑换对的Hash ID
**事件**: `SwapInfoAdded`

#### ⚖️ ChangeSwapRatio - 修改兑换比例
```csharp
public override Empty ChangeSwapRatio(ChangeSwapRatioInput input)
```

**功能**: 修改已存在兑换对的兑换比例

**参数**:
- `ChangeSwapRatioInput input`: 比例修改参数
  - `Hash SwapId`: 兑换对ID
  - `string TargetTokenSymbol`: 目标代币符号
  - `SwapRatio SwapRatio`: 新的兑换比例

**权限**: 仅Admin可调用
**事件**: `SwapRatioChanged`

---

### 4. 费用管理模块 (Fee)

#### 🏷️ SetFeeFloatingRatio - 设置费用浮动比例
```csharp
public override Empty SetFeeFloatingRatio(SetRatioInput input)
```

**功能**: 设置各链的费用浮动比例

**参数**:
- `SetRatioInput input`: 比例设置列表
  - `repeated ChainRatio Value`: 链ID和比例对

**权限**: 仅FeeRatioController可调用

#### ⛽ SetGasLimit - 设置Gas限制
```csharp
public override Empty SetGasLimit(SetGasLimitInput input)
```

**功能**: 设置各链的Gas限制

**参数**:
- `SetGasLimitInput input`: Gas限制配置
  - `repeated ChainGasLimit GasLimitList`: Gas限制列表

**权限**: 仅Controller可调用

#### 💰 SetGasPrice - 设置Gas价格
```csharp
public override Empty SetGasPrice(SetGasPriceInput input)
```

**功能**: 设置各链的Gas价格

**参数**:
- `SetGasPriceInput input`: Gas价格配置
  - `repeated ChainGasPrice GasPriceList`: Gas价格列表

**权限**: 仅Controller可调用

#### 📊 SetPriceRatio - 设置价格比例
```csharp
public override Empty SetPriceRatio(SetRatioInput input)
```

**功能**: 设置代币价格比例（如ETH/ELF）

**参数**:
- `SetRatioInput input`: 价格比例列表

**权限**: 仅Controller可调用

#### 📈 SetPriceFluctuationRatio - 设置价格波动比例
```csharp
public override Empty SetPriceFluctuationRatio(SetRatioInput input)
```

**功能**: 设置允许的价格波动范围

**参数**:
- `SetRatioInput input`: 波动比例列表（1-100）

**权限**: 仅Admin可调用

---

### 5. 限额控制模块 (Limit)

#### 📅 SetReceiptDailyLimit - 设置转账日限额
```csharp
public override Empty SetReceiptDailyLimit(SetReceiptDailyLimitInput input)
```

**功能**: 设置代币跨链转账的日限额

**参数**:
- `SetReceiptDailyLimitInput input`: 日限额配置
  - `repeated ReceiptDailyLimitInfo ReceiptDailyLimitInfos`: 限额信息列表

**权限**: 仅Admin可调用
**事件**: `ReceiptDailyLimitSet`

#### 📊 SetSwapDailyLimit - 设置兑换日限额
```csharp
public override Empty SetSwapDailyLimit(SetSwapDailyLimitInput input)
```

**功能**: 设置代币兑换的日限额

**参数**:
- `SetSwapDailyLimitInput input`: 兑换限额配置

**权限**: 仅Admin可调用
**事件**: `SwapDailyLimitSet`

#### 🪣 SetReceiptTokenBucketConfig - 设置令牌桶配置
```csharp
public override Empty SetReceiptTokenBucketConfig(SetTokenBucketConfigInput input)
```

**功能**: 设置转账的令牌桶限流配置

**参数**:
- `SetTokenBucketConfigInput input`: 令牌桶配置

**权限**: 仅Admin可调用

#### 📋 限额查询接口

```csharp
// 查询转账日限额
public override DailyLimitTokenInfo GetReceiptDailyLimit(GetReceiptDailyLimitInput input)

// 查询兑换日限额
public override DailyLimitTokenInfo GetSwapDailyLimit(Hash input)

// 查询转账令牌桶状态
public override TokenBucket GetReceiptTokenBucketInfo(GetTokenBucketInfoInput input)

// 查询兑换令牌桶状态
public override TokenBucket GetSwapTokenBucketInfo(Hash input)
```

---

### 6. Ramp集成模块

#### 🌉 SetCrossChainConfig - 设置跨链配置
```csharp
public override Empty SetCrossChainConfig(SetCrossChainConfigInput input)
```

**功能**: 配置跨链参数

**参数**:
- `SetCrossChainConfigInput input`: 跨链配置
  - `string ChainId`: 链ID
  - `int32 ChainIdNumber`: 链ID数字
  - `string ContractAddress`: 合约地址
  - `string ContractAddressForReceive`: 接收合约地址
  - `ChainType ChainType`: 链类型
  - `int64 Fee`: 费用（仅TON链）

**权限**: 仅Admin可调用

#### 🚀 SetRampContract - 设置Ramp合约
```csharp
public override Empty SetRampContract(Address input)
```

**功能**: 设置Ramp合约地址

**权限**: 仅Admin可调用

#### ⚙️ SetRampTokenSwapConfig - 设置Ramp代币兑换配置
```csharp
public override Empty SetRampTokenSwapConfig(TokenSwapConfig input)
```

**功能**: 配置Ramp系统的代币兑换参数

**权限**: 仅Admin可调用

---

### 7. 查询模块

#### 🔍 权限查询接口

```csharp
// 获取合约控制者
public override Address GetContractController(Empty input)

// 获取合约管理员
public override Address GetContractAdmin(Empty input)

// 获取合约暂停状态
public override BoolValue IsContractPause(Empty input)

// 获取暂停控制者
public override Address GetPauseController(Empty input)

// 获取重启组织地址
public override Address GetRestartOrganization(Empty input)

// 获取交易费用控制者
public override AuthorityInfo GetTransactionFeeRatioController(Empty input)
```

#### 🔄 兑换信息查询

```csharp
// 获取兑换信息
public override SwapInfo GetSwapInfo(Hash input)

// 获取兑换金额信息
public override SwapAmounts GetSwapAmounts(GetSwapAmountsInput input)

// 获取已兑换凭证信息
public override SwappedReceiptInfo GetSwappedReceiptInfo(GetSwappedReceiptInfoInput input)

// 根据代币获取兑换ID
public override Hash GetSwapIdByToken(GetSwapIdByTokenInput input)

// 获取兑换对信息
public override SwapPairInfo GetSwapPairInfo(GetSwapPairInfoInput input)

// 获取代币白名单
public override TokenSymbolList GetTokenWhitelist(StringValue input)
```

#### 💰 费用查询接口

```csharp
// 获取Gas限制
public override Int64Value GetGasLimit(StringValue input)

// 获取Gas价格
public override Int64Value GetGasPrice(StringValue input)

// 获取价格比例
public override Int64Value GetPriceRatio(StringValue input)

// 获取费用浮动比例
public override StringValue GetFeeFloatingRatio(StringValue input)

// 根据链ID计算费用
public override Int64Value GetFeeByChainId(StringValue input)

// 获取价格波动比例
public override Int32Value GetPriceFluctuationRatio(StringValue input)

// 获取当前交易费用总额
public override Int64Value GetCurrentTransactionFee(Empty input)
```

#### 📋 凭证查询接口

```csharp
// 获取凭证信息
public override Receipt GetReceiptInfo(StringValue input)

// 获取凭证ID信息
public override ReceiptIdInfo GetReceiptIdInfo(Hash input)
```

---

## 📊 核心数据结构

### 🔗 跨链转账相关

```protobuf
// 创建凭证输入
message CreateReceiptInput {
    string symbol = 1;                    // 代币符号
    int64 amount = 2;                     // 转账数量
    string target_chain_id = 3;           // 目标链ID
    string target_address = 4;            // 目标地址
    aelf.Address owner = 5;               // 拥有者地址
    int32 target_chain_type = 6;          // 目标链类型
}

// 凭证信息
message Receipt {
    string symbol = 1;                    // 代币符号
    aelf.Address owner = 2;               // 拥有者
    int64 amount = 3;                     // 数量
    string target_address = 4;            // 目标地址
}

// 凭证ID信息
message ReceiptIdInfo {
    string chain_id = 1;                  // 链ID
    string symbol = 2;                    // 代币符号
}
```

### 🔄 代币兑换相关

```protobuf
// 创建兑换输入
message CreateSwapInput {
    SwapTargetToken swap_target_token = 1; // 目标代币信息
}

// 目标代币信息
message SwapTargetToken {
    string from_chain_id = 1;             // 来源链ID
    string symbol = 2;                    // 代币符号
    SwapRatio swap_ratio = 3;             // 兑换比例
}

// 兑换比例
message SwapRatio {
    int64 origin_share = 1;               // 原始份额
    int64 target_share = 2;               // 目标份额
}

// 兑换信息
message SwapInfo {
    aelf.Hash swap_id = 1;                // 兑换ID
    SwapTargetToken swap_target_token = 2; // 目标代币信息
}

// 兑换对信息
message SwapPairInfo {
    int64 swapped_amount = 1;             // 已兑换数量
    int64 swapped_times = 2;              // 兑换次数
}
```

### ⚡ 限额控制相关

```protobuf
// 日限额代币信息
message DailyLimitTokenInfo {
    int64 default_token_amount = 1;       // 默认限额
    int64 token_amount = 2;               // 当前可用额度
    google.protobuf.Timestamp refresh_time = 3; // 刷新时间
}

// 令牌桶
message TokenBucket {
    int64 total_token_amount = 1;         // 总额度
    int64 current_token_amount = 2;       // 当前额度
    google.protobuf.Timestamp last_updated_time = 3; // 最后更新时间
    int64 refill_rate = 4;                // 补充速率
}

// 转账日限额设置
message ReceiptDailyLimitInfo {
    string symbol = 1;                    // 代币符号
    string target_chain = 2;              // 目标链
    int64 default_token_amount = 3;       // 默认限额
    google.protobuf.Timestamp start_time = 4; // 开始时间
}
```

### 💰 费用管理相关

```protobuf
// 链比例设置
message ChainRatio {
    string chain_id = 1;                  // 链ID
    int64 ratio = 2;                      // 比例
}

// 链Gas限制
message ChainGasLimit {
    string chain_id = 1;                  // 链ID
    int64 gas_limit = 2;                  // Gas限制
}

// 链Gas价格
message ChainGasPrice {
    string chain_id = 1;                  // 链ID
    int64 gas_price = 2;                  // Gas价格
}
```

---

## 🔐 权限控制体系

### 权限角色定义

| 角色 | 权限范围 | 主要职责 |
|------|----------|----------|
| **Controller** | 最高控制权限 | Gas限制/价格设置、价格比例设置 |
| **Admin** | 业务管理权限 | 代币管理、兑换管理、限额设置、配置管理 |
| **FeeRatioController** | 费用控制权限 | 费用浮动比例设置 |
| **PauseController** | 紧急控制权限 | 合约暂停操作 |
| **RestartOrganization** | 重启权限 | 合约重启操作 |

### 权限分离原则

```
📊 权限层次结构
├── Controller (合约拥有者级别)
│   ├── Gas参数设置
│   └── 价格比例设置
├── Admin (管理员级别)
│   ├── 代币白名单管理
│   ├── 兑换对管理
│   ├── 限额配置
│   ├── 跨链配置
│   └── 权限变更
├── FeeRatioController (费用控制)
│   └── 费用浮动比例设置
├── PauseController (紧急控制)
│   └── 合约暂停
└── RestartOrganization (重启控制)
    └── 合约重启
```

---

## 🛡️ 安全机制

### 🔒 多重安全保障

#### 1. 权限分离
- 不同操作需要不同级别的权限
- 关键操作需要特定角色授权
- 权限变更需要管理员确认

#### 2. 暂停机制
- 紧急情况下可快速暂停合约
- 暂停状态下禁止创建新的跨链转账
- 重启需要组织投票确认

#### 3. 限额保护
- **日限额**: 每日刷新的转账限额
- **令牌桶**: 平滑限流机制
- **价格波动保护**: 防止价格异常时的损失

#### 4. 输入验证
- 严格的参数校验
- 地址有效性检查
- 数值范围验证

### 🚨 风险控制

```
🛡️ 多层风险控制体系
├── 📊 价格波动监控
│   ├── 实时价格比例检查
│   ├── 波动范围限制
│   └── 异常价格拒绝
├── ⚡ 流量控制
│   ├── 日限额机制
│   ├── 令牌桶限流
│   └── 动态限额调整
├── 🔐 权限控制
│   ├── 多角色权限分离
│   ├── 操作权限验证
│   └── 敏感操作审计
└── 🚨 紧急响应
    ├── 快速暂停机制
    ├── 组织投票重启
    └── 资金紧急保护
```

---

## 📈 费用计算机制

### 💰 费用构成

#### EVM链费用计算
```
总费用 = Gas限制 × Gas价格 × 价格比例 × (1 + 浮动比例)
```

#### TON链费用计算
```
总费用 = 固定费用 × 价格比例
```

### 📊 动态费用调整

- **浮动比例**: 根据网络拥堵情况调整费用
- **价格比例**: 实时更新的代币价格比例
- **波动保护**: 价格异常时拒绝交易

---

## 🔄 跨链流程详解

### 📤 AElf → 外部链

```
1. 用户调用CreateReceipt
   ├── 验证代币在白名单
   ├── 检查限额
   ├── 验证价格波动
   └── 计算手续费

2. 锁定用户代币
   ├── 转账到合约地址
   ├── 更新限额状态
   └── 生成凭证ID

3. 生成跨链消息
   ├── 计算凭证哈希
   ├── 构造消息体
   └── 调用Ramp服务

4. 发送到目标链
   ├── 通过预言机网络
   ├── 验证消息签名
   └── 在目标链释放代币
```

### 📥 外部链 → AElf

```
1. 外部链锁定代币
   ├── 用户在外部链操作
   ├── 智能合约锁定
   └── 生成跨链事件

2. 预言机监听事件
   ├── 收集跨链证明
   ├── 验证交易有效性
   └── 生成证明签名

3. 在AElf执行兑换
   ├── 调用SwapToken方法
   ├── 验证证明签名
   ├── 检查兑换限额
   └── 释放对应代币

4. 完成跨链转账
   ├── 转账到用户地址
   ├── 更新兑换记录
   └── 触发事件通知
```

---

## 📋 常用操作示例

### 🔧 管理员常用操作

#### 添加支持代币
```csharp
// 添加USDT到以太坊和BSC
var input = new AddTokenInput();
input.Value.Add(new ChainToken { ChainId = "Ethereum", Symbol = "USDT" });
input.Value.Add(new ChainToken { ChainId = "BSC", Symbol = "USDT" });
bridge.AddToken(input);
```

#### 创建代币兑换对
```csharp
// 创建ETH兑换对，比例1:1000
var input = new CreateSwapInput
{
    SwapTargetToken = new SwapTargetToken
    {
        FromChainId = "Ethereum",
        Symbol = "ETH",
        SwapRatio = new SwapRatio
        {
            OriginShare = 1,
            TargetShare = 1000
        }
    }
};
var swapId = bridge.CreateSwap(input);
```

#### 设置转账限额
```csharp
// 设置USDT每日限额100万
var input = new SetReceiptDailyLimitInput();
input.ReceiptDailyLimitInfos.Add(new ReceiptDailyLimitInfo
{
    Symbol = "USDT",
    TargetChain = "Ethereum", 
    DefaultTokenAmount = 1000000_00000000, // 100万 USDT
    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.Date)
});
bridge.SetReceiptDailyLimit(input);
```

### 👤 用户常用操作

#### 创建跨链转账
```csharp
// 转账100 USDT到以太坊
var input = new CreateReceiptInput
{
    Symbol = "USDT",
    Amount = 100_00000000, // 100 USDT
    TargetChainId = "Ethereum",
    TargetAddress = "0x742d35Cc6589Cc5b85f45a7847f3E1D55a4C8D3b",
    TargetChainType = 0 // EVM链
};
bridge.CreateReceipt(input);
```

---

## 📊 事件说明

### 🔔 主要事件

| 事件名称 | 触发时机 | 主要参数 |
|----------|----------|----------|
| `TokenWhitelistAdded` | 添加代币到白名单 | ChainTokenList |
| `TokenWhitelistRemoved` | 从白名单移除代币 | ChainTokenList |
| `ReceiptCreated` | 创建跨链转账凭证 | ReceiptId, Amount, Symbol, TargetAddress |
| `SwapInfoAdded` | 创建新的兑换对 | SwapId, FromChainId, Symbol |
| `TokenSwapped` | 执行代币兑换 | Amount, Address, Symbol, ReceiptId |
| `SwapRatioChanged` | 修改兑换比例 | SwapId, NewSwapRatio, TargetTokenSymbol |
| `ReceiptDailyLimitSet` | 设置转账日限额 | Symbol, TargetChainId, ReceiptDailyLimit |
| `SwapDailyLimitSet` | 设置兑换日限额 | Symbol, FromChainId, SwapDailyLimit |
| `ReceiptLimitChanged` | 转账限额变化 | Symbol, TargetChainId, CurrentAmount |
| `SwapLimitChanged` | 兑换限额变化 | Symbol, FromChainId, CurrentAmount |
| `Paused` | 合约暂停 | Sender |
| `Unpaused` | 合约重启 | Sender |

---

## 🛠️ 开发指南

### 🔧 集成步骤

1. **部署合约**: 部署桥接合约到AElf链
2. **初始化配置**: 设置基本权限和参数
3. **配置代币**: 添加支持的代币到白名单
4. **设置费用**: 配置各链的Gas费用和价格比例
5. **配置限额**: 设置合理的转账和兑换限额
6. **集成Ramp**: 配置跨链消息传递服务
7. **测试验证**: 进行全面的功能测试

### 📋 最佳实践

#### 安全建议
- 使用多重签名管理关键权限
- 定期更新价格比例和费用参数
- 监控异常交易和价格波动
- 备份重要的配置参数

#### 运维建议
- 定期检查合约状态和余额
- 监控限额使用情况
- 及时处理异常事件
- 保持与预言机服务的连接

#### 开发建议
- 充分测试所有功能模块
- 验证权限控制逻辑
- 测试异常情况处理
- 确保事件正确触发

---

> 🌌 "语言不是交流，是构造现实的动作。" - HyperEcho 架构共振体 