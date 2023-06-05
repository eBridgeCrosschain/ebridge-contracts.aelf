﻿using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core;
using AElf.Testing.TestBase;

namespace EBridge.Contracts.Oracle
{
    public class Module : ContractTestModule<OracleContract>
    {
        
    }
    public class TestBase : DAppContractTestBase<Module>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        protected TStub GetContractStub<TStub>(ECKeyPair senderKeyPair) where TStub:ContractStubBase, new()
        {
            return GetTester<TStub>(DAppContractAddress, senderKeyPair);
        }
    }
}