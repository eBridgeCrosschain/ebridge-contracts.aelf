# EBridge.Contracts.TokenPool 代币池合约说明文档与接口文档

## 🌌 合约概述

**EBridge.Contracts.TokenPool** 是EBridge跨链桥接系统的核心流动性管理合约，专门负责管理跨链代币的流动性池，为桥接操作提供代币锁定和释放服务。该合约采用虚拟地址机制，支持多代币流动性管理，并为流动性提供者提供参与激励机制。

### 🎯 核心功能
- **💧 流动性管理**: 统一管理多种代币的流动性池
- **🔒 代币锁定**: 为桥接合约提供代币锁定服务
- **🔓 代币释放**: 安全释放锁定的代币给用户
- **🏦 流动性提供**: 支持用户添加/移除流动性并获得收益
- **🔗 桥接集成**: 与桥接合约紧密集成，提供流动性支持
- **🛡️ 安全管理**: 简洁高效的权限控制机制

### 📂 合约架构

```
EBridge.Contracts.TokenPool/
├── 📄 TokenPoolContract.cs ─────────── 主合约：初始化、流动性管理、桥接服务
├── 📄 TokenPoolContract_View.cs ───── 视图查询：流动性查询、管理信息查询
├── 📄 TokenPoolContract_Helper.cs ── 辅助工具：参数验证、地址计算
├── 📄 TokenPoolContractState.cs ──── 合约状态：状态变量定义
├── 📄 TokenPoolContractReferenceState.cs ── 引用状态：外部合约引用
└── 📄 EBridge.Contracts.TokenPool.csproj ── 项目配置
```

### 🏗️ 技术架构

```
🏛️ TokenPool 技术架构
├── 🎯 虚拟地址机制
│   ├── 按 (ChainId + Symbol) 生成虚拟地址
│   ├── 隔离不同代币的流动性池
│   └── 支持多链多代币管理
├── 💧 流动性池管理
│   ├── 总流动性跟踪 (TokenLiquidity)
│   ├── 用户流动性跟踪 (LiquidityProviderBalances)
│   └── 动态流动性添加/移除
├── 🔗 桥接服务集成
│   ├── Lock: 为跨链锁定代币
│   ├── Release: 为跨链释放代币
│   └── Migrator: 数据迁移支持
└── 🛡️ 权限控制
    ├── Admin: 管理员权限
    ├── BridgeContract: 桥接合约权限
    └── Provider: 流动性提供者权限
```

---

## 🔧 核心接口文档

### 1. 合约初始化与管理

#### 🚀 Initialize - 合约初始化
```csharp
public override Empty Initialize(InitializeInput input)
```

**功能**: 初始化代币池合约，设置基本配置和权限

**参数**:
- `InitializeInput input`: 初始化参数
  - `Address BridgeContractAddress`: 桥接合约地址
  - `Address Admin`: 管理员地址(可选，默认为调用者)

**权限**: 仅未初始化时可调用
**前置条件**: 
- 合约未初始化
- 桥接合约地址有效

**返回**: `Empty`

#### 🔑 权限管理接口

```csharp
// 设置管理员
public override Empty SetAdmin(Address input)

// 设置桥接合约
public override Empty SetBridgeContract(Address input)
```

**权限**: 仅Admin可调用
**功能**: 管理合约核心权限控制

---

### 2. 流动性管理模块

#### 💧 AddLiquidity - 添加流动性
```csharp
public override Empty AddLiquidity(AddLiquidityInput input)
```

**功能**: 用户向代币池添加流动性

**参数**:
- `AddLiquidityInput input`: 添加流动性参数
  - `string TokenSymbol`: 代币符号
  - `int64 Amount`: 添加数量

**权限**: 任何用户可调用
**前置条件**:
- 合约已初始化
- 数量大于0
- 用户有足够的代币余额

