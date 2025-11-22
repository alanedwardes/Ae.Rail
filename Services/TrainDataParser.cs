using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Ae.Rail.Data;
using Ae.Rail.Models.TafTsi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using TrainServiceEntity = Ae.Rail.Models.TrainService;
using VehicleEntity = Ae.Rail.Models.Vehicle;
using ServiceVehicleEntity = Ae.Rail.Models.ServiceVehicle;

namespace Ae.Rail.Services
{
	public sealed class TrainDataParser : ITrainDataParser
	{
		private readonly PostgresDbContext _dbContext;
		private readonly ILogger<TrainDataParser> _logger;

		public TrainDataParser(PostgresDbContext dbContext, ILogger<TrainDataParser> logger)
		{
			_dbContext = dbContext;
			_logger = logger;
		}

		public async Task<bool> ParseAndSaveAsync(string messageValue, CancellationToken cancellationToken)
		{
			if (string.IsNullOrWhiteSpace(messageValue))
			{
				_logger.LogWarning("Received empty or whitespace message value");
				return false;
			}

			// Try parse as JSON
			if (TryParseJson(messageValue, out var jsonDoc))
			{
				return await ParseAndSaveAsync(jsonDoc, cancellationToken);
			}

			// Try convert TAF/TSI XML to JSON
			if (TryConvertTafTsiXmlToJson(messageValue, out jsonDoc))
			{
				return await ParseAndSaveAsync(jsonDoc, cancellationToken);
			}

			_logger.LogWarning("Message format not recognized as JSON or TAF/TSI XML. Content preview: {Content}", 
				messageValue.Length > 100 ? messageValue.Substring(0, 100) : messageValue);

			return false;
		}

		public async Task<bool> ParseAndSaveAsync(JsonDocument payload, CancellationToken cancellationToken)
		{
			try
			{
				// Check if this is a valid train consist message
				if (!payload.RootElement.TryGetProperty("OperationalTrainNumberIdentifier", out _))
				{
					_logger.LogWarning("Missing OperationalTrainNumberIdentifier property");
					return false;
				}

				// Extract key identifiers
				var otn = payload.RootElement.GetProperty("OperationalTrainNumberIdentifier")
					.GetProperty("OperationalTrainNumber").GetString();

				if (string.IsNullOrEmpty(otn))
				{
					_logger.LogWarning("OperationalTrainNumber is null or empty");
					return false;
				}

			// Get start date
			if (!payload.RootElement.TryGetProperty("TrainOperationalIdentification", out var toi))
			{
				_logger.LogWarning("Missing TrainOperationalIdentification for OTN {Otn}", otn);
				return false;
			}

			if (!toi.TryGetProperty("TransportOperationalIdentifiers", out var toiArray))
			{
				_logger.LogWarning("Missing TransportOperationalIdentifiers for OTN {Otn}", otn);
				return false;
			}

			if (toiArray.GetArrayLength() == 0)
			{
				_logger.LogWarning("TransportOperationalIdentifiers array is empty for OTN {Otn}", otn);
				return false;
			}

			var toiFirst = toiArray[0];
			if (!toiFirst.TryGetProperty("StartDate", out var startDateProp))
			{
				_logger.LogWarning("Missing StartDate in TransportOperationalIdentifiers for OTN {Otn}", otn);
				return false;
			}

			var startDateStr = startDateProp.GetString();
			if (string.IsNullOrEmpty(startDateStr) || !DateTime.TryParse(startDateStr, out var startDate))
			{
				_logger.LogWarning("Invalid or missing StartDate '{StartDate}' for OTN {Otn}", startDateStr, otn);
				return false;
			}

			// Ensure UTC for PostgreSQL timestamptz
			startDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);

			var serviceDate = startDate.ToString("yyyy-MM-dd");

				// Get allocation
				if (!payload.RootElement.TryGetProperty("Allocation", out var allocations) || allocations.GetArrayLength() == 0)
				{
					_logger.LogWarning("Missing or empty Allocation array for OTN {Otn}", otn);
					return false;
				}

				var firstAllocation = allocations[0];

				// Get origin time
				if (!firstAllocation.TryGetProperty("TrainOriginDateTime", out var originDateTimeProp))
				{
					_logger.LogWarning("Missing TrainOriginDateTime in first Allocation for OTN {Otn}", otn);
					return false;
				}

			var originDateTimeStr = originDateTimeProp.GetString();
			if (string.IsNullOrEmpty(originDateTimeStr) || !DateTime.TryParse(originDateTimeStr, out var originDateTime))
			{
				_logger.LogWarning("Invalid or missing TrainOriginDateTime '{OriginDate}' for OTN {Otn}", originDateTimeStr, otn);
				return false;
			}

			// Ensure UTC for PostgreSQL timestamptz
			originDateTime = DateTime.SpecifyKind(originDateTime, DateTimeKind.Utc);

			var originStd = originDateTime.ToString("HH:mm");

				// Parse train service
				var trainService = ParseTrainService(payload, otn, serviceDate, originStd, originDateTime);
				if (trainService != null)
				{
					await UpsertTrainServiceAsync(trainService, cancellationToken);
				}

				// Parse vehicles
				var vehicles = ParseVehicles(payload);
				foreach (var vehicle in vehicles)
				{
					await UpsertVehicleAsync(vehicle, cancellationToken);
				}

			// Parse service vehicles
			var serviceVehicles = ParseServiceVehicles(payload, otn, serviceDate, originStd);
			foreach (var serviceVehicle in serviceVehicles)
			{
				await UpsertServiceVehicleAsync(serviceVehicle, cancellationToken);
			}

			// NOTE: Does NOT save - caller must call SaveChangesAsync for batching efficiency
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to parse and save train data");
			return false;
		}
		}

