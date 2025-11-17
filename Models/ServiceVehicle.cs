using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ae.Rail.Models
{
	[Table("service_vehicles")]
	public sealed class ServiceVehicle
	{
		[Key]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		[Column("operational_train_number")]
		[MaxLength(10)]
		[Required]
		public string OperationalTrainNumber { get; set; } = string.Empty;

		[Column("service_date")]
		[MaxLength(10)]
		[Required]
		public string ServiceDate { get; set; } = string.Empty;

		[Column("origin_std")]
		[MaxLength(5)]
		[Required]
		public string OriginStd { get; set; } = string.Empty;

		[Column("vehicle_id")]
		[MaxLength(20)]
		[Required]
		public string VehicleId { get; set; } = string.Empty;

		[Column("specific_type")]
		[MaxLength(50)]
		public string? SpecificType { get; set; }

		[Column("type_of_vehicle")]
		[MaxLength(50)]
		public string? TypeOfVehicle { get; set; }

		[Column("number_of_cabs")]
		public int? NumberOfCabs { get; set; }

		[Column("number_of_seats")]
		public int? NumberOfSeats { get; set; }

		[Column("length_unit")]
		[MaxLength(10)]
		public string? LengthUnit { get; set; }

		[Column("length_mm")]
		public int? LengthMm { get; set; }

		[Column("weight")]
		public int? Weight { get; set; }

		[Column("maximum_speed")]
		public int? MaximumSpeed { get; set; }

		[Column("train_brake_type")]
		[MaxLength(50)]
		public string? TrainBrakeType { get; set; }

		[Column("livery")]
		[MaxLength(100)]
		public string? Livery { get; set; }

		[Column("decor")]
		[MaxLength(100)]
		public string? Decor { get; set; }

		[Column("vehicle_status")]
		[MaxLength(50)]
		public string? VehicleStatus { get; set; }

		[Column("registered_status")]
		[MaxLength(50)]
		public string? RegisteredStatus { get; set; }

		[Column("registered_category")]
		[MaxLength(50)]
		public string? RegisteredCategory { get; set; }

		[Column("date_registered")]
		public DateTime? DateRegistered { get; set; }

		[Column("date_entered_service")]
		public DateTime? DateEnteredService { get; set; }

		[Column("resource_position")]
		public int? ResourcePosition { get; set; }

		[Column("planned_resource_group")]
		[MaxLength(50)]
		public string? PlannedResourceGroup { get; set; }

		[Column("resource_group_id")]
		[MaxLength(50)]
		public string? ResourceGroupId { get; set; }

		[Column("fleet_id")]
		[MaxLength(50)]
		public string? FleetId { get; set; }

		[Column("type_of_resource")]
		[MaxLength(10)]
		public string? TypeOfResource { get; set; }

		[Column("is_locomotive")]
		public bool IsLocomotive { get; set; }

		[Column("class_code")]
		[MaxLength(10)]
		public string? ClassCode { get; set; }

		[Column("created_at")]
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

		[Column("updated_at")]
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

		// Alias for backwards compatibility with controllers
		public DateTime LastUpdatedAt 
		{
			get => UpdatedAt;
			set => UpdatedAt = value;
		}
	}
}

