# 학습 노트
_최종 갱신: 2026-04-18_

## 1. YoutubeExplode 최신 API (v6.5.7)

### 초기화
```csharp
using var youtube = new YoutubeClient();
```

### 메타데이터
```csharp
var video = await youtube.Videos.GetAsync(url, ct);
string title = video.Title;
TimeSpan? duration = video.Duration;
bool isLive = video.IsLive;
```

### 스트림 매니페스트
```csharp
var manifest = await youtube.Videos.Streams.GetManifestAsync(url, ct);
var audio = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
var videoStream = manifest.GetVideoOnlyStreams().GetWithHighestVideoQuality();
```

## 2. YoutubeExplode.Converter

```csharp
var streams = new IStreamInfo[] { audio, videoStream };
await youtube.Videos.DownloadAsync(
    streams,
    new ConversionRequestBuilder(outputPath)
        .SetContainer(Container.Mp3)
        .SetFFmpegPath(ffmpegPath)
        .SetPreset(ConversionPreset.Medium)
        .Build(),
    progress,
    ct);
```

- 원샷: `await youtube.Videos.DownloadAsync(url, "out.mp4", progress, ct);` — 확장자로 컨테이너 추론
- 동일 컨테이너면 remux 만 (빠름), 다르면 인코딩

## 3. WPF 비동기/진행률

```csharp
var progress = new Progress<double>(p =>
{
    Progress = p;
    StatusText = $"{p:P0}";
});
await _downloadService.ConvertAsync(url, fmt, folder, progress, _cts.Token);
```
- `Progress<T>` 를 UI 스레드에서 생성하면 자동 marshalling
- 서비스는 `ConfigureAwait(false)`
- `ObservableCollection` 을 백그라운드에서 갱신하면 `BindingOperations.EnableCollectionSynchronization` 필요

## 4. 함정
- Muxed streams(video+audio 한 파일)는 360p 제한, deprecated → 항상 adaptive
- HLS: 라이브/플레이리스트 이벤트용 → 일반 VOD 에 불필요. Converter 가 직접 처리 못함
- DASH vs Progressive: `GetVideoOnlyStreams()` 로 통합 노출. MP4 출력 시 Container 필터링 권장
- 파일명 MAX_PATH 260 → 긴 제목 잘라야 함
- `YoutubeClient` 는 singleton 재사용
- 취소 시 Converter 의 FFmpeg 프로세스 종료 → 부분 파일 `finally` 에서 삭제

## 5. FFmpeg 확보
- Windows: `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip` (~103 MB)
- `ZipArchive` 로 `bin/ffmpeg.exe` 만 추출 → `%LOCALAPPDATA%\YtConverter\ffmpeg\`

## 6. 참고 링크
- https://github.com/Tyrrrz/YoutubeExplode
- https://www.nuget.org/packages/YoutubeExplode
- https://www.gyan.dev/ffmpeg/builds/
