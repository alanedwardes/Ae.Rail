using System;

namespace Ae.Rail.Models
{
	public sealed class ServiceVehicleView
	{
		public string OperationalTrainNumber { get; set; }
		public string ServiceDate { get; set; }
		public string OriginStd { get; set; }

		public string VehicleId { get; set; }
		public string SpecificType { get; set; }
		public string TypeOfVehicle { get; set; }
		public int? NumberOfCabs { get; set; }
		public int? NumberOfSeats { get; set; }
		public string LengthUnit { get; set; }
		public int? LengthMm { get; set; }
		public int? Weight { get; set; }
		public int? MaximumSpeed { get; set; }
		public string TrainBrakeType { get; set; }
		public string Livery { get; set; }
		public string Decor { get; set; }
		public string VehicleStatus { get; set; }
		public string RegisteredStatus { get; set; }
		public string RegisteredCategory { get; set; }
		public DateTime? DateRegistered { get; set; }
		public DateTime? DateEnteredService { get; set; }
		public int? ResourcePosition { get; set; }
		public string PlannedResourceGroup { get; set; }
		public string ResourceGroupId { get; set; }
		public string FleetId { get; set; }
		public string TypeOfResource { get; set; }
		public bool IsLocomotive { get; set; }
		public string ClassCode { get; set; }
		public DateTime LastUpdatedAt { get; set; }
	}
}



