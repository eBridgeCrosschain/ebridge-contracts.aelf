# ebridge-contracts.aelf

BRANCH | AZURE PIPELINES                                                                                                                                                                                                                                                                | TESTS                                                                                                                                                                                                                        | CODE COVERAGE
-------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|--------------
MASTER   | [![Build Status](https://dev.azure.com/eBridgeCrosschain/ebridge-contracts.aelf/_apis/build/status/eBridgeCrosschain.ebridge-contracts.aelf?branchName=master)](https://dev.azure.com/eBridgeCrosschain/ebridge-contracts.aelf/_build/latest?definitionId=2&branchName=master) | [![Test Status](https://img.shields.io/azure-devops/tests/eBridgeCrosschain/ebridge-contracts.aelf/2/master)](https://dev.azure.com/eBridgeCrosschain/ebridge-contracts.aelf/_build/latest?definitionId=2&branchName=master) | [![codecov](https://codecov.io/gh/eBridgeCrosschain/ebridge-contracts.aelf/branch/master/graph/badge.svg?token=KH9Z827QZE)](https://codecov.io/gh/eBridgeCrosschain/ebridge-contracts.aelf)
DEV    | [![Build Status](https://dev.azure.com/eBridgeCrosschain/ebridge-contracts.aelf/_apis/build/status/eBridgeCrosschain.ebridge-contracts.aelf?branchName=dev)](https://dev.azure.com/eBridgeCrosschain/ebridge-contracts.aelf/_build/latest?definitionId=2&branchName=dev)       | [![Test Status](https://img.shields.io/azure-devops/tests/eBridgeCrosschain/ebridge-contracts.aelf/2/dev)](https://dev.azure.com/eBridgeCrosschain/ebridge-contracts.aelf/_build/latest?definitionId=2&branchName=dev)       | [![codecov](https://codecov.io/gh/eBridgeCrosschain/ebridge-contracts.aelf/branch/dev/graph/badge.svg?token=KH9Z827QZE)](https://codecov.io/gh/eBridgeCrosschain/ebridge-contracts.aelf)

EBridge is an effortlessly Bridging You in and out of the aelf Ecosystem
## Installation

Before cloning the code and deploying the contract, command dependencies and development tools are needed. You can follow:

- [Common dependencies](https://aelf-boilerplate-docs.readthedocs.io/en/latest/overview/dependencies.html)
- [Building sources and development tools](https://aelf-boilerplate-docs.readthedocs.io/en/latest/overview/tools.html)

The following command will clone EBridge Contract into a folder. Please open a terminal and enter the following command:

```Bash
git clone https://github.com/eBridgeCrosschain/ebridge-contracts.aelf
```

The next step is to build the contract to ensure everything is working correctly. Once everything is built, you can run as follows:

```Bash
# enter the Launcher folder and build 
cd src/AElf.Boilerplate.BridgeContract.Launcher

# build
dotnet build

# run the node 
dotnet run
```

It will run a local temporary aelf node and automatically deploy the bridge contract on it. You can access the node from `localhost:1235`.

This temporary aelf node runs on a framework called Boilerplate for deploying smart contract easily. When running it, you might see errors showing incorrect password. To solve this, you need to back up your `aelf/keys`folder and start with an empty keys folder. Once you have cleaned the keys folder, stop and restart the node with `dotnet run`command shown above. It will automatically generate a new aelf account for you. This account will be used for running the aelf node and deploying the bridge contract.

### Test

You can easily run unit tests on bridge contracts. Navigate to the EBridge.Contracts.Bridge.Tests and run:

```Bash
cd ../../test/EBridge.Contracts.Bridge.Tests
dotnet test
```
