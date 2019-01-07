using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Loom.Client;
using Loom.Client.Samples;
using Loom.Nethereum.ABI.FunctionEncoding.Attributes;
using UnityEngine;
using UnityEngine.UI;

public class BlockchainTest : MonoBehaviour
{
	public Text deviceText;
	public Text blockchainText;

	public string key;
	public int value;
	
	public int blockchainValue;
	
	private Contract contract;
	private float timer;

	private void Update()
	{
		timer += Time.deltaTime;
	}

	public async void SendToBlockchain()
	{
		await CallContract(this.contract);
	}

	public async void ReceiveFromBlockchain()
	{
		await StaticCallContract(this.contract);
	}

	public void SetValue()
	{
		this.value++;
		UpdateDeviceText();
	}

	public async void CheckSetSend()
	{
		this.timer = 0f;
		await StaticCallContract(contract);
		if (this.blockchainValue != this.value)
		{
			this.blockchainText.text = "CHEATER IS DETECTED!";
			return;
		}
		SetValue();
		await CallContract(this.contract);
		UpdateBlockchainText(""+this.value);
		Debug.Log(this.timer);
	}
	
	private async Task CallContract(Contract contract)
	{
		await contract.CallAsync("SetMsg", new MapEntry
		{
			Key = this.key,
			Value = ""+this.value
		});
	}
	
	async Task StaticCallContract(Contract contract)
	{
		var result = await contract.StaticCallAsync<MapEntry>("GetMsg", new MapEntry
		{
			Key = this.key
		});

		if (result != null)
		{
			// This should print: { "key": "123", "value": "hello!" } in the Unity console window
			// provided `LoomQuickStartSample.CallContract()` was called first.
			Debug.Log("Smart contract returned: " + result.ToString());
			this.blockchainValue = Convert.ToInt32(result.Value);
			UpdateBlockchainText(""+this.blockchainValue);
		}
		else
		{
			throw new Exception("Smart contract didn't return anything!");
		}
	}

	public async void InitializeContract()
	{
		var privateKey = CryptoUtils.GeneratePrivateKey();
		var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
		var s ="";
		foreach (var p in privateKey)
		{
			s += p.ToString();
		}
		Debug.Log(s);
		contract = await GetContract(privateKey, publicKey);
	}
	
