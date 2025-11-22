using Ae.Rail.Data;
using Ae.Rail.Services;
using Ae.Rail.Models;
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
				var normalizedSearchTerm = searchTerm.NormalizeForSearch();
				
				// Search by CRS code (exact or starts with) or station name (contains)
				results = allStations
					.Where(s => !string.IsNullOrWhiteSpace(s.ThreeAlpha) && !string.IsNullOrWhiteSpace(s.NlcDesc))
					.Where(s => 
						s.ThreeAlpha.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.ThreeAlpha.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.NlcDesc.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.NlcDesc16.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.NlcDesc.NormalizeForSearch().Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase) ||
						s.NlcDesc16.NormalizeForSearch().Contains(normalizedSearchTerm, StringComparison.OrdinalIgnoreCase))
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
				// Ensure boardTime is UTC - if time is provided but has Unspecified kind (from datetime-local),
				// treat it as UTC since datetime-local doesn't include timezone info
				var boardTime = time.HasValue 
					? (time.Value.Kind == DateTimeKind.Unspecified 
						? DateTime.SpecifyKind(time.Value, DateTimeKind.Utc)
						: time.Value.ToUniversalTime())
					: DateTime.UtcNow;

				_logger.LogInformation("Fetching station board for {Crs} ({StationName}) at {Time} (UTC) with window {Window} minutes", 
					crs, station.NlcDesc, boardTime, timeWindow);

				// 1. Fetch API data (optional/can fail)
				Models.NationalRail.StationBoardWithDetails? board = null;
				try
				{
					board = await _nationalRailClient.GetArrDepBoardWithDetailsAsync(
						crs: crs.ToUpperInvariant(),
						time: boardTime,
						numRows: numRows,
						timeWindow: timeWindow
					);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to retrieve data from National Rail API for {Crs}", crs);
				}

				// 2. Fetch DB data (consist data)
				// Filter services within the time window
				var dbServices = await GetDbServices(station, boardTime, timeWindow);

				// 3. Merge API and DB data
				// This handles deduplication: if in API, use API + DB enrich. If only in DB, use DB.
				var mergedServices = MergeServices(board?.TrainServices, dbServices, station, boardTime, timeWindow);

			var response = new
			{
				station = station,
				board = new
				{
					generatedAt = board?.GeneratedAt ?? DateTime.UtcNow,
					locationName = board?.LocationName ?? station.NlcDesc,
					platformsAreHidden = board?.PlatformsAreHidden ?? false,
					servicesAreUnavailable = (board?.ServicesAreUnavailable ?? true) && mergedServices.Count == 0,
					nrccMessages = board?.NrccMessages?.Select(m => new
					{
						category = m.Category?.ToString(),
						severity = m.Severity?.ToString(),
						message = m.XhtmlMessage
					}).ToList()
				},
				services = mergedServices
			};

			return Ok(response);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve station board for {Crs}", crs);
				return StatusCode(500, "An error occurred while retrieving the station board.");
			}
		}

		private async Task<List<TrainService>> GetDbServices(StationCodeRecord station, DateTime boardTime, int timeWindowMinutes)
		{
			if (string.IsNullOrWhiteSpace(station.Tiploc))
			{
				return new List<TrainService>();
			}

			var serviceDate = boardTime.ToString("yyyy-MM-dd");
			var tiploc = station.Tiploc;
			var windowStart = boardTime;
			var windowEnd = boardTime.AddMinutes(timeWindowMinutes);

			// Fetch all services for the day that touch this station
			var allServices = await _dbContext.TrainServices
				.AsNoTracking()
				.Where(ts => ts.ServiceDate == serviceDate && 
							(ts.OriginLocationName == tiploc || ts.DestLocationName == tiploc))
				.ToListAsync();

			// Filter services where the scheduled time at this station falls within the window
			var filteredServices = allServices.Where(ts =>
			{
				DateTime? scheduledTime = null;

				if (ts.OriginLocationName == tiploc && ts.TrainOriginDateTime.HasValue)
				{
					scheduledTime = ts.TrainOriginDateTime.Value;
				}
				else if (ts.DestLocationName == tiploc && ts.TrainDestDateTime.HasValue)
				{
					scheduledTime = ts.TrainDestDateTime.Value;
				}

				if (!scheduledTime.HasValue)
					return false;

				return scheduledTime.Value >= windowStart && scheduledTime.Value <= windowEnd;
			}).OrderBy(ts => ts.OriginStd).ToList();

			return filteredServices;
		}

		private List<object> MergeServices(
			List<Models.NationalRail.ServiceItemWithLocations>? apiServices,
			List<TrainService> dbServices,
			StationCodeRecord station,
			DateTime boardTime,
			int timeWindowMinutes)
		{
			var result = new List<object>();
			var dbLookup = new Dictionary<string, TrainService>();
			
			// Index DB services
			foreach (var dbSvc in dbServices)
			{
				var key = $"{dbSvc.OperationalTrainNumber}|{dbSvc.ServiceDate}|{dbSvc.OriginStd}";
				// Use TryAdd to avoid duplicate keys issues if dirty data
				if (!dbLookup.ContainsKey(key))
				{
					dbLookup[key] = dbSvc;
				}
			}

			var usedDbIds = new HashSet<long>();

			// Process API services
			if (apiServices != null)
			{
				foreach (var service in apiServices)
				{
					TrainService? dbData = null;
					string? originStd = null;

					if (!string.IsNullOrWhiteSpace(service.TrainId) && service.Sdd.HasValue)
					{
						var serviceDate = service.Sdd.Value.ToString("yyyy-MM-dd");
						originStd = GetOriginStdFromService(service);
						
						if (!string.IsNullOrEmpty(originStd))
						{
							var key = $"{service.TrainId}|{serviceDate}|{originStd}";
							if (dbLookup.TryGetValue(key, out dbData))
							{
								usedDbIds.Add(dbData.Id);
							}
						}
					}

					// Get origin/destination stations from TIPLOC codes if dbData available
					var originStationFromDb = dbData != null && !string.IsNullOrWhiteSpace(dbData.OriginLocationName)
						? _stationLookup.GetByTiploc(dbData.OriginLocationName)
						: null;
					var destStationFromDb = dbData != null && !string.IsNullOrWhiteSpace(dbData.DestLocationName)
						? _stationLookup.GetByTiploc(dbData.DestLocationName)
						: null;

					// Map API service + dbData
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
						originStd = originStd,
						etd = service.Etd,
						atd = service.Atd,
						originDateTime = dbData?.TrainOriginDateTime,
						destDateTime = dbData?.TrainDestDateTime,
						
						// Status
						isCancelled = service.IsCancelled,
						arrivalType = service.ArrivalType?.ToString(),
						departureType = service.DepartureType?.ToString(),
						
						// Journey
						origin = service.Origin?.Select(o => _stationLookup.GetByThreeAlpha(o.Crs)).Where(s => s != null).ToList(),
						destination = service.Destination?.Select(d => _stationLookup.GetByThreeAlpha(d.Crs)).Where(s => s != null).ToList(),
						originTiploc = dbData?.OriginLocationName,
						destTiploc = dbData?.DestLocationName,
						originStation = originStationFromDb,
						destStation = destStationFromDb,
						
						// Calling points (preserved)
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
							isCancelled = l.IsCancelled,
							isPass = l.IsPass
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
							isCancelled = l.IsCancelled,
							isPass = l.IsPass
						}).ToList(),
						
						// Disruption info
						cancelReason = service.CancelReason,
						delayReason = service.DelayReason,
						adhocAlerts = service.AdhocAlerts,
						
						// Train characteristics (flattened from dbData)
						trainClass = dbData?.ClassCode,
						powerType = dbData?.PowerType,
						railClasses = dbData?.RailClasses,
						fleetId = dbData?.FleetId,
						resourceGroupId = dbData?.ResourceGroupId,
						
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
						
						// Database metadata (technical/internal fields only)
						dbData = dbData != null ? new
						{
							id = dbData.Id,
							originLocationPrimaryCode = dbData.OriginLocationPrimaryCode,
							destLocationPrimaryCode = dbData.DestLocationPrimaryCode,
							toiCore = dbData.ToiCore,
							toiVariant = dbData.ToiVariant,
							toiTimetableYear = dbData.ToiTimetableYear,
							toiStartDate = dbData.ToiStartDate,
							typeOfResource = dbData.TypeOfResource
						} : null
					});
				}
			}

			// Process remaining DB services
			var windowStart = boardTime;
			var windowEnd = boardTime.AddMinutes(timeWindowMinutes);

			foreach (var dbService in dbServices)
			{
				if (usedDbIds.Contains(dbService.Id))
					continue;

				var originStation = _stationLookup.GetByTiploc(dbService.OriginLocationName ?? string.Empty);
				var destStation = _stationLookup.GetByTiploc(dbService.DestLocationName ?? string.Empty);
				var tiploc = station.Tiploc;

				// Determine STD/STA
				DateTime? std = null;
				DateTime? sta = null;

				if (dbService.OriginLocationName == tiploc)
				{
					if (!string.IsNullOrWhiteSpace(dbService.ServiceDate) && !string.IsNullOrWhiteSpace(dbService.OriginStd))
					{
						try 
						{
							var datePart = DateTime.ParseExact(dbService.ServiceDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
							// OriginStd is expected to be HH:mm
							var timePart = TimeSpan.Parse(dbService.OriginStd);
							std = datePart.Add(timePart);
						}
						catch 
						{
							// Ignore parse errors, leave std as null
						}
					}
				}
				else if (dbService.DestLocationName == tiploc)
				{
					sta = dbService.TrainDestDateTime;
				}

				// Filter by time window - only include if scheduled time falls within window
				var scheduledTime = std ?? sta;
				if (!scheduledTime.HasValue || scheduledTime.Value < windowStart || scheduledTime.Value > windowEnd)
					continue;

				result.Add(new
				{
					// Service identification
					trainId = dbService.OperationalTrainNumber,
					rid = (string?)null,
					uid = (string?)null,
					rsid = (string?)null,
					sdd = dbService.ServiceDate != null ? DateTime.ParseExact(dbService.ServiceDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : (DateTime?)null,
					@operator = (string?)null,
					operatorCode = (string?)null,
					platform = (string?)null,
					platformIsHidden = false,
					
					// Times
					std = std,
					sta = sta,
					eta = (DateTime?)null,
					ata = (DateTime?)null,
					etd = (DateTime?)null,
					atd = (DateTime?)null,
					originStd = dbService.OriginStd,
					originDateTime = dbService.TrainOriginDateTime,
					destDateTime = dbService.TrainDestDateTime,
					
					// Status
					isCancelled = false,
					arrivalType = (string?)null,
					departureType = (string?)null,
					
					// Journey
					origin = originStation != null ? new List<StationCodeRecord> { originStation } : null,
					destination = destStation != null ? new List<StationCodeRecord> { destStation } : null,
					originTiploc = dbService.OriginLocationName,
					destTiploc = dbService.DestLocationName,
					originStation = originStation,
					destStation = destStation,
					
					// Calling points
					previousLocations = (List<object>?)null,
					subsequentLocations = (List<object>?)null,
					
					// Disruption info
					cancelReason = (string?)null,
					delayReason = (string?)null,
					adhocAlerts = (List<string>?)null,
					
					// Train characteristics (flattened from dbData)
					trainClass = dbService.ClassCode,
					powerType = dbService.PowerType,
					railClasses = dbService.RailClasses,
					fleetId = dbService.FleetId,
					resourceGroupId = dbService.ResourceGroupId,
					
					// Formation
					formation = (object?)null,
					
					// Database metadata (technical/internal fields only)
					dbData = new
					{
						id = dbService.Id,
						originLocationPrimaryCode = dbService.OriginLocationPrimaryCode,
						destLocationPrimaryCode = dbService.DestLocationPrimaryCode,
						toiCore = dbService.ToiCore,
						toiVariant = dbService.ToiVariant,
						toiTimetableYear = dbService.ToiTimetableYear,
						toiStartDate = dbService.ToiStartDate,
						typeOfResource = dbService.TypeOfResource
					}
				});
			}

			return result.OrderBy(x => {
				dynamic d = x;
				DateTime? sortTime = d.std ?? d.sta;
				return sortTime ?? DateTime.MaxValue;
			}).ToList();
		}

	/// <summary>
	/// Extract the origin station's scheduled departure time (STD) from the service's previous locations.
	/// This is needed to match against the database's origin_std field.
	/// </summary>
	private string? GetOriginStdFromService(Models.NationalRail.ServiceItemWithLocations service)
	{
		// Get the origin TIPLOC
		if (service.Origin == null || !service.Origin.Any())
			return null;

		var originTiploc = service.Origin[0].Tiploc;
		if (string.IsNullOrEmpty(originTiploc))
			return null;

		// Find the origin station in previous locations
		var originLocation = service.PreviousLocations?
			.FirstOrDefault(loc => loc.Tiploc == originTiploc);

		// Extract the scheduled departure time from the origin
		if (originLocation?.Std == null)
			return null;

		return originLocation.Std.Value.ToString("HH:mm");
	}
	}
}
