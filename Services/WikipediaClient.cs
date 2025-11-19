using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ae.Rail.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Ae.Rail.Services
{
	public interface IWikipediaClient
	{
		Task<WikipediaClassInfo?> GetBritishRailClassAsync(string classIdentifier, CancellationToken cancellationToken = default);
	}

	public sealed class WikipediaClient : IWikipediaClient
	{
		private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
		private static readonly Regex ReferenceRegex = new(@"\[\d+\]", RegexOptions.Compiled);

		private readonly HttpClient _httpClient;
		private readonly ILogger<WikipediaClient> _logger;

		public WikipediaClient(HttpClient httpClient, ILogger<WikipediaClient> logger)
		{
			_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task<WikipediaClassInfo?> GetBritishRailClassAsync(string classIdentifier, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(classIdentifier))
			{
				throw new ArgumentException("Class identifier cannot be null or empty.", nameof(classIdentifier));
			}

			var encodedTitle = BuildEncodedTitle(classIdentifier);

			var summary = await FetchSummaryAsync(encodedTitle, cancellationToken);
			if (summary == null)
			{
				_logger.LogInformation("Wikipedia page for {ClassIdentifier} was not found", classIdentifier);
				return null;
			}

			var infobox = await FetchInfoboxAsync(encodedTitle, cancellationToken);

			return new WikipediaClassInfo
			{
				Title = summary.Title ?? summary.DisplayTitle ?? $"British Rail Class {classIdentifier}",
				DisplayTitle = summary.DisplayTitle,
				Description = summary.Description,
				Extract = summary.Extract,
				PageUrl = summary.PageUrl,
				ThumbnailUrl = summary.ThumbnailUrl,
				Infobox = infobox
			};
		}

		private static string BuildEncodedTitle(string classIdentifier)
		{
			var trimmed = classIdentifier.Trim();
			var fullTitle = trimmed.StartsWith("British Rail", StringComparison.OrdinalIgnoreCase)
				? trimmed
				: $"British Rail Class {trimmed}";

			var normalized = fullTitle.Replace(' ', '_');
			return Uri.EscapeDataString(normalized);
		}

		private async Task<WikipediaSummary?> FetchSummaryAsync(string encodedTitle, CancellationToken cancellationToken)
		{
			var response = await _httpClient.GetAsync($"api/rest_v1/page/summary/{encodedTitle}?redirect=true", cancellationToken);

			if (response.StatusCode == HttpStatusCode.NotFound)
			{
				return null;
			}

			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
			var root = document.RootElement;

			return new WikipediaSummary
			{
				Title = TryGetString(root, "title"),
				DisplayTitle = ExtractPlainText(TryGetString(root, "displaytitle")),
				Description = TryGetString(root, "description"),
				Extract = TryGetString(root, "extract"),
				PageUrl = TryGetString(root, "content_urls", "desktop", "page"),
				ThumbnailUrl = TryGetString(root, "thumbnail", "source")
			};
		}

		private async Task<List<WikipediaInfoboxField>> FetchInfoboxAsync(string encodedTitle, CancellationToken cancellationToken)
		{
			var fields = new List<WikipediaInfoboxField>();

			try
			{
				var response = await _httpClient.GetAsync($"w/api.php?action=parse&page={encodedTitle}&prop=text&format=json&formatversion=2", cancellationToken);

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("Wikipedia parse request for {EncodedTitle} returned {StatusCode}", encodedTitle, response.StatusCode);
					return fields;
				}

				await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
				using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

				if (!document.RootElement.TryGetProperty("parse", out var parseElement) ||
					!parseElement.TryGetProperty("text", out var textElement))
				{
					return fields;
				}

				var html = textElement.GetString();
				if (string.IsNullOrWhiteSpace(html))
				{
					return fields;
				}

				var htmlDoc = new HtmlDocument();
				htmlDoc.LoadHtml(html);

				var infobox = htmlDoc.DocumentNode.SelectSingleNode("//table[contains(@class,'infobox')]");
				if (infobox == null)
				{
					return fields;
				}

				var rows = infobox.SelectNodes(".//tr");
				if (rows == null)
				{
					return fields;
				}

				foreach (var row in rows)
				{
					var header = row.SelectSingleNode("./th");
					var data = row.SelectSingleNode("./td");

					if (header == null || data == null)
					{
						continue;
					}

					var label = NormalizeWhitespace(header.InnerText);
					var value = NormalizeWhitespace(data.InnerText);

					if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(value))
					{
						continue;
					}

					fields.Add(new WikipediaInfoboxField
					{
						Label = label,
						Value = value
					});
				}
			}
			catch (HttpRequestException ex)
			{
				_logger.LogWarning(ex, "HTTP error while retrieving Wikipedia infobox for {EncodedTitle}", encodedTitle);
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Failed to parse Wikipedia infobox JSON for {EncodedTitle}", encodedTitle);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogWarning(ex, "Unexpected error while processing Wikipedia infobox for {EncodedTitle}", encodedTitle);
			}

			return fields;
		}

		private static string? TryGetString(JsonElement element, params string[] path)
		{
			JsonElement current = element;

			foreach (var key in path)
			{
				if (!current.TryGetProperty(key, out var child))
				{
					return null;
				}

				current = child;
			}

			return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
		}

		private static string? ExtractPlainText(string? html)
		{
			if (string.IsNullOrWhiteSpace(html))
			{
				return null;
			}

			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var text = NormalizeWhitespace(doc.DocumentNode.InnerText);

			return string.IsNullOrWhiteSpace(text) ? null : text;
		}

		private static string NormalizeWhitespace(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			var decoded = HtmlEntity.DeEntitize(value).Replace('\u00A0', ' ');
			var withoutReferences = ReferenceRegex.Replace(decoded, string.Empty);
			var normalized = WhitespaceRegex.Replace(withoutReferences, " ").Trim();

			return normalized;
		}

		private sealed class WikipediaSummary
		{
			public string? Title { get; init; }
			public string? DisplayTitle { get; init; }
			public string? Description { get; init; }
			public string? Extract { get; init; }
			public string? PageUrl { get; init; }
			public string? ThumbnailUrl { get; init; }
		}
	}
}

