using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ae.Rail.Models.NationalRail
{
	[JsonConverter(typeof(StringEnumConverter))]
	public enum FilterType
	{
		[EnumMember(Value = "to")]
		To,
		[EnumMember(Value = "from")]
		From
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum TimeType
	{
		[EnumMember(Value = "Forecast")]
		Forecast,
		[EnumMember(Value = "Actual")]
		Actual,
		[EnumMember(Value = "NoLog")]
		NoLog,
		[EnumMember(Value = "Delayed")]
		Delayed
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum ServiceType
	{
		[EnumMember(Value = "train")]
		Train,
		[EnumMember(Value = "bus")]
		Bus,
		[EnumMember(Value = "ferry")]
		Ferry
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum LoadingType
	{
		[EnumMember(Value = "Typical")]
		Typical,
		[EnumMember(Value = "Expected")]
		Expected
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum ToiletStatus
	{
		[EnumMember(Value = "Unknown")]
		Unknown,
		[EnumMember(Value = "InService")]
		InService,
		[EnumMember(Value = "NotInService")]
		NotInService
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum UncertaintyStatus
	{
		[EnumMember(Value = "Delay")]
		Delay,
		[EnumMember(Value = "Cancellation")]
		Cancellation,
		[EnumMember(Value = "Other")]
		Other
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum AssociationCategory
	{
		[EnumMember(Value = "join")]
		Join,
		[EnumMember(Value = "divide")]
		Divide,
		[EnumMember(Value = "LinkFrom")]
		LinkFrom,
		[EnumMember(Value = "LinkTo")]
		LinkTo,
		[EnumMember(Value = "next")]
		Next
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum NRCCCategory
	{
		[EnumMember(Value = "Trainservice")]
		Trainservice,
		[EnumMember(Value = "Station")]
		Station,
		[EnumMember(Value = "Connectingservices")]
		Connectingservices,
		[EnumMember(Value = "Systemrelated")]
		Systemrelated,
		[EnumMember(Value = "Miscellaneous")]
		Miscellaneous,
		[EnumMember(Value = "Priortrains")]
		Priortrains,
		[EnumMember(Value = "Priorother")]
		Priorother
	}

	[JsonConverter(typeof(StringEnumConverter))]
	public enum NRCCSeverity
	{
		[EnumMember(Value = "Normal")]
		Normal,
		[EnumMember(Value = "Minor")]
		Minor,
		[EnumMember(Value = "Major")]
		Major,
		[EnumMember(Value = "Severe")]
		Severe
	}
}

