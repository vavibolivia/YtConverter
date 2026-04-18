namespace YtConverter.App.Models;

public enum JobStatus
{
    Idle,
    Resolving,
    Downloading,
    Muxing,
    Completed,
    Failed,
    Canceled
}
