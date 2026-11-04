namespace Summer.Services;

public sealed record ServerQueryResult(bool IsOnline, string ServerName, string Map, int Online, int MaxPlayers, int PingMs);
