# 프로젝트 주요 정보
_최종 갱신: 2026-04-18 22:15_

## 개요
- **이름:** YouTube to MP3/MP4 Converter (WPF)
- **경로:** `C:\workspace\youtube`
- **목적:** YouTube URL → MP3 또는 MP4 파일로 변환하는 Windows 데스크톱 앱

## 기술 스택
- Windows 11, .NET SDK 8.0.418
- WPF (`net8.0-windows`, `UseWPF=true`)
- NuGet:
  - `YoutubeExplode` 6.5.7 — YouTube 스트림 추출
  - `YoutubeExplode.Converter` 6.5.7 — FFmpeg 기반 mux/트랜스코드
  - `CommunityToolkit.Mvvm` 8.4.2 — MVVM
- FFmpeg: gyan.dev release-essentials zip → `%LOCALAPPDATA%\YtConverter\ffmpeg\ffmpeg.exe` & `ffprobe.exe` (최초 실행 시 자동 다운로드)

## 디렉터리 구조
```
C:\workspace\youtube\
├── YtConverter.sln
├── requirements.md
├── docs\
│   ├── implementation-plan.md
│   ├── test-plan.md          # 20 테스트 에이전트 분담
│   ├── learning-notes.md
│   └── project-info.md       # 이 파일
├── logs\
│   ├── engineer.log          # Phase 별 진행
│   └── snap.ps1              # WPF 윈도우 스크린샷 스크립트
├── .claude\
│   ├── settings.json
│   └── agents\
│       ├── project-lead.md
│       ├── engineer.md
│       └── tester.md         # 20 역할: short-video, long-video, live-stream ...
└── src\
    ├── YtConverter.App\
    │   ├── App.xaml(.cs)
    │   ├── MainWindow.xaml(.cs)
    │   ├── Models\{OutputFormat, JobStatus}.cs
    │   ├── Logging\AppLogger.cs
    │   ├── Services\{IFfmpegProvisioner, FfmpegProvisioner, IDownloadService, DownloadService}.cs
    │   └── ViewModels\MainViewModel.cs
    └── YtConverter.Tests\
        ├── Program.cs        # E2E 러너: ffprobe로 음질/속도/길이/용량 검증
        └── YtConverter.Tests.csproj
```

## 팀 에이전트
| 이름 | 역할 | 도구 | 제한 |
| --- | --- | --- | --- |
| project-lead | 계획/테스트계획/학습노트 지속 갱신 | Read/Write/Web/Bash | 코드 작성 금지 |
| engineer | WPF 구현 + 디버깅, Phase 로그 | Read/Edit/Write/Bash | docs 수정 금지 |
| tester | 담당 ROLE 만 집중 검증, 결과 `logs/test-<ROLE>.log` | Read/Bash/Web | 코드 수정 금지 |

## 외부 경로
- FFmpeg 캐시: `%LOCALAPPDATA%\YtConverter\ffmpeg\`
- 앱 런타임 로그: `%LOCALAPPDATA%\YtConverter\logs\yt-YYYYMMDD.log`
- 테스트 출력: `%TEMP%\YtConverter.Tests\<타임스탬프>\`
- 기본 사용자 저장 폴더: `%USERPROFILE%\Music\YtConverter`

## 빌드 / 실행
```bash
# 빌드
cd C:/workspace/youtube && dotnet build YtConverter.sln

# 앱 실행 (WPF)
dotnet run --project src/YtConverter.App/YtConverter.App.csproj

# E2E 테스트 실행
dotnet run --project src/YtConverter.Tests/YtConverter.Tests.csproj -- "<url>"
# 기본 URL: https://www.youtube.com/watch?v=9q3Rg0xKmpM (Mariah Carey - If It's Over)

# Release 배포
dotnet publish src/YtConverter.App -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 최신 테스트 결과 (2026-04-18)
| 포맷 | 상태 | 크기 | 변환시간 | 영상길이 | 배속 | 비트레이트 | 스트림 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| MP3 | PASS | 6.66 MB | 10.3s | 3:42 | 21.5x | 252 kbps | mp3 48kHz |
| MP4 | PASS | 50.94 MB | 8.9s | 3:42 | 25.0x | 1927 kbps | h264 1440x1080 + aac 48kHz |

**전체 상태:** 100% 기능 동작 확인 (MP3/MP4 변환, FFmpeg 자동 프로비저닝, 진행률, 로깅, UI 렌더)

## 인증 / 배포 (민감정보 미기록)
- GitHub: 이메일 `vavibolivia@gmail.com`
- 인증 방법: **`gh auth login` OAuth 플로우 권장** (패스워드/토큰 파일에 기록 금지)
- 패키지 배포: 아직 미진행
