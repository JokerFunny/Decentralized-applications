using System.Collections.Concurrent;
using Grpc.Core;
using Shared.Protos;

namespace RegistryServer.Services;

public class RegistryServiceImpl : RegistryService.RegistryServiceBase
{
	// Thread-safe dictionary to store active nodes.
	// Key: NodeId, Value: PeerInfo (which contains Address and PublicKey)
	private static readonly ConcurrentDictionary<string, PeerInfo> s_rActiveNodes = new();
	private readonly ILogger<RegistryServiceImpl> _rLogger;

	public RegistryServiceImpl(ILogger<RegistryServiceImpl> logger)
	{
		_rLogger = logger;
	}

	public override Task<RegisterResponse> RegisterNode(RegisterRequest request, ServerCallContext context)
	{
		_rLogger.LogInformation("Attempting to register node: {NodeId} at {Address}", request.NodeId, request.Address);

		var newPeer = new PeerInfo
		{
			NodeId = request.NodeId,
			Address = request.Address,
			PublicKey = request.PublicKey
		};

		// Add or update the node in our dictionary
		s_rActiveNodes.AddOrUpdate(request.NodeId, newPeer, (key, existingVal) => newPeer);

		_rLogger.LogInformation("Node {NodeId} registered successfully. Total active nodes: {Count}", request.NodeId, s_rActiveNodes.Count);

		return Task.FromResult(new RegisterResponse
		{
			Success = true,
			Message = "Successfully registered on the tracker."
		});
	}

	public override Task<GetNodesResponse> GetActiveNodes(GetNodesRequest request, ServerCallContext context)
	{
		_rLogger.LogInformation("Node {NodeId} requested active peers list.", request.RequesterNodeId);

		var response = new GetNodesResponse();

		// Return all nodes EXCEPT the one making the request
		var peers = s_rActiveNodes.Values
			.Where(peer => peer.NodeId != request.RequesterNodeId)
			.ToList();

		response.Peers.AddRange(peers);

		return Task.FromResult(response);
	}

	public override Task<UnregisterResponse> UnregisterNode(UnregisterRequest request, ServerCallContext context)
	{
		_rLogger.LogInformation("Node {NodeId} is unregistering.", request.NodeId);

		var success = s_rActiveNodes.TryRemove(request.NodeId, out _);

		if (success)
		{
			_rLogger.LogInformation("Node {NodeId} removed. Remaining nodes: {Count}", request.NodeId, s_rActiveNodes.Count);
		}
		else
		{
			_rLogger.LogWarning("Failed to remove node {NodeId}. It might not exist.", request.NodeId);
		}

		return Task.FromResult(new UnregisterResponse
		{
			Success = success
		});
	}
}
