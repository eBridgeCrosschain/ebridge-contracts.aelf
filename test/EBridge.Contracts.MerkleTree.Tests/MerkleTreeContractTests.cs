using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Types;
using EBridge.Contracts.ReceiptMakerContract;
using EBridge.Contracts.Regiment;
using EBridge.Contracts.TestContract.ReceiptMaker;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace EBridge.Contracts.MerkleTreeContract;

public partial class MerkleTreeContractTests : MerkleTreeContractTestBase
{
    private Hash _regimentId;

    [Fact]
    public async Task InitializeMerkleTreeTest()
    {
        await MerkleTreeContractStub.Initialize.SendAsync(new InitializeInput
        {
            Owner = DefaultSenderAddress,
            RegimentContractAddress = RegimentContractAddress
        });
        await RegimentContractStub.Initialize.SendAsync(new Regiment.InitializeInput
        {
            Controller = MerkleTreeContractAddress
        });
    }

    [Fact]
    public async Task InitializeMerkleTreeTest_Duplicate()
    {
        await InitializeMerkleTreeTest();
        var executionResult = await MerkleTreeContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Owner = DefaultSenderAddress,
            RegimentContractAddress = RegimentContractAddress
        });
        executionResult.TransactionResult.Error.ShouldContain("Already initialized.");
    }


    [Fact]
    public async Task ChangeOwnerTest()
    {
        await InitializeMerkleTreeTest();
        var owner = await MerkleTreeContractStub.GetContractOwner.CallAsync(new Empty());
        owner.ShouldBe(DefaultSenderAddress);
        await MerkleTreeContractStub.ChangeOwner.SendAsync(UserAddress);
        var newOwner = await MerkleTreeContractStub.GetContractOwner.CallAsync(new Empty());
        newOwner.ShouldBe(UserAddress);
    }

    [Fact]
    public async Task ChangeOwnerTest_NoPermission()
    {
        await InitializeMerkleTreeTest();
        var executionResult = await UserMerkleTreeContractStub.ChangeOwner.SendWithExceptionAsync(UserAddress);
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task InitialRegiment()
    {
        await InitializeMerkleTreeTest();
        var executionResult = await MerkleTreeContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = DefaultSenderAddress,
            IsApproveToJoin = true
        });
        _regimentId = RegimentCreated.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentId;
        var regimentAddress = RegimentCreated.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(RegimentCreated))
                .NonIndexed).RegimentAddress;
        await MerkleTreeContractStub.AddAdmins.SendAsync(new AddAdminsInput
        {
            RegimentAddress = regimentAddress,
            OriginSenderAddress = DefaultSenderAddress,
            NewAdmins = {DefaultSenderAddress}
        });

        var regiment = await RegimentContractStub.GetRegimentInfo.CallAsync(regimentAddress);
        regiment.Manager.ShouldBe(DefaultSenderAddress);
    }

    [Fact]
    public async Task DuplicateCreateRegiment()
    {
        await InitializeMerkleTreeTest();
        await MerkleTreeContractStub.CreateRegiment.SendAsync(new CreateRegimentInput
        {
            Manager = DefaultSenderAddress,
            IsApproveToJoin = true
        });
        var executionResult = await MerkleTreeContractStub.CreateRegiment.SendWithExceptionAsync(new CreateRegimentInput
        {
            Manager = DefaultSenderAddress,
            IsApproveToJoin = true
        });
        executionResult.TransactionResult.Error.ShouldContain("RegimentId already exists");
    }

    [Fact]
    public async Task<Hash> CreateSpaceTest()
    {
        await InitialRegiment();
        var executionResult = await MerkleTreeContractStub.CreateSpace.SendAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 4,
                Operator = _regimentId
            }
        });
        var spaceId = SpaceCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .First(l => l.Name == nameof(SpaceCreated)).NonIndexed).SpaceId;
        {
            var spaceInfo = await MerkleTreeContractStub.GetSpaceInfo.CallAsync(spaceId);
            spaceInfo.Operator.ShouldBe(_regimentId);
            spaceInfo.MaxLeafCount.ShouldBe(4);

            var spaceCount = await MerkleTreeContractStub.GetRegimentSpaceCount.CallAsync(_regimentId);
            spaceCount.Value.ShouldBe(1);
        }
        return spaceId;
    }

    [Fact]
    public async Task NextSpaceId_Test()
    {
        await InitialRegiment();
        var input = new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 4,
                Operator = _regimentId
            }
        };
        var spaceInfoMap = new Dictionary<Hash, Hash>();
        long id = 1;
        var spaceId =
            HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.Value.Operator), HashHelper.ComputeFrom(id));
        spaceInfoMap.GetOrDefault(spaceId).ShouldBe(null);
        spaceInfoMap[spaceId] = spaceId;
        
        long baseId = long.MaxValue >> 4;
        for (int i = 1; i <= 3; i++)
        {
            long nextId = baseId + 16 * (id - 1) + i;
            spaceId =
                HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.Value.Operator), HashHelper.ComputeFrom(nextId));
            if (spaceInfoMap.GetOrDefault(spaceId) == null)
            {
                break;
            }
        }
        spaceInfoMap.GetOrDefault(spaceId).ShouldBe(null);
    }

    [Fact]
    public async Task CreateSpace_LeafCountIncorrect()
    {
        await InitialRegiment();
        var executionResult = await MerkleTreeContractStub.CreateSpace.SendWithExceptionAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 0,
                Operator = _regimentId
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Incorrect leaf count.");
        executionResult = await MerkleTreeContractStub.CreateSpace.SendWithExceptionAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 2 << 20,
                Operator = _regimentId
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Incorrect leaf count.");
    }

    [Fact]
    public async Task CreateSpace_NoOperators()
    {
        await InitialRegiment();
        var executionResult = await MerkleTreeContractStub.CreateSpace.SendWithExceptionAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 3
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Not set regiment address.");
    }

    [Fact]
    public async Task CreateSpace_RegimentNotExist()
    {
        await InitialRegiment();
        var executionResult = await MerkleTreeContractStub.CreateSpace.SendWithExceptionAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 3,
                Operator = new Hash()
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("Regiment Address not exist.");
    }

    [Fact]
    public async Task CreateSpace_NoPermission()
    {
        await InitialRegiment();
        var executionResult = await UserMerkleTreeContractStub.CreateSpace.SendWithExceptionAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                MaxLeafCount = 3,
                Operator = _regimentId
            }
        });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task CreateSpace_MultipleSpace()
    {
        await CreateSpaceTest();
        var executionResult = await MerkleTreeContractStub.CreateSpace.SendAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                Operator = _regimentId,
                MaxLeafCount = 8
            }
        });
        var spaceId = (SpaceCreated.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .First(i => i.Name == nameof(SpaceCreated)).NonIndexed)).SpaceId;
        await MerkleTreeContractStub.CreateSpace.SendAsync(new CreateSpaceInput
        {
            Value = new SpaceInfo
            {
                Operator = _regimentId,
                MaxLeafCount = 16
            }
        });
        {
            var spaceCount = await MerkleTreeContractStub.GetRegimentSpaceCount.CallAsync(_regimentId);
            spaceCount.Value.ShouldBe(3);
            var spaceInfo = await MerkleTreeContractStub.GetSpaceInfo.CallAsync(spaceId);
            spaceInfo.MaxLeafCount.ShouldBe(8);
        }
    }

    [Fact]
    public async Task<Hash> CreateLeafNodeTest()
    {
        var spaceId = await CreateSpaceTest();
        await ReceiptMakerContractImplStub.CreateReceipt.SendAsync(new CreateReceiptInput
        {
            RecorderId = spaceId
        });
        var receiptCount = await ReceiptMakerContractImplStub.GetReceiptCount.CallAsync(spaceId);
        receiptCount.Value.ShouldBe(5);
        return spaceId;
    }

    [Fact]
    public async Task RecordMerkleTreeTest_NoPermission()
    {
        var spaceId = await CreateLeafNodeTest();
        var executionResult = await UserMerkleTreeContractStub.RecordMerkleTree.SendWithExceptionAsync(
            new RecordMerkleTreeInput
            {
                SpaceId = spaceId,
                LeafNodeHash =
                {
                    HashHelper.ComputeFrom("1234")
                }
            });
        executionResult.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task RecordMerkleTreeTest_SpaceIdIsNull()
    {
        await InitialRegiment();
        var executionResult = await UserMerkleTreeContractStub.RecordMerkleTree.SendWithExceptionAsync(
            new RecordMerkleTreeInput
            {
                SpaceId = new Hash(),
                LeafNodeHash =
                {
                    HashHelper.ComputeFrom("1234")
                }
            });
        executionResult.TransactionResult.Error.ShouldContain("Incorrect space id.");
    }

    [Fact]
    public async Task<Hash> RecordedMerkleTreeTest_NotFull()
    {
        var spaceId = await CreateLeafNodeTest();
        {
            var remainLeaf = await MerkleTreeContractStub.GetRemainLeafCount.CallAsync(spaceId);
            remainLeaf.Value.ShouldBe(4);
        }
        var receiptHashList = await ReceiptMakerContractImplStub.GetReceiptHashList.CallAsync(
            new GetReceiptHashListInput
            {
                SpaceId = spaceId,
                FirstLeafIndex = 0,
                LastLeafIndex = 0
            });
        var lastLeafIndex = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
        {
            SpaceId = spaceId
        });
        lastLeafIndex.Value.ShouldBe(-2);
        var executionResult = await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                receiptHashList.ReceiptHashList
            }
        });
        var log = MerkleTreeRecorded.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .First(l => l.Name == nameof(MerkleTreeRecorded)).NonIndexed);
        log.LastLeafIndex.ShouldBe(0);
        var lastTreeIndex = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
        lastTreeIndex.Value.ShouldBe(0);
        var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
        treeCount.Value.ShouldBe(1);
        var fullTreeCount = await MerkleTreeContractStub.GetFullTreeCount.CallAsync(spaceId);
        fullTreeCount.Value.ShouldBe(0);
        var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
        {
            SpaceId = spaceId,
            MerkleTreeIndex = 0
        });
        merkleTree.IsFullTree.ShouldBe(false);
        merkleTree.LastLeafIndex.ShouldBe(0);
        return spaceId;
    }

    [Fact]
    public async Task RecordMerkleTreeTest_FullOnlyOne()
    {
        var spaceId = await CreateLeafNodeTest();
        var receiptHashList = await ReceiptMakerContractImplStub.GetReceiptHashList.CallAsync(
            new GetReceiptHashListInput
            {
                SpaceId = spaceId,
                FirstLeafIndex = 0,
                LastLeafIndex = 3
            });
        var executionResult = await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                receiptHashList.ReceiptHashList
            }
        });
        var log = MerkleTreeRecorded.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .First(l => l.Name == nameof(MerkleTreeRecorded)).NonIndexed);
        log.LastLeafIndex.ShouldBe(3);
        var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
        treeCount.Value.ShouldBe(1);
        var lastLeaf = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
        {
            SpaceId = spaceId
        });
        lastLeaf.Value.ShouldBe(3);
        var lastMerkleTree = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
        lastMerkleTree.Value.ShouldBe(0);
        var isFull = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
        {
            SpaceId = spaceId,
            MerkleTreeIndex = lastMerkleTree.Value
        });
        isFull.IsFullTree.ShouldBe(true);
        var fullTreeCount = await MerkleTreeContractStub.GetFullTreeCount.CallAsync(spaceId);
        fullTreeCount.Value.ShouldBe(1);
        var leafLocated =
            await MerkleTreeContractStub.GetLeafLocatedMerkleTree.CallAsync(new GetLeafLocatedMerkleTreeInput
            {
                SpaceId = spaceId,
                LeafIndex = 2
            });
        leafLocated.FirstLeafIndex.ShouldBe(0);
        leafLocated.LastLeafIndex.ShouldBe(3);
        leafLocated.MerkleTreeIndex.ShouldBe(0);
    }

    [Fact]
    public async Task<Hash> RecordMerkleTreeTest_CustomizeMultipleTree()
    {
        var spaceId = await CreateSpaceTest();
        var lastIndex = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
        {
            SpaceId = spaceId
        });
        lastIndex.Value.ShouldBe(-2);
        await ReceiptMakerContractImplStub.CreateReceiptDiy.SendAsync(new CreateReceiptDiyInput
        {
            RecorderId = spaceId,
            ReceiptHash = new TestContract.ReceiptMaker.HashList
            {
                Value =
                {
                    HashHelper.ComputeFrom("111"),
                    HashHelper.ComputeFrom("222"),
                    HashHelper.ComputeFrom("333"),
                    HashHelper.ComputeFrom("444"),
                    HashHelper.ComputeFrom("555"),
                    HashHelper.ComputeFrom("666"),
                    HashHelper.ComputeFrom("777"),
                    HashHelper.ComputeFrom("888"),
                    HashHelper.ComputeFrom("999"),
                    HashHelper.ComputeFrom("000")
                }
            }
        });
        var receiptHashList = await ReceiptMakerContractImplStub.GetReceiptHashList.CallAsync(
            new GetReceiptHashListInput
            {
                SpaceId = spaceId,
                FirstLeafIndex = 0,
                LastLeafIndex = 9
            });
        await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                receiptHashList.ReceiptHashList
            }
        });
        var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
        treeCount.Value.ShouldBe(3);
        var fullTreeCount = await MerkleTreeContractStub.GetFullTreeCount.CallAsync(spaceId);
        fullTreeCount.Value.ShouldBe(2);
        var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
        {
            SpaceId = spaceId,
            MerkleTreeIndex = 2
        });
        merkleTree.IsFullTree.ShouldBe(false);
        merkleTree.LastLeafIndex.ShouldBe(9);
        var leafLocated = await MerkleTreeContractStub.GetLeafLocatedMerkleTree.CallAsync(
            new GetLeafLocatedMerkleTreeInput
            {
                SpaceId = spaceId,
                LeafIndex = 5
            });
        leafLocated.FirstLeafIndex.ShouldBe(4);
        leafLocated.LastLeafIndex.ShouldBe(7);
        {
            var lastTreeIndex = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastTreeIndex.Value.ShouldBe(2);
        }
        return spaceId;
    }

    [Fact]
    public async Task<Hash> RecordMerkleTreeTest_CustomizeOnlyOneLeaf()
    {
        var spaceId = await CreateSpaceTest();
        await ReceiptMakerContractImplStub.CreateReceiptDiy.SendAsync(new CreateReceiptDiyInput
        {
            RecorderId = spaceId,
            ReceiptHash = new TestContract.ReceiptMaker.HashList
            {
                Value =
                {
                    HashHelper.ComputeFrom("111")
                }
            }
        });
        var receiptHashList = await ReceiptMakerContractImplStub.GetReceiptHashList.CallAsync(
            new GetReceiptHashListInput
            {
                SpaceId = spaceId,
                FirstLeafIndex = 0,
                LastLeafIndex = 0
            });
        await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                receiptHashList.ReceiptHashList
            }
        });
        var last = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
        {
            SpaceId = spaceId
        });
        last.Value.ShouldBe(0);
        return spaceId;
    }

    [Fact]
    public async Task<Hash> RecordMerkleTreeTest_OneFullAndNotFull()
    {
        var spaceId = await CreateLeafNodeTest();
        var receiptHashList = await ReceiptMakerContractImplStub.GetReceiptHashList.CallAsync(
            new GetReceiptHashListInput
            {
                SpaceId = spaceId,
                FirstLeafIndex = 0,
                LastLeafIndex = 4
            });
        var executionResult = await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                receiptHashList.ReceiptHashList
            }
        });
        var log = MerkleTreeRecorded.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .First(l => l.Name == nameof(MerkleTreeRecorded)).NonIndexed);
        log.LastLeafIndex.ShouldBe(3);
        {
            var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
            treeCount.Value.ShouldBe(2);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 0
            });
            merkleTree.IsFullTree.ShouldBe(true);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 1
            });
            merkleTree.IsFullTree.ShouldBe(false);
        }
        {
            var lastLeaf = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeaf.Value.ShouldBe(4);
        }
        {
            var lastTree = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastTree.Value.ShouldBe(1);
        }
        {
            var leafLocated =
                await MerkleTreeContractStub.GetLeafLocatedMerkleTree.CallAsync(new GetLeafLocatedMerkleTreeInput
                {
                    SpaceId = spaceId,
                    LeafIndex = 4
                });
            leafLocated.FirstLeafIndex.ShouldBe(4);
            leafLocated.LastLeafIndex.ShouldBe(4);
            leafLocated.MerkleTreeIndex.ShouldBe(1);
        }
        return spaceId;
    }

    [Fact]
    public async Task<Hash> UpdateMerkleTreeTest_Full()
    {
        var spaceId = await RecordMerkleTreeTest_OneFullAndNotFull();
        await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                HashHelper.ComputeFrom("666"),
                HashHelper.ComputeFrom("777"),
                HashHelper.ComputeFrom("888")
            }
        });
        {
            var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
            treeCount.Value.ShouldBe(2);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 1
            });
            merkleTree.IsFullTree.ShouldBe(true);
        }
        {
            var lastLeaf = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeaf.Value.ShouldBe(7);
        }
        {
            var lastTree = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastTree.Value.ShouldBe(1);
        }
        {
            var leafLocated =
                await MerkleTreeContractStub.GetLeafLocatedMerkleTree.CallAsync(new GetLeafLocatedMerkleTreeInput
                {
                    SpaceId = spaceId,
                    LeafIndex = 6
                });
            leafLocated.FirstLeafIndex.ShouldBe(4);
            leafLocated.LastLeafIndex.ShouldBe(7);
            leafLocated.MerkleTreeIndex.ShouldBe(1);
        }

        return spaceId;
    }

    [Fact]
    public async Task UpdateMerkleTreeTest_NewOneAndUpdate()
    {
        var spaceId = await UpdateMerkleTreeTest_Full();
        {
            await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
            {
                SpaceId = spaceId,
                LeafNodeHash =
                {
                    HashHelper.ComputeFrom("123")
                }
            });
            var lastLeaf = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeaf.Value.ShouldBe(8);
        }
        {
            await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
            {
                SpaceId = spaceId,
                LeafNodeHash =
                {
                    HashHelper.ComputeFrom("456")
                }
            });
            var lastLeaf = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeaf.Value.ShouldBe(9);
        }
        {
            var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
            treeCount.Value.ShouldBe(3);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 2
            });
            merkleTree.IsFullTree.ShouldBe(false);
        }
    }

    [Fact]
    public async Task<Hash> UpdateMerkleTreeTest_FullAndNotFull()
    {
        var spaceId = await RecordMerkleTreeTest_OneFullAndNotFull();
        await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                HashHelper.ComputeFrom("666"),
                HashHelper.ComputeFrom("777"),
                HashHelper.ComputeFrom("888"),
                HashHelper.ComputeFrom("999"),
                HashHelper.ComputeFrom("000")
            }
        });
        {
            var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
            treeCount.Value.ShouldBe(3);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 1
            });
            merkleTree.IsFullTree.ShouldBe(true);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 2
            });
            merkleTree.IsFullTree.ShouldBe(false);
        }
        {
            var lastLeaf = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeaf.Value.ShouldBe(9);
        }
        {
            var lastTree = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastTree.Value.ShouldBe(2);
        }
        {
            var leafLocated =
                await MerkleTreeContractStub.GetLeafLocatedMerkleTree.CallAsync(new GetLeafLocatedMerkleTreeInput
                {
                    SpaceId = spaceId,
                    LeafIndex = 6
                });
            leafLocated.FirstLeafIndex.ShouldBe(4);
            leafLocated.LastLeafIndex.ShouldBe(7);
            leafLocated.MerkleTreeIndex.ShouldBe(1);
        }
        return spaceId;
    }

    [Fact]
    public async Task UpdateMerkleTreeTest_OnlyOneAndNotFull()
    {
        var spaceId = await RecordedMerkleTreeTest_NotFull();
        {
            var remainLeaf = await MerkleTreeContractStub.GetRemainLeafCount.CallAsync(spaceId);
            remainLeaf.Value.ShouldBe(3);
        }
        await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                HashHelper.ComputeFrom("666"),
                HashHelper.ComputeFrom("777")
            }
        });
        {
            var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
            treeCount.Value.ShouldBe(1);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 0
            });
            merkleTree.IsFullTree.ShouldBe(false);
        }
        {
            var lastMerkleTreeIndex = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastMerkleTreeIndex.Value.ShouldBe(0);
        }
        {
            var remainLeaf = await MerkleTreeContractStub.GetRemainLeafCount.CallAsync(spaceId);
            remainLeaf.Value.ShouldBe(1);
        }
        {
            var lastLeafIndex = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeafIndex.Value.ShouldBe(2);
        }
    }

    [Fact]
    public async Task UpdateMerkleTreeTest_OnlyOneFull()
    {
        var spaceId = await RecordedMerkleTreeTest_NotFull();
        {
            var remainLeaf = await MerkleTreeContractStub.GetRemainLeafCount.CallAsync(spaceId);
            remainLeaf.Value.ShouldBe(3);
        }
        await MerkleTreeContractStub.RecordMerkleTree.SendAsync(new RecordMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                HashHelper.ComputeFrom("555"),
                HashHelper.ComputeFrom("666"),
                HashHelper.ComputeFrom("777"),
                HashHelper.ComputeFrom("888"),
                HashHelper.ComputeFrom("999"),
                HashHelper.ComputeFrom("000")
            }
        });
        {
            var lastTreeIndex = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastTreeIndex.Value.ShouldBe(1);
        }
        {
            var treeCount = await MerkleTreeContractStub.GetMerkleTreeCountBySpace.CallAsync(spaceId);
            treeCount.Value.ShouldBe(2);
        }
        {
            var remainLeaf = await MerkleTreeContractStub.GetRemainLeafCount.CallAsync(spaceId);
            remainLeaf.Value.ShouldBe(1);
        }
        {
            var lastMerkleTreeIndex = await MerkleTreeContractStub.GetLastMerkleTreeIndex.CallAsync(spaceId);
            lastMerkleTreeIndex.Value.ShouldBe(1);
        }
        {
            var lastLeafIndex = await MerkleTreeContractStub.GetLastLeafIndex.CallAsync(new GetLastLeafIndexInput
            {
                SpaceId = spaceId
            });
            lastLeafIndex.Value.ShouldBe(6);
        }
        {
            var merkleTree = await MerkleTreeContractStub.GetMerkleTreeByIndex.CallAsync(new GetMerkleTreeByIndexInput
            {
                SpaceId = spaceId,
                MerkleTreeIndex = 1
            });
            merkleTree.IsFullTree.ShouldBe(false);
        }
    }

    [Fact]
    public async Task ConstructMerkleTreeTest()
    {
        var spaceId = await CreateSpaceTest();
        var merkleTreeList = await MerkleTreeContractStub.ConstructMerkleTree.CallAsync(new ConstructMerkleTreeInput
        {
            SpaceId = spaceId,
            LeafNodeHash =
            {
                HashHelper.ComputeFrom("111"),
                HashHelper.ComputeFrom("222"),
                HashHelper.ComputeFrom("333"),
                HashHelper.ComputeFrom("444"),
                HashHelper.ComputeFrom("555")
            }
        });
        merkleTreeList.Value.Count.ShouldBe(2);
        merkleTreeList.Value[0].FirstLeafIndex.ShouldBe(0);
        merkleTreeList.Value[0].LastLeafIndex.ShouldBe(3);
        merkleTreeList.Value[0].MerkleTreeIndex.ShouldBe(0);
        merkleTreeList.Value[0].IsFullTree.ShouldBe(true);
        merkleTreeList.Value[1].FirstLeafIndex.ShouldBe(4);
        merkleTreeList.Value[1].LastLeafIndex.ShouldBe(4);
        merkleTreeList.Value[1].MerkleTreeIndex.ShouldBe(1);
        merkleTreeList.Value[1].IsFullTree.ShouldBe(false);
    }

    [Fact]
    public async Task MerkleProofTest()
    {
        var spaceId = await RecordMerkleTreeTest_OneFullAndNotFull();
        var merklePath = await MerkleTreeContractStub.GetMerklePath.CallAsync(new GetMerklePathInput
        {
            SpaceId = spaceId,
            ReceiptMaker = ReceiptMakerContractAddress,
            LeafNodeIndex = 3
        });
        var receiptHash = await ReceiptMakerContractImplStub.GetReceiptHash.CallAsync(new GetReceiptHashInput
        {
            SpaceId = spaceId,
            ReceiptIndex = 3
        });
        var ifProof = await MerkleTreeContractStub.MerkleProof.CallAsync(new MerkleProofInput
        {
            SpaceId = spaceId,
            MerklePath = merklePath,
            LastLeafIndex = 3,
            LeafNode = receiptHash
        });
        ifProof.Value.ShouldBe(true);
    }

    [Fact]
    public async Task MerkleProofTest_Failed()
    {
        var spaceId = await RecordMerkleTreeTest_OneFullAndNotFull();
        var merklePath = await MerkleTreeContractStub.GetMerklePath.CallAsync(new GetMerklePathInput
        {
            SpaceId = spaceId,
            ReceiptMaker = ReceiptMakerContractAddress,
            LeafNodeIndex = 3
        });
        var receiptHash = await ReceiptMakerContractImplStub.GetReceiptHash.CallAsync(new GetReceiptHashInput
        {
            SpaceId = spaceId,
            ReceiptIndex = 4
        });
        var ifProof = await MerkleTreeContractStub.MerkleProof.CallAsync(new MerkleProofInput
        {
            SpaceId = spaceId,
            MerklePath = merklePath,
            LastLeafIndex = 3,
            LeafNode = receiptHash
        });
        ifProof.Value.ShouldBe(false);
    }

    [Fact]
    public async Task MerkleProofTest_NotInFirstTree()
    {
        var spaceId = await RecordMerkleTreeTest_OneFullAndNotFull();
        var merklePath = await MerkleTreeContractStub.GetMerklePath.CallAsync(new GetMerklePathInput
        {
            SpaceId = spaceId,
            ReceiptMaker = ReceiptMakerContractAddress,
            LeafNodeIndex = 4
        });
        var receiptHash = await ReceiptMakerContractImplStub.GetReceiptHash.CallAsync(new GetReceiptHashInput
        {
            SpaceId = spaceId,
            ReceiptIndex = 4
        });
        var ifProof = await MerkleTreeContractStub.MerkleProof.CallAsync(new MerkleProofInput
        {
            SpaceId = spaceId,
            MerklePath = merklePath,
            LastLeafIndex = 4,
            LeafNode = receiptHash
        });
        ifProof.Value.ShouldBe(true);
    }

    [Fact]
    public async Task MerkleProofTest_MultipleTree()
    {
        var spaceId = await RecordMerkleTreeTest_CustomizeMultipleTree();
        var merklePath = await MerkleTreeContractStub.GetMerklePath.CallAsync(new GetMerklePathInput
        {
            SpaceId = spaceId,
            ReceiptMaker = ReceiptMakerContractAddress,
            LeafNodeIndex = 8
        });
        var receiptHash = await ReceiptMakerContractImplStub.GetReceiptHash.CallAsync(new GetReceiptHashInput
        {
            SpaceId = spaceId,
            ReceiptIndex = 8
        });
        var ifProof = await MerkleTreeContractStub.MerkleProof.CallAsync(new MerkleProofInput
        {
            SpaceId = spaceId,
            MerklePath = merklePath,
            LastLeafIndex = 9,
            LeafNode = receiptHash
        });
        ifProof.Value.ShouldBe(true);
    }

    [Fact]
    public async Task MerkleProofTest_FirstTreeLastIndex()
    {
        var spaceId = await RecordMerkleTreeTest_CustomizeMultipleTree();
        var merklePath = await MerkleTreeContractStub.GetMerklePath.CallAsync(new GetMerklePathInput
        {
            SpaceId = spaceId,
            ReceiptMaker = ReceiptMakerContractAddress,
            LeafNodeIndex = 3
        });
        var receiptHash = await ReceiptMakerContractImplStub.GetReceiptHash.CallAsync(new GetReceiptHashInput
        {
            SpaceId = spaceId,
            ReceiptIndex = 3
        });
        var ifProof = await MerkleTreeContractStub.MerkleProof.CallAsync(new MerkleProofInput
        {
            SpaceId = spaceId,
            MerklePath = merklePath,
            LastLeafIndex = 3,
            LeafNode = receiptHash
        });
        ifProof.Value.ShouldBe(true);
    }

    [Fact]
    public async Task MerkleProofTest_SecondTreeFirstIndex()
    {
        var spaceId = await RecordMerkleTreeTest_CustomizeMultipleTree();
        var merklePath = await MerkleTreeContractStub.GetMerklePath.CallAsync(new GetMerklePathInput
        {
            SpaceId = spaceId,
            ReceiptMaker = ReceiptMakerContractAddress,
            LeafNodeIndex = 4
        });
        var receiptHash = await ReceiptMakerContractImplStub.GetReceiptHash.CallAsync(new GetReceiptHashInput
        {
            SpaceId = spaceId,
            ReceiptIndex = 4
        });
        var ifProof = await MerkleTreeContractStub.MerkleProof.CallAsync(new MerkleProofInput
        {
            SpaceId = spaceId,
            MerklePath = merklePath,
            LastLeafIndex = 6,
            LeafNode = receiptHash
        });
        ifProof.Value.ShouldBe(true);
    }
}