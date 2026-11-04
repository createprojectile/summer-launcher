namespace Summer.Services;

public sealed record InstallResult(bool Success, string Version, string? Error);
