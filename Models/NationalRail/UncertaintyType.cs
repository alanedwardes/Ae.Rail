using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class UncertaintyType
	{
		[JsonProperty("reason")]
		public ReasonCodeWithLocation? Reason { get; set; }

		[JsonProperty("status")]
		public UncertaintyStatus? Status { get; set; }
	}
}

