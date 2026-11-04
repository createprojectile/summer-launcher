namespace Summer.Services;

public sealed record InstallProgress(double Percent, string Stage, string Detail, string Speed = "", string SizeText = "");