	private async Task<Contract> GetContract(byte[] privateKey, byte[] publicKey)
	{
		var writer = RpcClientFactory.Configure()
			.WithLogger(Debug.unityLogger)
			.WithHttp("http://127.0.0.1:46658/rpc")
			//.WithWebSocket("ws://127.0.0.1:46657/websocket")
			.Create();

		var reader = RpcClientFactory.Configure()
			.WithLogger(Debug.unityLogger)
			.WithHttp("http://127.0.0.1:46658/query")
			//.WithWebSocket("ws://127.0.0.1:9999/queryws")
			.Create();

		var client = new DAppChainClient(writer, reader)
		{
			Logger = Debug.unityLogger
		};
		// required middleware
		client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]{
			new NonceTxMiddleware(publicKey, client),
			new SignedTxMiddleware(privateKey)
		});

		var contractAddr = await client.ResolveContractAddressAsync("BluePrint");
		var callerAddr = Address.FromPublicKey(publicKey);
		return new Contract(client, contractAddr, callerAddr);
	}
	
	public async void InitializeContractEvm()
	{
		var privateKey = CryptoUtils.GeneratePrivateKey();
		var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);
		var s ="";
		foreach (var p in privateKey)
		{
			s += p.ToString();
		}
		Debug.Log(s);
		contractEvm = await GetContractEVM(privateKey, publicKey);
	}
	private EvmContract contractEvm;
	
	async Task<EvmContract> GetContractEVM(byte[] privateKey, byte[] publicKey)
	{
		var writer = RPCClientFactory.Configure()
			.WithLogger(Debug.unityLogger)
			.WithWebSocket("ws://127.0.0.1:46658/websocket")
			.Create();

		var reader = RPCClientFactory.Configure()
			.WithLogger(Debug.unityLogger)
			.WithWebSocket("ws://127.0.0.1:46658/queryws")
			.Create();

		var client = new DAppChainClient(writer, reader)
			{ Logger = Debug.unityLogger };

		// required middleware
		client.TxMiddleware = new TxMiddleware(new ITxMiddlewareHandler[]
		{
			new NonceTxMiddleware(publicKey, client),
			new SignedTxMiddleware(privateKey)
		});

		// ABI of the Solidity contract
		const string abi = "[{\"constant\":false,\"inputs\":[{\"name\":\"_tileState\",\"type\":\"string\"}],\"name\":\"SetTileMapState\",\"outputs\":[],\"payable\":false,\"stateMutability\":\"nonpayable\",\"type\":\"function\"},{\"constant\":true,\"inputs\":[],\"name\":\"GetTileMapState\",\"outputs\":[{\"name\":\"\",\"type\":\"string\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"},{\"anonymous\":false,\"inputs\":[{\"indexed\":false,\"name\":\"state\",\"type\":\"string\"}],\"name\":\"OnTileMapStateUpdate\",\"type\":\"event\"}]\r\n";
		// Note: With EVM based smart contracts, you can't access them by name.
		// Put the address of your deployed contract here.
		var contractAddr = Address.FromHexString("0xf420fbbb810698a74120df3723315ee06f472870");
		var callerAddr = Address.FromPublicKey(publicKey);

		return new EvmContract(client, contractAddr, callerAddr, abi);
	}

	public async Task CallContractEVM(EvmContract contract)
	{
		if (contract == null)
		{
			throw new Exception("Not signed in!");
		}
		Debug.Log("Calling smart contract...");

		await contract.CallAsync("SetTileMapState", "hello " + UnityEngine.Random.Range(0, 10000));

		Debug.Log("Smart contract method finished executing.");
	}
	
	public async Task StaticCallContractEVM(EvmContract contract)
	{
		if (contract == null)
		{
			throw new Exception("Not signed in!");
		}

		Debug.Log("Calling smart contract...");

		string result = await contract.StaticCallSimpleTypeOutputAsync<string>("GetTileMapState");
		if (result != null)
		{
			Debug.Log("Smart contract returned: " + result);
		} else
		{
			Debug.LogError("Smart contract didn't return anything!");
		}
	}
	
	public class OnTileMapStateUpdateEvent
	{
		[Parameter("string", "state", 1)]
		public string State { get; set; }
	}

	private void ContractEventReceived(object sender, EvmChainEventArgs e)
	{
		Debug.LogFormat("Received smart contract event: " + e.EventName);
		if (e.EventName == "OnTileMapStateUpdate")
		{
			OnTileMapStateUpdateEvent onTileMapStateUpdateEvent = e.DecodeEventDTO<OnTileMapStateUpdateEvent>();
			Debug.LogFormat("OnTileMapStateUpdate event data: " + onTileMapStateUpdateEvent.State);
		}
	}
	
	async void Start()
	{
		// The private key is used to sign transactions sent to the DAppChain.
		// Usually you'd generate one private key per player, or let them provide their own.
		// In this sample we just generate a new key every time.
		var privateKey = CryptoUtils.GeneratePrivateKey();
		var publicKey = CryptoUtils.PublicKeyFromPrivateKey(privateKey);

		// Connect to the contract
		var contractEvm = await GetContractEVM(privateKey, publicKey);
		// This should print something like: "hello 6475" in the Unity console window if some data is already stored
		await StaticCallContractEVM(contractEvm);
		// Listen for events
		contractEvm.EventReceived += ContractEventReceived;
		// Store the string in a contract
		await CallContractEVM(contractEvm);
	}
	
	private void UpdateBlockchainText(string value)
	{
		this.blockchainText.text = $"KEY: {this.key}\nVALUE: {value}";
	}
	
	private void UpdateDeviceText()
	{
		this.deviceText.text = $"KEY: {this.key}\nVALUE: {this.value}";
	}
}
