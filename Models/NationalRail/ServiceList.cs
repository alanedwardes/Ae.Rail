using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class ServiceList
	{
		[JsonProperty("scheduleStartDate")]
		public DateTime? ScheduleStartDate { get; set; }

		[JsonProperty("serviceList")]
		public List<ServiceListItem>? Services { get; set; }
	}

	public sealed class ServiceListItem
	{
		[JsonProperty("rid")]
		public string? Rid { get; set; }

		[JsonProperty("uid")]
		public string? Uid { get; set; }

		[JsonProperty("trainid")]
		public string? TrainId { get; set; }

		[JsonProperty("rsid")]
		public string? Rsid { get; set; }

		[JsonProperty("originName")]
		public string? OriginName { get; set; }

		[JsonProperty("originCrs")]
		public string? OriginCrs { get; set; }

		[JsonProperty("destinationName")]
		public string? DestinationName { get; set; }

		[JsonProperty("destinationCrs")]
		public string? DestinationCrs { get; set; }

		[JsonProperty("scheduledDeparture")]
		public DateTime? ScheduledDeparture { get; set; }

		[JsonProperty("scheduledArrival")]
		public DateTime? ScheduledArrival { get; set; }
	}
}

