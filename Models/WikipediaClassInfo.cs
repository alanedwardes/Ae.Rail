using System.Collections.Generic;

namespace Ae.Rail.Models
{
	public sealed class WikipediaClassInfo
	{
		public string Title { get; set; } = string.Empty;
		public string? DisplayTitle { get; set; }
		public string? Description { get; set; }
		public string? Extract { get; set; }
		public string? PageUrl { get; set; }
		public string? ThumbnailUrl { get; set; }
		public List<WikipediaInfoboxField> Infobox { get; set; } = new();
	}

	public sealed class WikipediaInfoboxField
	{
		public string Label { get; set; } = string.Empty;
		public string Value { get; set; } = string.Empty;
	}
}

