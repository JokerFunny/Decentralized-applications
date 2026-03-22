using DecentralizedChat.PeerNode.Security;
using DecentralizedChat.PeerNode.Services;
using DecentralizedChat.PeerNode.Storage;
using Microsoft.AspNetCore.Mvc;
using Shared.Protos;

namespace DecentralizedChat.PeerNode.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class ChatController : ControllerBase
	{
		private readonly NodeIdentityManager _rIdentityManager;
		private readonly RegistryService.RegistryServiceClient _rRegistryClient;
		private readonly MessageStore _rMessageStore;
		private readonly P2PClientService _rP2PClient;

		public ChatController(NodeIdentityManager identityManager, RegistryService.RegistryServiceClient registryClient,
			MessageStore messageStore, P2PClientService p2pClient)
		{
			_rIdentityManager = identityManager;
			_rRegistryClient = registryClient;
			_rMessageStore = messageStore;
			_rP2PClient = p2pClient;
		}

		[HttpGet("info")]
		[Tags("1. System")]
		public IActionResult GetNodeInfo()
			=> Ok(new { _rIdentityManager.NodeId, _rIdentityManager.PublicKeyBase64 });

		[HttpGet("peers")]
		[Tags("2. P2P Communication")]
		public async Task<IActionResult> GetActivePeers()
		{
			var response = await _rRegistryClient.GetActiveNodesAsync(new GetNodesRequest { RequesterNodeId = _rIdentityManager.NodeId });
			return Ok(response.Peers);
		}

		// Fetching messages from dedicated storage service.
		[HttpGet("inbox")]
		[Tags("2. P2P Communication")]
		public IActionResult GetInbox()
			=> Ok(_rMessageStore.GetAllMessages());

		[HttpPost("send")]
		[Tags("2. P2P Communication")]
		public async Task<IActionResult> SendMessage([FromQuery] string targetNodeId, [FromBody] string messageText)
		{
			// 1. Find the target peer node.
			var response = await _rRegistryClient.GetActiveNodesAsync(
				new GetNodesRequest { RequesterNodeId = _rIdentityManager.NodeId });

			var targetPeer = response.Peers.FirstOrDefault(
				p => p.NodeId.Equals(targetNodeId, StringComparison.OrdinalIgnoreCase));

			if (targetPeer == null)
				return BadRequest($"Node '{targetNodeId}' is not registered or offline.");

			var (success, resultMessage) = await _rP2PClient.SendSecureMessageAsync(targetPeer, messageText);

			if (success)
				return Ok(resultMessage);

			return BadRequest(resultMessage);
		}
	}
}
