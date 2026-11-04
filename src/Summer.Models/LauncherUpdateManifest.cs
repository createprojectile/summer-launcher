using System.Text.Json;
using System.Text.Json.Serialization;

namespace Summer.Models;

public sealed class LauncherUpdateManifest
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	[JsonPropertyName("version")]
	public string Version { get; set; } = string.Empty;

	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;

	[JsonPropertyName("sha256")]
	public string Sha256 { get; set; } = string.Empty;

	[JsonPropertyName("size")]
	public long Size { get; set; }

	[JsonPropertyName("mandatory")]
	public bool Mandatory { get; set; } = true;

	[JsonPropertyName("changelog")]
	public string? Changelog { get; set; }

	public static LauncherUpdateManifest? Parse(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}
		try
		{
			return JsonSerializer.Deserialize<LauncherUpdateManifest>(json, JsonOptions);
		}
		catch
		{
			return null;
		}
	}
}
