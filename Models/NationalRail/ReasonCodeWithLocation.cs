using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class ReasonCodeWithLocation
	{
		[JsonProperty("tiploc")]
		public string? Tiploc { get; set; }

		[JsonProperty("near")]
		public bool Near { get; set; }

		[JsonProperty("Value")]
		public int Value { get; set; }
	}
}

