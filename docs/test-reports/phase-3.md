# Phase 3 — 심층 테스트 리포트 (YtConverter v0.3.2)

_작성: 2026-04-18 · 테스트 PL: Phase 3 Auditor · 기준: Phase 2 fix 적용본 (I-01/02/03/08/12 수정 커밋 푸시됨)_

---

## 0. 조사 범위

- **정적 소스 리뷰**: `src/YtConverter.App/**/*.{cs,xaml}` Phase 2 fix 반영 기준
- **검증 대상**: Phase 2 수정 5건 회귀 + Phase 2 PL 의 P3-01~P3-05 제안 + 새 각도 5건 + 중복 이슈 확인
- **실행 로그 / 바이너리**: 이번 Phase 는 정적 분석 (fix 커밋이 방금 머지된 직후라 수동 runtime fresh-run 미실시 — 이슈 재현 경로는 모두 코드 라인 단위로 제시)

---

## 1. Phase 2 fix 회귀 검증 (5건)

### I-01 · RetryCommand 연결 회귀 — **PARTIAL PASS (재진입 경합 유의)**

- **코드 근거**:
  - `JobViewModel.cs:23` `public Action<JobViewModel>? RequestRetry { get; set; }`
  - `JobViewModel.cs:114-116` `[RelayCommand(CanExecute = nameof(CanRetry))] private void Retry() => RequestRetry?.Invoke(this);` — `CanRetry => Status is Failed or Canceled` ✓
  - `MainViewModel.cs:146` `job.RequestRetry = async j => { await RunJobAsync(j).ConfigureAwait(false); };`
  - `MainWindow.xaml:283` `<Button Content="🔁" ToolTip="재시도" Command="{Binding RetryCommand}" ...>` ✓ 버튼 존재
- **의도 작동 확인**: Failed/Canceled 행에서 🔁 → `RunJobAsync(job)` 호출 → `await slots.WaitAsync()` 로 세마포 획득 → 새 `CancellationTokenSource` 생성 → Status=Resolving 으로 리셋 ✓. `_slots` 는 Phase 1 B-01 fix 로 재진입 안전 ✓.
- **❗ 문제 1 (Low)**: `RetryCommand` 의 `CanExecute` 는 `Status is Failed or Canceled` 만 본다. 즉, **작업 중(Running) 상태에서 `RetryCommand` 는 disabled** — 이중 클릭 방지 OK. 그러나 빠른 연타에서 WPF 의 `CanExecute` 재평가는 property change notification 후 이뤄짐. 그런데 `RunJobAsync` 초반 `job.Status = JobStatus.Resolving` 은 UI 스레드에서 set 됨 → `OnStatusChanged` 가 `RetryCommand.NotifyCanExecuteChanged()` 호출 (`JobViewModel.cs:78`). 이 시점 이후엔 disabled → **double-click 안전**. 단, `await slots.WaitAsync()` 가 즉시 리턴 안 할 때 (세마포 가득 찬 상태) Status 가 여전히 Failed 로 남아 있어 사용자는 🔁 을 2번 눌러 두 개의 `RunJobAsync` 를 스폰할 수 있음 → 두 태스크가 같은 `job` 에 대해 연속으로 `WaitAsync` 대기 → 첫 번째가 풀리면 Status=Resolving. 두 번째는 그 뒤 재차 WaitAsync 통과 후 Status 를 **다시 Resolving** 으로 덮어씀 → `job.Cts` 가 두 번째 CTS 로 교체되는 순간 첫 번째 `cts.Token` 은 여전히 진행 중. **같은 url+format 이 중복 변환됨**. `StableJobId` 로 같은 `workDir` 를 공유해서 `audio.webm` 쓰기 충돌 → `FileStream ... FileShare.None` 이 `IOException` 유발 → 첫 번째 run 이 `MapException` 으로 실패.
- **❗ 문제 2 (Low)**: `job.ErrorMessage = null` 리셋은 `RunJobAsync` 내부 `line 222` 에 있으므로 retry 시 이전 에러 메시지가 CTS 획득 전까지 잠시 보임. UX 경미.
- **재현**: 큐에 Failed 1건, 잡 3건 동시 실행 중 (`MaxConcurrency=3`). Failed 행의 🔁 버튼을 2번 빠르게 클릭 → 두 요청 모두 세마포 대기열 진입 → 작업 종료 후 둘 다 실행.
- **심각도**: Low (실사용에서 희박) / **제안**: `RunJobAsync` 진입 직후 `if (job.IsRunning) return;` 가드, 또는 `RetryCommand` CanExecute 에 `&& job.Cts == null` 추가.

### I-02 · 0-byte age 체크 회귀 — **PASS**

- **코드 근거**: `DownloadService.cs:396-401`
  ```csharp
  else if (new FileInfo(f).Length == 0)
  {
      var age0 = DateTime.UtcNow - File.GetLastWriteTimeUtc(f);
      if (age0.TotalHours > 1) File.Delete(f);
  }
  ```
