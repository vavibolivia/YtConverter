# 구현 계획 — YouTube → MP3/MP4 WPF 변환기
_최종 갱신: 2026-04-18_

## 1. 개요
- 목적: YouTube URL 입력 → MP3(오디오) / MP4(비디오) 로 변환/저장하는 Windows 데스크톱 앱
- 타깃: `net8.0-windows` (WPF, Nullable, ImplicitUsings 활성화)
- 실행 형태: 단일 EXE (self-contained publish, win-x64)

## 2. 솔루션 / 프로젝트 구조
```
C:\workspace\youtube\
├─ requirements.md
├─ docs\
│  ├─ implementation-plan.md
│  ├─ test-plan.md
│  └─ learning-notes.md
├─ YtConverter.sln
└─ src\
   └─ YtConverter.App\
      ├─ YtConverter.App.csproj   (OutputType=WinExe, UseWPF=true, TargetFramework=net8.0-windows)
      ├─ App.xaml / App.xaml.cs
      ├─ MainWindow.xaml / MainWindow.xaml.cs
      ├─ ViewModels\
      │   ├─ MainViewModel.cs
      │   ├─ JobViewModel.cs
      │   └─ RelayCommand.cs
      ├─ Services\
      │   ├─ IDownloadService.cs
      │   ├─ DownloadService.cs            // YoutubeExplode 래퍼
      │   ├─ IFfmpegProvisioner.cs
      │   ├─ FfmpegProvisioner.cs          // 다운로드/캐시/검증
      │   ├─ IFileSystem.cs
      │   └─ FileSystem.cs
      ├─ Logging\
      │   ├─ AppLogger.cs
      │   └─ LogSink.cs
      ├─ Models\
      │   ├─ ConversionJob.cs
      │   ├─ OutputFormat.cs               // enum: Mp3, Mp4
      │   └─ JobStatus.cs
      ├─ Converters\
      │   └─ BoolToVisibilityConverter.cs
      └─ Assets\ (아이콘)
```

## 3. NuGet 패키지
| 패키지 | 버전(기준일 2026-04-18) | 용도 |
| --- | --- | --- |
| `YoutubeExplode` | 6.5.7 | 스트림 매니페스트/다운로드 |
| `YoutubeExplode.Converter` | 6.x (≥ 6.2) | FFmpeg 기반 mux/트랜스코드 |
| `CommunityToolkit.Mvvm` | 8.x | `ObservableObject`, `RelayCommand` |
| `Serilog` + `Serilog.Sinks.File` | 최신 | 로그 |
| (선택) `Polly` | 최신 | 네트워크 재시도 |

## 4. UI 와이어프레임 (MainWindow)
```
┌────────────────────────────────────────────────────────────┐
│ YouTube Converter                                 [_][□][X]│
├────────────────────────────────────────────────────────────┤
│ URL     : [____________________________________] [붙여넣기]│
│ 포맷    : ( ) MP3 192kbps  (•) MP4 최고화질                │
│ 저장 폴더: [C:\Users\xxx\Music          ] [찾아보기] [열기]│
│                                                            │
│ [ 변환 시작 ]        [ 취소 ]                              │
│                                                            │
│ ┌─ 진행 상황 ────────────────────────────────────────────┐ │
│ │ 영상 제목 : "...."                                     │ │
│ │ 상태      : 스트림 해석 중 / 다운로드 중 / 변환 중 ... │ │
│ │ [████████████░░░░░░] 62%   2:13 / 3:32                 │ │
│ └────────────────────────────────────────────────────────┘ │
│ ┌─ 로그 ────────────────────────────────────────────────┐ │
│ │ 2026-04-18 21:55:12  INFO  Manifest loaded             │ │
│ └────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
```

