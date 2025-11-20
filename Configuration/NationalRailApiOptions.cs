namespace Ae.Rail.Configuration
{
	public sealed class NationalRailApiOptions
	{
		public string? BaseUrl { get; set; }
		public string? ApiKey { get; set; }

		/// <summary>
		/// Optional override for QueryServices/GetServiceDetails endpoints.
		/// Falls back to BaseUrl when not supplied.
		/// </summary>
		public string? QueryBaseUrl { get; set; }

		/// <summary>
		/// Optional override for QueryServices/GetServiceDetails API key.
		/// Falls back to ApiKey when not supplied.
		/// </summary>
		public string? QueryApiKey { get; set; }
	}
}

