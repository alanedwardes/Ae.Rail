using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class ServiceItemLocation
	{
		[JsonProperty("cancelReason")]
		public ReasonCodeWithLocation? CancelReason { get; set; }

		[JsonProperty("delayReason")]
		public ReasonCodeWithLocation? DelayReason { get; set; }

		[JsonProperty("locationName")]
		public string? LocationName { get; set; }

		[JsonProperty("tiploc")]
		public string? Tiploc { get; set; }

		[JsonProperty("crs")]
		public string? Crs { get; set; }

		[JsonProperty("isOperational")]
		public bool IsOperational { get; set; }

		[JsonProperty("isPass")]
		public bool IsPass { get; set; }

		[JsonProperty("isCancelled")]
		public bool IsCancelled { get; set; }

		[JsonProperty("platform")]
		public string? Platform { get; set; }

		[JsonProperty("platformIsHidden")]
		public bool PlatformIsHidden { get; set; }

		[JsonProperty("serviceIsSuppressed")]
		public bool ServiceIsSuppressed { get; set; }

		[JsonProperty("sta")]
		public DateTime? Sta { get; set; }

		[JsonProperty("staSpecified")]
		public bool StaSpecified { get; set; }

		[JsonProperty("ata")]
		public DateTime? Ata { get; set; }

		[JsonProperty("ataSpecified")]
		public bool AtaSpecified { get; set; }

		[JsonProperty("eta")]
		public DateTime? Eta { get; set; }

		[JsonProperty("etaSpecified")]
		public bool EtaSpecified { get; set; }

		[JsonProperty("arrivalType")]
		public TimeType? ArrivalType { get; set; }

		[JsonProperty("arrivalTypeSpecified")]
		public bool ArrivalTypeSpecified { get; set; }

		[JsonProperty("arrivalSource")]
		public string? ArrivalSource { get; set; }

		[JsonProperty("arrivalSourceInstance")]
		public string? ArrivalSourceInstance { get; set; }

		[JsonProperty("std")]
		public DateTime? Std { get; set; }

		[JsonProperty("stdSpecified")]
		public bool StdSpecified { get; set; }

		[JsonProperty("atd")]
		public DateTime? Atd { get; set; }

		[JsonProperty("atdSpecified")]
		public bool AtdSpecified { get; set; }

		[JsonProperty("etd")]
		public DateTime? Etd { get; set; }

		[JsonProperty("etdSpecified")]
		public bool EtdSpecified { get; set; }

		[JsonProperty("departureType")]
		public TimeType? DepartureType { get; set; }

		[JsonProperty("departureTypeSpecified")]
		public bool DepartureTypeSpecified { get; set; }

		[JsonProperty("departureSource")]
		public string? DepartureSource { get; set; }

		[JsonProperty("departureSourceInstance")]
		public string? DepartureSourceInstance { get; set; }

		[JsonProperty("lateness")]
		public string? Lateness { get; set; }

		[JsonProperty("associations")]
		public List<Association>? Associations { get; set; }

		[JsonProperty("adhocAlerts")]
		public List<string>? AdhocAlerts { get; set; }
	}
}

