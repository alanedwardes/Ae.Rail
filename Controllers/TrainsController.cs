using Ae.Rail.Data;
using Ae.Rail.Models;
using Ae.Rail.Services;
using Ae.Rail.Models.NationalRail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Ae.Rail.Controllers
{
	[Route("/api/v1/trains")]
	public sealed class TrainsController : ControllerBase
	{
	private readonly ILogger<TrainsController> _logger;
	private readonly PostgresDbContext _dbContext;
	private readonly ITiplocLookup _tiplocLookup;
	private readonly IStationCodeLookup _stationCodeLookup;
	private readonly IStationFinder _stationFinder;
	private readonly INationalRailApiClient _nationalRailClient;

	public TrainsController(
		ILogger<TrainsController> logger,
		PostgresDbContext dbContext,
		ITiplocLookup tiplocLookup,
		IStationCodeLookup stationCodeLookup,
		IStationFinder stationFinder,
		INationalRailApiClient nationalRailClient)
	{
		_logger = logger;
		_dbContext = dbContext;
		_tiplocLookup = tiplocLookup;
		_stationCodeLookup = stationCodeLookup;
		_stationFinder = stationFinder;
		_nationalRailClient = nationalRailClient;
	}

		[HttpGet]
		public async Task<IActionResult> GetByOperationalTrainNumber([FromQuery(Name = "OperationalTrainNumber")] string operationalTrainNumber)
		{
			if (string.IsNullOrWhiteSpace(operationalTrainNumber))
			{
				return BadRequest("Query parameter 'OperationalTrainNumber' is required.");
			}

			try
			{
			var rawRecords = await _dbContext.TrainServices
				.Where(r => r.OperationalTrainNumber == operationalTrainNumber)
				.OrderByDescending(r => r.TrainOriginDateTime ?? DateTime.MinValue)
				.Select(r => new
				{
					r.OperationalTrainNumber,
					r.ServiceDate,
					OriginPlannedDepTime = r.OriginStd,
					r.OriginLocationPrimaryCode,
					r.OriginLocationName,
					r.DestLocationPrimaryCode,
					r.DestLocationName,
					r.RailClasses,
					r.PowerType,
					r.ToiCore,
					r.ToiVariant,
					r.ToiTimetableYear,
					r.ToiStartDate,
					r.TrainOriginDateTime,
					r.TrainDestDateTime,
					r.UpdatedAt
				})
				.ToListAsync();

			var records = rawRecords.Select(x => new
			{
				x.OperationalTrainNumber,
				x.ServiceDate,
				x.OriginPlannedDepTime,
				origin = _stationCodeLookup.GetByTiploc(x.OriginLocationName),
				destination = _stationCodeLookup.GetByTiploc(x.DestLocationName),
				x.RailClasses,
				x.PowerType,
				x.ToiCore,
				x.ToiVariant,
				x.ToiTimetableYear,
				x.ToiStartDate,
				x.TrainOriginDateTime,
				x.TrainDestDateTime,
				x.UpdatedAt
			}).ToList();

			return Ok(records);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve train for OTN {OperationalTrainNumber}", operationalTrainNumber);
				return StatusCode(500, "An error occurred while retrieving the train record.");
			}
		}

		// GET /api/v1/trains/vehicles/autocomplete?q=91111&limit=10
		[HttpGet("vehicles/autocomplete")]
		public async Task<IActionResult> VehiclesAutocomplete([FromQuery(Name = "q")] string q, [FromQuery] int limit = 10)
		{
			try
			{
				if (limit <= 0) limit = 10;
				if (limit > 50) limit = 50;

				var baseQuery = _dbContext.Vehicles.AsNoTracking().AsQueryable();

				if (!string.IsNullOrWhiteSpace(q))
				{
					var token = q.Trim();
					baseQuery = baseQuery.Where(v =>
						EF.Functions.Like(v.VehicleId ?? "", "%" + token + "%")
						|| EF.Functions.Like(v.ClassCode ?? "", "%" + token + "%")
						|| EF.Functions.Like(v.SpecificType ?? "", "%" + token + "%")
						|| EF.Functions.Like(v.TypeOfVehicle ?? "", "%" + token + "%")
					);
				}

				var results = await baseQuery
					.OrderByDescending(v => v.IsLocomotive)
					.ThenBy(v => v.VehicleId)
					.Select(v => new
					{
						v.VehicleId,
						v.ClassCode,
						v.SpecificType,
						v.TypeOfVehicle,
						v.MaximumSpeed,
						v.NumberOfSeats,
						v.Weight,
						v.TrainBrakeType,
						v.Livery,
						v.Decor,
						v.PowerType,
						v.IsLocomotive,
						v.IsDrivingVehicle,
						v.LastUpdatedAt
					})
					.Take(limit)
					.ToListAsync();

				return Ok(results);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to execute vehicle autocomplete for query {Query}", q);
				return StatusCode(500, "An error occurred while searching for vehicles.");
			}
		}

		// GET /api/v1/trains/vehicles/by-id?VehicleId=91111
		[HttpGet("vehicles/by-id")]
		public async Task<IActionResult> VehicleById([FromQuery(Name = "VehicleId")] string vehicleId)
		{
			if (string.IsNullOrWhiteSpace(vehicleId))
			{
				return BadRequest("Query parameter 'VehicleId' is required.");
			}

			try
			{
				var v = await _dbContext.Vehicles.AsNoTracking()
					.Where(x => x.VehicleId == vehicleId)
					.Select(x => new
					{
						x.VehicleId,
						x.ClassCode,
						x.SpecificType,
						x.TypeOfVehicle,
						x.MaximumSpeed,
						x.NumberOfSeats,
						x.Weight,
						x.TrainBrakeType,
						x.Livery,
						x.Decor,
						x.PowerType,
						x.IsLocomotive,
						x.IsDrivingVehicle,
						x.LastUpdatedAt
					})
					.FirstOrDefaultAsync();

				if (v == null) return NotFound();
				return Ok(v);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve vehicle by id {VehicleId}", vehicleId);
				return StatusCode(500, "An error occurred while retrieving the vehicle.");
			}
		}

		// GET /api/v1/trains/vehicle-usage?VehicleId=91111&limit=10
		[HttpGet("vehicle-usage")]
		public async Task<IActionResult> GetVehicleUsage([FromQuery(Name = "VehicleId")] string vehicleId, [FromQuery] int limit = 10)
		{
			if (string.IsNullOrWhiteSpace(vehicleId))
			{
				return BadRequest("Query parameter 'VehicleId' is required.");
			}

			try
			{
				if (limit <= 0) limit = 10;
				if (limit > 50) limit = 50;

			// Join service vehicles to train services to get routing, times, etc.
			var query =
				from sv in _dbContext.ServiceVehicles.AsNoTracking()
				where sv.VehicleId == vehicleId
				join ts in _dbContext.TrainServices.AsNoTracking()
					on new { sv.OperationalTrainNumber, sv.ServiceDate, sv.OriginStd }
					equals new { ts.OperationalTrainNumber, ts.ServiceDate, ts.OriginStd }
				select new
				{
					ts.OperationalTrainNumber,
					ServiceDate = ts.ServiceDate,
					OriginStd = ts.OriginStd,
					Sta = ts.TrainDestDateTime.HasValue ? ts.TrainDestDateTime.Value.ToString("HH:mm") : null,
					OriginLocationName = ts.OriginLocationName,
					DestLocationName = ts.DestLocationName,
					ts.TrainOriginDateTime,
					ts.TrainDestDateTime
				};

			// Order by actual origin datetime when available, else by date/time strings
			var raw = await query
				.OrderByDescending(x => x.TrainOriginDateTime ?? DateTime.MinValue)
				.ThenByDescending(x => x.ServiceDate)
				.ThenByDescending(x => x.OriginStd)
				.Take(limit)
				.ToListAsync();

			var results = raw.Select(x => new
			{
				x.OperationalTrainNumber,
				x.ServiceDate,
				x.OriginStd,
				x.Sta,
				origin = _stationCodeLookup.GetByTiploc(x.OriginLocationName),
				destination = _stationCodeLookup.GetByTiploc(x.DestLocationName),
				x.TrainOriginDateTime,
				x.TrainDestDateTime
			}).ToList();

			return Ok(results);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve vehicle usage for {VehicleId}", vehicleId);
				return StatusCode(500, "An error occurred while retrieving vehicle usage.");
			}
		}

		// GET /api/v1/trains/consist-instance?OperationalTrainNumber=1D31&ServiceDate=2025-11-09&OriginStd=20:45
		[HttpGet("consist-instance")]
		public async Task<IActionResult> GetConsistByInstance(
			[FromQuery(Name = "OperationalTrainNumber")] string operationalTrainNumber,
			[FromQuery(Name = "ServiceDate")] string serviceDate,
			[FromQuery(Name = "OriginStd")] string originStd)
		{
			if (string.IsNullOrWhiteSpace(operationalTrainNumber) ||
				string.IsNullOrWhiteSpace(serviceDate) ||
				string.IsNullOrWhiteSpace(originStd))
			{
				return BadRequest("Query parameters 'OperationalTrainNumber', 'ServiceDate' (yyyy-MM-dd) and 'OriginStd' (HH:mm) are required.");
			}

			try
			{
				var record = await FindTrainServiceAsync(operationalTrainNumber, serviceDate, originStd);

				if (record == null)
				{
					return NotFound();
				}

		var payload = new
		{
			record.OperationalTrainNumber,
			record.ServiceDate,
			OriginPlannedDepTime = record.OriginStd,
			origin = _stationCodeLookup.GetByTiploc(record.OriginLocationName),
			destination = _stationCodeLookup.GetByTiploc(record.DestLocationName),
			record.ClassCode,
			record.RailClasses,
			record.PowerType,
			record.ToiCore,
			record.ToiVariant,
			record.ToiTimetableYear,
			record.ToiStartDate,
			record.TrainOriginDateTime,
			record.TrainDestDateTime
		};

		return Ok(payload);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve consist instance for OTN {OperationalTrainNumber} on {ServiceDate} at {OriginStd}", operationalTrainNumber, serviceDate, originStd);
				return StatusCode(500, "An error occurred while retrieving the consist record.");
			}
		}

		// GET /api/v1/trains/autocomplete?q=Kings to Leeds&limit=10
		[HttpGet("autocomplete")]
		public async Task<IActionResult> Autocomplete([FromQuery(Name = "q")] string q, [FromQuery] int limit = 10)
		{
			try
			{
				if (limit <= 0) limit = 10;
				if (limit > 50) limit = 50;

				var baseQuery = _dbContext.TrainServices.AsNoTracking().AsQueryable();

			if (!string.IsNullOrWhiteSpace(q))
			{
				// Special handling for "origin to destination" format
				var tokens = q.Split(new[] { " to " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (tokens.Length == 2)
				{
					var originToken = tokens[0];
					var destToken = tokens[1];

					// Use StationFinder to identify stations
					var originStations = _stationFinder.FindStationTiplocsBySearchTerm(originToken);
					var destStations = _stationFinder.FindStationTiplocsBySearchTerm(destToken);

					baseQuery = baseQuery.Where(r =>
						(
							EF.Functions.Like(r.OriginLocationName ?? "", "%" + originToken + "%")
							|| (originStations.Count > 0 && originStations.Contains(r.OriginLocationName))
						)
						&&
						(
							EF.Functions.Like(r.DestLocationName ?? "", "%" + destToken + "%")
							|| (destStations.Count > 0 && destStations.Contains(r.DestLocationName))
						)
					);
				}
				else
				{
					var token = q.Trim();

					// Use StationFinder to identify stations
					var matchingStations = _stationFinder.FindStationTiplocsBySearchTerm(token);

					baseQuery = baseQuery.Where(r =>
						EF.Functions.Like(r.OperationalTrainNumber ?? "", "%" + token + "%")
						|| EF.Functions.Like(r.OriginLocationName ?? "", "%" + token + "%")
						|| EF.Functions.Like(r.DestLocationName ?? "", "%" + token + "%")
						|| (matchingStations.Count > 0 && matchingStations.Contains(r.OriginLocationName))
						|| (matchingStations.Count > 0 && matchingStations.Contains(r.DestLocationName))
					);
				}
			}

			// Table is already deduped by (OTN, ServiceDate, OriginStd, TrainOriginDateTime) keeping latest by updated_at
			var raw = await baseQuery
				.OrderByDescending(r => r.TrainOriginDateTime ?? DateTime.MinValue)
				.Select(r => new
				{
					r.OperationalTrainNumber,
					ServiceDate = r.ServiceDate,
					OriginStd = r.OriginStd,
					Sta = r.TrainDestDateTime.HasValue ? r.TrainDestDateTime.Value.ToString("HH:mm") : null,
					OriginLocationName = r.OriginLocationName,
					DestLocationName = r.DestLocationName
				})
				.Take(limit)
				.ToListAsync();

			var results = raw.Select(x => new
			{
				x.OperationalTrainNumber,
				x.ServiceDate,
				x.OriginStd,
				x.Sta,
				origin = _stationCodeLookup.GetByTiploc(x.OriginLocationName),
				destination = _stationCodeLookup.GetByTiploc(x.DestLocationName)
			}).ToList();

			return Ok(results);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to execute autocomplete for query {Query}", q);
				return StatusCode(500, "An error occurred while searching for services.");
			}
		}

		// GET /api/v1/trains/consist-realtime?OperationalTrainNumber=1D31&ServiceDate=2025-11-09&OriginStd=20:45
		[HttpGet("consist-realtime")]
		public async Task<IActionResult> GetConsistRealtime(
			[FromQuery(Name = "OperationalTrainNumber")] string operationalTrainNumber,
			[FromQuery(Name = "ServiceDate")] string serviceDate,
			[FromQuery(Name = "OriginStd")] string originStd)
		{
			if (string.IsNullOrWhiteSpace(operationalTrainNumber) ||
				string.IsNullOrWhiteSpace(serviceDate) ||
				string.IsNullOrWhiteSpace(originStd))
			{
				return BadRequest("Query parameters 'OperationalTrainNumber', 'ServiceDate' (yyyy-MM-dd) and 'OriginStd' (HH:mm) are required.");
			}

			try
			{
				var record = await FindTrainServiceAsync(operationalTrainNumber, serviceDate, originStd);

				if (record == null)
				{
					return NotFound();
				}

				var realTime = await GetRealTimeServiceInfoAsync(record, HttpContext.RequestAborted);

				if (realTime == null)
				{
					return NoContent();
				}

				return Ok(realTime);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve real-time data for OTN {OperationalTrainNumber} on {ServiceDate} at {OriginStd}", operationalTrainNumber, serviceDate, originStd);
				return StatusCode(500, "An error occurred while retrieving real-time information.");
			}
		}

		// GET /api/v1/trains/consist-vehicles?OperationalTrainNumber=1D31&ServiceDate=2025-11-09&OriginStd=20:45
		[HttpGet("consist-vehicles")]
		public async Task<IActionResult> GetConsistVehicles(
			[FromQuery(Name = "OperationalTrainNumber")] string operationalTrainNumber,
			[FromQuery(Name = "ServiceDate")] string serviceDate,
			[FromQuery(Name = "OriginStd")] string originStd)
		{
			if (string.IsNullOrWhiteSpace(operationalTrainNumber) ||
				string.IsNullOrWhiteSpace(serviceDate) ||
				string.IsNullOrWhiteSpace(originStd))
			{
				return BadRequest("Query parameters 'OperationalTrainNumber', 'ServiceDate' (yyyy-MM-dd) and 'OriginStd' (HH:mm) are required.");
			}

			try
			{
				var vehicles = await (
					from svc in _dbContext.ServiceVehicles.AsNoTracking()
					where svc.OperationalTrainNumber == operationalTrainNumber
						&& svc.ServiceDate == serviceDate
						&& svc.OriginStd == originStd
					join master in _dbContext.Vehicles.AsNoTracking()
						on svc.VehicleId equals master.VehicleId into masterGroup
					from master in masterGroup.DefaultIfEmpty()
					orderby svc.ResourcePosition ?? int.MaxValue, svc.VehicleId
					select new
					{
						svc.VehicleId,
						svc.SpecificType,
						svc.TypeOfVehicle,
						svc.NumberOfCabs,
						svc.NumberOfSeats,
						svc.LengthUnit,
						svc.LengthMm,
						svc.Weight,
						svc.MaximumSpeed,
						svc.TrainBrakeType,
						svc.Livery,
						svc.Decor,
						svc.VehicleStatus,
						svc.RegisteredStatus,
						svc.RegisteredCategory,
						svc.DateRegistered,
						svc.DateEnteredService,
						svc.ResourcePosition,
						svc.PlannedResourceGroup,
						svc.ResourceGroupId,
						svc.FleetId,
						svc.TypeOfResource,
						svc.IsLocomotive,
						svc.ClassCode,
						PowerType = master != null ? master.PowerType : null,
						IsDrivingVehicle = master != null ? (bool?)master.IsDrivingVehicle : null
					})
					.ToListAsync();

				return Ok(vehicles);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to retrieve vehicles for OTN {OperationalTrainNumber} on {ServiceDate} at {OriginStd}", operationalTrainNumber, serviceDate, originStd);
				return StatusCode(500, "An error occurred while retrieving vehicles for the service instance.");
			}
		}

	private async Task<TrainService?> FindTrainServiceAsync(string operationalTrainNumber, string serviceDate, string originStd)
	{
		return await _dbContext.TrainServices
			.Where(r =>
				r.OperationalTrainNumber == operationalTrainNumber &&
				r.ServiceDate == serviceDate &&
				r.OriginStd == originStd)
			.OrderByDescending(r => r.TrainOriginDateTime ?? DateTime.MinValue)
			.FirstOrDefaultAsync();
	}

	private async Task<object?> GetRealTimeServiceInfoAsync(TrainService record, CancellationToken cancellationToken)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(record.OperationalTrainNumber) || string.IsNullOrWhiteSpace(record.ServiceDate))
				{
					return null;
				}

				if (!DateOnly.TryParseExact(record.ServiceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var serviceDate))
				{
					return null;
				}

				var filterTime = NormalizeStdTime(record.OriginStd, includeSeconds: true);

				var serviceList = await _nationalRailClient.QueryServicesAsync(
					record.OperationalTrainNumber,
					serviceDate,
					filterTime: filterTime,
					cancellationToken: cancellationToken);

				var rid = SelectBestMatchingRid(serviceList?.Services, record.OriginStd);
				if (string.IsNullOrWhiteSpace(rid))
				{
					return null;
				}

				var details = await _nationalRailClient.GetServiceDetailsByRidAsync(rid, cancellationToken);
				if (details == null)
				{
					return null;
				}

				return new
				{
					details.Rid,
					details.Uid,
					details.TrainId,
					details.Rsid,
					details.GeneratedAt,
					details.Operator,
					details.OperatorCode,
					ServiceType = details.ServiceType?.ToString(),
					details.IsPassengerService,
					details.IsCharter,
					details.Category,
					details.CancelReason,
					details.DelayReason,
					locations = BuildLocationPayload(details.Locations)
				};
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to load real-time information for service {Otn} on {ServiceDate} {OriginStd}", record.OperationalTrainNumber, record.ServiceDate, record.OriginStd);
				return null;
			}
		}

		private List<object>? BuildLocationPayload(List<ServiceItemLocation>? locations)
		{
			if (locations == null || locations.Count == 0)
			{
				return null;
			}

			var result = new List<object>(locations.Count);

			for (var i = 0; i < locations.Count; i++)
			{
				var loc = locations[i];
				var station = !string.IsNullOrWhiteSpace(loc.Crs) ? _stationCodeLookup.GetByThreeAlpha(loc.Crs) : null;

				result.Add(new
				{
					order = i,
					station,
					loc.LocationName,
					loc.Tiploc,
					loc.Crs,
					loc.Platform,
					loc.IsPass,
					loc.IsCancelled,
					ScheduledArrival = loc.Sta,
					ExpectedArrival = loc.Eta,
					ActualArrival = loc.Ata,
					ScheduledDeparture = loc.Std,
					ExpectedDeparture = loc.Etd,
					ActualDeparture = loc.Atd,
					ArrivalType = loc.ArrivalType?.ToString(),
					DepartureType = loc.DepartureType?.ToString(),
					loc.AdhocAlerts,
					IsOrigin = i == 0,
					IsDestination = i == locations.Count - 1
				});
			}

			return result;
		}

	private static string? NormalizeStdTime(string? originStd, bool includeSeconds = false)
		{
			if (string.IsNullOrWhiteSpace(originStd))
			{
				return null;
			}

		var cleaned = originStd.Replace(":", string.Empty, StringComparison.Ordinal);
		if (cleaned.Length != 4)
		{
			return null;
		}

		return includeSeconds ? $"{cleaned}00" : cleaned;
		}

		private static string? SelectBestMatchingRid(List<ServiceListItem>? services, string? originStd)
		{
			if (services == null || services.Count == 0)
			{
				return null;
			}

			if (!string.IsNullOrWhiteSpace(originStd))
			{
				var normalized = NormalizeStdTime(originStd);

				var match = services.FirstOrDefault(s =>
					!string.IsNullOrWhiteSpace(s.Rid) &&
					s.ScheduledDeparture.HasValue &&
					s.ScheduledDeparture.Value.ToString("HHmm") == normalized);

				if (match != null)
				{
					return match.Rid;
				}
			}

			return services.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.Rid))?.Rid;
		}
	}
}


