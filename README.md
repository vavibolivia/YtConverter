# YtConverter

YouTube URL → MP3 / MP4. Windows 데스크톱 (.NET 8 WPF, single-file exe).

**[Download latest → YtConverter.App.exe](https://github.com/vavibolivia/YtConverter/releases/latest/download/YtConverter.App.exe)**
Windows 10/11 x64 · 런타임 불필요 · ~69 MB

---

## Author

**Sukho Jung** — .NET / Windows desktop engineer. Open to roles.
[vavibolivia@gmail.com](mailto:vavibolivia@gmail.com) · [github.com/vavibolivia](https://github.com/vavibolivia)

- C# / .NET 8 · WPF · MVVM · async / IProgress / CancellationToken
- Media pipeline: YoutubeExplode + FFmpeg auto-provisioning, Unicode filenames
- Race / cross-thread 버그 추적 및 수정, 메모리 누수 검증
- Self-contained single-file exe 배포, GitHub Releases

---

## Features

- MP3 (libmp3lame Q2, 48 kHz), MP4 (H.264 + AAC, up to 1440×1080)
- 20–25× realtime
- 병렬 큐 + 중단 복구 (재시작 시 큐 복원, 동일 파일 skip)
- FFmpeg 최초 실행 시 자동 설치
- 한국어 UI, 이모지/특수문자 파일명 처리
- 로그: `%LOCALAPPDATA%\YtConverter\logs`

---

## Stack

| | |
| --- | --- |
| UI | WPF, XAML, MVVM (CommunityToolkit.Mvvm 8.4) |
| Runtime | .NET 8 (`net8.0-windows`) |
| YouTube | [YoutubeExplode 6.5](https://github.com/Tyrrrz/YoutubeExplode) |
| Convert | YoutubeExplode.Converter + [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) |
| Queue | JSON (`%LOCALAPPDATA%\YtConverter\queue.json`) |

---

## Build

```bash
git clone https://github.com/vavibolivia/YtConverter.git
cd YtConverter
dotnet publish src/YtConverter.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

```bash
dotnet run --project src/YtConverter.Tests              # E2E
dotnet run --project src/YtConverter.Tests -- --stress  # stress loop
```

Docs: [implementation-plan](docs/implementation-plan.md) · [test-plan](docs/test-plan.md) · [learning-notes](docs/learning-notes.md) · [manual](docs/manual.md)

---

## License

[MIT](LICENSE). YoutubeExplode (LGPL-3.0), FFmpeg (LGPL/GPL).
