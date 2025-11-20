using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class ServiceDetails
	{
		[JsonProperty("locations")]
		public List<ServiceItemLocation>? Locations { get; set; }

		[JsonProperty("formation")]
		public List<LocFormationData>? Formation { get; set; }

		[JsonProperty("cancelReason")]
		public ReasonCodeWithLocation? CancelReason { get; set; }

		[JsonProperty("delayReason")]
		public ReasonCodeWithLocation? DelayReason { get; set; }

		[JsonProperty("category")]
		public string? Category { get; set; }

		[JsonProperty("isReverseFormation")]
		public bool IsReverseFormation { get; set; }

		[JsonProperty("divertedVia")]
		public BaseServiceDetailsDivertedVia? DivertedVia { get; set; }

		[JsonProperty("diversionReason")]
		public ReasonCodeWithLocation? DiversionReason { get; set; }

		[JsonProperty("generatedAt")]
		public DateTime? GeneratedAt { get; set; }

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

		[JsonProperty("operator")]
		public string? Operator { get; set; }

		[JsonProperty("operatorCode")]
		public string? OperatorCode { get; set; }

		[JsonProperty("serviceType")]
		public ServiceType? ServiceType { get; set; }

		[JsonProperty("isPassengerService")]
		public bool IsPassengerService { get; set; }

		[JsonProperty("isCharter")]
		public bool IsCharter { get; set; }
	}

	public sealed class LocFormationData
	{
		[JsonProperty("tiploc")]
		public string? Tiploc { get; set; }

		[JsonProperty("serviceLoading")]
		public ServiceLoading? ServiceLoading { get; set; }

		[JsonProperty("coaches")]
		public List<CoachData>? Coaches { get; set; }

		[JsonProperty("source")]
		public string? Source { get; set; }

		[JsonProperty("sourceInstance")]
		public string? SourceInstance { get; set; }
	}

	public sealed class BaseServiceDetailsDivertedVia
	{
		[JsonProperty("tiploc")]
		public string? Tiploc { get; set; }

		[JsonProperty("Value")]
		public string? Value { get; set; }
	}
}

