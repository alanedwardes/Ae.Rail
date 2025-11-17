using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class StationBoardWithDetails
	{
		[JsonProperty("trainServices")]
		public List<ServiceItemWithLocations>? TrainServices { get; set; }

		[JsonProperty("busServices")]
		public List<ServiceItemWithLocations>? BusServices { get; set; }

		[JsonProperty("ferryServices")]
		public List<ServiceItemWithLocations>? FerryServices { get; set; }

		[JsonProperty("isTruncated")]
		public bool IsTruncated { get; set; }

		[JsonProperty("generatedAt")]
		public DateTime GeneratedAt { get; set; }

		[JsonProperty("locationName")]
		public string? LocationName { get; set; }

		[JsonProperty("crs")]
		public string? Crs { get; set; }

		[JsonProperty("filterLocationName")]
		public string? FilterLocationName { get; set; }

		[JsonProperty("filtercrs")]
		public string? FilterCrs { get; set; }

		[JsonProperty("filterType")]
		public FilterType? FilterType { get; set; }

		[JsonProperty("stationManager")]
		public string? StationManager { get; set; }

		[JsonProperty("stationManagerCode")]
		public string? StationManagerCode { get; set; }

		[JsonProperty("nrccMessages")]
		public List<NRCCMessage>? NrccMessages { get; set; }

		[JsonProperty("platformsAreHidden")]
		public bool PlatformsAreHidden { get; set; }

		[JsonProperty("servicesAreUnavailable")]
		public bool ServicesAreUnavailable { get; set; }
	}
}