**流程**:
1. 验证参数有效性
2. 生成代币虚拟地址
3. 更新用户流动性余额
4. 更新总流动性
5. 转账代币到虚拟地址
6. 触发 `LiquidityAdded` 事件

**事件**: `LiquidityAdded`

#### 🏦 RemoveLiquidity - 移除流动性
```csharp
public override Empty RemoveLiquidity(RemoveLiquidityInput input)
```

**功能**: 用户从代币池移除流动性

**参数**:
- `RemoveLiquidityInput input`: 移除流动性参数
  - `string TokenSymbol`: 代币符号
  - `int64 Amount`: 移除数量

**权限**: 任何用户可调用
**前置条件**:
- 合约已初始化
- 数量大于0
- 池中有足够流动性
- 用户有足够的流动性份额

**流程**:
1. 验证参数和余额
2. 减少用户流动性余额
3. 减少总流动性
4. 从虚拟地址转账代币给用户
5. 触发 `LiquidityRemoved` 事件

**事件**: `LiquidityRemoved`

---

### 3. 桥接服务模块

#### 🔒 Lock - 锁定代币
```csharp
public override Empty Lock(LockInput input)
```

**功能**: 为桥接合约锁定用户代币到流动性池

**参数**:
- `LockInput input`: 锁定参数
  - `Address Sender`: 发送者地址
  - `string TargetTokenSymbol`: 目标代币符号
  - `string TargetChainId`: 目标链ID
  - `int64 Amount`: 锁定数量

**权限**: 仅BridgeContract可调用
**前置条件**:
- 合约已初始化
- 数量大于0
- 发送者地址有效
- 目标链ID有效

**流程**:
1. 验证调用权限和参数
2. 生成代币虚拟地址
3. 增加池中流动性
4. 从桥接合约转账代币到虚拟地址
5. 触发 `Locked` 事件

**事件**: `Locked`

#### 🔓 Release - 释放代币
```csharp
public override Empty Release(ReleaseInput input)
```

**功能**: 为桥接合约从流动性池释放代币给用户

**参数**:
- `ReleaseInput input`: 释放参数
  - `Address Receiver`: 接收者地址
  - `string TargetTokenSymbol`: 目标代币符号
  - `string FromChainId`: 来源链ID
  - `int64 Amount`: 释放数量

**权限**: 仅BridgeContract可调用
**前置条件**:
- 合约已初始化
- 数量大于0
- 接收者地址有效
- 池中有足够流动性

**流程**:
1. 验证调用权限和参数
2. 检查池中流动性充足
3. 减少池中流动性
4. 从虚拟地址转账代币给接收者
5. 触发 `Released` 事件

**事件**: `Released`

#### 🔄 Migrator - 数据迁移(仅tokenpool合约刚部署时，迁移数据使用)
```csharp
public override Empty Migrator(MigratorInput input)
```

**功能**: 为合约升级或数据迁移提供支持

**参数**:
- `MigratorInput input`: 迁移参数
  - `Address Provider`: 流动性提供者
  - `string TokenSymbol`: 代币符号
  - `int64 DepositAmount`: 存款数量
  - `int64 LockAmount`: 锁定数量

**权限**: 仅BridgeContract可调用
**功能**: 批量迁移历史流动性数据

---

### 4. 视查询模块

#### 🔍 流动性查询接口

```csharp
// 获取代币池信息
public override TokenPoolInfo GetTokenPoolInfo(GetTokenPoolInfoInput input)

// 获取用户流动性
public override Int64Value GetLiquidity(GetLiquidityInput input)

// 获取可移除流动性
public override Int64Value GetRemovableLiquidity(GetLiquidityInput input)
```

**功能**: 查询代币池状态和用户流动性信息

#### 🏛️ 管理信息查询

```csharp
// 获取管理员地址
public override Address GetAdmin(Empty input)

// 获取桥接合约地址
public override Address GetBridgeContract(Empty input)
```

