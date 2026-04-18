# Phase 1 — 심층 테스트 리포트 (YtConverter v0.3.0)

_작성: 2026-04-18 · 테스트 PL: Phase 1 Auditor · 저장소: vavibolivia/YtConverter @ main_

---

## 0. 조사 범위 및 출처

- **런타임 로그**: `C:\Users\jjsuk\AppData\Local\YtConverter\logs\yt-20260418.log` (297 라인, 오늘 22:06 ~ 23:39)
- **큐 상태**: `C:\Users\jjsuk\AppData\Local\YtConverter\queue.json` (2건, 모두 Failed)
- **작업 캐시**: `C:\Users\jjsuk\AppData\Local\YtConverter\work\F16729F50A0274B8\audio.webm` (1.8 MB 고아 파일)
- **사용자 저장 폴더**: `C:\Users\jjsuk\Music\YtConverter\` (6개 mp3 + **210 MB 의 `.stream-0.tmp` 고아**)
- **정적 분석**: `src/YtConverter.App/**/*.{cs,xaml}` 전수 grep
- **stress 러너**: `src/YtConverter.Tests/StressRunner.cs` — 과거 20+회 반복 기록 확인

---

## 1. Phase 1 시나리오 (13건, 이전 20 역할에 없던 새 각도 중심)

| ID | 카테고리 | 시나리오 | 실행 방식 | 결과 |
| --- | --- | --- | --- | --- |
| P1-01 | 회귀/재개 | 재개 대상 `.part` 파일이 `.mp3.stream-0.tmp` 같은 **구버전 naming**과 공존 시 | 로그+파일 조사 | **FAIL** (B-10 발견) |
| P1-02 | 회귀/재개 | `&t=` 또는 `&list=` 만 다른 같은 영상 2건을 연속 재개 시 동일 work 재사용 여부 | 코드 static | **FAIL** (B-02 발견) |
| P1-03 | UI | 1000건 대량 큐 붙여넣기 시 `PersistQueue` 가 키 입력마다 JSON 쓰기를 유발하는지 | MainViewModel.Add grep | **FAIL** (디바운스 없음, I/O 폭주 가능) |
| P1-04 | UI | 이모지/깊은 CJK/RTL 타이틀 → `SanitizeFileName` 120자 자르기와 결합 시 표면 문제 | DownloadService:362 | manual verify — 120자 컷이 UTF-16 surrogate pair 절단 가능 |
| P1-05 | UI | 언어 전환 시 진행 중인 잡의 `StatusText` 가 `RefreshJobStatusTexts` 로 덮여쓰기되어 `{0:P0}` 진행률 유실 | MainViewModel:32-48 | **FAIL** (B-14 관련) — 언어 바꾸는 순간 Downloading 70% 가 `"Downloading 0%"`로 리셋됨 |
| P1-06 | 파일 시스템 | 읽기 전용 출력 폴더 → `FileStream CreateNew` | DownloadService:92 catch 범위 | **FAIL** (B-03: UnauthorizedAccessException 미처리) |
| P1-07 | 파일 시스템 | UNC `\\server\share\out` + Range resume (SMB 는 range seek 지원 불안정) | 코드 static | manual verify — 확인 불가, 잠재 위험 |
| P1-08 | 파일 시스템 | 동일 파일명을 유발하는 두 URL 동시 시작 → placeholder race | DownloadService:92-99 | **FAIL** (B-08: placeholder 재예약 누락) |
| P1-09 | 동시성 | 변환 중 동시 작업 수 슬라이더 1→6 변경 | MainViewModel.OnMaxConcurrencyChanged | **FAIL** (B-01: 세마포 교체로 N+3 누수) |
| P1-10 | 동시성 | `CancelAll` 직후 `StartAll` — 취소 중인 잡이 Failed 로 전이되기 전에 재시작 | MainViewModel:160 | **FAIL** (B-05: Failed/Canceled 가 즉시 pending 목록에 포함) |
| P1-11 | 에러 복구 | CDN 5xx → 현재 코드는 `EnsureSuccessStatusCode()` 로 즉시 throw, 재시도 없음 | DownloadService:259 | **FAIL** — 단일 실패로 전체 작업 종료 |
| P1-12 | 에러 복구 | FFmpeg stderr 메시지가 빌드 configuration 이 뒷쪽에 와서 진짜 에러가 `[^2000..]` 절단에 밀림 | log 186, 196, 202, 215 | **FAIL** (B-09: 진짜 에러 미표시) |
| P1-13 | 국제화 | `{0:P0}` 는 `CurrentCulture` 를 사용 — 앱 언어가 ko 여도 OS 가 en-US 면 `10%` 로 나타나 UI 불일치 | 코드 static | **FAIL** (B-14: CultureInfo 연동 없음) |

---

## 2. 발견 이슈 (15건)

### 심각도 요약

| 심각도 | 건수 | IDs |
| --- | --- | --- |
| **High** | 4 | B-01, B-02, B-03, B-10 |
| **Medium** | 8 | B-04, B-05, B-06, B-07, B-08, B-11, B-12, B-13 |
| **Low** | 3 | B-09, B-14, B-15 |

### 상세 — High

#### B-01 · 동시 작업 수 변경 시 SemaphoreSlim 교체로 Slot 초과 / 누수 (High)

- **위치**: `MainViewModel.cs:77-82`
- **재현**:
  1. MaxConcurrency=3 상태에서 3개 잡을 StartAll
  2. 3개가 모두 실행 중일 때 ComboBox 에서 1 로 변경 → `_slots` 가 `new SemaphoreSlim(1, 1)` 로 교체됨
  3. 실행 중인 3개 잡이 끝날 때 `finally { _slots.Release(); }` 가 새 세마포에 호출됨 → **Release 가 CurrentCount(1) > maxCount(1)** 위반으로 `SemaphoreFullException` 발생 가능
- **기대**: 변경 시 drain 또는 큐 전환, 실행 중 잡은 이전 세마포로 Release
- **실제**: 즉시 교체. 이전 세마포는 GC 대상이 될 뿐 아무도 Release 하지 않고, 새 세마포는 3번 Release 되어 용량 초과
- **제안**: `_slots` 교체 대신 `_slots.Wait` / `Release` 를 내부 `ConcurrencyGate` 로 감싸, 변경은 "다음 대기 잡부터 적용"으로 지연. 또는 `Interlocked` + `int _targetMax` 로 현재 허용치 체크 후 대기.

#### B-02 · StableJobId 가 URL 의 `&t=` / `&list=` 쿼리 파라미터를 포함 (High)

- **위치**: `DownloadService.cs:337-342`
- **재현**:
  1. `https://www.youtube.com/watch?v=X&t=10s` MP3 로 다운 (work hash A)
  2. 같은 영상을 `?v=X` 만으로 재추가 (hash B) — **같은 video ID 지만 다른 work 디렉토리**
  3. 두 작업 모두 오디오를 각기 210 MB 씩 다운로드 → 디스크 중복
