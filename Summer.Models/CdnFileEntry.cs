namespace Summer.Models;

public sealed record CdnFileEntry(string Path, string Sha256, long Size, string? Url = null);
