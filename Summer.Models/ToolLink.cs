using System.Windows.Media;

namespace Summer.Models;

public sealed class ToolLink
{
	public required string Title { get; init; }

	public required string Description { get; init; }

	public required string Url { get; init; }

	public required string Source { get; init; }

	public required Geometry Icon { get; init; }
}
