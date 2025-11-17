using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class EndPointLocation
	{
		[JsonProperty("isOperationalEndPoint")]
		public bool IsOperationalEndPoint { get; set; }

		[JsonProperty("locationName")]
		public string? LocationName { get; set; }

		[JsonProperty("crs")]
		public string? Crs { get; set; }

		[JsonProperty("tiploc")]
		public string? Tiploc { get; set; }

		[JsonProperty("via")]
		public string? Via { get; set; }

		[JsonProperty("futureChangeTo")]
		public ServiceType? FutureChangeTo { get; set; }

		[JsonProperty("futureChangeToSpecified")]
		public bool FutureChangeToSpecified { get; set; }
	}
}

