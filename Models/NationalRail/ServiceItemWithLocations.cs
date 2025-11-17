using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class ServiceItemWithLocations
	{
		[JsonProperty("previousLocations")]
		public List<ServiceItemLocation>? PreviousLocations { get; set; }

		[JsonProperty("subsequentLocations")]
		public List<ServiceItemLocation>? SubsequentLocations { get; set; }

		[JsonProperty("formation")]
		public FormationData? Formation { get; set; }

		[JsonProperty("origin")]
		public List<EndPointLocation>? Origin { get; set; }

		[JsonProperty("destination")]
		public List<EndPointLocation>? Destination { get; set; }

		[JsonProperty("currentOrigins")]
		public List<EndPointLocation>? CurrentOrigins { get; set; }

		[JsonProperty("currentDestinations")]
		public List<EndPointLocation>? CurrentDestinations { get; set; }

		[JsonProperty("cancelReason")]
		public ReasonCodeWithLocation? CancelReason { get; set; }

		[JsonProperty("delayReason")]
		public ReasonCodeWithLocation? DelayReason { get; set; }

		[JsonProperty("category")]
		public string? Category { get; set; }

		[JsonProperty("activities")]
		public string? Activities { get; set; }

		[JsonProperty("length")]
		public int? Length { get; set; }

		[JsonProperty("isReverseFormation")]
		public bool IsReverseFormation { get; set; }

		[JsonProperty("detachFront")]
		public bool DetachFront { get; set; }

		[JsonProperty("futureDelay")]
		public bool FutureDelay { get; set; }

		[JsonProperty("futureCancellation")]
		public bool FutureCancellation { get; set; }

		[JsonProperty("diversion")]
		public BaseServiceItemDiversion? Diversion { get; set; }

		[JsonProperty("uncertainty")]
		public UncertaintyType? Uncertainty { get; set; }

		[JsonProperty("affectedBy")]
		public string? AffectedBy { get; set; }

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

		[JsonProperty("isPassengerService")]
		public bool IsPassengerService { get; set; }

		[JsonProperty("isCharter")]
		public bool IsCharter { get; set; }

		[JsonProperty("isCancelled")]
		public bool IsCancelled { get; set; }

		[JsonProperty("isCircularRoute")]
		public bool IsCircularRoute { get; set; }

		[JsonProperty("filterLocationCancelled")]
		public bool FilterLocationCancelled { get; set; }

		[JsonProperty("filterLocationOperational")]
		public bool FilterLocationOperational { get; set; }

		[JsonProperty("isOperationalCall")]
		public bool IsOperationalCall { get; set; }

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

		[JsonProperty("platform")]
		public string? Platform { get; set; }

		[JsonProperty("platformIsHidden")]
		public bool PlatformIsHidden { get; set; }

		[JsonProperty("serviceIsSuppressed")]
		public bool ServiceIsSuppressed { get; set; }

		[JsonProperty("adhocAlerts")]
		public List<string>? AdhocAlerts { get; set; }
	}
}

