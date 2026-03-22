using DecentralizedChat.PeerNode.Security;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DecentralizedChat.PeerNode
{
	/// <summary>
	/// Dynamically injects the Node's Identity and Public Key into the Swagger UI header.
	/// </summary>
	public class NodeInfoDocumentFilter : IDocumentFilter
	{
		private readonly NodeIdentityManager _rIdentityManager;

		public NodeInfoDocumentFilter(NodeIdentityManager identityManager)
		{
			_rIdentityManager = identityManager;
		}

		public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
		{
			// Retrieve the active identity details.
			string nodeId = _rIdentityManager.NodeId;
			string publicKey = _rIdentityManager.PublicKeyBase64;

			// Define the base static description.
			string baseDescription = "REST Gateway to securely communicate over the P2P gRPC network.";

			// Completely overwrite the description property on every page refresh.
			swaggerDoc.Info.Description =
				$"### Active Node ID: `{nodeId}`\n" +
				$"**Public Key (Base64):**\n```text\n{publicKey}\n```\n\n" +
				$"---\n{baseDescription}";
		}
	}
}
