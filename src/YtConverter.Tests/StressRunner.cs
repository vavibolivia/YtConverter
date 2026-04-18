using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YtConverter.App.Models;
using YtConverter.App.Services;

namespace YtConverter.Tests;

internal static class StressRunner
{
    private sealed record TestCase(string Role, string Label, string Url, OutputFormat Format, bool ExpectFail, int CancelAfterMs = 0);

    public static async Task<int> RunAsync(CancellationToken globalCt)
    {
        var outRoot = Path.Combine(Path.GetTempPath(), "YtConverter.Stress");
        Directory.CreateDirectory(outRoot);
        var logPath = Path.Combine(outRoot, $"stress-{DateTime.Now:yyyyMMdd_HHmmss}.log");
        var summaryPath = Path.Combine(outRoot, "summary.md");

        Console.OutputEncoding = Encoding.UTF8;
        Log(logPath, $"[STRESS] 시작 — 로그: {logPath}");

        var ffmpeg = new FfmpegProvisioner();
        await ffmpeg.EnsureAsync(globalCt);
        var svc = new DownloadService(ffmpeg);

        // 시나리오 (20 역할 중 자동화 가능한 것 선별)
        var validUrl1 = "https://www.youtube.com/watch?v=9q3Rg0xKmpM&list=RD9q3Rg0xKmpM&start_radio=1"; // Mariah Carey
        var validUrl2 = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";                                  // Rick Astley
        var cases = new[]
        {
            new TestCase("A01-short-video",    "Mariah MP3",  validUrl1, OutputFormat.Mp3, false),
            new TestCase("A03-mp4-muxed",       "Rick MP4",    validUrl2, OutputFormat.Mp4, false),
            new TestCase("A07-playlist-url",    "Playlist→MP3", validUrl1, OutputFormat.Mp3, false),
            new TestCase("A08-invalid-url",     "Garbage URL", "not_a_valid_url_xyzzy", OutputFormat.Mp3, true),
            new TestCase("A08-invalid-id",      "잘못된 ID",    "https://www.youtube.com/watch?v=AAAAAAAAAA1", OutputFormat.Mp3, true),
            new TestCase("A14-cancel-flow",     "Mariah→취소",  validUrl2, OutputFormat.Mp4, true, CancelAfterMs: 1500),
        };

        int iter = 0, totalPass = 0, totalFail = 0, unexpectedFail = 0;
        var totalBytes = 0L;
        var swGlobal = Stopwatch.StartNew();
        var perRoleStats = new Dictionary<string, (int pass, int fail, long bytes, double totalSec)>();

        while (!globalCt.IsCancellationRequested)
        {
            iter++;
            Log(logPath, $"───── Iteration {iter} ─────");

            foreach (var tc in cases)
            {
                if (globalCt.IsCancellationRequested) break;

                var iterDir = Path.Combine(outRoot, $"iter-{iter:D4}");
                Directory.CreateDirectory(iterDir);

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(globalCt);
                if (tc.CancelAfterMs > 0)
                    cts.CancelAfter(tc.CancelAfterMs);

                var progress = new Progress<ConversionProgress>(_ => { });
                var sw = Stopwatch.StartNew();
                ConversionResult? result = null;
                string? error = null;
                bool canceled = false;
                try
                {
                    result = await svc.ConvertAsync(tc.Url, tc.Format, iterDir, progress, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                sw.Stop();

                long bytes = 0;
                if (result is not null && File.Exists(result.OutputPath))
                {
                    bytes = new FileInfo(result.OutputPath).Length;
                    try { File.Delete(result.OutputPath); } catch { }
                }

                bool passed;
                string verdict;
                if (tc.ExpectFail)
                {
                    passed = canceled || error is not null;
                    verdict = passed ? "PASS(expected-fail)" : "FAIL(no-error)";
                }
                else
                {
                    passed = result is not null && bytes > 0;
                    verdict = passed ? "PASS" : (canceled ? "FAIL(canceled)" : $"FAIL({error})");
                }

                if (passed) totalPass++; else { totalFail++; if (!tc.ExpectFail) unexpectedFail++; }
                totalBytes += bytes;

                var cur = perRoleStats.GetValueOrDefault(tc.Role);
                perRoleStats[tc.Role] = (
                    cur.pass + (passed ? 1 : 0),
                    cur.fail + (passed ? 0 : 1),
                    cur.bytes + bytes,
                    cur.totalSec + sw.Elapsed.TotalSeconds);

                Log(logPath, $"  [{tc.Role}] {tc.Label,-18} {tc.Format,-3} {sw.Elapsed.TotalSeconds,5:F1}s  {bytes,10:N0}B  → {verdict}");

                // 디스크 청소
                try { Directory.Delete(iterDir, true); } catch { }
            }

            // 동시 변환 (A10)
            if (iter % 3 == 0 && !globalCt.IsCancellationRequested)
            {
                var concurDir = Path.Combine(outRoot, $"concur-{iter:D4}");
                Directory.CreateDirectory(concurDir);
                var concurSw = Stopwatch.StartNew();
                try
                {
                    var tasks = new[] { validUrl1, validUrl2, validUrl1 }.Select((u, i) =>
                    {
                        var subDir = Path.Combine(concurDir, $"c{i}");
                        Directory.CreateDirectory(subDir);
                        return svc.ConvertAsync(u, i % 2 == 0 ? OutputFormat.Mp3 : OutputFormat.Mp4, subDir, new Progress<ConversionProgress>(_ => { }), globalCt);
                    });
                    var results = await Task.WhenAll(tasks);
                    concurSw.Stop();
                    var okCount = results.Count(r => r is not null);
                    Log(logPath, $"  [A10-concurrent]   3건 동시 {concurSw.Elapsed.TotalSeconds,5:F1}s  성공 {okCount}/3");
                    if (okCount == 3) totalPass++; else { totalFail++; unexpectedFail++; }
                }
                catch (Exception ex)
                {
                    concurSw.Stop();
                    Log(logPath, $"  [A10-concurrent]   FAIL: {ex.Message}");
                    totalFail++; unexpectedFail++;
                }
                try { Directory.Delete(concurDir, true); } catch { }
            }

            // 메모리/GC 통계 (A19)
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var memMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
            var gen2 = GC.CollectionCount(2);

            Log(logPath, $"  [summary] iter={iter} pass={totalPass} fail={totalFail} unexpected={unexpectedFail} mem={memMb:F1}MB gen2GC={gen2} uptime={swGlobal.Elapsed:hh\\:mm\\:ss}");

            // 요약 리포트 갱신
            WriteSummary(summaryPath, iter, totalPass, totalFail, unexpectedFail, totalBytes, swGlobal.Elapsed, perRoleStats, memMb);

            // 작은 숨 고르기
            try { await Task.Delay(1000, globalCt); }
            catch (OperationCanceledException) { break; }
        }

        Log(logPath, $"[STRESS] 종료 — iter={iter} pass={totalPass} fail={totalFail} unexpected={unexpectedFail} uptime={swGlobal.Elapsed:hh\\:mm\\:ss}");
        return unexpectedFail == 0 ? 0 : 1;
    }

    private static void Log(string path, string line)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss");
        var msg = $"[{stamp}] {line}";
        Console.WriteLine(msg);
        try { File.AppendAllText(path, msg + Environment.NewLine); } catch { }
    }

    private static void WriteSummary(
        string path, int iter, int pass, int fail, int unexpected, long bytes,
        TimeSpan uptime, Dictionary<string, (int pass, int fail, long bytes, double totalSec)> perRole, double memMb)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 스트레스 테스트 요약");
        sb.AppendLine($"_갱신: {DateTime.Now:yyyy-MM-dd HH:mm:ss}_");
        sb.AppendLine();
        sb.AppendLine($"- 반복: **{iter}**회");
        sb.AppendLine($"- PASS: **{pass}** / FAIL: **{fail}** (기대치 않은 실패: {unexpected})");
        var total = pass + fail;
        sb.AppendLine($"- 성공률: **{(total > 0 ? 100.0 * pass / total : 0):F2}%**");
        sb.AppendLine($"- 총 변환 바이트: {bytes / 1024.0 / 1024.0:F1} MB");
        sb.AppendLine($"- 가동 시간: {uptime:hh\\:mm\\:ss}");
        sb.AppendLine($"- 현재 메모리: {memMb:F1} MB");
        sb.AppendLine();
        sb.AppendLine("| 역할 | PASS | FAIL | 평균 시간(s) | 누적 바이트 |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var kv in perRole.OrderBy(k => k.Key))
        {
            var (p, f, b, t) = kv.Value;
            var avg = (p + f) > 0 ? t / (p + f) : 0;
            sb.AppendLine($"| {kv.Key} | {p} | {f} | {avg:F1} | {b:N0} |");
        }
        try { File.WriteAllText(path, sb.ToString()); } catch { }
    }
}
