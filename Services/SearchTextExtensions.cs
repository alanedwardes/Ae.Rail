using System;
using System.Linq;
using System.Text;

namespace Ae.Rail.Services
{
	/// <summary>
	/// Extension methods for text processing in station searches.
	/// </summary>
	public static class SearchTextExtensions
	{
		/// <summary>
		/// Normalizes text for station search by removing all punctuation and extra whitespace.
		/// Allows flexible matching (e.g., "kings cross" matches "king's cross", "st pancras" matches "St. Pancras").
		/// </summary>
		/// <param name="text">The text to normalize</param>
		/// <returns>Normalized text with punctuation removed and whitespace collapsed</returns>
		public static string NormalizeForSearch(this string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return string.Empty;
			}

			var sb = new StringBuilder(text.Length);
			foreach (var c in text)
			{
				if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
				{
					sb.Append(c);
				}
			}

			// Collapse multiple spaces and trim
			var result = sb.ToString();
			while (result.Contains("  ", StringComparison.Ordinal))
			{
				result = result.Replace("  ", " ", StringComparison.Ordinal);
			}

			return result.Trim();
		}
	}
}

