using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Summer.Models;

public sealed class GameManifest
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		ReadCommentHandling = JsonCommentHandling.Skip
	};

	[JsonPropertyName("version")]
	public string Version { get; set; } = string.Empty;

	[JsonPropertyName("buildDate")]
	public string? BuildDate { get; set; }

	[JsonPropertyName("url")]
	public string? Url { get; set; }

	[JsonPropertyName("files")]
	public List<CdnFileEntry> Files { get; set; } = new List<CdnFileEntry>();

	public static GameManifest Parse(string json)
	{
		GameManifest? gameManifest = JsonSerializer.Deserialize<GameManifest>(json, JsonOptions) ?? throw new InvalidDataException("Манифест CDN пуст");
		if (string.IsNullOrWhiteSpace(gameManifest.Version))
		{
			throw new InvalidDataException("В манифесте CDN нет версии сборки");
		}
		gameManifest.Version = gameManifest.Version.Trim();
		return gameManifest;
	}
}
