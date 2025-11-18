using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ae.Rail.Services
{
	public interface IReprocessingService
	{
		/// <summary>
		/// Reprocesses messages from the message_envelopes table.
		/// </summary>
		/// <param name="startTime">Optional start timestamp to filter messages (inclusive)</param>
		/// <param name="endTime">Optional end timestamp to filter messages (inclusive)</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Statistics about the reprocessing operation</returns>
		Task<ReprocessingResult> ReprocessMessagesAsync(DateTime? startTime = null, DateTime? endTime = null, CancellationToken cancellationToken = default);
	}

	public sealed class ReprocessingResult
	{
		public long TotalCount { get; set; }
		public long ProcessedCount { get; set; }
		public long SuccessCount { get; set; }
		public long ErrorCount { get; set; }
		public TimeSpan Duration { get; set; }
		public DateTime? StartTime { get; set; }
		public DateTime? EndTime { get; set; }
	}
}

