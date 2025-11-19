using System;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Controllers
{
	[ApiController]
	[Route("/api/v1/wiki")]
	public sealed class WikipediaController : ControllerBase
	{
		private readonly IWikipediaClient _wikipediaClient;
		private readonly ILogger<WikipediaController> _logger;

		public WikipediaController(IWikipediaClient wikipediaClient, ILogger<WikipediaController> logger)
		{
			_wikipediaClient = wikipediaClient ?? throw new ArgumentNullException(nameof(wikipediaClient));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		[HttpGet("british-rail-class")]
		public async Task<IActionResult> GetBritishRailClassAsync(
			[FromQuery(Name = "class")] string classIdentifier,
			CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(classIdentifier))
			{
				return BadRequest("Query parameter 'class' is required.");
			}

			try
			{
				var result = await _wikipediaClient.GetBritishRailClassAsync(classIdentifier, cancellationToken);

				if (result == null)
				{
					return NotFound();
				}

				return Ok(result);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve Wikipedia data for class {ClassIdentifier}", classIdentifier);
				return StatusCode(502, "Failed to retrieve Wikipedia data.");
			}
		}
	}
}

