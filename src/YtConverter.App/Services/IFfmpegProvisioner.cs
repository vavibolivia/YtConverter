using System.Threading;
using System.Threading.Tasks;

namespace YtConverter.App.Services;

public interface IFfmpegProvisioner
{
    Task<string> EnsureAsync(CancellationToken ct = default);
}