		private TrainServiceEntity? ParseTrainService(JsonDocument payload, string otn, string serviceDate, string originStd, DateTime originDateTime)
		{
			try
			{
				var root = payload.RootElement;
				var allocations = root.GetProperty("Allocation");
				var firstAllocation = allocations[0];

				var trainService = new TrainServiceEntity
				{
					OperationalTrainNumber = otn,
					ServiceDate = serviceDate,
					OriginStd = originStd,
					TrainOriginDateTime = originDateTime,
					UpdatedAt = DateTime.UtcNow
				};

			// Destination time
			if (firstAllocation.TryGetProperty("TrainDestDateTime", out var destTimeProp))
			{
				var destTimeStr = destTimeProp.GetString();
				if (!string.IsNullOrEmpty(destTimeStr) && DateTime.TryParse(destTimeStr, out var destTime))
				{
					trainService.TrainDestDateTime = DateTime.SpecifyKind(destTime, DateTimeKind.Utc);
				}
			}

			// Origin location
			if (firstAllocation.TryGetProperty("TrainOriginLocation", out var originLocation))
			{
				if (originLocation.TryGetProperty("LocationPrimaryCode", out var originCode))
					trainService.OriginLocationPrimaryCode = originCode.GetString();
				
				if (originLocation.TryGetProperty("LocationSubsidiaryIdentification", out var originSubsidiary))
				{
					if (originSubsidiary.TryGetProperty("LocationSubsidiaryCode", out var originName))
						trainService.OriginLocationName = originName.GetString();
				}
			}

			// Destination location
			if (firstAllocation.TryGetProperty("TrainDestLocation", out var destLocation))
			{
				if (destLocation.TryGetProperty("LocationPrimaryCode", out var destCode))
					trainService.DestLocationPrimaryCode = destCode.GetString();
				
				if (destLocation.TryGetProperty("LocationSubsidiaryIdentification", out var destSubsidiary))
				{
					if (destSubsidiary.TryGetProperty("LocationSubsidiaryCode", out var destName))
						trainService.DestLocationName = destName.GetString();
				}
			}

				// Resource group info
				JsonElement bestResourceGroup = default;
				bool foundResourceGroup = false;

				foreach (var allocation in allocations.EnumerateArray())
				{
					if (!allocation.TryGetProperty("ResourceGroup", out var rg))
						continue;

					// If we haven't found any yet, take this one as candidate
					if (!foundResourceGroup)
					{
						bestResourceGroup = rg;
						foundResourceGroup = true;
					}

					// Check if this is a preferred type (Locomotive or Unit)
					if (rg.TryGetProperty("TypeOfResource", out var torProp))
					{
						var tor = torProp.GetString();
						if (string.Equals(tor, "L", StringComparison.OrdinalIgnoreCase) || 
							string.Equals(tor, "U", StringComparison.OrdinalIgnoreCase))
						{
							bestResourceGroup = rg;
							foundResourceGroup = true;
							break; // Found a main traction unit, stop searching
						}
					}
				}

				if (foundResourceGroup)
				{
					if (bestResourceGroup.TryGetProperty("FleetId", out var fleetId))
						trainService.FleetId = fleetId.GetString();
					if (bestResourceGroup.TryGetProperty("TypeOfResource", out var typeOfResource))
						trainService.TypeOfResource = typeOfResource.GetString();
					if (bestResourceGroup.TryGetProperty("ResourceGroupId", out var resourceGroupId))
						trainService.ResourceGroupId = resourceGroupId.GetString();

					// Class code
					string? firstVehicleId = null;
					if (bestResourceGroup.TryGetProperty("Vehicle", out var vehicles) && vehicles.GetArrayLength() > 0)
					{
						var v = vehicles[0];
						if (v.TryGetProperty("VehicleId", out var vid))
							firstVehicleId = vid.GetString();
					}

					trainService.ClassCode = DeriveClassCode(trainService.TypeOfResource, trainService.ResourceGroupId, firstVehicleId);

					// Power type
					trainService.PowerType = GetPowerType(trainService.TypeOfResource);

					// Rail classes
					trainService.RailClasses = GetRailClasses(trainService.TypeOfResource);
				}

				// TOI info
				if (root.TryGetProperty("TrainOperationalIdentification", out var toi))
				{
					if (toi.TryGetProperty("TransportOperationalIdentifiers", out var toiArray) && toiArray.GetArrayLength() > 0)
					{
						var toiFirst = toiArray[0];
						if (toiFirst.TryGetProperty("Core", out var core))
							trainService.ToiCore = core.GetString();
						if (toiFirst.TryGetProperty("Variant", out var variant))
							trainService.ToiVariant = variant.GetString();
					if (toiFirst.TryGetProperty("TimetableYear", out var ttYear))
					{
						if (ttYear.ValueKind == JsonValueKind.Number)
						{
							trainService.ToiTimetableYear = ttYear.GetInt32();
						}
						else if (ttYear.ValueKind == JsonValueKind.String)
						{
							var ttYearStr = ttYear.GetString();
							if (!string.IsNullOrEmpty(ttYearStr) && int.TryParse(ttYearStr, out var ttYearInt))
								trainService.ToiTimetableYear = ttYearInt;
						}
					}
					if (toiFirst.TryGetProperty("StartDate", out var startDate))
					{
						var startDateStr = startDate.GetString();
						if (!string.IsNullOrEmpty(startDateStr) && DateTime.TryParse(startDateStr, out var sd))
							trainService.ToiStartDate = DateTime.SpecifyKind(sd, DateTimeKind.Utc);
					}
					}
				}

				return trainService;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to parse train service");
				return null;
			}
		}

