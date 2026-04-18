# 🎵 YtConverter — YouTube → MP3 / MP4 변환기

[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)](https://learn.microsoft.com/dotnet/desktop/wpf/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](#라이선스)
[![Release](https://img.shields.io/github/v/release/vavibolivia/YtConverter?include_prereleases&label=release)](https://github.com/vavibolivia/YtConverter/releases)
[![Downloads](https://img.shields.io/github/downloads/vavibolivia/YtConverter/total)](https://github.com/vavibolivia/YtConverter/releases)

> **가볍고 빠른 Windows 전용** YouTube URL → MP3 / MP4 변환 데스크톱 앱.
> URL 붙여넣기 → 포맷 선택 → 저장. 끝. 광고/회원가입/번들웨어 **전혀 없음**.

![screenshot](docs/screenshot.png)

---

## ✨ 주요 기능

- 🎧 **MP3 변환** — 원본 오디오 최고 비트레이트 (보통 192~256 kbps / 48 kHz)
- 🎬 **MP4 변환** — 원본 영상 최고 화질 (H.264 + AAC, 최대 1440x1080 확인)
- ⚡ **실시간 대비 20~25배 빠른 변환** (3분 42초 영상을 10초 내외)
- 📁 **폴더 열기** — 변환 결과를 탐색기로 바로 열기
- 🛡️ **FFmpeg 자동 설치** — 최초 실행 시 gyan.dev essentials 자동 다운로드·캐시
- 🇰🇷 **한국어 UI**, Unicode 파일명 지원 (이모지·특수문자 안전 처리)
- 🔁 **취소·에러 복구** — 중간 취소 시 임시 파일 자동 정리
- 📝 **로그 창** — 변환 이력·에러를 UI 및 `%LOCALAPPDATA%\YtConverter\logs` 에 기록

---

## 🚀 빠른 시작

### 1. 다운로드
[Releases 페이지](https://github.com/vavibolivia/YtConverter/releases/latest) 에서 `YtConverter.App.exe` 를 받아 더블클릭하면 끝입니다.

- **설치 불필요** (self-contained single-file, 약 72 MB)
- .NET 런타임 별도 설치 **필요 없음**
- Windows 10 / 11 (64-bit)

### 2. 사용
1. URL 창에 YouTube 링크 붙여넣기 (또는 **붙여넣기** 버튼)
2. **MP3 (오디오)** 또는 **MP4 (영상)** 라디오 선택
3. 저장 폴더 확인 (기본: `내 음악\YtConverter`)
4. **변환 시작** 클릭
5. 완료 후 **폴더 열기** 로 결과 확인

자세한 사용법은 [**매뉴얼**](docs/manual.md) 참고.

---

## 🧪 검증된 변환 품질

테스트 영상: Mariah Carey - If It's Over (MTV Unplugged, 3:42)

| 포맷 | 크기 | 변환 시간 | 실시간 대비 | 스트림 정보 |
| --- | ---: | ---: | ---: | --- |
| MP3 | 6.66 MB | 10.3 s | **21.5×** | mp3 252 kbps / 48 kHz |
| MP4 | 50.94 MB | 8.9 s | **25.0×** | h264 1440×1080 + aac 129 kbps |

스트레스 테스트(무한 반복)에서 메모리 누수·크래시 없이 안정 동작 확인.

---

## 🏗️ 기술 스택

| 계층 | 사용 기술 |
| --- | --- |
| UI | WPF, XAML, MVVM (CommunityToolkit.Mvvm 8.4) |
| 런타임 | .NET 8 (net8.0-windows) |
| YouTube 추출 | [YoutubeExplode 6.5](https://github.com/Tyrrrz/YoutubeExplode) |
| MP3/MP4 변환 | [YoutubeExplode.Converter](https://github.com/Tyrrrz/YoutubeExplode) + [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) (자동 프로비저닝) |
| 로그 | 자체 파일 로거 (`%LOCALAPPDATA%\YtConverter\logs`) |

상세 설계: [docs/implementation-plan.md](docs/implementation-plan.md)
학습 노트: [docs/learning-notes.md](docs/learning-notes.md)
테스트 계획 (20 에이전트): [docs/test-plan.md](docs/test-plan.md)

---

## 🛠️ 직접 빌드

```bash
git clone https://github.com/vavibolivia/YtConverter.git
cd YtConverter
dotnet build YtConverter.sln

# 개발 실행
dotnet run --project src/YtConverter.App

# 릴리스 단일 파일 exe
dotnet publish src/YtConverter.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

요구: **.NET 8 SDK**, Windows 10/11

---

## 🧪 E2E 테스트

```bash
# 기본 URL(Mariah Carey) 로 MP3 + MP4 변환 + ffprobe 검증
dotnet run --project src/YtConverter.Tests

# 사용자 URL
dotnet run --project src/YtConverter.Tests -- "https://www.youtube.com/watch?v=..."

# 무한 스트레스 테스트 (취소·동시·무효 URL 섞어서 반복)
dotnet run --project src/YtConverter.Tests -- --stress
```

---

## ❓ FAQ

**Q. 왜 최초 실행이 느려요?**
A. FFmpeg(~100MB)을 `%LOCALAPPDATA%\YtConverter\ffmpeg\` 에 다운로드·캐시합니다. 이후 실행은 즉시 시작됩니다.

**Q. 라이브 스트리밍도 되나요?**
A. 라이브는 지원하지 않습니다 ("라이브 스트림은 변환할 수 없습니다" 에러로 차단).

**Q. 재생목록 URL 을 넣으면?**
A. 재생목록의 **현재 영상** 만 변환합니다. 일괄 변환은 로드맵.

**Q. 연령제한/비공개/지역제한 영상은?**
A. 명확한 한국어 에러 메시지를 보여주고 앱은 정상 상태를 유지합니다.

**Q. Mac/Linux 는?**
A. WPF 는 Windows 전용입니다. 다른 플랫폼 계획은 추후 검토.

---

## 📋 로드맵

- [ ] 재생목록 일괄 변환
- [ ] 비트레이트/해상도 선택 UI
- [ ] 드래그앤드롭 URL 입력
- [ ] 변환 큐 / 동시 여러 건
- [ ] winget 패키지 배포
- [ ] 자동 업데이트 (Velopack)
- [ ] 다국어 UI (ko/en)

---

## ⚖️ 면책 / 라이선스

본 프로그램은 **개인의 합법적 사용 범위** (타임시프트, 백업 등) 를 위한 도구입니다.
저작권으로 보호되는 콘텐츠의 무단 배포/재배포 책임은 사용자에게 있습니다.
YouTube 서비스 약관과 각국 저작권법을 준수하십시오.

본 프로젝트는 [MIT License](LICENSE) 로 배포됩니다.
YoutubeExplode (LGPL-3.0), FFmpeg (LGPL/GPL) 등 의존 라이브러리의 라이선스도 함께 확인하세요.

---

## 🙌 기여

- 🐛 [Issues](https://github.com/vavibolivia/YtConverter/issues) — 버그 리포트·기능 제안
- 🔧 Pull Request 환영 — 작은 문구 수정도 좋습니다
- ⭐ **Star** 는 프로젝트에 큰 힘이 됩니다

---

## 👤 작성자 / Author

**Sukho Jung** (정석호)

[![Email](https://img.shields.io/badge/Email-vavibolivia%40gmail.com-D14836?logo=gmail&logoColor=white)](mailto:vavibolivia@gmail.com)
[![GitHub](https://img.shields.io/badge/GitHub-vavibolivia-181717?logo=github)](https://github.com/vavibolivia)

> **🟢 Open to opportunities** — 채용/프리랜스/협업 제안 환영합니다.

### 이력 요약 / Profile
- **Windows 데스크톱 / .NET 엔지니어** (WPF · WinForms · MVVM)
- **C# / .NET 8** 기반 제품·도구 설계 및 구현
- 오디오/비디오 처리 (FFmpeg, YoutubeExplode)
- 비동기·진행률 보고·취소 토큰 등 **사용자 체감 품질**에 강점
- 20-역할 테스트 에이전트 설계 + 무한 스트레스 테스트로 **안정성 확보** 경험

### 연락 / Contact
- 📧 **vavibolivia@gmail.com** — 채용·협업·문의 환영
- 🐙 **https://github.com/vavibolivia** — 추가 프로젝트

### Tech Stack Highlights
`C#` `.NET 8` `WPF` `XAML` `MVVM` `async/await` `IProgress<T>` `CancellationToken`
`YoutubeExplode` `FFmpeg` `ZipArchive` `HttpClient` `WinUI` `Clean Architecture`
`MSBuild` `dotnet publish` `self-contained single-file exe`

---

### For recruiters / 채용 담당자께

이 프로젝트(YtConverter) 는 다음 역량을 보여줍니다:
1. **요구사항 → 설계 → 구현 → 테스트 → 배포** 전체 사이클을 혼자 완수
2. **UX 품질** — 실시간 진행률, 취소, 에러 매핑, 한국어 현지화
3. **견고함** — race condition 수정, 메모리 누수 검증, 에러 경계
4. **배포 엔지니어링** — self-contained single-file exe, FFmpeg 자동 프로비저닝
5. **문서화** — 구현 계획, 테스트 계획(20 에이전트), 학습 노트, 사용 매뉴얼

부담 없이 **vavibolivia@gmail.com** 으로 연락 주세요.

---

## 🔍 Keywords

`youtube` `mp3` `mp4` `converter` `downloader` `wpf` `dotnet` `csharp` `ffmpeg` `youtubeexplode`
`windows` `음원추출` `유튜브다운로드` `유튜브mp3` `유튜브mp4변환` `한국어`

---

## 🌐 English Summary

**YtConverter** is a lightweight WPF desktop app for Windows that converts any YouTube URL into MP3 (audio) or MP4 (video) files. No ads, no sign-up, no bundled crapware.

- **.NET 8** + **WPF** + **YoutubeExplode** + **FFmpeg** (auto-provisioned)
- Single-file self-contained `.exe` (~72 MB) — download and run
- 20–25× realtime conversion speed
- Korean UI, Unicode filename-safe
- MIT License

Download from [Releases](https://github.com/vavibolivia/YtConverter/releases/latest) and double-click to run. Windows 10 / 11 (x64).

**Disclaimer**: For personal lawful use only. Respect YouTube ToS and local copyright law.
