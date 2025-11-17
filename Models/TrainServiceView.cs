using System;

namespace Ae.Rail.Models
{
	public sealed class TrainServiceView
	{
		public string OperationalTrainNumber { get; set; }
		public string ServiceDate { get; set; } // yyyy-MM-dd
		public string OriginStd { get; set; } // HH:mm
		public string Sta { get; set; } // HH:mm
		public string OriginLocationPrimaryCode { get; set; }
		public string DestLocationPrimaryCode { get; set; }
		public string OriginLocationName { get; set; }
		public string DestLocationName { get; set; }
		public DateTime? TrainOriginDateTime { get; set; }
		public DateTime? TrainDestDateTime { get; set; }
		public string ToiCore { get; set; }
		public string ToiVariant { get; set; }
		public int? ToiTimetableYear { get; set; }
		public DateTime? ToiStartDate { get; set; }
		public string RailClasses { get; set; }
		public string PowerType { get; set; }
		public DateTime LastUpdatedAt { get; set; }
	}
}