		private List<VehicleEntity> ParseVehicles(JsonDocument payload)
		{
			var vehicles = new List<VehicleEntity>();

			try
			{
				var root = payload.RootElement;
				if (!root.TryGetProperty("Allocation", out var allocations))
					return vehicles;

				foreach (var allocation in allocations.EnumerateArray())
				{
					if (!allocation.TryGetProperty("ResourceGroup", out var resourceGroup))
						continue;

					if (!resourceGroup.TryGetProperty("Vehicle", out var vehicleArray))
						continue;

					var typeOfResource = string.Empty;
					if (resourceGroup.TryGetProperty("TypeOfResource", out var torProp))
						typeOfResource = torProp.GetString() ?? string.Empty;

					var resourceGroupId = string.Empty;
					if (resourceGroup.TryGetProperty("ResourceGroupId", out var rgIdProp))
						resourceGroupId = rgIdProp.GetString() ?? string.Empty;

					var fleetId = string.Empty;
					if (resourceGroup.TryGetProperty("FleetId", out var fleetIdProp))
						fleetId = fleetIdProp.GetString() ?? string.Empty;

					foreach (var veh in vehicleArray.EnumerateArray())
					{
						var vehicle = ParseSingleVehicle(veh, typeOfResource, resourceGroupId, fleetId);
						if (vehicle != null)
							vehicles.Add(vehicle);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to parse vehicles");
			}

			return vehicles;
		}

		private VehicleEntity? ParseSingleVehicle(JsonElement veh, string typeOfResource, string resourceGroupId, string fleetId)
		{
			try
			{
				if (!veh.TryGetProperty("VehicleId", out var vehicleIdProp))
				{
					_logger.LogWarning("Missing VehicleId in Vehicle element");
					return null;
				}

				var vehicleId = vehicleIdProp.GetString();
				if (string.IsNullOrEmpty(vehicleId))
				{
					_logger.LogWarning("VehicleId is null or empty");
					return null;
				}

				var vehicle = new VehicleEntity
				{
					VehicleId = vehicleId,
					TypeOfResource = typeOfResource,
					ResourceGroupId = resourceGroupId,
					FleetId = fleetId,
					UpdatedAt = DateTime.UtcNow
				};

		if (veh.TryGetProperty("SpecificType", out var st)) vehicle.SpecificType = st.GetString();
		if (veh.TryGetProperty("TypeOfVehicle", out var tov)) vehicle.TypeOfVehicle = tov.GetString();
		if (veh.TryGetProperty("Cabs", out var cabs) && cabs.ValueKind == JsonValueKind.Number) vehicle.NumberOfCabs = cabs.GetInt32();
		if (veh.TryGetProperty("NumberOfSeats", out var seats) && seats.ValueKind == JsonValueKind.Number) vehicle.NumberOfSeats = seats.GetInt32();
		
		ParseLength(veh, out var lengthUnit, out var lengthMm);
		vehicle.LengthUnit = lengthUnit;
		vehicle.LengthMm = lengthMm;

		if (veh.TryGetProperty("Weight", out var weight) && weight.ValueKind == JsonValueKind.Number) vehicle.Weight = weight.GetInt32();
				if (veh.TryGetProperty("MaximumSpeed", out var maxSpeed) && maxSpeed.ValueKind == JsonValueKind.Number) vehicle.MaximumSpeed = maxSpeed.GetInt32();
				if (veh.TryGetProperty("TrainBrakeType", out var tbt)) vehicle.TrainBrakeType = tbt.GetString();
				if (veh.TryGetProperty("Livery", out var livery)) vehicle.Livery = livery.GetString();
				if (veh.TryGetProperty("Decor", out var decor)) vehicle.Decor = decor.GetString();
				if (veh.TryGetProperty("VehicleStatus", out var vs)) vehicle.VehicleStatus = vs.GetString();
				if (veh.TryGetProperty("RegisteredStatus", out var rs)) vehicle.RegisteredStatus = rs.GetString();
				if (veh.TryGetProperty("RegisteredCategory", out var rc)) vehicle.RegisteredCategory = rc.GetString();
				
				if (veh.TryGetProperty("DateRegistered", out var dr) && DateTime.TryParse(dr.GetString(), out var drDate))
					vehicle.DateRegistered = DateTime.SpecifyKind(drDate, DateTimeKind.Utc);
				if (veh.TryGetProperty("DateEnteredService", out var des) && DateTime.TryParse(des.GetString(), out var desDate))
					vehicle.DateEnteredService = DateTime.SpecifyKind(desDate, DateTimeKind.Utc);

				if (veh.TryGetProperty("ResourcePosition", out var rp) && rp.ValueKind == JsonValueKind.Number)
					vehicle.ResourcePosition = rp.GetInt32();
				if (veh.TryGetProperty("PlannedResourceGroup", out var prg))
					vehicle.PlannedResourceGroup = prg.GetString();

				// Is locomotive
				vehicle.IsLocomotive = string.Equals(typeOfResource, "L", StringComparison.OrdinalIgnoreCase);

				// Class code
				vehicle.ClassCode = DeriveClassCode(typeOfResource, resourceGroupId, vehicleId);

				// Power type
				vehicle.PowerType = GetPowerTypeFromClass(vehicle.ClassCode);

				// Driving vehicle (has cabs)
				vehicle.IsDrivingVehicle = vehicle.NumberOfCabs.GetValueOrDefault() > 0;

				return vehicle;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to parse single vehicle");
				return null;
			}
		}

		private List<ServiceVehicleEntity> ParseServiceVehicles(JsonDocument payload, string otn, string serviceDate, string originStd)
		{
			var serviceVehicles = new List<ServiceVehicleEntity>();

			try
			{
				var root = payload.RootElement;
				if (!root.TryGetProperty("Allocation", out var allocations))
					return serviceVehicles;

				foreach (var allocation in allocations.EnumerateArray())
				{
					if (!allocation.TryGetProperty("ResourceGroup", out var resourceGroup))
						continue;

					if (!resourceGroup.TryGetProperty("Vehicle", out var vehicleArray))
						continue;

					var typeOfResource = string.Empty;
					if (resourceGroup.TryGetProperty("TypeOfResource", out var torProp))
						typeOfResource = torProp.GetString() ?? string.Empty;

					var resourceGroupId = string.Empty;
					if (resourceGroup.TryGetProperty("ResourceGroupId", out var rgIdProp))
						resourceGroupId = rgIdProp.GetString() ?? string.Empty;

					var fleetId = string.Empty;
					if (resourceGroup.TryGetProperty("FleetId", out var fleetIdProp))
						fleetId = fleetIdProp.GetString() ?? string.Empty;

					foreach (var veh in vehicleArray.EnumerateArray())
					{
						var serviceVehicle = ParseSingleServiceVehicle(veh, otn, serviceDate, originStd, typeOfResource, resourceGroupId, fleetId);
						if (serviceVehicle != null)
							serviceVehicles.Add(serviceVehicle);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to parse service vehicles");
			}

			return serviceVehicles;
		}

		private ServiceVehicleEntity? ParseSingleServiceVehicle(JsonElement veh, string otn, string serviceDate, string originStd, string typeOfResource, string resourceGroupId, string fleetId)
		{
			try
			{
				if (!veh.TryGetProperty("VehicleId", out var vehicleIdProp))
				{
					_logger.LogWarning("Missing VehicleId in ServiceVehicle element for OTN {Otn}", otn);
					return null;
				}

				var vehicleId = vehicleIdProp.GetString();
				if (string.IsNullOrEmpty(vehicleId))
				{
					_logger.LogWarning("VehicleId is null or empty in ServiceVehicle for OTN {Otn}", otn);
					return null;
				}

				var serviceVehicle = new ServiceVehicleEntity
				{
					OperationalTrainNumber = otn,
					ServiceDate = serviceDate,
					OriginStd = originStd,
					VehicleId = vehicleId,
					TypeOfResource = typeOfResource,
					ResourceGroupId = resourceGroupId,
					FleetId = fleetId,
					UpdatedAt = DateTime.UtcNow
				};

		if (veh.TryGetProperty("SpecificType", out var st)) serviceVehicle.SpecificType = st.GetString();
		if (veh.TryGetProperty("TypeOfVehicle", out var tov)) serviceVehicle.TypeOfVehicle = tov.GetString();
		if (veh.TryGetProperty("Cabs", out var cabs) && cabs.ValueKind == JsonValueKind.Number) serviceVehicle.NumberOfCabs = cabs.GetInt32();
		if (veh.TryGetProperty("NumberOfSeats", out var seats) && seats.ValueKind == JsonValueKind.Number) serviceVehicle.NumberOfSeats = seats.GetInt32();
		
		ParseLength(veh, out var lengthUnit, out var lengthMm);
		serviceVehicle.LengthUnit = lengthUnit;
		serviceVehicle.LengthMm = lengthMm;

		if (veh.TryGetProperty("Weight", out var weight) && weight.ValueKind == JsonValueKind.Number) serviceVehicle.Weight = weight.GetInt32();
				if (veh.TryGetProperty("MaximumSpeed", out var maxSpeed) && maxSpeed.ValueKind == JsonValueKind.Number) serviceVehicle.MaximumSpeed = maxSpeed.GetInt32();
				if (veh.TryGetProperty("TrainBrakeType", out var tbt)) serviceVehicle.TrainBrakeType = tbt.GetString();
				if (veh.TryGetProperty("Livery", out var livery)) serviceVehicle.Livery = livery.GetString();
				if (veh.TryGetProperty("Decor", out var decor)) serviceVehicle.Decor = decor.GetString();
				if (veh.TryGetProperty("VehicleStatus", out var vs)) serviceVehicle.VehicleStatus = vs.GetString();
				if (veh.TryGetProperty("RegisteredStatus", out var rs)) serviceVehicle.RegisteredStatus = rs.GetString();
				if (veh.TryGetProperty("RegisteredCategory", out var rc)) serviceVehicle.RegisteredCategory = rc.GetString();
				
				if (veh.TryGetProperty("DateRegistered", out var dr) && DateTime.TryParse(dr.GetString(), out var drDate))
					serviceVehicle.DateRegistered = DateTime.SpecifyKind(drDate, DateTimeKind.Utc);
				if (veh.TryGetProperty("DateEnteredService", out var des) && DateTime.TryParse(des.GetString(), out var desDate))
					serviceVehicle.DateEnteredService = DateTime.SpecifyKind(desDate, DateTimeKind.Utc);

				if (veh.TryGetProperty("ResourcePosition", out var rp) && rp.ValueKind == JsonValueKind.Number)
					serviceVehicle.ResourcePosition = rp.GetInt32();
				if (veh.TryGetProperty("PlannedResourceGroup", out var prg))
					serviceVehicle.PlannedResourceGroup = prg.GetString();

				// Is locomotive
				serviceVehicle.IsLocomotive = string.Equals(typeOfResource, "L", StringComparison.OrdinalIgnoreCase);

				// Class code
				serviceVehicle.ClassCode = DeriveClassCode(typeOfResource, resourceGroupId, vehicleId);

				return serviceVehicle;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to parse single service vehicle");
				return null;
			}
		}

	private async Task UpsertTrainServiceAsync(TrainServiceEntity trainService, CancellationToken cancellationToken)
	{
		// Check local tracked entities first (for duplicates in same message)
		var local = _dbContext.Set<TrainServiceEntity>()
			.Local
			.FirstOrDefault(t =>
				t.OperationalTrainNumber == trainService.OperationalTrainNumber &&
				t.ServiceDate == trainService.ServiceDate &&
				t.OriginStd == trainService.OriginStd &&
				t.TrainOriginDateTime == trainService.TrainOriginDateTime);

		if (local != null)
		{
			// Already being tracked in this batch, update it
			local.OriginLocationPrimaryCode = trainService.OriginLocationPrimaryCode;
			local.OriginLocationName = trainService.OriginLocationName;
			local.DestLocationPrimaryCode = trainService.DestLocationPrimaryCode;
			local.DestLocationName = trainService.DestLocationName;
			local.TrainDestDateTime = trainService.TrainDestDateTime;
			local.ResourceGroupId = trainService.ResourceGroupId;
			local.TypeOfResource = trainService.TypeOfResource;
			local.FleetId = trainService.FleetId;
			local.PowerType = trainService.PowerType;
			local.ClassCode = trainService.ClassCode;
			local.RailClasses = trainService.RailClasses;
			local.ToiCore = trainService.ToiCore;
			local.ToiVariant = trainService.ToiVariant;
			local.ToiTimetableYear = trainService.ToiTimetableYear;
			local.ToiStartDate = trainService.ToiStartDate;
			local.UpdatedAt = DateTime.UtcNow;
			return;
		}

		// Check database
		var existing = await _dbContext.Set<TrainServiceEntity>()
			.FirstOrDefaultAsync(t =>
				t.OperationalTrainNumber == trainService.OperationalTrainNumber &&
				t.ServiceDate == trainService.ServiceDate &&
				t.OriginStd == trainService.OriginStd &&
				t.TrainOriginDateTime == trainService.TrainOriginDateTime,
				cancellationToken);

		if (existing != null)
			{
				// Update existing
				existing.OriginLocationPrimaryCode = trainService.OriginLocationPrimaryCode;
				existing.OriginLocationName = trainService.OriginLocationName;
				existing.DestLocationPrimaryCode = trainService.DestLocationPrimaryCode;
				existing.DestLocationName = trainService.DestLocationName;
				existing.TrainDestDateTime = trainService.TrainDestDateTime;
				existing.FleetId = trainService.FleetId;
				existing.TypeOfResource = trainService.TypeOfResource;
				existing.ResourceGroupId = trainService.ResourceGroupId;
				existing.ClassCode = trainService.ClassCode;
				existing.PowerType = trainService.PowerType;
				existing.RailClasses = trainService.RailClasses;
				existing.ToiCore = trainService.ToiCore;
				existing.ToiVariant = trainService.ToiVariant;
				existing.ToiTimetableYear = trainService.ToiTimetableYear;
				existing.ToiStartDate = trainService.ToiStartDate;
				existing.UpdatedAt = DateTime.UtcNow;
			}
			else
			{
				// Insert new
				trainService.CreatedAt = DateTime.UtcNow;
				await _dbContext.Set<TrainServiceEntity>().AddAsync(trainService, cancellationToken);
			}
		}

	private async Task UpsertVehicleAsync(VehicleEntity vehicle, CancellationToken cancellationToken)
	{
		// Check local tracked entities first (for duplicates in same message)
		var local = _dbContext.Set<VehicleEntity>()
			.Local
			.FirstOrDefault(v => v.VehicleId == vehicle.VehicleId);

		if (local != null)
		{
			// Already being tracked in this batch, update it
			local.SpecificType = vehicle.SpecificType;
			local.TypeOfVehicle = vehicle.TypeOfVehicle;
			local.NumberOfCabs = vehicle.NumberOfCabs;
			local.NumberOfSeats = vehicle.NumberOfSeats;
			local.LengthUnit = vehicle.LengthUnit;
			local.LengthMm = vehicle.LengthMm;
			local.Weight = vehicle.Weight;
			local.MaximumSpeed = vehicle.MaximumSpeed;
			local.TrainBrakeType = vehicle.TrainBrakeType;
			local.Livery = vehicle.Livery;
			local.Decor = vehicle.Decor;
			local.VehicleStatus = vehicle.VehicleStatus;
			local.RegisteredStatus = vehicle.RegisteredStatus;
			local.RegisteredCategory = vehicle.RegisteredCategory;
			local.DateRegistered = vehicle.DateRegistered;
			local.DateEnteredService = vehicle.DateEnteredService;
			local.ResourcePosition = vehicle.ResourcePosition;
			local.PlannedResourceGroup = vehicle.PlannedResourceGroup;
			local.ResourceGroupId = vehicle.ResourceGroupId;
			local.FleetId = vehicle.FleetId;
			local.TypeOfResource = vehicle.TypeOfResource;
			local.IsLocomotive = vehicle.IsLocomotive;
			local.ClassCode = vehicle.ClassCode;
			local.PowerType = vehicle.PowerType;
			local.UpdatedAt = DateTime.UtcNow;
			return;
		}

		// Check database
		var existing = await _dbContext.Set<VehicleEntity>()
			.FirstOrDefaultAsync(v => v.VehicleId == vehicle.VehicleId, cancellationToken);

		if (existing != null)
			{
				// Update existing
				existing.SpecificType = vehicle.SpecificType;
				existing.TypeOfVehicle = vehicle.TypeOfVehicle;
				existing.NumberOfCabs = vehicle.NumberOfCabs;
				existing.NumberOfSeats = vehicle.NumberOfSeats;
				existing.LengthUnit = vehicle.LengthUnit;
				existing.LengthMm = vehicle.LengthMm;
				existing.Weight = vehicle.Weight;
				existing.MaximumSpeed = vehicle.MaximumSpeed;
				existing.TrainBrakeType = vehicle.TrainBrakeType;
				existing.Livery = vehicle.Livery;
				existing.Decor = vehicle.Decor;
				existing.VehicleStatus = vehicle.VehicleStatus;
				existing.RegisteredStatus = vehicle.RegisteredStatus;
				existing.RegisteredCategory = vehicle.RegisteredCategory;
				existing.DateRegistered = vehicle.DateRegistered;
				existing.DateEnteredService = vehicle.DateEnteredService;
				existing.ResourcePosition = vehicle.ResourcePosition;
				existing.PlannedResourceGroup = vehicle.PlannedResourceGroup;
				existing.ResourceGroupId = vehicle.ResourceGroupId;
				existing.FleetId = vehicle.FleetId;
				existing.TypeOfResource = vehicle.TypeOfResource;
				existing.IsLocomotive = vehicle.IsLocomotive;
				existing.ClassCode = vehicle.ClassCode;
				existing.PowerType = vehicle.PowerType;
				existing.IsDrivingVehicle = vehicle.IsDrivingVehicle;
				existing.UpdatedAt = DateTime.UtcNow;
			}
			else
			{
				// Insert new
				vehicle.CreatedAt = DateTime.UtcNow;
				await _dbContext.Set<VehicleEntity>().AddAsync(vehicle, cancellationToken);
			}
		}

	private async Task UpsertServiceVehicleAsync(ServiceVehicleEntity serviceVehicle, CancellationToken cancellationToken)
	{
		// Check local tracked entities first (for duplicates in same message)
		var local = _dbContext.Set<ServiceVehicleEntity>()
			.Local
			.FirstOrDefault(sv =>
				sv.OperationalTrainNumber == serviceVehicle.OperationalTrainNumber &&
				sv.ServiceDate == serviceVehicle.ServiceDate &&
				sv.OriginStd == serviceVehicle.OriginStd &&
				sv.VehicleId == serviceVehicle.VehicleId);

		if (local != null)
		{
			// Already being tracked in this batch, update it
			local.SpecificType = serviceVehicle.SpecificType;
			local.TypeOfVehicle = serviceVehicle.TypeOfVehicle;
			local.NumberOfCabs = serviceVehicle.NumberOfCabs;
			local.NumberOfSeats = serviceVehicle.NumberOfSeats;
			local.LengthUnit = serviceVehicle.LengthUnit;
			local.LengthMm = serviceVehicle.LengthMm;
			local.Weight = serviceVehicle.Weight;
			local.MaximumSpeed = serviceVehicle.MaximumSpeed;
			local.TrainBrakeType = serviceVehicle.TrainBrakeType;
			local.Livery = serviceVehicle.Livery;
			local.Decor = serviceVehicle.Decor;
			local.VehicleStatus = serviceVehicle.VehicleStatus;
			local.RegisteredStatus = serviceVehicle.RegisteredStatus;
			local.RegisteredCategory = serviceVehicle.RegisteredCategory;
			local.DateRegistered = serviceVehicle.DateRegistered;
			local.DateEnteredService = serviceVehicle.DateEnteredService;
			local.ResourcePosition = serviceVehicle.ResourcePosition;
			local.PlannedResourceGroup = serviceVehicle.PlannedResourceGroup;
			local.ResourceGroupId = serviceVehicle.ResourceGroupId;
			local.FleetId = serviceVehicle.FleetId;
			local.TypeOfResource = serviceVehicle.TypeOfResource;
			local.IsLocomotive = serviceVehicle.IsLocomotive;
			local.ClassCode = serviceVehicle.ClassCode;
			local.UpdatedAt = DateTime.UtcNow;
			return;
		}

		// Check database
		var existing = await _dbContext.Set<ServiceVehicleEntity>()
			.FirstOrDefaultAsync(sv =>
				sv.OperationalTrainNumber == serviceVehicle.OperationalTrainNumber &&
				sv.ServiceDate == serviceVehicle.ServiceDate &&
				sv.OriginStd == serviceVehicle.OriginStd &&
				sv.VehicleId == serviceVehicle.VehicleId,
				cancellationToken);

		if (existing != null)
			{
				// Update existing
				existing.SpecificType = serviceVehicle.SpecificType;
				existing.TypeOfVehicle = serviceVehicle.TypeOfVehicle;
				existing.NumberOfCabs = serviceVehicle.NumberOfCabs;
				existing.NumberOfSeats = serviceVehicle.NumberOfSeats;
				existing.LengthUnit = serviceVehicle.LengthUnit;
				existing.LengthMm = serviceVehicle.LengthMm;
				existing.Weight = serviceVehicle.Weight;
				existing.MaximumSpeed = serviceVehicle.MaximumSpeed;
				existing.TrainBrakeType = serviceVehicle.TrainBrakeType;
				existing.Livery = serviceVehicle.Livery;
				existing.Decor = serviceVehicle.Decor;
				existing.VehicleStatus = serviceVehicle.VehicleStatus;
				existing.RegisteredStatus = serviceVehicle.RegisteredStatus;
				existing.RegisteredCategory = serviceVehicle.RegisteredCategory;
				existing.DateRegistered = serviceVehicle.DateRegistered;
				existing.DateEnteredService = serviceVehicle.DateEnteredService;
				existing.ResourcePosition = serviceVehicle.ResourcePosition;
				existing.PlannedResourceGroup = serviceVehicle.PlannedResourceGroup;
				existing.ResourceGroupId = serviceVehicle.ResourceGroupId;
				existing.FleetId = serviceVehicle.FleetId;
				existing.TypeOfResource = serviceVehicle.TypeOfResource;
				existing.IsLocomotive = serviceVehicle.IsLocomotive;
				existing.ClassCode = serviceVehicle.ClassCode;
				existing.UpdatedAt = DateTime.UtcNow;
			}
			else
			{
				// Insert new
				serviceVehicle.CreatedAt = DateTime.UtcNow;
				await _dbContext.Set<ServiceVehicleEntity>().AddAsync(serviceVehicle, cancellationToken);
			}
		}

		private static bool TryParseJson(string value, out JsonDocument doc)
		{
			try
			{
				doc = JsonDocument.Parse(value);
				return true;
			}
			catch
			{
				doc = null!;
				return false;
			}
		}

		private static bool TryConvertTafTsiXmlToJson(string value, out JsonDocument doc)
		{
			doc = null!;
			try
			{
				if (string.IsNullOrWhiteSpace(value) || !value.Contains("<PassengerTrainConsistMessage", StringComparison.OrdinalIgnoreCase))
					return false;

				var serializer = new XmlSerializer(typeof(PassengerTrainConsistMessage));
				using var reader = new StringReader(value);
				var dto = (PassengerTrainConsistMessage)serializer.Deserialize(reader);
				if (dto == null)
					return false;

				var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions
				{
					DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
					WriteIndented = false
				});
				doc = JsonDocument.Parse(json);
				return true;
			}
			catch
			{
				doc = null!;
				return false;
			}
		}

		private static string? GetPowerType(string? typeOfResource)
		{
			if (string.IsNullOrEmpty(typeOfResource))
				return null;

			return typeOfResource.ToUpperInvariant() switch
			{
				"DE" => "Diesel",
				"DMU" => "Diesel",
				"EL" => "Electric",
				"EM" => "Electric",
				"EMU" => "Electric",
				"U" => "Electric/Diesel",
				_ => null
			};
		}

		private static string? GetRailClasses(string? typeOfResource)
		{
			if (string.IsNullOrEmpty(typeOfResource))
				return null;

			return typeOfResource.ToUpperInvariant().Contains("M") ? "Multiple Unit" : "Loco Hauled";
		}

		private static void ParseLength(JsonElement veh, out string? lengthUnit, out int? lengthMm)
	{
		lengthUnit = null;
		lengthMm = null;

		if (veh.TryGetProperty("Length", out var length))
		{
			if (length.TryGetProperty("Unit", out var unit))
				lengthUnit = unit.GetString();
			if (length.TryGetProperty("Value", out var val) && val.ValueKind == JsonValueKind.Number)
				lengthMm = (int)val.GetDecimal();
		}
	}

	private static string? DeriveClassCode(string? typeOfResource, string? resourceGroupId, string? vehicleId)
	{
		if (string.Equals(typeOfResource, "U", StringComparison.OrdinalIgnoreCase) && 
			!string.IsNullOrEmpty(resourceGroupId) && 
			resourceGroupId.Length >= 3)
		{
			return resourceGroupId.Substring(0, 3);
		}
		
		if (!string.IsNullOrEmpty(vehicleId) && vehicleId.Length >= 2)
		{
			return vehicleId.Substring(0, 2);
		}

		return null;
	}

	private static string? GetPowerTypeFromClass(string? classCode)
		{
			if (string.IsNullOrWhiteSpace(classCode) || !int.TryParse(classCode, out var cls))
				return null;

			return cls switch
			{
				>= 1 and <= 70 => "Diesel",
				>= 71 and <= 96 => "Electric",
				97 => "Diesel",
				98 => "Steam",
				>= 101 and <= 299 => "Diesel",
				>= 300 and <= 398 => "Electric",
				399 => "Diesel",
				>= 400 and <= 799 => "Electric",
				800 => "Diesel/Electric (bi-mode)",
				801 => "Electric",
				802 => "Diesel/Electric (bi-mode)",
				901 => "Diesel",
				>= 910 and <= 939 => "Electric",
				>= 950 and <= 999 => "Diesel",
				_ => null
			};
		}
	}
}

