using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YtConverter.App.Models;
using YtConverter.App.Services;

namespace YtConverter.Tests;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var url = args.Length > 0
            ? args[0]
            : "https://www.youtube.com/watch?v=9q3Rg0xKmpM&list=RD9q3Rg0xKmpM&start_radio=1";

        var outDir = Path.Combine(
            Path.GetTempPath(), "YtConverter.Tests", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(outDir);

        var reportPath = Path.Combine(outDir, "report.md");
        var jsonPath = Path.Combine(outDir, "report.json");

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine($"[TEST] URL      = {url}");
        Console.WriteLine($"[TEST] OutDir   = {outDir}");

        var ffmpeg = new FfmpegProvisioner();
        var svc = new DownloadService(ffmpeg);

        var ensureSw = Stopwatch.StartNew();
        var ffmpegPath = await ffmpeg.EnsureAsync();
        ensureSw.Stop();
        Console.WriteLine($"[TEST] FFmpeg   = {ffmpegPath} (준비 {ensureSw.Elapsed.TotalSeconds:F1}s)");
        Console.WriteLine($"[TEST] FFprobe  = {ffmpeg.FfprobePath}");

        var formats = new[] { OutputFormat.Mp3, OutputFormat.Mp4 };
        var results = new List<TestResult>();

        foreach (var fmt in formats)
        {
            Console.WriteLine();
            Console.WriteLine($"===== {fmt} 변환 시작 =====");
            var lastStatus = "";
            var progress = new Progress<ConversionProgress>(p =>
            {
                var line = $"  [{p.Status}] {p.Ratio,6:P1}";
                if (line != lastStatus)
                {
                    Console.WriteLine(line);
                    lastStatus = line;
                }
            });

            var sw = Stopwatch.StartNew();
            ConversionResult? convResult = null;
            string? error = null;
            try
            {
                convResult = await svc.ConvertAsync(url, fmt, outDir, progress, CancellationToken.None);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            sw.Stop();

            if (convResult is null)
            {
                Console.WriteLine($"  FAIL: {error}");
                results.Add(new TestResult(fmt.ToString(), null, null, sw.Elapsed, 0, null, error));
                continue;
            }

            var fi = new FileInfo(convResult.OutputPath);
            var probe = await FfprobeAsync(ffmpeg.FfprobePath, convResult.OutputPath);

            Console.WriteLine($"  파일       : {fi.Name}");
            Console.WriteLine($"  크기       : {fi.Length / 1024.0 / 1024.0:F2} MB ({fi.Length:N0} bytes)");
            Console.WriteLine($"  소요 시간  : {sw.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"  영상 길이  : {convResult.Duration}");
            var realtimeRatio = convResult.Duration.TotalSeconds / Math.Max(sw.Elapsed.TotalSeconds, 0.01);
            Console.WriteLine($"  변환 속도  : {realtimeRatio:F1}x (실시간 대비)");
            if (probe is not null)
            {
                Console.WriteLine($"  컨테이너   : {probe.FormatName}");
                Console.WriteLine($"  측정 길이  : {probe.Duration:F2}s");
                Console.WriteLine($"  비트레이트 : {probe.BitRate / 1000} kbps");
                foreach (var s in probe.Streams)
                    Console.WriteLine($"  스트림     : {s.CodecType} / {s.CodecName} / {(s.Bitrate > 0 ? s.Bitrate / 1000 + "kbps" : "-")} / {(s.SampleRate > 0 ? s.SampleRate + "Hz" : "")} {s.Resolution}");
            }

            results.Add(new TestResult(
                fmt.ToString(),
                convResult.OutputPath,
                convResult.VideoTitle,
                sw.Elapsed,
                fi.Length,
                probe,
                null));
        }

        var report = BuildReport(url, outDir, ensureSw.Elapsed, results);
        await File.WriteAllTextAsync(reportPath, report);
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine();
        Console.WriteLine($"[TEST] 리포트: {reportPath}");
        Console.WriteLine($"[TEST] JSON  : {jsonPath}");

        var anyFail = results.Any(r => r.Error is not null);
        return anyFail ? 1 : 0;
    }

    private static string BuildReport(string url, string outDir, TimeSpan ensureTime, List<TestResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# E2E 변환 테스트 리포트");
        sb.AppendLine($"_생성: {DateTime.Now:yyyy-MM-dd HH:mm:ss}_");
        sb.AppendLine();
        sb.AppendLine($"- URL: {url}");
        sb.AppendLine($"- 출력 폴더: {outDir}");
        sb.AppendLine($"- FFmpeg 준비: {ensureTime.TotalSeconds:F1}s");
        sb.AppendLine();
        sb.AppendLine("| 포맷 | 상태 | 파일 | 크기(MB) | 변환시간 | 영상길이 | 실시간배속 | 컨테이너 | 비트레이트 | 오디오 | 비디오 | 오류 |");
        sb.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | --- | ---: | --- | --- | --- |");
        foreach (var r in results)
        {
            var status = r.Error is null ? "PASS" : "FAIL";
            var sizeMb = r.SizeBytes / 1024.0 / 1024.0;
            var speed = r.Probe?.Duration > 0 ? (r.Probe.Duration / Math.Max(r.Elapsed.TotalSeconds, 0.01)).ToString("F1") : "-";
            var audio = r.Probe?.Streams.FirstOrDefault(s => s.CodecType == "audio");
            var video = r.Probe?.Streams.FirstOrDefault(s => s.CodecType == "video");
            var audioDesc = audio is null ? "-" : $"{audio.CodecName} {audio.SampleRate}Hz";
            var videoDesc = video is null ? "-" : $"{video.CodecName} {video.Resolution}";
            var br = r.Probe is null ? "-" : $"{r.Probe.BitRate / 1000} kbps";
            sb.AppendLine($"| {r.Format} | {status} | {Path.GetFileName(r.OutputPath ?? "-")} | {sizeMb:F2} | {r.Elapsed.TotalSeconds:F1}s | {r.Probe?.Duration:F1}s | {speed}x | {r.Probe?.FormatName ?? "-"} | {br} | {audioDesc} | {videoDesc} | {r.Error ?? "-"} |");
        }
        return sb.ToString();
    }

    private static async Task<ProbeResult?> FfprobeAsync(string ffprobePath, string file)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = $"-v quiet -print_format json -show_format -show_streams \"{file}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) return null;
            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;
            var format = root.GetProperty("format");
            var duration = double.Parse(format.GetProperty("duration").GetString() ?? "0", CultureInfo.InvariantCulture);
            long bitrate = 0;
            if (format.TryGetProperty("bit_rate", out var frmBr))
                long.TryParse(frmBr.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out bitrate);
            var formatName = format.GetProperty("format_name").GetString() ?? "";
            var streams = new List<StreamInfo>();
            foreach (var s in root.GetProperty("streams").EnumerateArray())
            {
                var type = s.GetProperty("codec_type").GetString() ?? "";
                var codec = s.GetProperty("codec_name").GetString() ?? "";
                long strBitrate = 0;
                if (s.TryGetProperty("bit_rate", out var br)) long.TryParse(br.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out strBitrate);
                int sampleRate = 0;
                if (s.TryGetProperty("sample_rate", out var sr)) int.TryParse(sr.GetString(), out sampleRate);
                string resolution = "";
                if (s.TryGetProperty("width", out var w) && s.TryGetProperty("height", out var h))
                    resolution = $"{w.GetInt32()}x{h.GetInt32()}";
                streams.Add(new StreamInfo(type, codec, strBitrate, sampleRate, resolution));
            }
            return new ProbeResult(formatName, duration, bitrate, streams);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ffprobe 실패: {ex.Message}");
            return null;
        }
    }

    public sealed record ProbeResult(string FormatName, double Duration, long BitRate, List<StreamInfo> Streams);
    public sealed record StreamInfo(string CodecType, string CodecName, long Bitrate, int SampleRate, string Resolution);
    public sealed record TestResult(string Format, string? OutputPath, string? VideoTitle, TimeSpan Elapsed, long SizeBytes, ProbeResult? Probe, string? Error);
}
