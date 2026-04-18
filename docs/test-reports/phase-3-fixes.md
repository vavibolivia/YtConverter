# Phase 3 — 수정 내역
_적용: 2026-04-19 / 엔지니어_

## 수정
| ID | 심각도 | 문제 | 수정 |
| --- | --- | --- | --- |
| **I-14** | High | FfmpegProvisioner 병렬 첫 기동 시 zip 다운로드 race (3개 스레드 동시 `File.Create`) | 전역 `SemaphoreSlim _ensureLock`, double-check after lock acquire |
| **I-15** | High | Windows Defender Controlled Folder Access 가 `%LocalAppData%\YtConverter` 차단 시 ctor 크래시 | AppLogger / QueueStore / DownloadService 의 `Directory.CreateDirectory` 모두 try/catch 로 감쌈. 실패 시 Warn 로그 + 런타임은 동작 지속 |
| **I-16 / P3-04** | Medium | 🔁 📂 ⏹ ✕ 아이콘 버튼 4종에 `AutomationProperties.Name` 없음 → 스크린리더 식별 불가 | 각 버튼에 Loc 키(`btn_retry`, `btn_open_in_folder`, `btn_cancel`, `btn_remove`) 바인딩, ToolTip + AutomationProperties.Name 모두 설정 (8개 언어 번역 포함) |
| **P3-01 / I-01 부수** | Low | Retry 연타 race — Status 가 Failed 상태에서 버튼 빠르게 2번 누르면 세마포 대기열에 2개 진입 → 중복 실행 | `RunJobAsync` 진입부에 `if (job.IsRunning || job.Cts is not null) return;` 가드 |
| **NEW-C** | Medium | Clipboard regex 가 `m.youtube.com`, `music.youtube.com` 미지원 | regex 에 `m\.` `music\.` 브랜치 추가 (MainWindow.xaml.cs + MainViewModel.cs 동시 업데이트) |

## 미해결 / 다음 Phase
- NEW-A (Low-Med) 프록시 변경 대응
- NEW-D (Low) DisplayTitle surrogate pair 안전 컷
- I-04 .tmp 실패 silent swallow (로깅)
- I-05 work/ 루트 7일 purge
- I-06 Playlist 전개 기능
- I-07 디스크 공간 precheck
- I-09 Queue schema versioning
- I-10 FileDrop/ANSI URL
- I-11 AppLogger DetachSink