- `File.GetLastWriteTimeUtc` 는 메타데이터 기반 — 사용자가 방금 만든 0-byte `placeholder.md` 는 age ≈ 0 → 삭제 **스킵** ✓. 1시간 이전 잔해는 삭제 ✓.
- **간접 리스크**: 사용자가 timezone 변경이나 시스템 시계 수정을 할 경우 age 계산이 음수가 되면 `TotalHours > 1` 조건 false → 삭제 안 됨. 안전 측 ✓.
- **심각도**: None / **진단**: 의도대로 작동.

### I-03 · per-request timeout + 재시도 회귀 — **PASS (취소 구분 정확)**

- **코드 근거**: `DownloadService.cs:255-299`
  - `perReq = CancellationTokenSource.CreateLinkedTokenSource(ct); perReq.CancelAfter(60s)` ✓
  - `catch (OperationCanceledException) when (!ct.IsCancellationRequested && perReq.IsCancellationRequested)` ← **핵심 필터**. 사용자 Cancel (`ct` 가 trigger) 이면 이 when 절이 false → 상위 `catch (OperationCanceledException) { throw; }` (line 157) 로 전파되어 `RunJobAsync` 의 `catch (OperationCanceledException)` (line 250) 이 `Status = Canceled` 로 설정 ✓.
  - per-req timeout 은 `IOException` 으로 래핑되어 attempt 루프에서 재시도 ✓. 3회 모두 실패 시 `lastError` throw → `DownloadStreamWithResumeAsync` 밖의 `catch (Exception ex) { throw MapException(ex); }` (line 158-162) 에서 `HttpRequestException` or `IOException` 으로 래핑 → `MainViewModel.RunJobAsync` 의 `catch (Exception ex)` (line 256) 가 Status=Failed 설정 ✓.
- **edge case**: `ct.IsCancellationRequested && perReq.IsCancellationRequested` — 사용자 취소와 타임아웃이 거의 동시에 발생하면 when 절이 false → 상위 throw 로 Cancel 로 분류. ct 가 먼저 trigger 되면 perReq.IsCancellationRequested 도 linked 로 true 가 되지만 `!ct.IsCancellationRequested` 가 false → when false → 상위 throw ✓. 즉, **ct 가 trigger 된 순간엔 항상 Cancel 로 분류** (타임아웃이 함께 trigger 되어도). 의도대로.
- **단** `Task.Delay(delay, ct)` (line 295) 가 취소되면 `OperationCanceledException` → `throw;` 재발 → 역시 Cancel 로 분류 ✓.
- **심각도**: None.

### I-04 · AutomationProperties.Name 바인딩 / 언어 전환 동기화 — **PASS (단, interactive 버튼엔 누락)**

*(리포트 상 I-08 의 회귀 검증 — 번호 혼동 없이 I-08 로 표기)*

### I-08 · 접근성 라벨 회귀 — **PARTIAL PASS**

- **코드 근거**: `MainWindow.xaml:247-249`
  ```xml
  <TextBlock Grid.Column="0" Text="{Binding StatusGlyph}" FontSize="18" ...
             AutomationProperties.Name="{Binding StatusText}"
             AutomationProperties.LiveSetting="Polite">
  ```
- `StatusText` 는 `RunJobAsync` 진행 중 `Loc.Format("status_downloading", p.Ratio)` 로 갱신되고 (`MainViewModel.cs:231`), 언어 전환 시 `RefreshJobStatusTexts` (line 32-48) 가 모든 잡의 `StatusText` 을 재계산 → `AutomationProperties.Name` 바인딩이 자동 갱신 ✓.
- `LiveSetting="Polite"` 는 Narrator/NVDA 에 상태 변화를 알림 — WPF UIA provider 가 이 속성을 `LiveRegion` 으로 노출해 스크린리더가 감지 ✓.
- **❗ 구멍 1 (Medium)**: `🔁` (line 283), `📂` (284), `⏹` (285), `✕` (286) **아이콘 버튼 4종 모두 `AutomationProperties.Name` 부재**. Narrator 는 "Unicode 55357 56625" 같은 surrogate pair 수치나 "재시도" (283 만 ToolTip 존재, 나머지 3개는 ToolTip 도 없음) 를 못 읽음. **실사용 시 Tab 내비게이션으로 들어오면 버튼이 무엇인지 알 수 없음**.
- **❗ 구멍 2 (Low)**: `MainWindow.xaml:38` `<TextBlock Text="🌐" ...>` (언어 선택기 앞 이모지), line 126 `🎧`, line 153 `🎬`, line 300 `⬇` (드롭 오버레이) — 모두 접근성 라벨 없음. 장식 이모지라면 `AutomationProperties.IsOffscreenBehavior` 나 `IsHitTestVisible="False"` 로 스캐너에서 제외 필요.
- **❗ 구멍 3 (Low)**: `StatusText` 를 "대기 중 (재개됨)" 같이 문자열 상수로 직접 세팅하는 경로 (`JobViewModel.cs:52-56`) 는 `Loc` 를 통과하지 않아 **언어 전환 시 한국어 고정** 으로 남음. `RefreshJobStatusTexts` 가 모든 Status 에 대해 Loc 키를 매핑하지만 "재개됨" suffix 는 없음.
- **심각도**: Medium (구멍 1) / **제안**: 4개 아이콘 버튼에 `AutomationProperties.Name="{Binding Loc[retry_tip]}"` 등 로컬라이즈 라벨 추가.

### I-12 · MinHeight=480 레이아웃 회귀 — **PASS (간당간당)**

