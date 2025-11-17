using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ae.Rail.Services
{
	public interface ITrainDataParser
	{
		/// <summary>
		/// Parse a message and save to structured tables (trainservice, vehicles, service_vehicles).
		/// </summary>
		/// <param name="messageValue">Raw message value (JSON or XML)</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>True if parsed and saved successfully, false otherwise</returns>
		Task<bool> ParseAndSaveAsync(string messageValue, CancellationToken cancellationToken);

		/// <summary>
		/// Parse a JSON payload document and save to structured tables.
		/// </summary>
		/// <param name="payload">JSON document</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>True if parsed and saved successfully, false otherwise</returns>
		Task<bool> ParseAndSaveAsync(JsonDocument payload, CancellationToken cancellationToken);
	}
}