**功能**: 查询合约管理配置信息

---

## 📊 核心数据结构

### 💧 流动性管理相关

```protobuf
// 添加流动性输入
message AddLiquidityInput {
    string token_symbol = 1;             // 代币符号
    int64 amount = 2;                    // 添加数量
}

// 移除流动性输入
message RemoveLiquidityInput {
    string token_symbol = 1;             // 代币符号
    int64 amount = 2;                    // 移除数量
}

// 获取流动性输入
message GetLiquidityInput {
    string token_symbol = 1;             // 代币符号
    aelf.Address provider = 2;           // 提供者地址(可选)
}

// 代币池信息
message TokenPoolInfo {
    aelf.Hash token_virtual_hash = 1;    // 代币虚拟哈希
    aelf.Address token_virtual_address = 2; // 代币虚拟地址
    int64 liquidity = 3;                 // 总流动性
}
```

### 🔗 Bridge服务相关

```protobuf
// 锁定输入
message LockInput {
    aelf.Address sender = 1;             // 发送者地址
    string target_token_symbol = 2;      // 目标代币符号
    string target_chain_id = 3;          // 目标链ID
    int64 amount = 4;                    // 锁定数量
}

// 释放输入
message ReleaseInput {
    aelf.Address receiver = 1;           // 接收者地址
    string target_token_symbol = 2;      // 目标代币符号
    string from_chain_id = 3;            // 来源链ID
    int64 amount = 4;                    // 释放数量
}

// 迁移输入
message MigratorInput {
    aelf.Address provider = 1;           // 流动性提供者
    string token_symbol = 2;             // 代币符号
    int64 deposit_amount = 3;            // 存款数量
    int64 lock_amount = 4;               // 锁定数量
}
```

### 🏗️ 合约状态结构

```csharp
public partial class TokenPoolContractState : ContractState
{
    // 初始化状态
    public BoolState IsInitialized { get; set; }
    
    // 管理员地址
    public SingletonState<Address> Admin { get; set; }

    // 代币流动性映射：虚拟地址 => 流动性数量
    public MappedState<Address, long> TokenLiquidity { get; set; }

    // 流动性提供者余额：用户地址 => 虚拟地址 => 流动性数量
    public MappedState<Address, Address, long> LiquidityProviderBalances { get; set; }
}
```

---

## 🎯 虚拟地址机制

### 🔑 虚拟地址生成原理

```csharp
// 虚拟地址计算公式
tokenVirtualHash = HashHelper.ConcatAndCompute(
    HashHelper.ComputeFrom(ChainHelper.ConvertChainIdToBase58(Context.ChainId)),
    HashHelper.ComputeFrom(symbol)
);
tokenVirtualAddress = Context.ConvertVirtualAddressToContractAddress(tokenVirtualHash);
```

### 💡 设计优势

```
🎯 虚拟地址机制优势
├── 🔗 多代币隔离
│   ├── 每种代币有独立的虚拟地址
│   ├── 避免不同代币流动性混淆
│   └── 支持同名代币在不同链的区分
├── 🛡️ 安全管理
│   ├── 代币存储在虚拟地址中
│   ├── 合约拥有虚拟地址控制权
│   └── 防止直接访问代币资产
├── 📊 清晰追踪
│   ├── 每个虚拟地址对应一个代币池
│   ├── 流动性状态独立管理
│   └── 便于审计和监控
└── ⚡ 高效操作
    ├── 哈希计算确定性
    ├── 地址生成无需存储
    └── 支持动态代币添加
```

---

## 🔐 权限控制体系

### 权限角色定义

| 角色 | 权限范围 | 主要职责 |
|------|----------|----------|
| **Admin** | 管理员权限 | 设置管理员、设置桥接合约 |
| **BridgeContract** | 桥接服务权限 | 代币锁定、代币释放、数据迁移 |
| **LiquidityProvider** | 流动性提供权限 | 添加流动性、移除流动性 |

