namespace YtConverter.App.Models;

public sealed class JobSnapshot
{
    public string Url { get; set; } = string.Empty;
    public OutputFormat Format { get; set; }
    public JobStatus Status { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