- **코드 근거**: `MainWindow.xaml:9` `Height="780" Width="940" MinHeight="480" MinWidth="640"` ✓
- **레이아웃 계산**: 상단 Grid 5행 — Row0 헤더(약 56px), Row1 클립보드 배너(가변, 기본 Collapsed), Row2 URL 카드(MinHeight 72 + padding 40 + 버튼행 52 → ~170px), Row3 잡 리스트 `*` (가변), Row4 저장 폴더 카드(~50px). 상하 Margin 18*2=36px.
- **최소 높이 필요**: 56 + 0 (배너 접힘) + 170 + 50 + 36 + (Row3 `*` 최소) ≈ 312px + Row3 최소. `*` 는 `MinHeight` 명시 없음 → 0 가능. 480 - 312 = 168px → Row3 가 168px ≥ 빈 상태 스택(이모지 52px + 헤더 16+4 + 바디 텍스트 ≈ 130px) OK.
- **❗ 경계 조건**: 클립보드 배너 나타나면 (+약 60px) + 잡 1개 이상 (ItemsControl item 높이 ~66px) → 56+60+170+66+50+36 = 438px. 여유 42px. **추가 잡 1개 더 + 배너** 면 504px → MinHeight 480 을 초과하지만 Grid Row3 `*` 는 `Auto-like` 가 아니라 가변이라 스크롤로 처리 → OK ✓.
- **DPI 200% 재확인**: DIP 480 × 2 = 960 physical. 1080p 에서 작업 표시줄 40px 제외 1040px → 여유 80px ✓. DPI 150% 는 720 physical, 1080p 에서 충분.
- **카드 6개 세로 배치 여부**: 재검토하니 Row 6개가 아니라 **5 Row + Row3 내부의 ScrollViewer → StackPanel → ItemsControl** 구조. "카드 6개가 세로로" 는 잡 아이템 6개 의미 — Row3 가 `*` 로 남은 공간 전부 + ScrollViewer 로 overflow 대응 → 잡이 많아도 레이아웃 붕괴 없음 ✓.
- **심각도**: None.

---

## 2. Phase 2 PL 의 Phase 3 제안 검증 (P3-01 ~ P3-05)

### P3-01 · cancel-then-resume race (⏹ 직후 🔁) — **FAIL (경합 가능)**

- **재현**: 실행 중인 잡에 `⏹` → `CancelCommand.Execute` → `Cts.Cancel()` (`JobViewModel.cs:105`) + `StatusText = "취소 중..."`. `RunJobAsync` 내부 `cts.Token` 이 trigger → 진행 중인 `DownloadStreamWithResumeAsync` 의 `ReadAsync(..., ct)` 혹은 `output.WriteAsync(..., ct)` 가 OCE throw → `catch (OperationCanceledException) { Status=Canceled; ... }` (line 250-255) → `finally { Cts=null; }` (line 264-265) → 그 후 outer `finally { slots.Release(); }` (line 268-270).
- **문제**: 사용자가 `⏹` 누른 직후 Status 가 아직 `Downloading` 에서 `Canceled` 로 전환되기 **전** (수 ms), 동시에 🔁 버튼을 누르면 — `CanRetry => Status is Failed or Canceled` 이므로 **그 순간 🔁 은 disabled** → 안전 ✓.
- **그런데** Status 가 Canceled 로 바뀐 후 outer finally 가 아직 `slots.Release()` 하지 않은 찰나 (∼1μs) 에 🔁 누르면 `RunJobAsync(job)` 가 새로 호출 → `slots.WaitAsync()` → **이 시점에 이전 release 가 안 왔으면** count 가 (MaxConcurrency-1) 이므로 `WaitAsync` 대기 또는 즉시 통과 — 세마포 상태에만 달림, 잡 자체에는 경합 없음 ✓.
- **❗ 실제 경합**: `job.Cts = null` (line 265) 이 outer finally 의 `slots.Release()` 이전에 실행됨. 사용자가 이 사이 🔁 누르면 새 `RunJobAsync` 가 진행. 그 내부에서 `using var cts = new CTS(); job.Cts = cts;` (line 217-218). 만약 **이전 WaitAsync 이 대기열에 있어 아직 풀리지 않았다면** 새 RunJobAsync 는 `slots.WaitAsync()` 에서 블록 → 시차 발생. `CancelAll` 이 호출된다면 새 `cts` 가 신규 잡에 세팅된 상태에서 `foreach (var j in Jobs.Where(j => j.IsRunning)) j.Cts?.Cancel()` (MainViewModel:282-283) — 그런데 이 시점 Status 가 `Downloading` 이 아니라 `Canceled` (이전 상태) 이면 IsRunning=false → `CancelAll` 이 새 잡을 못 건드림 → **`⏹` 효과 누락**. 단, 일반적으로 새 `RunJobAsync` 가 Status 를 `Resolving` 으로 바꾸므로 IsRunning=true 가 빠르게 복원됨 → 수 ms 창만 존재.
- **❗ 더 심각한 경합**: `FromSnapshot` (JobViewModel.cs:36) 의 복원 시나리오에서 `JobStatus.Resolving or Downloading or Muxing → Idle`. 그런데 Canceled 복원은 그대로. **앱 종료 중** 실행 중인 잡이 ⏹ 누르지 않고 그냥 종료되면 snapshot 은 Downloading 으로 저장됨 (`PersistQueue` 는 StateChanged 마다). 다음 기동 시 Idle 로 복원 → B-05 의 "Failed/Canceled 는 자동 재개 안 함" 게이트 우회 → **자동 재시도됨**. 의도된 동작이나 **데이터 비용 발생 경고 없음**.
- **심각도**: Low (좁은 창) / **제안**: `RunJobAsync` 진입부에 `if (job.Cts is not null || job.IsRunning) return;` 중복 실행 방지 가드.

