using System.Text.Json;
using Shared.DTOs;
using Worker.Services;

namespace Worker
{
	public class MedicineConsumerWorker : BackgroundService
	{
		private readonly ILogger<MedicineConsumerWorker> _rLogger;
		private readonly IMessageConsumer _rMessageConsumer;
		private readonly IMedicineService _rMedicineService;

		public MedicineConsumerWorker(
			ILogger<MedicineConsumerWorker> logger,
			IMessageConsumer messageConsumer,
			IMedicineService medicineService)
		{
			_rLogger = logger;
			_rMessageConsumer = messageConsumer;
			_rMedicineService = medicineService;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_rLogger.LogInformation("Starting MedicineConsumerWorker...");

			// Start listening and pass the processing method.
			await _rMessageConsumer.StartConsumingAsync(ProcessMessageAsync, stoppingToken);

			await Task.Delay(Timeout.Infinite, stoppingToken);
		}

		private async Task<bool> ProcessMessageAsync(string messageJson)
		{
			try
			{
				var dto = JsonSerializer.Deserialize<MedicineRootDto>(messageJson);

				if (dto?.Medicine == null)
				{
					_rLogger.LogWarning("Deserialized message is null or has invalid format.");
					return false; // Nack.
				}

				// Delegate the business logic to the service.
				await _rMedicineService.SaveMedicineAsync(dto.Medicine);

				return true; // Ack.
			}
			catch (Exception ex)
			{
				_rLogger.LogError(ex, "Error processing message and saving to DB.");
				return false; // Nack.
			}
		}
	}
}
