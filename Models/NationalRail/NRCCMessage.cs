using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class NRCCMessage
	{
		[JsonProperty("category")]
		public NRCCCategory? Category { get; set; }

		[JsonProperty("severity")]
		public NRCCSeverity? Severity { get; set; }

		[JsonProperty("xhtmlMessage")]
		public string? XhtmlMessage { get; set; }
	}
}