- **기대**: videoId + format 기준으로만 hash — resume/dedup 공유
- **실제**: 오늘 로그 라인 164 (`&t=2586s`) 와 라인 238 (no `&t`) 이 **서로 다른 work** 를 만듦 (로그 228 의 `DC8CF167…` vs 292 의 `56D6F9B7…`)
- **제안**: `video.Id.Value + "|" + fmt` 로 해시. manifest 조회가 먼저 필요하지만, 이미 `_yt.Videos.GetAsync` 직후라 가능.

#### B-03 · 읽기 전용 / 권한 없는 출력 폴더에서 UnauthorizedAccessException 미처리 (High)

- **위치**: `DownloadService.cs:90-99`
- **재현**: 출력 폴더에 파일 만들 권한 없음 (관리자 폴더, 네트워크 읽기전용 공유)
- **기대**: `err_io` 로 매핑되어 UI 에 "저장 공간이 부족하거나 파일을 쓸 수 없습니다" 표시
- **실제**: `catch (IOException)` 만 있음 → `UnauthorizedAccessException` 은 캐치되지 않고 상위로 전파, MapException 은 `IOException` 만 매핑하므로 raw 메시지 노출
- **제안**:
  ```csharp
  catch (IOException) { /* 기존 */ }
  catch (UnauthorizedAccessException ex) { throw MapException(new IOException(ex.Message, ex)); }
  ```
  또는 `MapException` 에 `UnauthorizedAccessException => err_io` 케이스 추가.

#### B-10 · 사용자 Music 폴더에 210 MB 고아 파일 존재 (High, 회귀)

- **위치**: `C:\Users\jjsuk\Music\YtConverter\keep praying… He's still listening..mp3.stream-0.tmp`
- **재현**: 과거 빌드에서 FFmpeg 가 segment 모드로 쓰다가 crash → tmp 청소 안 됨
- **기대**: 사용자 폴더에 앱이 만든 모든 중간물은 성공/실패/취소 어느 경우에도 삭제
- **실제**: 210,604,619 B = 오디오 스트림 길이와 **정확히 일치** → 과거 코드가 사용자 폴더를 직접 다운로드 경로로 썼거나, `.part` 명명 전환 과정에서 누락
- **제안**: 앱 시작 시 출력 폴더 내 `*.part`, `*.stream-*.tmp`, `*.ytdl-tmp` 파일을 수명 기준 (24h+ 또는 잡 큐에 매칭 없음) 으로 스캔→삭제하는 클린업 유틸 추가. 사용자 폴더 청소는 **매우 조심스럽게** (사용자 동의 프롬프트).

