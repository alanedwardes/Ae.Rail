using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class BaseServiceItemDiversion
	{
		[JsonProperty("reason")]
		public ReasonCodeWithLocation? Reason { get; set; }

		[JsonProperty("divertedVia")]
		public DivertedVia? DivertedVia { get; set; }

		[JsonProperty("between")]
		public DiversionBetween? Between { get; set; }

		[JsonProperty("rerouteDelay")]
		public int? RerouteDelay { get; set; }
	}

	public sealed class DivertedVia
	{
		[JsonProperty("tiploc")]
		public string? Tiploc { get; set; }

		[JsonProperty("Value")]
		public string? Value { get; set; }
	}

	public sealed class DiversionBetween
	{
		[JsonProperty("start")]
		public string? Start { get; set; }

		[JsonProperty("end")]
		public string? End { get; set; }
	}
}

