using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Models.NationalRail;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Ae.Rail.Services
{
	/// <summary>
	/// Client for the National Rail Live Departure Boards Staff Version Web Service (LDBSVWS).
	/// </summary>
	public interface INationalRailApiClient
	{
		/// <summary>
		/// Gets the arrival and departure board with details for a given station.
		/// </summary>
		/// <param name="crs">The CRS code of the station (e.g., "PAD" for Paddington).</param>
		/// <param name="time">The time for the board.</param>
		/// <param name="numRows">Number of services to return (default 10).</param>
		/// <param name="timeWindow">Time window in minutes (default 120).</param>
		/// <param name="filterCrs">Optional CRS code to filter services.</param>
		/// <param name="filterType">Filter type: "to" or "from" (default "to").</param>
		/// <param name="filterToc">Optional TOC code to filter by operator.</param>
		/// <param name="services">Service types: "P" for passenger, "F" for freight, etc. (default "P").</param>
		/// <param name="getNonPassengerServices">Whether to include non-passenger services (default false).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>Station board with service details.</returns>
		Task<StationBoardWithDetails?> GetArrDepBoardWithDetailsAsync(
			string crs,
			DateTime time,
			int? numRows = null,
			int? timeWindow = null,
			string? filterCrs = null,
			string? filterType = null,
			string? filterToc = null,
			string? services = null,
			bool? getNonPassengerServices = null,
			CancellationToken cancellationToken = default);
	}

	public sealed class NationalRailApiClient : INationalRailApiClient
	{
		private readonly HttpClient _httpClient;
		private readonly ILogger<NationalRailApiClient> _logger;

		public NationalRailApiClient(HttpClient httpClient, ILogger<NationalRailApiClient> logger)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task<StationBoardWithDetails?> GetArrDepBoardWithDetailsAsync(
			string crs,
			DateTime time,
			int? numRows = null,
			int? timeWindow = null,
			string? filterCrs = null,
			string? filterType = null,
			string? filterToc = null,
			string? services = null,
			bool? getNonPassengerServices = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(crs))
				throw new ArgumentException("CRS code cannot be null or empty.", nameof(crs));

			// Format time as required by the API: yyyyMMddTHHmmss (e.g., 20251117T050000)
			var timeString = time.ToString("yyyyMMdd'T'HHmmss");

			try
			{
				// Build query string
				var queryParams = new System.Collections.Generic.List<string>();
				
				if (numRows.HasValue)
					queryParams.Add($"numRows={numRows.Value}");
				
				if (timeWindow.HasValue)
					queryParams.Add($"timeWindow={timeWindow.Value}");
				
				if (!string.IsNullOrWhiteSpace(filterCrs))
					queryParams.Add($"filterCRS={Uri.EscapeDataString(filterCrs)}");
				
				if (!string.IsNullOrWhiteSpace(filterType))
					queryParams.Add($"filterType={Uri.EscapeDataString(filterType)}");
				
				if (!string.IsNullOrWhiteSpace(filterToc))
					queryParams.Add($"filterTOC={Uri.EscapeDataString(filterToc)}");
				
				if (!string.IsNullOrWhiteSpace(services))
					queryParams.Add($"services={Uri.EscapeDataString(services)}");
				
				if (getNonPassengerServices.HasValue)
					queryParams.Add($"getNonPassengerServices={getNonPassengerServices.Value.ToString().ToLowerInvariant()}");

				var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;
				var requestUri = $"/api/20220120/GetArrDepBoardWithDetails/{Uri.EscapeDataString(crs)}/{Uri.EscapeDataString(timeString)}{queryString}";

				_logger.LogDebug("Calling National Rail API: {RequestUri}", requestUri);

				var response = await _httpClient.GetAsync(requestUri, cancellationToken);

				if (!response.IsSuccessStatusCode)
				{
					var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
					_logger.LogWarning("National Rail API request failed with status {StatusCode}: {ErrorContent}", 
						response.StatusCode, errorContent);
					return null;
				}

				var content = await response.Content.ReadAsStringAsync(cancellationToken);
				
				var result = JsonConvert.DeserializeObject<StationBoardWithDetails>(content);
				
				_logger.LogInformation("Successfully retrieved station board for {Crs} at {Time}", crs, time);
				
				return result;
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "HTTP request failed while calling National Rail API for station {Crs}", crs);
				throw;
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to deserialize National Rail API response for station {Crs}", crs);
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error while calling National Rail API for station {Crs}", crs);
				throw;
			}
		}
	}
}

