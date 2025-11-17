using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Services
{
	/// <summary>
	/// Provides bidirectional lookups between various station code types (TIPLOC, 3ALPHA, STANOX, NLC, UIC).
	/// </summary>
	public interface IStationCodeLookup
	{
		StationCodeRecord? GetByTiploc(string tiplocCode);
		StationCodeRecord? GetByThreeAlpha(string threeAlphaCode);
		StationCodeRecord? GetByStanox(string stanoxCode);
		StationCodeRecord? GetByNlc(int nlcCode);
		StationCodeRecord? GetByUic(string uicCode);
		
		IReadOnlyCollection<StationCodeRecord> GetAllRecords();
	}

	public sealed class StationCodeRecord
	{
		public int Nlc { get; set; }
		public string Stanox { get; set; } = string.Empty;
		public string Tiploc { get; set; } = string.Empty;
		public string ThreeAlpha { get; set; } = string.Empty;
		public string Uic { get; set; } = string.Empty;
		public string NlcDesc { get; set; } = string.Empty;
		public string NlcDesc16 { get; set; } = string.Empty;
	}

	public sealed class StationCodeLookup : IStationCodeLookup
	{
		private readonly ILogger<StationCodeLookup> _logger;
		private readonly Dictionary<string, StationCodeRecord> _tiplocToRecord = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, StationCodeRecord> _threeAlphaToRecord = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, StationCodeRecord> _stanoxToRecord = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<int, StationCodeRecord> _nlcToRecord = new();
		private readonly Dictionary<string, StationCodeRecord> _uicToRecord = new(StringComparer.OrdinalIgnoreCase);

		private const string ResourceName = "Ae.Rail.Resources.CORPUSExtract.json";

		public StationCodeLookup(ILogger<StationCodeLookup> logger)
		{
			_logger = logger;
			LoadFromEmbeddedJson();
		}

		public StationCodeRecord? GetByTiploc(string tiplocCode)
		{
			if (string.IsNullOrWhiteSpace(tiplocCode)) return null;
			return _tiplocToRecord.TryGetValue(tiplocCode.Trim(), out var record) ? record : null;
		}

		public StationCodeRecord? GetByThreeAlpha(string threeAlphaCode)
		{
			if (string.IsNullOrWhiteSpace(threeAlphaCode)) return null;
			return _threeAlphaToRecord.TryGetValue(threeAlphaCode.Trim(), out var record) ? record : null;
		}

		public StationCodeRecord? GetByStanox(string stanoxCode)
		{
			if (string.IsNullOrWhiteSpace(stanoxCode)) return null;
			return _stanoxToRecord.TryGetValue(stanoxCode.Trim(), out var record) ? record : null;
		}

		public StationCodeRecord? GetByNlc(int nlcCode)
		{
			if (nlcCode <= 0) return null;
			return _nlcToRecord.TryGetValue(nlcCode, out var record) ? record : null;
		}

		public StationCodeRecord? GetByUic(string uicCode)
		{
			if (string.IsNullOrWhiteSpace(uicCode)) return null;
			return _uicToRecord.TryGetValue(uicCode.Trim(), out var record) ? record : null;
		}

		public IReadOnlyCollection<StationCodeRecord> GetAllRecords()
		{
			return _tiplocToRecord.Values.ToArray();
		}

		private void LoadFromEmbeddedJson()
		{
			try
			{
				var asm = Assembly.GetExecutingAssembly();
				using var stream = asm.GetManifestResourceStream(ResourceName);
				if (stream == null)
				{
					_logger.LogWarning("CORPUS JSON embedded resource not found: {ResourceName}. Station code lookups will be unavailable.", ResourceName);
					return;
				}

				using var reader = new StreamReader(stream);
				var json = reader.ReadToEnd();
				
				using var doc = JsonDocument.Parse(json);
				var root = doc.RootElement;
				
				if (!root.TryGetProperty("TIPLOCDATA", out var tiplocData) || tiplocData.ValueKind != JsonValueKind.Array)
				{
					_logger.LogWarning("CORPUS JSON does not contain expected TIPLOCDATA array.");
					return;
				}

				int loaded = 0;
				int skipped = 0;
				
				foreach (var element in tiplocData.EnumerateArray())
				{
					var record = new StationCodeRecord
					{
						Nlc = element.TryGetProperty("NLC", out var nlc) ? nlc.GetInt32() : 0,
						Stanox = element.TryGetProperty("STANOX", out var stanox) ? stanox.GetString()?.Trim() ?? string.Empty : string.Empty,
						Tiploc = element.TryGetProperty("TIPLOC", out var tiploc) ? tiploc.GetString()?.Trim() ?? string.Empty : string.Empty,
						ThreeAlpha = element.TryGetProperty("3ALPHA", out var threeAlpha) ? threeAlpha.GetString()?.Trim() ?? string.Empty : string.Empty,
						Uic = element.TryGetProperty("UIC", out var uic) ? uic.GetString()?.Trim() ?? string.Empty : string.Empty,
						NlcDesc = element.TryGetProperty("NLCDESC", out var nlcDesc) ? nlcDesc.GetString()?.Trim() ?? string.Empty : string.Empty,
						NlcDesc16 = element.TryGetProperty("NLCDESC16", out var nlcDesc16) ? nlcDesc16.GetString()?.Trim() ?? string.Empty : string.Empty
					};

					// Skip records with all blank/empty codes
					bool hasAnyCode = !string.IsNullOrWhiteSpace(record.Tiploc) ||
					                   !string.IsNullOrWhiteSpace(record.ThreeAlpha) ||
					                   !string.IsNullOrWhiteSpace(record.Stanox) ||
					                   !string.IsNullOrWhiteSpace(record.Uic) ||
					                   record.Nlc > 0;

					if (!hasAnyCode)
					{
						skipped++;
						continue;
					}

					// Index by all available codes
					if (!string.IsNullOrWhiteSpace(record.Tiploc))
					{
						_tiplocToRecord[record.Tiploc] = record;
					}
					
					if (!string.IsNullOrWhiteSpace(record.ThreeAlpha))
					{
						_threeAlphaToRecord[record.ThreeAlpha] = record;
					}
					
					if (!string.IsNullOrWhiteSpace(record.Stanox))
					{
						_stanoxToRecord[record.Stanox] = record;
					}
					
					if (record.Nlc > 0)
					{
						_nlcToRecord[record.Nlc] = record;
					}
					
					if (!string.IsNullOrWhiteSpace(record.Uic))
					{
						_uicToRecord[record.Uic] = record;
					}

					loaded++;
				}

				_logger.LogInformation("Loaded {Count} station code records from CORPUS JSON ({Skipped} skipped).", loaded, skipped);
				_logger.LogInformation("Indices: {Tiploc} TIPLOCs, {ThreeAlpha} 3ALPHA, {Stanox} STANOX, {Nlc} NLC, {Uic} UIC",
					_tiplocToRecord.Count, _threeAlphaToRecord.Count, _stanoxToRecord.Count, _nlcToRecord.Count, _uicToRecord.Count);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to load CORPUS JSON from embedded resource.");
			}
		}
	}
}