### 权限分离设计

```
🔐 权限控制架构
├── Admin (管理级别)
│   ├── SetAdmin - 变更管理员
│   └── SetBridgeContract - 设置桥接合约
├── BridgeContract (服务级别)
│   ├── Lock - 锁定代币服务
│   ├── Release - 释放代币服务
│   └── Migrator - 数据迁移服务
└── Anyone (用户级别)
    ├── AddLiquidity - 添加流动性
    ├── RemoveLiquidity - 移除流动性
    └── View Methods - 查询操作
```

---

## 💰 流动性经济模型

### 🏦 流动性池机制

#### 流动性贡献模式
```
💧 流动性池运作模式
├── 📈 流动性添加
│   ├── 用户存入代币到虚拟地址
│   ├── 记录用户流动性份额
│   ├── 增加池中总流动性
│   └── 获得流动性提供者身份
├── 🔄 桥接服务
│   ├── Lock: 桥接锁定增加池流动性
│   ├── Release: 桥接释放减少池流动性
│   └── 为跨链操作提供资金支持
└── 📉 流动性移除
    ├── 检查可移除流动性
    ├── 减少用户流动性份额
    ├── 减少池中总流动性
    └── 释放代币给用户
```

#### 可移除流动性计算
```csharp
// 可移除流动性 = Min(池中总流动性, 用户流动性份额)
removableLiquidity = Math.Min(tokenLiquidity, userLiquidity);
```

### 📊 流动性安全保障

- **余额验证**: 确保移除时有足够流动性
- **权限控制**: 只有流动性提供者可移除自己的份额
- **原子操作**: 所有流动性操作保证原子性
- **事件追踪**: 完整的流动性变化事件记录

---

## 🔄 与桥接合约的协作

### 🤝 服务接口集成

```
🌉 TokenPool ←→ Bridge 协作流程
├── 📤 AElf → 外部链
│   ├── Bridge.CreateReceipt 创建转账凭证
│   ├── TokenPool.Lock 锁定用户代币
│   ├── 增加池中流动性
│   └── 代币安全存储在虚拟地址
├── 📥 外部链 → AElf
│   ├── Bridge.SwapToken 执行代币兑换
│   ├── TokenPool.Release 释放池中代币
│   ├── 减少池中流动性
│   └── 代币安全转账给用户
└── 🔧 数据迁移
    ├── Bridge 调用 Migrator
    ├── 批量迁移历史数据
    └── 保证数据一致性
```

### 🛡️ 安全协作机制

- **权限验证**: 只有授权的桥接合约可调用服务接口
- **流动性检查**: 释放前确保池中有足够流动性
- **事件同步**: 桥接操作与流动性变化事件同步
- **原子操作**: 桥接与流动性操作保证原子性

---

## 📊 事件说明

### 🔔 主要事件

| 事件名称 | 触发时机 | 主要参数 |
|----------|----------|----------|
| `LiquidityAdded` | 用户添加流动性 | TokenSymbol, Amount, Provider |
| `LiquidityRemoved` | 用户移除流动性 | TokenSymbol, Amount, Provider |
| `Locked` | 桥接锁定代币 | Amount, FromChainId, ToChainId, Sender, TargetTokenSymbol |
| `Released` | 桥接释放代币 | Amount, FromChainId, ToChainId, Receiver, TargetTokenSymbol |

### 📈 事件应用场景

- **流动性监控**: 跟踪流动性提供者的操作
- **桥接审计**: 记录所有跨链代币锁定和释放
- **收益计算**: 为流动性提供者计算收益
- **异常检测**: 监控异常的流动性变化

---

## 📋 常用操作示例

### 🏦 流动性提供者操作

#### 添加流动性
```csharp
// 向USDT池添加10000 USDT流动性
var input = new AddLiquidityInput
{
    TokenSymbol = "USDT",
    Amount = 10000_00000000 // 10000 USDT
};
tokenPool.AddLiquidity(input);
```

