using System;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class Association
	{
		[JsonProperty("category")]
		public AssociationCategory? Category { get; set; }

		[JsonProperty("rid")]
		public string? Rid { get; set; }

		[JsonProperty("uid")]
		public string? Uid { get; set; }

		[JsonProperty("trainid")]
		public string? TrainId { get; set; }

		[JsonProperty("rsid")]
		public string? Rsid { get; set; }

		[JsonProperty("sdd")]
		public DateTime? Sdd { get; set; }

		[JsonProperty("origin")]
		public string? Origin { get; set; }

		[JsonProperty("originCRS")]
		public string? OriginCRS { get; set; }

		[JsonProperty("originTiploc")]
		public string? OriginTiploc { get; set; }

		[JsonProperty("destination")]
		public string? Destination { get; set; }

		[JsonProperty("destCRS")]
		public string? DestCRS { get; set; }

		[JsonProperty("destTiploc")]
		public string? DestTiploc { get; set; }

		[JsonProperty("isCancelled")]
		public bool IsCancelled { get; set; }
	}
}

