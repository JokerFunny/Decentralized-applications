using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GrpcSecure.ApiClient
{
	public class SessionInfoDocumentFilter : IDocumentFilter
	{
		private readonly SecureChatSession _session;

		// Inject the singleton session manager
		public SessionInfoDocumentFilter(SecureChatSession session)
		{
			_session = session;
		}

		public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
		{
			// Check if the handshake has completed
			string displayId = string.IsNullOrEmpty(_session.CurrentSessionId)
				? "Waiting for handshake..."
				: _session.CurrentSessionId;

			// Define base static description.
			string baseDescription = "Gateway to securely communicate with the backend.";

			// Completely overwrite the property on every refresh.
			swaggerDoc.Info.Description = $"### Active gRPC Session ID: `{displayId}`\n\n{baseDescription}";
		}
	}
}