#### 移除流动性
```csharp
// 从USDT池移除5000 USDT流动性
var input = new RemoveLiquidityInput
{
    TokenSymbol = "USDT", 
    Amount = 5000_00000000 // 5000 USDT
};
tokenPool.RemoveLiquidity(input);
```

#### 查询流动性
```csharp
// 查询用户在USDT池的流动性
var input = new GetLiquidityInput
{
    TokenSymbol = "USDT",
    Provider = userAddress // 可选，默认为调用者
};
var liquidity = tokenPool.GetLiquidity(input);
```

### 🔧 管理员操作

#### 设置桥接合约
```csharp
// 设置或更新桥接合约地址
var bridgeAddress = Address.FromBase58("2Ub1hQvGTY3c4SfLF5TBxm9jCDFqmP3WhGBkYHpvBhqNNNBZxM");
tokenPool.SetBridgeContract(bridgeAddress);
```

#### 查询池信息
```csharp
// 查询USDT代币池详细信息
var input = new GetTokenPoolInfoInput
{
    TokenSymbol = "USDT"
};
var poolInfo = tokenPool.GetTokenPoolInfo(input);
// poolInfo.Liquidity 为池中总流动性
// poolInfo.TokenVirtualAddress 为虚拟地址
```

### 🌉 Bridge合约集成

#### 锁定代币(由Bridge合约调用)
```csharp
// 为跨链转账锁定用户代币
var input = new LockInput
{
    Sender = userAddress,
    TargetTokenSymbol = "USDT",
    TargetChainId = "Ethereum",
    Amount = 1000_00000000 // 1000 USDT
};
tokenPool.Lock(input); // 只能由Bridge合约调用
```

#### 释放代币(由Bridge合约调用)
```csharp
// 为跨链兑换释放代币给用户
var input = new ReleaseInput
{
    Receiver = userAddress,
    TargetTokenSymbol = "USDT", 
    FromChainId = "Ethereum",
    Amount = 1000_00000000 // 1000 USDT
};
tokenPool.Release(input); // 只能由Bridge合约调用
```

---

## 🛠️ 开发指南

### 🔧 集成步骤

1. **部署合约**: 部署TokenPool合约到AElf链
2. **初始化配置**: 设置桥接合约地址和管理员
3. **准备流动性**: 流动性提供者添加初始流动性
4. **集成测试**: 测试与桥接合约的协作
5. **监控部署**: 部署监控和事件处理

### 📋 最佳实践

#### 流动性管理建议
- 鼓励多样化的流动性提供者参与
- 监控各代币池的流动性充足度
- 设置合理的流动性激励机制
- 定期检查虚拟地址余额一致性

#### 安全建议
- 严格控制桥接合约权限
- 定期审计流动性变化
- 监控异常的大额操作
- 备份重要的配置信息

#### 性能优化
- 合理设置Gas费用
- 优化批量操作流程
- 缓存常用查询结果
- 定期清理过期数据

---

## 🔍 故障排查指南

### 常见问题及解决方案

#### 流动性不足
```
❌ 问题: Pool liquidity is not enough
✅ 解决: 
  1. 检查池中总流动性
  2. 鼓励流动性提供者添加流动性
  3. 检查是否有大额锁定未释放
```

#### 权限错误
```
❌ 问题: No permission
✅ 解决:
  1. 确认调用者权限
  2. 检查桥接合约地址设置
  3. 验证管理员地址正确性
```

#### 虚拟地址异常
```
❌ 问题: Invalid symbol
✅ 解决:
  1. 检查代币符号格式
  2. 确认代币已在系统中注册
  3. 验证虚拟地址计算逻辑
```

---

> 🌌 "流动性不是静止的资产，而是跨链宇宙中价值流动的动态载体。" - HyperEcho 流动性共振体 