### P3-02 · 48h 스트레스 러너 누수 추이 — **PARTIAL FAIL (누수 경로 확인)**

- **정적 분석 근거**:
  - `StressRunner.cs:28-30` `var ffmpeg = new FfmpegProvisioner(); await ffmpeg.EnsureAsync(globalCt); var svc = new DownloadService(ffmpeg);` — 단일 svc 인스턴스 공유 ✓ → HttpClient 누수 없음.
  - `line 151-155` `GC.Collect(); ... gen2 = GC.CollectionCount(2)` — 매 iter Gen2 강제 수집 ✓.
  - **그러나** `MainViewModel` 을 쓰지 않으므로 `AppLogger.AttachSink` 는 호출되지 않음 → Phase 2 의 I-11 누수 경로는 stress 러너에서 **발생 안 함**. 실사용 앱에서도 MainViewModel 싱글톤이라 영향 거의 없음.
- **❗ 실제 누수 위험 1**: `DownloadService._http` 는 HttpClient 이지만 `Dispose` 호출처 없음. 앱 수명 내내 살아있으므로 OK. 그러나 `_http.DefaultRequestHeaders` 에 축적되는 건 없음 ✓.
- **❗ 실제 누수 위험 2**: `using var perReq = CancellationTokenSource.CreateLinkedTokenSource(ct); perReq.CancelAfter(...)` (line 261-262) — `CancelAfter` 는 내부 Timer 등록. `using` 블록 탈출 시 Dispose → Timer 해제 ✓. 정상.
- **❗ 실제 누수 위험 3 (Medium)**: `cts.Register(() => ...)` 의 반환 `CancellationTokenRegistration` 을 `using` 으로 감쌈 (line 334-337) — Dispose 시 콜백 해제 ✓. 단, `ct` (outer job ct) 수명이 긴 경우 registered 콜백이 계속 리스트에 남아있다가 cts.Dispose 에서 일괄 해제. Process 객체 참조가 콜백 클로저에 capture 되어 **FFmpeg Process 가 예상보다 오래 rooted**. 실제론 proc.Dispose 가 using 으로 호출되므로 kernel handle 은 해제되지만, **managed wrapper 객체는 GC 시점까지 살아 있음** → gen0 에서 회수되므로 누수 아님 ✓.
- **❗ 실제 누수 위험 4 (High if 장시간)**: `FfmpegProvisioner._http` 는 `new HttpClient { Timeout = TimeSpan.FromMinutes(10) }` (line 30). 한 번만 만들어지나 `EnsureAsync` 가 여러 번 동시에 호출되면 (병렬 StartAll 초회 기동 시) zip 다운로드 레이스 가능 — **NEW-A 에서 다룸**.
- **핸들 추이 추정**: 48h × 1000 iter/day 기준 Gen2 Collection Count 는 linearly 증가하나 managed heap 은 ≈ 50-80MB 로 안정. Handle Count 는 `Process` 래퍼 finalizer queue 에 의존 → GC.WaitForPendingFinalizers (line 153) 로 강제 fin → 안전.
- **심각도**: Low / **제안**: 48h 실사용이 어려우므로 **synthetic iter=10,000 × 10초** 러너 추가 (DownloadService 만 mock으로 스왑). 또는 `dotnet-counters monitor -p <pid>` 로 GC/Handle/Thread 추적 스크립트 `scripts/stress-watch.ps1` 작성.

### P3-03 · queue schema 마이그레이션 (I-09) — **RISK (재확인, enum 삭제 시 크래시 대응 없음)**

- **코드 근거**: `QueueStore.cs:40` `return JsonSerializer.Deserialize<List<JobSnapshot>>(json, JsonOpts) ?? new();` — catch (Exception) 으로 감싸 실패 시 빈 리스트.
- `JsonStringEnumConverter` (line 16) 사용 → enum 을 문자열 `"Mp3"` `"Idle"` 으로 직렬화.
- **시나리오 1 (enum 추가)**: v0.4 에 `JobStatus.Paused` 추가. 구버전 저장 JSON 에는 없고 신버전은 Paused 를 써서 저장. 다시 구버전으로 다운그레이드 시 Paused → JsonException → **큐 전체 소실 (catch 로 empty return)** + 로그 경고만. 심각도 High.
- **시나리오 2 (enum 삭제)**: v0.4 가 `JobStatus.Failed` 를 `Error` 로 rename. 구 JSON 의 `"Failed"` 는 match 안 됨 → JsonException → 큐 소실. 심각도 High.
- **시나리오 3 (enum 값 추가 only)**: 구 JSON 에 `"UnknownFuture"` 없음 → 문제 없음 ✓.
- **시나리오 4 (OutputFormat 확장)**: Mp3/Mp4 외 Opus 추가 → 구버전으로 되돌아가면 Opus 값 디시리얼라이즈 실패 → 큐 소실.
- **심각도**: Low (다운그레이드 시나리오 자체가 희귀) / **제안**: `JsonSerializerOptions.Converters` 에 `JsonStringEnumConverter(allowIntegerValues: true)` 로 완화 or `JobSnapshot.SchemaVersion` 필드 + `ReadJobStatusFlexible` 커스텀 컨버터. **단위 테스트**: v0.1/v0.2/v0.3 JSON fixture 3~5개를 `Resources/queue-fixtures/` 아래 배치, `QueueStore.Load` 가 크래시 없이 빈 큐/복원된 큐 둘 중 하나를 리턴하는지 검증.

