using System;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Controllers
{
	/// <summary>
	/// Backoffice API controller for administrative operations
	/// </summary>
	[ApiController]
	[Route("backoffice")]
	public sealed class BackofficeController : ControllerBase
	{
		private readonly IReprocessingService _reprocessingService;
		private readonly ILogger<BackofficeController> _logger;

		public BackofficeController(IReprocessingService reprocessingService, ILogger<BackofficeController> logger)
		{
			_reprocessingService = reprocessingService;
			_logger = logger;
		}

		/// <summary>
		/// Triggers reprocessing of messages from the message_envelopes table.
		/// </summary>
		/// <param name="startTime">Optional start timestamp (ISO 8601 format) to filter messages (inclusive)</param>
		/// <param name="endTime">Optional end timestamp (ISO 8601 format) to filter messages (inclusive)</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Reprocessing statistics</returns>
		[HttpPost("reprocess")]
		public async Task<ActionResult<ReprocessingResult>> ReprocessMessages(
			[FromQuery] DateTime? startTime = null,
			[FromQuery] DateTime? endTime = null,
			CancellationToken cancellationToken = default)
		{
			_logger.LogInformation(
				"Reprocess request received (StartTime: {StartTime}, EndTime: {EndTime})",
				startTime?.ToString("o") ?? "null",
				endTime?.ToString("o") ?? "null");

			// Validate time range if both provided
			if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
			{
				return BadRequest(new { error = "startTime cannot be greater than endTime" });
			}

			try
			{
				var result = await _reprocessingService.ReprocessMessagesAsync(startTime, endTime, cancellationToken);
				return Ok(result);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Reprocessing failed");
				return StatusCode(500, new { error = "Reprocessing failed", message = ex.Message });
			}
		}

		/// <summary>
		/// Health check endpoint for the backoffice API
		/// </summary>
		[HttpGet("health")]
		public ActionResult<object> Health()
		{
			return Ok(new 
			{ 
				status = "healthy", 
				timestamp = DateTime.UtcNow,
				api = "backoffice"
			});
		}
	}
}

