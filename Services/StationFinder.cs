using System;
using System.Collections.Generic;
using System.Linq;

namespace Ae.Rail.Services
{
	/// <summary>
	/// Provides station identification and search functionality across multiple code types.
	/// </summary>
	public interface IStationFinder
	{
		/// <summary>
		/// Finds all TIPLOC codes matching the given search term.
		/// Searches across station names (full and short), CRS codes, and TIPLOC codes.
		/// </summary>
		/// <param name="searchTerm">The term to search for (station name, CRS code, or TIPLOC)</param>
		/// <returns>List of matching TIPLOC codes</returns>
		IReadOnlyList<string> FindStationTiplocsBySearchTerm(string searchTerm);
	}

	public sealed class StationFinder : IStationFinder
	{
		private readonly IStationCodeLookup _stationCodeLookup;

		public StationFinder(IStationCodeLookup stationCodeLookup)
		{
			_stationCodeLookup = stationCodeLookup;
		}

		public IReadOnlyList<string> FindStationTiplocsBySearchTerm(string searchTerm)
		{
			if (string.IsNullOrWhiteSpace(searchTerm))
			{
				return Array.Empty<string>();
			}

			var token = searchTerm.Trim();

			// Search by name (full/short), CRS code, or TIPLOC in StationCodeLookup (CORPUS data)
			var matchingStations = _stationCodeLookup.GetAllRecords()
				.Where(s =>
					(!string.IsNullOrWhiteSpace(s.NlcDesc) && s.NlcDesc.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
					(!string.IsNullOrWhiteSpace(s.NlcDesc16) && s.NlcDesc16.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
					(!string.IsNullOrWhiteSpace(s.ThreeAlpha) && s.ThreeAlpha.Equals(token, StringComparison.OrdinalIgnoreCase)) ||
					(!string.IsNullOrWhiteSpace(s.Tiploc) && s.Tiploc.Contains(token, StringComparison.OrdinalIgnoreCase)))
				.Select(s => s.Tiploc)
				.Where(t => !string.IsNullOrWhiteSpace(t))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			return matchingStations;
		}
	}
}

