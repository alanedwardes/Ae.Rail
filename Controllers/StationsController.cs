using Ae.Rail.Data;
using Ae.Rail.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Ae.Rail.Controllers
{
	[Route("/api/v1/stations")]
	public sealed class StationsController : ControllerBase
	{
		private readonly ILogger<StationsController> _logger;
		private readonly IStationCodeLookup _stationLookup;
		private readonly INationalRailApiClient _nationalRailClient;
		private readonly PostgresDbContext _dbContext;

		public StationsController(
			ILogger<StationsController> logger,
			IStationCodeLookup stationLookup,
			INationalRailApiClient nationalRailClient,
			PostgresDbContext dbContext)
		{
			_logger = logger;
			_stationLookup = stationLookup;
			_nationalRailClient = nationalRailClient;
			_dbContext = dbContext;
		}

		// GET /api/v1/stations/autocomplete?q=paddington&limit=10
		[HttpGet("autocomplete")]
		public IActionResult Autocomplete([FromQuery(Name = "q")] string q, [FromQuery] int limit = 10)
		{
			try
			{
				if (limit <= 0) limit = 10;
				if (limit > 50) limit = 50;

			var allStations = _stationLookup.GetAllRecords();
			
			IEnumerable<StationCodeRecord> results;
			
			if (string.IsNullOrWhiteSpace(q))
			{
				// Return popular stations if no query
				results = allStations
					.Where(s => !string.IsNullOrWhiteSpace(s.ThreeAlpha) && !string.IsNullOrWhiteSpace(s.NlcDesc))
					.OrderBy(s => s.NlcDesc)
					.Take(limit);
			}
			else
			{
				var searchTerm = q.Trim();
				
				// Search by CRS code (exact or starts with) or station name (contains)
				results = allStations
					.Where(s => !string.IsNullOrWhiteSpace(s.ThreeAlpha) && !string.IsNullOrWhiteSpace(s.NlcDesc))
					.Where(s => 
						s.ThreeAlpha.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.ThreeAlpha.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.NlcDesc.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.NlcDesc16.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
					.OrderBy(s => 
						// Exact match first
						s.ThreeAlpha.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ? 0 :
						// Starts with CRS second
						s.ThreeAlpha.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ? 1 :
						// Name starts with
						s.NlcDesc.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ? 2 :
						// Name contains
						3)
					.ThenBy(s => s.NlcDesc)
					.Take(limit);
			}

			return Ok(results.ToList());
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to execute station autocomplete for query {Query}", q);
				return StatusCode(500, "An error occurred while searching for stations.");
			}
		}

		// GET /api/v1/stations/board?crs=PAD&time=2025-11-17T05:00:00
		[HttpGet("board")]
		public async Task<IActionResult> GetStationBoard(
			[FromQuery(Name = "crs")] string crs,
			[FromQuery(Name = "time")] DateTime? time = null,
			[FromQuery] int numRows = 15,
			[FromQuery] int timeWindow = 120)
		{
			if (string.IsNullOrWhiteSpace(crs))
			{
				return BadRequest("Query parameter 'crs' (station code) is required.");
			}

			try
			{
				// Validate CRS and get station info
				var station = _stationLookup.GetByThreeAlpha(crs);
				if (station == null)
				{
					return NotFound($"Station with CRS code '{crs}' not found.");
				}

				// Use provided time or current time
				var boardTime = time ?? DateTime.UtcNow;

				_logger.LogInformation("Fetching station board for {Crs} ({StationName}) at {Time}", 
					crs, station.NlcDesc, boardTime);

				// Call National Rail API
				var board = await _nationalRailClient.GetArrDepBoardWithDetailsAsync(
					crs: crs.ToUpperInvariant(),
					time: boardTime,
					numRows: numRows,
					timeWindow: timeWindow
				);

				if (board == null)
				{
					return StatusCode(502, "Failed to retrieve data from National Rail API.");
				}

				// Enrich services with database information
				var enrichedServices = await EnrichServicesWithDbData(board.TrainServices);

			var response = new
			{
				station = station,
				board = new
				{
					generatedAt = board.GeneratedAt,
					locationName = board.LocationName,
					platformsAreHidden = board.PlatformsAreHidden,
					servicesAreUnavailable = board.ServicesAreUnavailable,
					nrccMessages = board.NrccMessages?.Select(m => new
					{
						category = m.Category?.ToString(),
						severity = m.Severity?.ToString(),
						message = m.XhtmlMessage
					}).ToList()
				},
				services = enrichedServices
			};

			return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve station board for {Crs}", crs);
				return StatusCode(500, "An error occurred while retrieving the station board.");
			}
		}

		private async Task<List<object>> EnrichServicesWithDbData(List<Models.NationalRail.ServiceItemWithLocations>? services)
		{
			if (services == null || services.Count == 0)
			{
				return new List<object>();
			}

			var result = new List<object>();

			// Get all train IDs to look up in one query
			var trainIds = services
				.Where(s => !string.IsNullOrWhiteSpace(s.TrainId))
				.Select(s => s.TrainId!)
				.Distinct()
				.ToList();

			// Batch lookup in database
			Dictionary<string, object> dbLookup = new();
			if (trainIds.Any())
			{
				var dbServices = await _dbContext.TrainServices
					.Where(ts => trainIds.Contains(ts.OperationalTrainNumber))
					.Select(ts => new
					{
						ts.OperationalTrainNumber,
						ts.ServiceDate,
						ts.OriginStd,
						ts.OriginLocationName,
						ts.DestLocationName,
						ts.RailClasses,
						ts.PowerType,
						ts.TrainOriginDateTime,
						ts.TrainDestDateTime
					})
					.ToListAsync();

				// Index by OperationalTrainNumber (may have multiple entries for same train on different dates)
				foreach (var dbService in dbServices)
				{
					if (!dbLookup.ContainsKey(dbService.OperationalTrainNumber))
					{
						dbLookup[dbService.OperationalTrainNumber] = dbService;
					}
				}
			}

			// Enrich each service
			foreach (var service in services)
			{
				object? dbData = null;
				if (!string.IsNullOrWhiteSpace(service.TrainId) && dbLookup.TryGetValue(service.TrainId, out var found))
				{
					dbData = found;
				}

				result.Add(new
				{
					// National Rail API data
					trainId = service.TrainId,
					rid = service.Rid,
					uid = service.Uid,
					rsid = service.Rsid,
					sdd = service.Sdd,
					@operator = service.Operator,
					operatorCode = service.OperatorCode,
					platform = service.Platform,
					platformIsHidden = service.PlatformIsHidden,
					
					// Times
					sta = service.Sta,
					eta = service.Eta,
					ata = service.Ata,
					std = service.Std,
					etd = service.Etd,
					atd = service.Atd,
					
					// Status
					isCancelled = service.IsCancelled,
					arrivalType = service.ArrivalType?.ToString(),
					departureType = service.DepartureType?.ToString(),
					
					// Journey - resolve to StationCodeRecord using CRS (3ALPHA) from National Rail API
					origin = service.Origin?.Select(o => _stationLookup.GetByThreeAlpha(o.Crs)).Where(s => s != null).ToList(),
					destination = service.Destination?.Select(d => _stationLookup.GetByThreeAlpha(d.Crs)).Where(s => s != null).ToList(),
					
					// Calling points
					previousLocations = service.PreviousLocations?.Select(l => new
					{
						station = _stationLookup.GetByThreeAlpha(l.Crs),
						sta = l.Sta,
						eta = l.Eta,
						ata = l.Ata,
						std = l.Std,
						etd = l.Etd,
						atd = l.Atd,
						platform = l.Platform,
						isCancelled = l.IsCancelled
					}).ToList(),
					subsequentLocations = service.SubsequentLocations?.Select(l => new
					{
						station = _stationLookup.GetByThreeAlpha(l.Crs),
						sta = l.Sta,
						eta = l.Eta,
						ata = l.Ata,
						std = l.Std,
						etd = l.Etd,
						atd = l.Atd,
						platform = l.Platform,
						isCancelled = l.IsCancelled
					}).ToList(),
					
					// Disruption info
					cancelReason = service.CancelReason,
					delayReason = service.DelayReason,
					adhocAlerts = service.AdhocAlerts,
					
					// Formation
					formation = service.Formation != null ? new
					{
						coaches = service.Formation.Coaches?.Select(c => new
						{
							number = c.Number,
							coachClass = c.CoachClass,
							toilet = c.Toilet?.Status?.ToString()
						}).ToList()
					} : null,
					
					// Database data (if available)
					dbData = dbData
				});
			}

			return result;
		}
	}
}

