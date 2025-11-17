using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Services
{
	public interface ITiplocLookup
	{
		bool TryGetName(string tiplocCode, out string name);
		IReadOnlyCollection<string> FindCodesByNameContains(string nameFragment);
	}

	public sealed class TiplocLookup : ITiplocLookup
	{
		private readonly ILogger<TiplocLookup> _logger;
		private readonly Dictionary<string, string> _codeToName = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, HashSet<string>> _nameTokensToCodes = new(StringComparer.OrdinalIgnoreCase);

		private const string ResourceName = "Ae.Rail.Resources.TIPLOC_Eastings_and_Northings.csv";

		public TiplocLookup(ILogger<TiplocLookup> logger)
		{
			_logger = logger;
			LoadFromEmbeddedCsv();
		}

		public bool TryGetName(string tiplocCode, out string name)
		{
			if (string.IsNullOrWhiteSpace(tiplocCode))
			{
				name = null;
				return false;
			}

			if (_codeToName.TryGetValue(tiplocCode.Trim(), out var found))
			{
				name = found;
				return true;
			}

			name = null;
			return false;
		}

		public IReadOnlyCollection<string> FindCodesByNameContains(string nameFragment)
		{
			if (string.IsNullOrWhiteSpace(nameFragment))
			{
				return Array.Empty<string>();
			}

			// Simple contains across names; for speed, use token index when the fragment is a single token
			var fragment = nameFragment.Trim();
			var token = fragment.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

			if (!string.IsNullOrEmpty(token) && _nameTokensToCodes.TryGetValue(token, out var fastCodes))
			{
				// Further filter by full contains on the original fragment
				return fastCodes.Where(c =>
					_codeToName.TryGetValue(c, out var nm) &&
					nm.Contains(fragment, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			}

			// Fallback linear scan (still fine for typical CSV size)
			return _codeToName.Where(kvp => kvp.Value.Contains(fragment, StringComparison.OrdinalIgnoreCase))
				.Select(kvp => kvp.Key)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private void LoadFromEmbeddedCsv()
		{
			try
			{
				var asm = Assembly.GetExecutingAssembly();
				using var stream = asm.GetManifestResourceStream(ResourceName);
				if (stream == null)
				{
					_logger.LogWarning("TIPLOC CSV embedded resource not found: {ResourceName}. Autocomplete by TIPLOC names will be unavailable.", ResourceName);
					return;
				}

				using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

				// Read header
				var headerLine = reader.ReadLine();
				if (string.IsNullOrWhiteSpace(headerLine))
				{
					_logger.LogWarning("TIPLOC CSV header missing or empty.");
					return;
				}

				var headers = SplitCsvLine(headerLine).ToArray();
				int idxCode = FindHeaderIndex(headers, new[] { "TIPLOC", "Tiploc", "Code" });
				int idxName = FindHeaderIndex(headers, new[] { "Station Name", "Name", "Description", "StationName" });

				if (idxCode < 0 || idxName < 0)
				{
					_logger.LogWarning("TIPLOC CSV headers not recognized. Headers: {Headers}", string.Join("|", headers));
					return;
				}

				string line;
				int loaded = 0;
				while ((line = reader.ReadLine()) != null)
				{
					if (string.IsNullOrWhiteSpace(line)) continue;
					var cols = SplitCsvLine(line).ToArray();
					if (cols.Length <= Math.Max(idxCode, idxName)) continue;

					var code = cols[idxCode]?.Trim();
					var name = cols[idxName]?.Trim();
					if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name)) continue;

					_codeToName[code] = name;

					foreach (var t in TokenizeName(name))
					{
						if (!_nameTokensToCodes.TryGetValue(t, out var set))
						{
							set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
							_nameTokensToCodes[t] = set;
						}
						set.Add(code);
					}

					loaded++;
				}

				_logger.LogInformation("Loaded {Count} TIPLOC records from embedded CSV.", loaded);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to load TIPLOC CSV from embedded resource.");
			}
		}

		private static int FindHeaderIndex(string[] headers, IEnumerable<string> candidates)
		{
			int i = 0;
			foreach (var h in headers)
			{
				foreach (var cand in candidates)
				{
					if (string.Equals(h?.Trim(), cand, StringComparison.OrdinalIgnoreCase))
					{
						return i;
					}
				}
				i++;
			}
			return -1;
		}

		private static IEnumerable<string> SplitCsvLine(string line)
		{
			// Minimal CSV parser: handles quoted fields, commas, and escaped quotes ("")
			if (line == null) yield break;

			var sb = new StringBuilder();
			bool inQuotes = false;

			for (int i = 0; i < line.Length; i++)
			{
				var ch = line[i];
				if (inQuotes)
				{
					if (ch == '\"')
					{
						// Escaped quote
						if (i + 1 < line.Length && line[i + 1] == '\"')
						{
							sb.Append('\"');
							i++;
						}
						else
						{
							inQuotes = false;
						}
					}
					else
					{
						sb.Append(ch);
					}
				}
				else
				{
					if (ch == ',')
					{
						yield return sb.ToString();
						sb.Clear();
					}
					else if (ch == '\"')
					{
						inQuotes = true;
					}
					else
					{
						sb.Append(ch);
					}
				}
			}

			yield return sb.ToString();
		}

		private static IEnumerable<string> TokenizeName(string name)
		{
			return name.Split(new[] { ' ', '-', '/', '\t' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(t => t.Trim())
				.Where(t => t.Length > 1); // ignore single-letter tokens
		}
	}
}