### 상세 — Medium

#### B-04 · 1 KB 임계로 기존 파일 재사용 판정 — 부분 손상본 통과 가능 (Med)

- **위치**: `DownloadService.cs:76-84`
- **재현**: 50% 에서 취소된 이전 변환의 `.mp3` 가 2 KB 라도 남아있으면 `fi.Length > 1024` 가 true → 재사용 결정, 사용자는 잘린 파일 재생
- **제안**: magic byte (ID3/RIFF) 검증 또는 duration probe (ffprobe `-show_entries format=duration`) 로 검증. 아니면 임계를 10 MB 이상/크기 비율 90% 이상으로 강화.

#### B-05 · Failed/Canceled 잡이 CanStartAll/pending 에 즉시 포함 (Med)

- **위치**: `MainViewModel.cs:160, 176`
- **재현**: Auto-resume 이 켜진 상태에서 영구 실패한 URL (삭제된 영상) → 매 앱 시작마다 재시도
- **제안**: `Idle` (미시작) 만 자동 재개. Failed/Canceled 는 사용자가 명시적으로 재시도 버튼을 눌러야 함. 또는 `FailedAttempts` 카운터로 3회 초과 시 자동 스킵.

#### B-06 · Clipboard 검출 Regex 가 너무 느슨 (Med)

- **위치**: `MainViewModel.cs:306-308`, `MainWindow.xaml.cs:14-16`
- **재현**: `youtube.com/about` 같은 비-video 페이지 URL 붙여넣기 → 배너 출현
- **제안**: `(watch\?v=|youtu\.be/|shorts/|embed/)` 로 비디오 경로 제한.

#### B-07 · QueueStore `.tmp` 파편 청소 없음 (Med)

- **위치**: `QueueStore.cs:45-62`
- **재현**: `File.Replace` 중 AV 스캐너가 .tmp 에 LOCK → `.tmp` 잔존
- **제안**: `Load()` 시작부에 `_path + ".tmp"` 존재하면 삭제.

#### B-08 · Placeholder race 가능 (Med)

- **위치**: `DownloadService.cs:86-99, 183-194`
- **재현**: 같은 URL 2건 동시 시작:
  1. 두 스레드 모두 `File.Exists(outputPath)` false
  2. A 가 `CreateNew` 성공, B 가 IOException → GUID suffix 로 `_abc123` 파일 생성
  3. A 가 mux 성공, `File.Delete(outputPath)` 후 `File.Move(partPath, outputPath)` 완료
  4. B 의 최종 이동은 `_abc123` 으로 — OK
  5. 하지만 A 가 exit 과정에서 B 의 suffix 파일을 볼 수 없어 중복 파일이 남음
- **제안**: placeholder 에 Guid 를 항상 포함시키거나, 같은 URL+format 에 대해 `ConcurrentDictionary<string, Task>` 로 coalesce.

#### B-11 · FFmpeg stream ordering 가정 (Med)

- **위치**: `DownloadService.cs:115-133, 280-284`
- **재현**: 미래에 `chosen` 순서가 바뀌거나 audio-only MP4 폴백이 추가되면 `-map 0:v:0 -map 1:a:0` 이 깨짐
- **제안**: 명시적으로 "video stream" 과 "audio stream" 을 별도 변수로 넘겨 `-i $video -i $audio` 로 고정.

#### B-12 · ObservableCollection cross-thread 접근 (Med)

- **위치**: `MainViewModel.cs:132-136`, JobViewModel `OnStatusChanged` → `StateChanged` → `PersistQueue`
- **재현**: RunJobAsync 는 ThreadPool 에서 실행. 진행 중 `job.Status = JobStatus.Downloading` → `StateChanged()` → `_queue.Save(Jobs.Select(...))` → `ObservableCollection<JobViewModel>.GetEnumerator()` 가 non-UI thread 에서 호출됨. 동시에 UI thread 가 Remove/Add 호출하면 `InvalidOperationException: Collection was modified`.
- **제안**: `PersistQueue` 를 항상 Dispatcher 에서 실행, 또는 `Jobs` 를 `List<JobSnapshot>` 으로 스냅숏 후 저장.

#### B-13 · LocalizationService 저장 실패 무언 스왈로우 (Med)

- **위치**: `LocalizationService.cs:81-89`
- **제안**: catch 에서 `AppLogger.Instance.Warn` 로 최소 기록.

### 상세 — Low

#### B-09 · FFmpeg stderr tail 이 의미없는 configuration 뒷부분만 표시 (Low)

- **위치**: `DownloadService.cs:319-321`
- **실제**: 로그 라인 186~202 에 `[^2000..]` tail 이 `--enable-libwebp --ena` 로 끝남 — 진짜 에러(Error opening input 등)는 **stdout/stderr 앞쪽**에 있고 configuration 덤프는 항상 뒷쪽
- **제안**: `exit=-22` 같은 flag 실패는 보통 초반 "Error" 라인. stderr 에서 `"Error "` / `"error"` / `"Invalid"` 매칭된 라인만 추려 보여주기 (grep `-E "(Error|Invalid|Failed|Permission)"`).