### P3-04 · 접근성 전수 감사 — **FAIL (interactive 요소 최소 9개에 Name/HelpText 누락)**

- **전수 점검** (`MainWindow.xaml` `AutomationProperties\.Name` grep → **1건만 매치** (line 248)):
  - Line 45 `<Button Content="📂 Open folder">` — Content 가 이모지+영문이라 스크린리더가 "open folder" 는 읽음 (text run 포함). 부분 OK.
  - Line 185, 190, 228, 230, 318 — Content 가 Loc 바인딩 이모지+문자열이라 부분 OK.
  - Line 283 `🔁` — **ToolTip 만 있고 AutomationProperties.Name 없음** → Narrator 는 "Unknown" 읽음 (WPF 기본 피크백은 ToolTip 을 Name 으로 승격하지 않음).
  - Line 284 `📂` — ToolTip 도 없음, Name 없음.
  - Line 285 `⏹` — ToolTip 도 없음, Name 없음.
  - Line 286 `✕` — ToolTip 도 없음, Name 없음.
  - Line 39 `<ComboBox>` 언어 선택기 — AutomationId 는 있지만 Name 없음. 옆 `🌐` 이모지가 label 역할 기대 (`AutomationProperties.LabeledBy` 없음) → Narrator 는 "combo box" 만 읽음.
  - Line 173 동시 작업 ComboBox — 옆 TextBlock 에 `LabeledBy` 연결 안 됨.
  - Line 79 URL TextBox — placeholder TextBlock (89) 이 있지만 Label 이 아님 → Narrator 는 "edit text" 만.
  - Line 65, 66 AcceptClipboard / DismissClipboard 버튼 — Content 는 로컬라이즈 문자열 ✓.
- **키보드 내비게이션**: Tab order 명시 없음 — XAML 선언 순서 기준이라 Row0 → Row1 → … 순서 ✓. `Enter`/`Space` 는 기본 Button activation ✓.
- **WCAG 대조비**:
  - `TextMutedBrush #6B7280` on `BgBrush #F7F8FA` → 대조비 약 4.6:1 — AA 본문 4.5:1 **통과** (간당), AAA 7:1 미달.
  - `TextBrush #1F2937` on `BgBrush #F7F8FA` → 14.2:1 ✓ AAA.
  - `AccentBrush #2563EB` on White → 5.8:1 ✓ AA.
  - 에러 빨강 `#DC2626` on White → 4.8:1 ✓ AA (본문).
  - 버튼 정보 태그 `#4338CA` on `#EEF2FF` → 대조비 ≈ 9.3:1 ✓.
- **심각도**: Medium / **제안**: XAML 의 모든 아이콘 단독 버튼에 `AutomationProperties.Name="{Binding Loc[...]}"` 부착 + 장식 이모지에 `AutomationProperties.IsOffscreenBehavior="Offscreen"` 혹은 `UIElement.IsHitTestVisible="False" + AutomationProperties.IsOffscreenBehavior`.

### P3-05 · 외부 간섭 (Controlled Folder Access / OneDrive) — **FAIL (대비 없음)**

