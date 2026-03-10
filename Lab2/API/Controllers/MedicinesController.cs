using API.Services;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

namespace Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class MedicinesController : ControllerBase
	{
		private readonly IMessageProducer _rMessageProducer;
		private readonly ILogger<MedicinesController> _rLogger;

		// Injecting both the producer and the logger
		public MedicinesController(IMessageProducer messageProducer, ILogger<MedicinesController> logger)
		{
			_rMessageProducer = messageProducer;
			_rLogger = logger;
		}

		[HttpPost]
		public async Task<IActionResult> CreateMedicine([FromBody] MedicineRootDto request)
		{
			_rLogger.LogInformation("Received a POST request to create a new medicine record.");

			// 1. Basic Validation
			if (request?.Medicine == null || string.IsNullOrWhiteSpace(request.Medicine.Name))
			{
				_rLogger.LogWarning("Validation failed: Medicine object is null or Name is empty.");
				return BadRequest("Medicine data is invalid. Name is required.");
			}

			try
			{
				_rLogger.LogInformation("Processing medicine: {MedicineName}", request.Medicine.Name);

				// 2. Publish message asynchronously
				await _rMessageProducer.PublishMessageAsync(request);

				// 3. Return 202 Accepted
				return Accepted(new { Message = "Medicine data has been successfully queued." });
			}
			catch (Exception ex)
			{
				// In production, log this exception with all details
				_rLogger.LogError(ex, "An unexpected error occurred while processing the request for {MedicineName}.", request.Medicine?.Name);

				return StatusCode(500, $"Internal server error: {ex.Message}");
			}
		}
	}
}