#### B-14 · `{0:P0}` 는 `CurrentCulture` 를 사용 — 앱 언어와 독립 (Low)

- **위치**: `MainViewModel.cs:40, 197`
- **제안**: `Loc.Format` 내부에서 `CultureInfo.CreateSpecificCulture(Loc.Language)` 를 전달, 또는 % 포맷을 수동으로 `{0}%` 로 작성.

#### B-15 · placeholder 는 `PersistQueue` / StateChanged 연결로 매 초 JSON 쓰기 (Low)

- **위치**: `JobViewModel.OnStatusChanged`, `PersistQueue`
- **재현**: Downloading 중 Progress 는 저장되지 않지만 Status 는 Resolving→Downloading→Muxing→Completed 로 4번 상태 전환 + ErrorMessage/OutputPath 변경 시에도 Save
- **영향**: 6개 병렬 시 작은 파일 I/O 증가, 플래시 메모리 수명
- **제안**: `Save` 를 30s 디바운스 또는 중요 상태(Completed/Failed/Canceled) 에만 제한.

---

## 3. 관찰된 사실 (Observations — 이슈는 아님)

- **queue.json 복원 동작 양호**: 로그 247~294 에 걸쳐 app restart 3회, 매번 정확히 이어받기 byte offset 증가 (`8M → 9.5M → 14M → 성공`). Range resume 로직은 대체로 건강함.
- **duplicate 파일명 핸들링 부분 작동**: 1개 영상이 `(1), (2), (6)` suffix 로 3번 저장됨 — 큐 snapshot 에서 `OutputPath` 가 기존 파일명을 기억하여 재사용은 하지만 B-02 의 work 중복 문제와는 별개.
- **Admin tier 로그 출력 양호**: 라인 173-174.
- **음량 있는 mux 성공**: 라인 228~236 (MP3 성공), 235~236 (MP4 성공) — FFmpeg args 구조는 정확.

---

## 4. Next Phase 제안 — Phase 2 에서 다룰 시나리오 5개

1. **P2-01 (Fault Injection)** · 중간에 네트워크 차단: 다운로드 50% 에서 `netsh interface set interface "Wi-Fi" admin=disable` 시뮬레이션 → Range resume 가 어떻게 복구하는지 (현재 timeout=Infinite 라 영원히 매달림 가능). **요구**: HttpClient PerRequestTimeout + 지수 백오프 재시도.

2. **P2-02 (Playlist Expansion)** · `&list=XYZ` 를 만나면 현재는 첫 비디오만 처리 — 사용자는 "재생목록을 큐에 풀어주는" 기능을 기대할 수 있음. UX 결정 필요. **요구**: 옵션 다이얼로그 또는 기본은 첫 영상 + hint.

3. **P2-03 (Storage Saturation)** · 50 MB 남은 디스크에서 3 GB 영상 시도 → `IOException: There is not enough space` 는 어느 시점에 catch 되나? FFmpeg 가 0 바이트 output 만들고 죽으면 placeholder 정리 로직과 충돌할까?

4. **P2-04 (Accessibility / Screen reader)** · Narrator 로 Job row 를 읽을 때 `StatusGlyph` (이모지) 가 "Unicode 9203" 으로 읽힘. `AutomationProperties.Name` 에 `StatusText` 바인딩 필요.

5. **P2-05 (Queue Schema Evolution)** · 미래에 `JobSnapshot` 에 필드 추가 시 구버전 `queue.json` 로딩 호환성. 현재는 `JsonSerializer.Deserialize<List<JobSnapshot>>` 으로 extra field ignore 는 되지만 missing field (신규 필드) 는 default 값. **요구**: version 필드 + migration.

추가 후보:
- 24h 스트레스 러너에서 FFmpeg 프로세스가 Kill 후에도 zombie 로 남는지 (`proc.Kill(entireProcessTree: true)` 의 확실성)
- `settings.json` 손상 시 (로컬라이제이션) 앱 기동 여부
- DPI 스케일링 200% 에서 long 타이틀 텍스트 잘림

---

## 5. 권장 Triage

| Priority | 이슈 | 권장 |
| --- | --- | --- |
| P0 (다음 릴리스 전 필수) | B-01, B-03, B-12 | 프로덕션 크래시/누수 가능 |
| P1 (다음 sprint) | B-02, B-05, B-08, B-10 | UX / 디스크 낭비 |
| P2 (주시) | B-04, B-06, B-07, B-09, B-11, B-13, B-14, B-15 | polish |

---

_End of Phase 1 report._
