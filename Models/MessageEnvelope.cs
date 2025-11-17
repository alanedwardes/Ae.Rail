using System;
using System.Text.Json;

namespace Ae.Rail.Models
{
	public sealed class MessageEnvelope
	{
		public long Id { get; set; }
		public DateTime ReceivedAt { get; set; }
		public JsonDocument Payload { get; set; }
	}
}

