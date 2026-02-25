namespace GrpcSecure.ApiClient.DTO
{
	public class ChatRequest
	{
		public string TargetSessionId { get; set; } = string.Empty;
		public string Message { get; set; } = string.Empty;
	}
}
