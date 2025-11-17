using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ae.Rail.Models.NationalRail
{
	public sealed class FormationData
	{
		[JsonProperty("serviceLoading")]
		public ServiceLoading? ServiceLoading { get; set; }

		[JsonProperty("coaches")]
		public List<CoachData>? Coaches { get; set; }

		[JsonProperty("source")]
		public string? Source { get; set; }

		[JsonProperty("sourceInstance")]
		public string? SourceInstance { get; set; }
	}

	public sealed class ServiceLoading
	{
		[JsonProperty("loadingCategory")]
		public LoadingCategory? LoadingCategory { get; set; }

		[JsonProperty("loadingPercentage")]
		public LoadingPercentage? LoadingPercentage { get; set; }
	}

	public sealed class LoadingCategory
	{
		[JsonProperty("type")]
		public LoadingType? Type { get; set; }

		[JsonProperty("src")]
		public string? Src { get; set; }

		[JsonProperty("srcInst")]
		public string? SrcInst { get; set; }

		[JsonProperty("Value")]
		public string? Value { get; set; }
	}

	public sealed class LoadingPercentage
	{
		[JsonProperty("type")]
		public LoadingType? Type { get; set; }

		[JsonProperty("src")]
		public string? Src { get; set; }

		[JsonProperty("srcInst")]
		public string? SrcInst { get; set; }

		[JsonProperty("Value")]
		public int? Value { get; set; }
	}

	public sealed class CoachData
	{
		[JsonProperty("coachClass")]
		public string? CoachClass { get; set; }

		[JsonProperty("toilet")]
		public ToiletAvailability? Toilet { get; set; }

		[JsonProperty("loading")]
		public CoachLoading? Loading { get; set; }

		[JsonProperty("number")]
		public string? Number { get; set; }
	}

	public sealed class ToiletAvailability
	{
		[JsonProperty("status")]
		public ToiletStatus? Status { get; set; }

		[JsonProperty("Value")]
		public string? Value { get; set; }
	}

	public sealed class CoachLoading
	{
		[JsonProperty("source")]
		public string? Source { get; set; }

		[JsonProperty("sourceInstance")]
		public string? SourceInstance { get; set; }

		[JsonProperty("Value")]
		public int? Value { get; set; }
	}
}