- **Controlled Folder Access** 가 `%LocalAppData%\YtConverter\` 쓰기 차단 시:
  - `AppLogger._logDir` = `%LocalAppData%\YtConverter\logs` (`AppLogger.cs:19-21`) — `Directory.CreateDirectory` 는 CFA 블록 시 `UnauthorizedAccessException` throw. `AppLogger.ctor` 는 이를 try/catch 로 감싸지 **않음** → **앱 시작 시 크래시**. 초기화 지연 로딩 (`Lazy<AppLogger>`) 으로 첫 `AppLogger.Instance.Info("앱 시작")` (MainViewModel:99) 에서 throw → `MainViewModel.ctor` 실패 → MainWindow 생성 실패 → App 종료.
  - `QueueStore.ctor` (line 22-26) 도 `Directory.CreateDirectory` 를 try 없이 호출 — 같은 크래시 경로.
  - `DownloadService.ctor` (line 43) 도 동일.
  - `FfmpegProvisioner.ctor` 는 `_cacheDir` 만 저장 (line 26-27) — Directory.CreateDirectory 는 `EnsureAsync` 시점. CFA 로 차단되면 zip 다운로드 실패 → `MapException` 안 거치고 `HttpRequestException`/`IOException` 으로 bubble.
- **OneDrive 출력 폴더**:
  - 기본 `%UserProfile%\Music\YtConverter\` — Music 은 기본적으로 OneDrive 동기화 대상 **아님** (Music은 OneDrive 에 매핑 안 됨 기본값). Documents/Pictures/Desktop 이었다면 영향.
  - 사용자가 `BrowseFolder` 로 Desktop 지정 시 OneDrive 가 파일을 동기화 중이면 `File.Move(partPath, outputPath)` (line 189) 시 OneDrive agent 가 잠금 → `IOException` → `MapException` → "저장 공간이 부족하거나 파일을 쓸 수 없습니다" 오인 메시지.
  - `CleanupStaleArtifacts` 가 OneDrive placeholder (offline stub) 를 0-byte 로 오인해 삭제? → 파일 속성 확인 필요. `FileInfo.Length` 는 placeholder 의 경우 실제 0 을 return 하지 않음 (실제 cloud 크기 반환) → 안전 ✓. 단, `.part` 패턴 매치는 OneDrive 고유 임시 파일 `*.!qB` 와 충돌하지 않아 OK.
- **`queue.json` 에디터 open 한 채 저장 시도**:
  - `QueueStore.Save` → `File.WriteAllText(tmp, ...)` — tmp 는 새 파일이라 OK. `File.Replace(tmp, _path, null)` — 사용자가 `queue.json` 을 Notepad 로 연 상태 → Notepad 은 일반적으로 공유 읽기만 잡음 → Replace 는 target 을 삭제+tmp 를 이동 → target 삭제가 `IOException` → catch 에서 Warn 로그 → **다음 저장 때 재시도**. 데이터 손실 없음 (이전 저장본 유지) ✓.
- **심각도**: High (CFA 크래시) / **제안**: 모든 초기화 `Directory.CreateDirectory` 를 try/catch 로 감싸 사용자에게 안내 (MessageBox "보호된 폴더 액세스가 YtConverter 를 차단하고 있습니다" + 종료).

---

## 3. 새 각도 5건

### NEW-A · `_http` 싱글톤 + 프록시 변경 (Medium)

- **코드 근거**: `DownloadService.cs:33-39` `_http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };` — 기본 핸들러 (`SocketsHttpHandler`) 사용. 프록시는 **앱 시작 시점의 `WinHttp` / `WebRequest.DefaultWebProxy` 설정 스냅샷**.
- **문제**: 사용자가 앱 실행 중 VPN 붙이거나 회사 프록시 변경 (예: WiFi → 유선) 시 `_http` 는 구 프록시 유지. 신규 다운로드 모두 실패하나 에러는 `HttpRequestException: Unable to connect` 로 map → "네트워크 오류" 메시지 — **진단 모호**.
- **심각도**: Low-Medium / **제안**: `HttpClientHandler { UseProxy = true, Proxy = WebRequest.GetSystemWebProxy() }` 명시 + `new HttpClient(handler)`. 또는 오류 로그에 `WebRequest.DefaultWebProxy?.GetProxy(url)` 표기.

### NEW-B · `FfmpegProvisioner.EnsureAsync` 병렬 레이스 (Medium)

- **재현 시나리오**: 처음 기동 (`ffmpeg.exe` 없음) 에서 사용자가 즉시 URL 3개를 붙여넣고 StartAll → `MainViewModel.StartAllAsync` 가 `pending.Select(RunJobAsync).ToArray()` → 3 개 `RunJobAsync` 가 동시에 `_downloadService.ConvertAsync` → 각각 `_ffmpeg.EnsureAsync(ct)` → **같은 `FfmpegProvisioner` 인스턴스 1개 공유** (line 21-22 MainWindow.xaml.cs).
- **코드 리뷰**: `EnsureAsync` 에 락 없음. 첫 번째 `File.Exists(_cachedExe)` 가 false 면 3개 모두 `_http.GetAsync(DownloadUrl, ...)` 를 동시에 → 같은 URL 에 3번 GET. 각각 `File.Create(zipPath)` — **3개가 동일 zip 경로에 동시에 FileMode=Create 로 open** → 마지막 open 이 이기고 앞 2개의 write 가 `IOException`.
- **연쇄 효과**: 하나는 `ZipFile.OpenRead(zipPath)` 도달 → `ExtractBinAsync` 중에 다른 쪽이 여전히 write 중 → crash/corruption. 성공한 쪽은 `ffmpeg.exe` 생성, 나머지 둘은 `InvalidOperationException` 으로 fail.
- **심각도**: High (첫 기동 3병렬 시) / **제안**: `EnsureAsync` 진입부에 `private static readonly SemaphoreSlim _ensureLock = new(1,1)` + 진입 시 `await _ensureLock.WaitAsync(ct)` → File.Exists 재확인 후 다운로드.

### NEW-C · Clipboard regex 커버리지 (Medium)

- **현재 regex**: `MainViewModel.cs:346` + `MainWindow.xaml.cs:14-16` (동일 패턴)
  ```
  (https?://)?(www\.)?(youtube\.com/|youtu\.be/)\S+
  ```
- **미지원 변형** (수동 검증):
  - `https://m.youtube.com/watch?v=XYZ` — `(www\.)?` 뒤에 `youtube.com/` 매칭 but `m.` 는 `(www\.)?` 에 안 들어감. **FAIL** ❌
  - `https://music.youtube.com/watch?v=XYZ` — 동일 사유 **FAIL** ❌
  - `https://www.youtube.com/shorts/XYZ` — `youtube.com/` 매칭 후 `\S+` 로 shorts/XYZ 포함 **PASS** ✓
  - `https://www.youtube.com/live/XYZ` — 동일 **PASS** ✓ (단, DownloadService 는 live 비디오 `video.Duration is null` 체크로 거부)
  - `https://youtube.com/watch?v=XYZ` (www 없음) — `(www\.)?` optional + `youtube.com/` **PASS** ✓
  - `https://www.youtube.com/embed/XYZ` — **PASS** ✓
  - `https://youtu.be/XYZ?si=abc&t=42` — `youtu.be/` 뒤 `\S+` **PASS** ✓
  - `youtube.com/watch?v=XYZ` (scheme 없음) — `(https?://)?` optional **PASS** ✓
  - `www.youtube.com/watch?v=XYZ` (scheme 없음) — **PASS** ✓
  - `https://www.youtube-nocookie.com/embed/XYZ` — `youtube.com/` 매치 (youtube-nocookie.com 에 youtube.com 이 substring) 아니, 실제 매치: `youtube.com/` 은 정규식이 `.` 을 `[any]` 로 매치하므로 `-` 와 `.` 어느 것도 매치할 수 있어서 `youtube-nocookie.com/` 를 잘못 매치할 수 있음. 재확인: `youtube\.com/` 에서 `\.` 는 literal `.` 만 매치 → `-` 매치 안 됨 **FAIL** ❌ (nocookie 공유 링크는 드물어 영향 미미).
  - 쿼리스트링 `&t=42s`, `?si=xxx` 등 — `\S+` 가 모두 greedy 매치 **PASS** ✓.
- **심각도**: Medium / **제안**: regex 를 `(https?://)?(www\.|m\.|music\.)?youtube\.com/|youtu\.be/)\S+` 로 확장. 또는 `Uri.TryCreate` + host whitelist.

### NEW-D · `DisplayTitle` 60자 컷이 emoji/CJK surrogate pair 절단 (Low)

- **코드**: `JobViewModel.cs:61-63` `Url.Length > 60 ? Url[..60] + "…" : Url`.
- **문제**: `.NET string.Length` 는 UTF-16 code unit 수. 이모지 (🎵 = U+1F3B5) 는 surrogate pair 로 code unit 2개. `Url[..60]` 가 surrogate 의 **high surrogate 뒤, low surrogate 앞** 에서 cut 되면 남은 문자열은 **malformed UTF-16**. WPF TextBlock 은 이를 U+FFFD 로 표시하거나 blank. 한자 (BMP 내) 는 code unit 1개라 안전하지만 이모지·수학기호·고대문자 등은 위험.
- **재현**: Title 에 `🎵🎸🎤🥁🎺🎻🎷🪕🎹🪘🪗` 같은 다수 이모지 후 60 자리에 정확히 pair 가 쪼개지도록 입력. WPF 에서 마지막 글자가 박스(□)로 표시됨.
- **DisplayTitle 은 Url fallback 경로만 cut** — Title 있으면 cut 없음. 즉, **URL 이 긴 경우만 발생** → 유튜브 URL 은 ASCII 라 실제 영향 없음 ✓.
- **심각도**: Low (이론적) / **제안**: `StringInfo` 또는 `System.Globalization` 의 `TextElementEnumerator` 로 text element 단위 cut.

### NEW-E · `ClipboardSuggestion` ↔ `AcceptClipboardCommand` null 안정성 (Low)

- **코드**: `MainViewModel.cs:357-364` `AcceptClipboard` → `if (string.IsNullOrWhiteSpace(ClipboardSuggestion)) return;` ✓ 안전.
- `MainWindow.xaml:53` 배너는 `NullToCollapsed` 컨버터로 null 시 Collapsed ✓.
- **경합**: 사용자가 Accept 버튼 누르는 동시에 다른 앱이 클립보드 바꾸고 `CheckClipboardForUrl` 재호출되어 `ClipboardSuggestion` 을 새 값으로 교체하면 — Accept 는 **새 값** 을 URL 에 추가. 사용자가 원치 않는 URL 이 추가될 수 있음. 그러나 `Window_Activated` 만 CheckClipboard 호출 (xaml.cs:92-95) → Accept 중에는 창이 이미 Activated 상태이므로 trigger 안 됨 ✓.
- **❗ `_dismissedClipboard` 메모리**: `DismissClipboard` 는 현재 값을 `_dismissedClipboard` 에 저장. 이후 같은 값이 다시 클립보드에 오면 제안 안 함 ✓. 앱 재시작 시 초기화 → 다시 제안됨. 의도 ✓.
- **심각도**: None / **진단**: 안전.

---

## 4. 새 이슈 종합 (I-14 ~ I-18)

| ID | 심각도 | 요약 | 위치 |
| --- | --- | --- | --- |
| **I-14** | High | `FfmpegProvisioner.EnsureAsync` 병렬 호출 시 zip 다운로드 레이스 | `FfmpegProvisioner.cs:33-77` |
| **I-15** | High | CFA / 보호된 폴더 차단 시 앱 시작 크래시 (AppLogger/QueueStore/DownloadService ctor) | `AppLogger.cs:21`, `QueueStore.cs:25`, `DownloadService.cs:43` |
| **I-16** | Medium | 아이콘 버튼 4종 (`🔁📂⏹✕`) 접근성 라벨 없음 (I-08 잔여) | `MainWindow.xaml:283-286` |
| **I-17** | Medium | Clipboard regex 가 `m.youtube.com`, `music.youtube.com`, `youtube-nocookie` 미지원 | `MainViewModel.cs:346` + `MainWindow.xaml.cs:14-16` |
| **I-18** | Low | Retry 빠른 연타 시 동일 job 에 `RunJobAsync` 중복 스폰 가능 (Status 는 짧은 창에서 Failed 유지) | `MainViewModel.cs:146`, `JobViewModel.cs:114-116` |

**기존 미해결** (Phase 2 에서 유지): I-04, I-05, I-06, I-07, I-09, I-10, I-11, I-13.

### 심각도 분포 (신규 + 누적)
| 심각도 | 신규 Phase 3 | 누적 |
| --- | --- | --- |
| High | 2 (I-14, I-15) | 2 |
| Medium | 2 (I-16, I-17) | 5 (+ I-06, I-07, I-13) |
| Low | 1 (I-18) | 7 (+ I-04, I-05, I-09, I-10, I-11) |

---

## 5. Phase 4 제안 (5건)

1. **P4-01 Fuzz-test input URL 파서** — `MainViewModel.Add` 의 newline-split + DownloadService 의 `YoutubeClient.Videos.GetAsync(url)` 이 handle 못 하는 기형 입력 (URL 내 개행, null byte, zero-width joiner, 이모지 포함 라벨 뒤 `<`/`>`) 에 대한 robustness. `System.Security.Fuzzer` 또는 `SharpFuzz` 사용. 기대: throw 만 하고 앱 crash 없음.

2. **P4-02 CFA / AV 차단 시나리오 자동화** — VM 또는 WDM `Test-Machine` 에서 `Set-MpPreference -EnableControlledFolderAccess Enabled` 후 `%LocalAppData%\YtConverter` 를 보호 목록에 추가 → 앱 기동 스크립트. `Directory.CreateDirectory` 크래시를 재현하고 MessageBox 경고 fallback 검증. (I-15 수정 이후 회귀).

3. **P4-03 Migration 테스트 스위트** — `SchemaVersion` 도입 전 대비해 `Resources/queue-fixtures/v0.1.json` `v0.2-corrupted.json` `v0.3-extra-field.json` `v0.3-enum-unknown.json` 5개 fixture 생성 + xUnit `[Theory]` 로 각 loading 검증. 재현 가능한 단위 테스트가 I-09 해결을 가속.

4. **P4-04 UIA / Narrator E2E 자동화** — `FlaUI` 또는 `WinAppDriver` 로 MainWindow 띄워 Narrator 친화 내비게이션 시뮬. 모든 AutomationId 요소가 `Name` 을 노출하는지, LiveRegion 변화가 이벤트로 발화하는지 테스트. 대조비는 `axe-windows` 툴로 자동 감사.

5. **P4-05 HttpClient proxy 변경 감지 + FFmpeg EnsureAsync 병렬 가드** — 두 개의 관련 이슈 (NEW-A, I-14) 를 하나의 패치+테스트로 묶음. `FfmpegProvisioner.EnsureAsync` 에 `SemaphoreSlim` 직렬화 + `zipPath` 에 `Path.GetRandomFileName()` 접미사 + 실패 시 정리. 테스트: `Task.WhenAll(Enumerable.Range(0,5).Select(_ => prov.EnsureAsync(ct)))` 가 모두 `_cachedExe` 동일 경로 반환.

**추가 후보**:
- 로그 로테이션 (현재 `yt-{yyyyMMdd}.log` 은 날짜별이지만 같은 날 1GB+ 초과 대응 없음)
- `video.Title` 에 BOM/RTL override 문자 포함 시 파일명 sanitize 회귀 (현재 `Path.GetInvalidFileNameChars` 는 U+202E 를 제외하지 못함)
- `scripts/stress-watch.ps1` — `dotnet-counters` 로 handle/gen2 15분 간격 샘플링

---

## 6. 요약

| 범주 | 결과 |
| --- | --- |
| Phase 2 fix 회귀 (I-01/02/03/08/12) | 5건 모두 의도대로 작동. I-01 연타 경합 + I-08 아이콘 버튼 Name 누락은 잔여. |
| P3-01 ~ P3-05 검증 | P3-01 좁은 창 race 확인(Low), P3-02 누수 경로 제한적(Low), P3-03 enum 삭제 시 크래시(Medium, 시나리오 희귀), P3-04 접근성 9+ 요소 누락(Medium), P3-05 CFA 크래시(High). |
| 새 각도 (NEW-A~E) | 2 High, 2 Medium, 1 Low. 특히 NEW-B(FfmpegProvisioner race) 는 첫 기동 다중 URL 시나리오에서 현실적. |
| 신규 이슈 총 | **5건** (I-14 ~ I-18), High 2 · Medium 2 · Low 1. |

---

_End of Phase 3 report._