### 바인딩 계획 (MainViewModel)
- `string Url`
- `OutputFormat SelectedFormat` (Mp3 / Mp4)
- `string OutputFolder`
- `bool IsBusy`, `double Progress` (0–1), `string StatusText`, `string VideoTitle`
- `ICommand StartCommand`, `CancelCommand`, `BrowseFolderCommand`, `OpenFolderCommand`, `PasteCommand`
- `ObservableCollection<string> LogLines`

## 5. 핵심 클래스 책임
- **MainWindow.xaml.cs**: View-only, DataContext=MainViewModel.
- **MainViewModel**: 입력 검증, 커맨드 enable/disable, 진행률 바인딩, `CancellationTokenSource` 관리.
- **DownloadService** `Task ConvertAsync(string url, OutputFormat fmt, string outDir, IProgress<double> p, CancellationToken ct)`
  1. `YoutubeClient.Videos.GetAsync` → 제목/길이
  2. 라이브/연령제한/프라이빗 사전 체크 (`video.IsLive`, 예외 매핑)
  3. MP3: `GetAudioOnlyStreams().GetWithHighestBitrate()` → `Videos.DownloadAsync` + `SetContainer("mp3")`
  4. MP4: 최고화질 비디오 + 최고비트 오디오 → `SetContainer("mp4")`
  5. 파일명은 `SanitizeFileName(title)`.
- **FfmpegProvisioner**
  - 탐색 순서: `%LOCALAPPDATA%\YtConverter\ffmpeg\ffmpeg.exe` → 앱 실행 폴더 `ffmpeg.exe` → PATH
  - 없으면 `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip` 다운로드 → `bin/ffmpeg.exe` 추출 → 캐시.
- **AppLogger**: `%LOCALAPPDATA%\YtConverter\logs\yt-YYYYMMDD.log`, UI 로그 창 팬아웃.
- **ConversionJob/JobStatus**: `Queued → Resolving → Downloading → Muxing → Completed|Failed|Canceled`.

## 6. 진행률 / 취소 / 에러 흐름
- `Progress<double>` 를 UI 스레드에서 생성 (자동 SynchronizationContext marshalling)
- 모든 서비스는 `async` + `CancellationToken`, 취소는 `OperationCanceledException` → `Canceled` 상태
- 예외 매핑:
  | 예외 | UI 메시지 |
  | --- | --- |
  | `VideoUnplayableException` | "재생 불가 영상입니다 (연령/지역 제한)." |
  | `VideoUnavailableException` | "삭제되었거나 비공개 영상입니다." |
  | `HttpRequestException` | "네트워크 오류. 재시도하세요." |
  | `IOException` | "저장 공간이 부족하거나 파일을 쓸 수 없습니다." |

## 7. 빌드·배포
- Debug: `dotnet build src/YtConverter.App/YtConverter.App.csproj`
- Release: `dotnet publish src/YtConverter.App -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true`
- 사용자: EXE 더블클릭 → 최초 실행 시 FFmpeg 자동 다운로드

## 8. 리스크
| 리스크 | 대응 |
| --- | --- |
| YoutubeExplode 파손 | NuGet 업데이트 안내 |
| 장시간 영상 | 스트리밍 유지, ETA 표시 |
| 지역/연령/라이브 | 사전 감지 후 명확 에러 |
| 네트워크 불안정 | 지수 백오프 재시도 |
| 파일명 중복 | `(1)`, `(2)` 부여 |
| FFmpeg 다운로드 실패 | 수동 경로 폴백 |
| 디스크 부족 | 사전 여유 체크 |
| Unicode/특수문자 | `Path.GetInvalidFileNameChars()` 치환 |

## 9. 구현 순서
1. 솔루션/프로젝트 스캐폴딩
2. NuGet + MVVM 뼈대
3. `FfmpegProvisioner`
4. `DownloadService` MP3 → MP4
5. MainWindow UI + 바인딩
6. 에러/취소/진행률
7. 파일명 sanitize + "폴더 열기"
8. Publish + 스모크 테스트
