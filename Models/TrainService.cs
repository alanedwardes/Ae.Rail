using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ae.Rail.Models
{
	[Table("train_services")]
	public sealed class TrainService
	{
		[Key]
		[Column("id")]
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public long Id { get; set; }

		[Column("operational_train_number")]
		[MaxLength(10)]
		public string? OperationalTrainNumber { get; set; }

		[Column("service_date")]
		[MaxLength(10)]
		public string? ServiceDate { get; set; } // yyyy-MM-dd

		[Column("origin_std")]
		[MaxLength(5)]
		public string? OriginStd { get; set; } // HH:mm

		[Column("train_origin_datetime")]
		public DateTime? TrainOriginDateTime { get; set; }

		[Column("train_dest_datetime")]
		public DateTime? TrainDestDateTime { get; set; }

		[Column("origin_location_primary_code")]
		[MaxLength(10)]
		public string? OriginLocationPrimaryCode { get; set; }

		[Column("origin_location_name")]
		[MaxLength(100)]
		public string? OriginLocationName { get; set; }

		[Column("dest_location_primary_code")]
		[MaxLength(10)]
		public string? DestLocationPrimaryCode { get; set; }

		[Column("dest_location_name")]
		[MaxLength(100)]
		public string? DestLocationName { get; set; }

		[Column("fleet_id")]
		[MaxLength(50)]
		public string? FleetId { get; set; }

		[Column("type_of_resource")]
		[MaxLength(10)]
		public string? TypeOfResource { get; set; }

		[Column("resource_group_id")]
		[MaxLength(50)]
		public string? ResourceGroupId { get; set; }

		[Column("class_code")]
		[MaxLength(10)]
		public string? ClassCode { get; set; }

		[Column("power_type")]
		[MaxLength(50)]
		public string? PowerType { get; set; }

		[Column("rail_classes")]
		[MaxLength(50)]
		public string? RailClasses { get; set; }

		[Column("toi_core")]
		[MaxLength(20)]
		public string? ToiCore { get; set; }

		[Column("toi_variant")]
		[MaxLength(10)]
		public string? ToiVariant { get; set; }

		[Column("toi_timetable_year")]
		public int? ToiTimetableYear { get; set; }

		[Column("toi_start_date")]
		public DateTime? ToiStartDate { get; set; }

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

