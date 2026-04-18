# Phase 2 — 심층 테스트 리포트 (YtConverter v0.3.1)

_작성: 2026-04-18 · 테스트 PL: Phase 2 Auditor · 기준 커밋: Phase 1 fix 적용본_

---

## 0. 조사 범위

- **소스**: `src/YtConverter.App/**/*.{cs,xaml}` 정적 검토 (Phase 1 fix 반영본)
- **런타임 로그**: `C:\Users\jjsuk\AppData\Local\YtConverter\logs\yt-20260418.log` (296 라인)
- **큐 파일**: `C:\Users\jjsuk\AppData\Local\YtConverter\queue.json` (2건, 모두 Failed)
- **출력 폴더**: `C:\Users\jjsuk\Music\YtConverter\` (6 MP3 + `.mp3.stream-0.tmp` 210 MB 고아 **잔존**)
- **작업 캐시**: `C:\Users\jjsuk\AppData\Local\YtConverter\work\F16729F50A0274B8\audio.webm` (1.8 MB 고아)
- **빌드**: `bin/Debug/net8.0-windows/YtConverter.App.exe` 갱신 시각 `23:48` — 마지막 실행 `23:39:46` **이후에 재빌드됨** (fix 반영 binary 가 아직 실행되지 않음)

> 주의: 2번의 재기동(23:39 이전) 로그 어디에도 `"고아 임시파일 정리"` 문자열 없음 — 새 바이너리가 한 번도 돈 적 없기 때문이며, 이는 fix 의 문제는 아님.

---

## 1. Phase 1 수정 회귀 검증 (7건)

| ID | 결과 | 근거 |
| --- | --- | --- |
| **B-01** | **PASS (B-01 fix verified)** | `MainViewModel.cs:205-272` — `RunJobAsync` 가 진입 시 `var slots = _slots;` 로컬 참조를 캡처. try/catch/finally 의 3 경로(성공·OperationCanceledException·일반 예외) 모두 **동일한** outer `finally` 를 경유해 `slots.Release()`. `_slots` 필드가 교체되어도 원래 인스턴스가 release 되므로 `SemaphoreFullException` 불가. 추가로 `line 267-270` 에서 모든 작업이 유휴일 때만 세마포 재설정 — 실행 중 교체 배제. |
| **B-02** | **PASS (B-02 fix verified)** — *단, private/shorts 커버 조건부* | `DownloadService.cs:104` → `StableJobId(video.Id.Value, format)`. `_yt.Videos.GetAsync(url)` 는 `shorts/`, `youtu.be/`, `watch?v=X&t=&list=` 를 모두 같은 canonical `video.Id` 로 정규화하므로 shorts OK. **Private/deleted**: `VideoUnavailableException` 이 `_yt.Videos.GetAsync` 라인 60 에서 먼저 throw → `MapException` 으로 래핑되어 hash 단계까지 **도달하지 않음**. 즉 "hash 가 비공개에서도 동작" 여부는 N/A (그 전에 실패 분기로 빠짐). |
| **B-03** | **PASS (B-03 fix verified)** | `DownloadService.cs:386-395` switch expression. C# switch 는 상단부터 pattern match — `UnauthorizedAccessException => ...` (line 392) 가 `IOException => ...` (line 393) 보다 위에 있음. `UnauthorizedAccessException` 이 `IOException` 의 하위 클래스가 아니므로 (양쪽 모두 `SystemException` 직속) 순서가 기술적으로 필수는 아니나, **명시적 처리**로 UI 메시지 "저장 폴더에 쓰기 권한이 없습니다" 분기가 정확히 발화. |
| **B-05** | **PARTIAL PASS** (B-05 fix verified — 단 혼합 시나리오 주의) | `MainViewModel.cs:133` `if (Jobs.Any(j => j.Status == JobStatus.Idle))` 게이트로 Failed/Canceled **단독** 일 때 자동 재개 억제 ✓. 그러나 **Idle + Failed 혼합** 시 게이트 통과 후 `StartAllCommand.ExecuteAsync` → `StartAllAsync(line 187)` 이 `JobStatus.Idle or JobStatus.Failed or JobStatus.Canceled` 모두 포함해 실행. **결과적으로 Failed 도 자동 재시도됨**. 사양의 엄밀한 해석("Idle 만")에는 어긋남. 또한 `MainWindow.xaml` 에 개별 잡의 "재시도" 버튼이 없음 → Failed 를 다시 돌리려면 "모두 시작" 밖에 없음 (다른 Idle 작업과 번들) → 역설적으로 B-05 해결 후 오히려 Failed 단독 복원 시나리오에서 **사용자가 재시도 불가** 한 UX 구멍 발생. 아래 **I-01** 참조. |
| **B-07** | **PASS (B-07 fix verified)** | `QueueStore.cs:34-35` `Load()` 진입 직후 `_path + ".tmp"` 존재 시 `File.Delete`. `try { ... } catch { }` 로 감싸 AV 스캐너가 파일을 잠가도 Load 자체는 통과. 빈 `catch { }` 가 예외를 **조용히 삼키는** 것은 맞으나, `Load()` 전체가 이미 `catch (Exception)` 에서 `AppLogger.Warn` 을 호출하므로 **심각도 낮음**. 다만 .tmp 삭제 실패 자체는 로그에 남지 않음 (아래 **I-04**). |
| **B-10** | **PASS (B-10 fix verified)** — *단, 0-byte 삭제 조건부* | `DownloadService.cs:346-378` `CleanupStaleArtifacts`. `.mp3.stream-`, `.mp4.stream-`, `.part`, `.ytdl-tmp` 만 age-gated 로 삭제. 성공 변환 `Title.mp3` 는 어느 분기에도 걸리지 않음 ✓ (검증: `"keep praying… He's still listening..mp3"` 는 `.mp3` 로 끝나지만 `.mp3.stream-` 포함 안함). **그러나**: `else if (new FileInfo(f).Length == 0)` 분기가 **age 체크 없이** 0-byte 파일을 무조건 삭제. 사용자가 Output 폴더를 바꿔 공용 폴더(예: Desktop)로 지정했다면 **자신의 0-byte 메모 파일이 앱 재시작마다 조용히 사라짐**. 아래 **I-02** 참조. |
| **B-12** | **PASS (B-12 fix verified)** — *단, headless 제약* | `MainViewModel.cs:144-163` `PersistQueue` — `Application.Current?.Dispatcher` 가 있으면 `CheckAccess()` 로 UI 스레드 여부 확인, UI 스레드에서 스냅숏 생성 후 `Task.Run` 으로 I/O 위임. `dispatcher is null` (테스트/`Program.cs` 직접 실행) 분기에서는 **호출 스레드가 곧 스냅숏 스레드** — JobViewModel 을 UI 없이 사용하는 테스트는 자체적으로 single-threaded 이므로 안전. 단, 미래에 헤드리스 병렬 테스트 추가 시 `Jobs.Select(...).ToList()` 가 다른 스레드의 Add/Remove 와 충돌 가능. 아래 **I-05** 참조. |

---

## 2. Phase 1 Next 제안 검증 (5건)

### P2-01 · Fault injection — 네트워크 차단 중 Infinite timeout 매달림

**결과: FAIL (기존 이슈 재확인)**

- `DownloadService.cs:34` `_http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };`
- 청크별 `SendAsync`/`ReadAsync` 에 per-request timeout 없음. 사용자가 Wi-Fi 끊으면 TCP keepalive 가 기본값(2h+) 이라 `ReadAsync` 가 수시간 매달림.
- 작업의 `CancellationToken ct` 는 `Input.ReadAsync(buf, ct)` 에 전달되므로 사용자가 ⏹ 버튼 누르면 탈출 가능. 그러나 **자동 복구 없음**.
- **재현 방법**: 3 GB mp4 60% 지점에서 `netsh interface set interface "Wi-Fi" admin=disable` — 관찰 시 progress 정지, 로그에도 에러 안 남음.
- **권장**: `HttpClient.Timeout = TimeSpan.FromMinutes(2)` + 청크 단위 재시도 (exponential backoff), 또는 `req.Options.SetRequestTimeout(30s)`.

### P2-02 · Playlist 처리 UX

**결과: FAIL (기능 부재)**

- `DownloadService.ConvertAsync(url)` 은 `video.Id.Value` 기반 단일 비디오만 다운로드. `&list=XYZ` 는 hash 에 포함되지 않음 (B-02 fix 덕분) → 항상 첫 영상만 변환.
- `MainViewModel.Add()` (line 166-180) 도 URL 을 그대로 큐에 넣을 뿐 playlist 전개 안 함.
- **UX 결함**: 사용자가 playlist URL 을 붙여넣으면 "30 곡 저장되겠지" 기대하지만 1곡만 저장됨. Hint 도 없음.
- **권장**: `url` 에 `list=` 감지 시 "재생목록을 모두 추가할까요?" 프롬프트 + `YoutubeClient.Playlists.GetVideosAsync` 로 전개.

### P2-03 · 디스크 포화

**결과: PARTIAL — 정책 부재**

- 사전 디스크 공간 체크 없음 (grep `DriveInfo` / `Free` → no match).
- 다운로드 중 `IOException` 발생 시 `MapException` → "저장 공간이 부족하거나…" 메시지 ✓.
- FFmpeg mux 중 디스크 포화: `RunFfmpegAsync` 의 catch 에서 `.part` 삭제 (line 179). 그러나 `work/*/audio.webm`, `video.mp4` 는 남음 (다음 run 에 재사용 의도). 재시도 시 디스크 여전히 부족이면 또 실패 → 영원히 work 가 커짐.
- **finally 블록** (line 203-218) 은 `placeholderStillReserved == true` 일 때만 출력 폴더 0-byte placeholder 만 삭제 — work dir 의 부분 파일은 무관.
- **권장**: 변환 시작 전 `DriveInfo(outputFolder).AvailableFreeSpace` 와 `video.Size * 1.5` 비교해 조기 실패.

### P2-04 · 접근성 (스크린리더가 StatusGlyph 이모지를 "Unicode 9203" 로 읽음)

**결과: FAIL (접근성 결함 재확인)**

- `MainWindow.xaml:247` `<TextBlock Grid.Column="0" Text="{Binding StatusGlyph}" FontSize="18" ...>` — `AutomationProperties.Name` 바인딩 없음.
- `JobViewModel.StatusGlyph` 는 ⏳ 🔎 ⬇ ⚙ ✅ ❌ ⏹. Narrator 가 **읽을 수 있는 한국어/영문 레이블 없음**.
- 검색: `AutomationProperties.Name` 은 XAML 전체에 **0건**, `AutomationId` 만 11건 (자동화 테스트용).
- **권장**: `<TextBlock AutomationProperties.Name="{Binding StatusText}" ...>` 또는 XAML 에서 `AutomationProperties.LiveSetting="Polite"` 로 상태 변화 알림.

### P2-05 · Queue schema evolution

**결과: RISK (대비 없음)**

- `QueueStore.Load()` 는 `JsonSerializer.Deserialize<List<JobSnapshot>>`. extra field 는 무시, missing field 는 default value ✓.
- **버전 필드 없음** — `JobSnapshot` 에 `SchemaVersion` 등 없음.
- 위험 1: enum 값 재정의 (`JobStatus`, `OutputFormat`) — 구버전 `.json` 에 `"Mp3"` 저장되어 있는데 enum 에서 삭제되면 JsonException → catch 로 빈 리스트 로드 → **큐 소실**.
- 위험 2: 새 필수 필드 추가 시 구버전 JSON 은 default(null/empty) → 동작은 하지만 Title/Url 공란 복원.
- **권장**: `JobSnapshot.SchemaVersion` 추가 + `Load()` 마이그레이션 분기.

---

## 3. 새 각도 5건

### NEW-01 · 24h 스트레스 메모리/파일 핸들

**결과: WARN — 싱크 누적 리스크**

- `AppLogger._sinks` = `ConcurrentQueue<Action<string>>`. `AttachSink` 만 있고 Detach 없음.
- `MainViewModel.ctor:98` `AppLogger.Instance.AttachSink(AppendLog);` — MainViewModel 재생성 시 이전 sink 클로저가 계속 큐에 남아 **이전 VM 및 `LogLines` 컬렉션이 GC 안 됨**.
- 프로덕션에선 MainViewModel 이 싱글톤처럼 1회만 생성되어 실제 누수 영향 미미. **테스트 하네스** 는 반복 생성 가능 → gen2 누적.
- 로그 파일: `File.AppendAllText` (AppLogger.cs:39) 는 open/write/close — handle 누수 없음 ✓.
- `LogLines.RemoveAt(0)` 로 500 라인 유지 ✓ (MainViewModel:373).
- `StressRunner` 는 `DownloadService` 인스턴스 1개 공유 → HttpClient 누수 없음 ✓.
- **권장**: `DetachSink` API 추가, 또는 `WeakReference<Action>` 으로 sink 보관.

### NEW-02 · FFmpeg kill 후 좀비

**결과: PASS (대체로 안전)**

- `DownloadService.cs:306-309` `ct.Register(() => { if (!proc.HasExited) proc.Kill(entireProcessTree: true); });`
- `Process.Kill(true)` 는 .NET 5+ Windows 에서 **Job Object API** 사용 — child 프로세스도 종결 ✓.
- `WaitForExitAsync(CancellationToken.None)` 로 Kill 후 exit 까지 블록 — 핸들 disposal 전 확실한 종료 ✓.
- `using proc` 은 `WaitForExitAsync` 이후 dispose → kernel 객체 누수 없음.
- FFmpeg 자체는 보통 child 를 스폰하지 않으므로 영향 미미. 다만 **Kill 이후 handle disposal 사이** 1-2ms race 에서 `proc.Refresh` 전에 `HasExited` 가 정확하지 않을 수 있으나 `catch { }` 로 안전.
- `ct.Register` 의 반환 `CancellationTokenRegistration` 은 `using` 으로 dispose → 콜백 해제 OK.

### NEW-03 · settings.json 손상/읽기실패 시 기동

**결과: PASS (graceful fallback)**

- `LocalizationService.cs:67-78` `Load()`:
  ```csharp
  try { ... var s = JsonSerializer.Deserialize<SettingsDto>(json);
        if (s is not null && Translations.ContainsKey(s.Lang)) _lang = s.Lang; }
  catch { }
  ```
- 손상 JSON / IO Lock / missing 디렉터리 모두 `catch` 로 흡수. `_lang = "en"` 기본값 유지 → 앱은 영어로 기동 ✓.
- 알 수 없는 언어 코드 (예: `"xx"`)도 `Translations.ContainsKey` 로 거부 ✓.
- **결함 (B-13 기존 지적)**: 손상 시 **로그 조차 없음** — 사용자는 왜 언어가 리셋됐는지 모름. 앞으로 `AppLogger.Warn($"settings.json 손상, 기본값 사용: {ex.Message}")` 권장.

### NEW-04 · DPI 스케일링 200%

**결과: WARN — 최소 창 크기 과대**

- `MainWindow.xaml:9` `MinHeight="620" MinWidth="740"` (DIP 기준).
- 200% DPI 에선 physical 1240×1480. 1920×1080 디스플레이의 작업 표시줄 40px, 창 chrome 30px 제외 시 가용 1920×1010 → **세로 부족** (1480 > 1010).
- 결과: Windows 가 자동으로 창을 화면에 맞추나 `MinHeight` 이 커서 **ResizeMode=NoResize** 가 아닌 이상 사용자가 창을 세로로 줄일 수 없음 → 하단 "저장 폴더" Row 가 가려짐.
- Card (`Style="{StaticResource Card}"`) 는 내부에서 상대 레이아웃이라 일반 해상도에선 OK.
- **권장**: `MinHeight="480" MinWidth="640"` 로 완화, 또는 100% DPI 기준 MaxHeight 에 가까운 값 사용.

### NEW-05 · 브라우저 탭 드래그 Drop 데이터 포맷

**결과: PARTIAL FAIL — UniformResourceLocator(ANSI) 미지원, FileDrop 시 배너 혼선**

- `MainWindow.xaml.cs:74-90` `ExtractDroppedText` 지원 포맷:
  - `DataFormats.UnicodeText` ✓
  - `DataFormats.Text` ✓
  - `"UniformResourceLocatorW"` (wide) ✓
- **미지원**: `"UniformResourceLocator"` (ANSI 레거시). 구버전 IE 나 일부 앱이 유니코드 포맷을 제공하지 않을 때 누락.
- **미지원**: `DataFormats.FileDrop` — Chrome 이 이미지/링크를 드래그할 때 FileDrop 에 임시 파일 경로가 같이 옴. UnicodeText 가 fallback 으로 있지만, Chrome 이 UnicodeText 없이 FileDrop 만 주면 `null` 반환 → Drop 배너는 나타나지만 (`HasDroppableUrl` 의 regex 검사가 통과했기 때문은 아님… 다시 보니 통과 못 해서 배너 안 뜸) — 실제로는 배너도 안 뜨고 아무 반응 없음 → UX 혼선.
- **재현**: Chrome 주소창 "자물쇠" 아이콘을 앱 창으로 드래그 → UnicodeText 포함되어 OK. 단, Chrome 의 이미지 탭을 드래그하면 UnicodeText 없고 FileDrop 만 → 드롭 불가.
- **권장**: `DataFormats.FileDrop` 분기 추가 (파일이 `.url` 이면 내용에서 URL= 추출) + `"UniformResourceLocator"` ANSI 분기.

---

## 4. 새로 발견된 이슈 (우선순위 Top 3 + 추가)

### I-01 · Failed 작업을 단독으로 재시도할 UI 없음 (Medium, B-05 fix 부수효과)

- **위치**: `MainWindow.xaml` (Job row, line 280-284) — 아이콘 버튼 📂/⏹/✕ 만 있고 **🔁(재시도)** 없음.
- **재현**:
  1. 큐에 Failed 1건만 존재 (Idle 없음)
  2. `StartAllCommand.CanExecute` 는 `true` (Failed 포함)
  3. 그러나 자동 재개 게이트 `MainViewModel.cs:133` 은 `Idle` 만 체크 → 자동 트리거 안 함 ✓ (B-05 의도)
  4. 사용자가 "모두 시작" 누르면 Failed 재시도 되지만, **다른 잡이 혼재 된 경우** Failed 도 같이 시작됨 (의도치 않게).
- **영향**: B-05 fix 는 "auto-resume 억제" 만 달성. Manual 재시도 경로가 애매.
- **제안**: 각 Failed/Canceled 행에 `🔁` 버튼 추가, `RetryCommand` 이 해당 잡만 `RunJobAsync` 호출.

### I-02 · CleanupStaleArtifacts 가 사용자의 0-byte 파일을 무조건 삭제 (Medium)

- **위치**: `DownloadService.cs:368-372`
- **재현**:
  1. OutputFolder 를 `C:\Users\jjsuk\Desktop` 등 공용 폴더로 변경
  2. 사용자가 `placeholder.md` 0-byte 파일 소유
  3. 앱 재시작 → `CleanupStaleArtifacts` 가 age 체크 없이 즉시 삭제
- **영향**: 데이터 손실 가능. 특히 기본 OutputFolder 가 `%UserProfile%\Music\YtConverter\` 인데 사용자가 다른 음원 라이브러리 폴더 (`%UserProfile%\Music`) 로 바꾸면 0-byte metadata/lock 파일이 희생될 수 있음.
- **제안**: 0-byte 삭제에도 **age > 1h** 조건 추가. 또는 `.part`/`.stream-*` 패턴과 **조합된 이름** 일 때만 삭제.

### I-03 · HttpClient Timeout=Infinite 에 per-request timeout 래퍼 없음 (High, P2-01 확정)

- **위치**: `DownloadService.cs:34, 257` `using var resp = await _http.SendAsync(req, ...)`.
- **재현**: 다운로드 중 네트워크 단절 (Wi-Fi 비활성화). 진행률 멈춘 뒤 수시간 대기, TCP keepalive 타이머가 흘러야 예외 발생. 앱 강제 종료 외 복구 불가.
- **영향**: 장시간 매달림 → 사용자가 앱 크래시로 오인 → kill → 다음 기동 시 B-05 auto-resume 게이트에 걸려야 하는데 Idle 이면 재개… 무한 루프 가능.
- **제안**: HttpRequestMessage 당 `CancellationTokenSource.CreateLinkedTokenSource(ct)` 로 60s 보조 타이머 + 재시도 3회 exponential backoff.

### I-04 · `.tmp` 삭제 실패 silently swallowed (Low, B-07 부수)

- **위치**: `QueueStore.cs:35` `try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }`
- **영향**: AV/FS filter 가 .tmp 잠그면 매번 실패하지만 로그에 흔적 없음 → 장기적 디스크 오염 진단 어려움.
- **제안**: 빈 catch 를 `catch (Exception ex) { AppLogger.Instance.Warn($"queue.tmp 정리 실패: {ex.Message}"); }`.

### I-05 · work/ 루트의 오래된 job dir 축적 (Low)

- **위치**: `DownloadService.cs:39-42` `_workRoot = %LocalAppData%\YtConverter\work`. 정리는 성공 시 `line 197 Directory.Delete(workDir, true)` 만.
- **재현**: 사용자가 작업을 Cancel/Failed 로 끝낸 경우 work dir 이 남음. 같은 video+format 재시도 전까지 삭제 안 됨.
- **증거**: 현재 `F16729F50A0274B8\audio.webm` (1.8 MB) 고아.
- **제안**: 앱 시작 시 `_workRoot` 내 lastWrite > 7d 폴더 purge.

### I-06 · Playlist 전개 UX 부재 (Medium, P2-02 확정)

- (P2-02 와 동일, 별도 이슈 카드로 관리)

### I-07 · 디스크 공간 precheck 없음 (Medium, P2-03 확정)

- (P2-03 와 동일)

### I-08 · 접근성: StatusGlyph AutomationProperties.Name 없음 (Medium, P2-04 확정)

- (P2-04 와 동일)

### I-09 · Queue schema versioning 부재 (Low, P2-05 확정)

- (P2-05 와 동일)

### I-10 · Drop 데이터 포맷 UniformResourceLocator(ANSI) / FileDrop 미처리 (Low)

- NEW-05 참조.

### I-11 · AppLogger sink detach 불가 → 테스트 가동 시 메모리 누수 경로 (Low)

- NEW-01 참조.

### I-12 · MinHeight=620 DPI 200% 에선 1080p 에서 세로 짤림 (Low)

- NEW-04 참조.

### I-13 · MaxConcurrency 변경 시 `_slots` 는 모든 잡 유휴일 때만 재설정 — 부분 변경 미반영 (Medium)

- **위치**: `MainViewModel.cs:77-91, 266-270`.
- **재현**:
  1. MaxConcurrency=3, 잡 3건 실행 중
  2. 슬라이더 1 로 변경 → 로그에 "예약: 1건 적용" 출력
  3. 잡 1개가 끝나도 나머지 2개가 계속 실행 중이므로 `!Jobs.Any(j => j.IsRunning)` 은 false → 교체 안 됨
  4. 결국 처음 3개가 모두 끝나야 반영 → 대기 중인 **새 작업 2개** 는 기존 세마포 (count=3) 로 시작 가능 → "1로 줄였는데 3개가 동시 실행"
- **영향**: UX 기대 불일치. 단, 안전성은 확보 (B-01 의 SemaphoreFullException 없음).
- **제안**: `int _targetMax` 를 atomic 으로 유지, `RunJobAsync` 진입 시 `while (_runningCount >= _targetMax) await Task.Delay(...)` 패턴으로 교체, `_slots` 자체를 제거.

---

## 5. 회귀/진단 요약

| 범주 | 항목 | 상태 |
| --- | --- | --- |
| Fix 검증 | B-01 | **PASS** |
| Fix 검증 | B-02 | **PASS** (private/shorts 는 pre-fetch 단계에서 분기됨) |
| Fix 검증 | B-03 | **PASS** |
| Fix 검증 | B-05 | **PARTIAL** (I-01 파생) |
| Fix 검증 | B-07 | **PASS** (I-04 부수) |
| Fix 검증 | B-10 | **PASS** (I-02 파생) |
| Fix 검증 | B-12 | **PASS** (프로덕션 한정) |
| P2 시나리오 | P2-01 ~ P2-05 | 5건 모두 결함 확인 |
| 새 각도 | NEW-01 ~ NEW-05 | 2 WARN · 2 PARTIAL · 1 PASS |

**신규 이슈 총 13건** (I-01 ~ I-13).

### 심각도 분포 (새 이슈)

| 심각도 | 건수 | IDs |
| --- | --- | --- |
| High | 1 | I-03 |
| Medium | 5 | I-01, I-02, I-06, I-07, I-08, I-13 |
| Low | 6 | I-04, I-05, I-09, I-10, I-11, I-12 |

---

## 6. Phase 3 제안 (5건)

1. **P3-01 Race with cancel-then-resume 재현**: `CancelAll` → Failed 상태 전이 중에 사용자가 `StartAll` 누르는 경우 — `JobStatus` 가 Canceled 이전에 새 `RunJobAsync` 가 돌면 `Cts` 참조 경합 가능. `JobViewModel.Cts` 는 nullable mutable field — 두 RunJobAsync 가 같은 잡을 동시 실행할 가능성 없는지 (StartAllAsync 가 `.ToList()` 스냅숏 후 iterating 이므로 안전해 보이나, 동일 잡이 두 번 iterate 되면 `IsRunning` 체크는 시차 — 검증 필요).

2. **P3-02 Long-session 스트레스 48h 러너**: `StressRunner.cs` 의 현재 이터레이션 로직에 B-01/B-12 회귀 injection 추가 (동시에 MaxConcurrency 랜덤 변경, PersistQueue 를 병렬 10 스레드에서 호출). 24시간+ 연속 돌려 Gen2 GC count / Handle count / Thread count 추적, `dotnet-counters` 통합.

3. **P3-03 Queue 마이그레이션 테스트 스위트**: `JobSnapshot` 에 `SchemaVersion` 도입 전/후 호환성. 구버전 JSON fixture 10개 (v0.1 ~ v0.3 포맷 mockup) 로 Load 시 크래시 없고 의미 있는 default 값을 채우는지 단위 테스트.

4. **P3-04 접근성 전수 감사**: Narrator / NVDA 로 모든 버튼·상태·진행률·에러 메시지 검증. `AutomationProperties.Name`·`HelpText`·`LiveSetting` 일괄 도입. 키보드 내비게이션 (Tab order, Enter/Space 활성화) 검증. WCAG 2.1 AA 대조비 체크 (`TextMutedBrush` 가 AA 통과하는지).

5. **P3-05 외부 간섭 시나리오**: (a) AV 실시간 검사가 `.part` 잠금 시 `File.Move` 실패 재현, (b) OneDrive/Dropbox 가 Music 폴더에 달라붙어 있을 때 파일 동기화 충돌, (c) 사용자가 `queue.json` 을 에디터에서 열어놓은 채 저장 시도, (d) Windows Defender Controlled Folder Access 가 Music 폴더 쓰기 차단 시 B-03 메시지 경로 재확인.

**추가 후보 (여력 시)**:
- FFmpeg 경로에 한글/공백 포함 시 `ProcessStartInfo.Arguments` 의 quoting 정확성
- `video.Id.Value` 가 age-restricted 영상에서 비정상값 반환 시 hash collision
- YouTube 가 403 을 주면서 Range 를 무시하고 Full body 반환 시 resume 로직의 seek (`output.Seek(existing)`) 동작
- `sb.Append($"-i \"{i}\" ")` input 경로에 `"` 문자 포함 시 escape 취약
- 24h+ 세션 후 `JobViewModel.Cts` 누수 (never null after Cancel)

---

_End of Phase 2 report._
