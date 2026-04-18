# YouTube → MP3/MP4 Converter — 디자인 가이드

> 대상: 비전문 일반 사용자 (한국어 UI)
> 플랫폼: WPF (.NET 8), Windows 10/11
> 작성일: 2026-04-18
> 리뷰 대상 스크린샷: `C:\workspace\youtube\logs\screenshot.png`

---

## 0. 목표

"YouTube 링크를 붙여넣고 한 번만 누르면 내 폴더에 MP3/MP4가 생긴다" — 이 한 문장이 **첫 화면만 봐도 이해되는** 수준까지 UI를 단순·친절하게 개선한다. 전문가용 옵션은 숨기거나 기본값으로 대체한다.

---

## 1. 디자인 원칙 (Product Principles)

비전문 사용자를 위한 7대 원칙. 모든 개선안은 이 원칙에 비추어 의사결정한다.

| # | 원칙 | 설명 | 측정 지표 |
|---|------|------|-----------|
| P1 | **3-클릭 룰** | 앱 실행 → 첫 다운로드 완료까지 최대 3번의 주요 클릭 (붙여넣기 → 포맷 선택 → 시작) | Time-to-First-Success ≤ 15초 |
| P2 | **색 + 텍스트 + 아이콘 3중 표현** | 상태를 색만으로 전달하지 않는다. 색맹/저시력자도 구분 가능하도록 텍스트/아이콘 중복 | WCAG 1.4.1 준수 |
| P3 | **기본값이 곧 정답** | 포맷 MP3·동시작업 3·폴더 "내 음악/YtConverter" 가 그냥 좋게 작동. 설정 없이도 바로 쓰인다 | 첫 사용 시 0-설정 완료 가능 |
| P4 | **친절한 빈 상태** | "작업 리스트가 비어 있으면 다음에 뭘 할지" 를 화면이 알려준다 (onboarding illustration) | 신규 사용자가 5초 내 첫 동작 시작 |
| P5 | **진행 상태는 항상 보이게** | 숨겨진 로딩·백그라운드 작업 금지. 모든 상태는 카드/토스트/요약 바 중 하나에 반영 | 사용자 문의 "지금 뭐 하는 거예요?" = 0 |
| P6 | **되돌릴 수 있게, 실수 용서** | "모두 취소" 는 되돌릴 수 있게 확인 다이얼로그. 완료 폴더 바로 열기·파일 재시도 지원 | 실수-복구 경로 100% |
| P7 | **작은 앱, 큰 여유** | 창 내부에 공백(breathing room)을 넉넉히. 글씨는 최소 13pt, 클릭 영역 ≥ 32px | Fitts' Law 준수 |

---

## 2. 경쟁 도구 UX 리서치 (출처 포함)

### 2.1 4K Video Downloader — "Smart Mode" 와 "Paste Link" 버튼
- **핵심 UX**: 한 번 포맷/품질/폴더를 설정하면 이후에는 "링크 복사 → Paste Link 한 번" 으로 끝. 사용자가 결정할 것이 매번 줄어든다.
- **적용 가능 패턴**: 첫 다운로드 후 "다음부터 이 설정을 기본값으로 쓸까요?" 체크박스 제안 → 두 번째부터는 클립보드 감지 시 자동으로 큐 추가.
- 참고: https://www.4kdownload.com/blog/2026/04/13/4k-video-downloader-plus-26-1-0-is-out/
- 참고: https://www.addictivetips.com/software/4k-video-downloader-review/

### 2.2 JDownloader 2 — Clipboard Observer (링크 그래버)
- **핵심 UX**: 백그라운드에서 클립보드를 감시하다 유효한 URL이 복사되면 자동으로 "링크 그래버" 탭에 올려주고, 사용자가 확인 후 실제 큐로 이동.
- **적용 가능 패턴**: 우리 앱에서도 앱 포커스 시 클립보드를 체크 → 유튜브 URL이면 "이 링크를 추가할까요?" 배너 노출 (자동 추가가 아니라 제안형이 덜 무섭다).
- 참고: https://technical-tips.com/blog/software/jdownloader-link-automatically-from-clipboard-2765
- 참고: https://www.videoproc.com/download-record-video/how-to-use-jdownloader-2.htm

### 2.3 cobalt.tools — "paste paste and download" 철학
- **핵심 UX**: 화면 전체가 URL 입력창 하나. 로고·체크박스 1~2개 외에는 아무것도 없음. 마스코트 캐릭터 "meowbalt" 로 차가움을 누그러뜨린다.
- **적용 가능 패턴**: 첫 화면의 시선 80% 를 URL 입력창에 할애. 포맷·동시성·폴더는 "고급 설정" 토글 뒤에 숨기거나 측면 패널로 이동. 비어 있을 때 마스코트/아이콘 + "유튜브 링크를 붙여넣어 보세요" 한 줄.
- 참고: https://cobalt.tools/

### 2.4 y2mate — 3 스텝 시각화
- **핵심 UX**: (1) 링크 붙여넣기 (2) 포맷 선택 (3) 다운로드 — 숫자로 스텝을 명시하고 현재 단계가 하이라이트됨.
- **적용 가능 패턴**: 상단에 "1 붙여넣기 → 2 포맷 → 3 시작" 가로 스텝퍼를 얇게 두어 초보자가 "내가 지금 어디 있는지" 알게 한다.
- 참고: https://www.oreateai.com/blog/y2mate-your-goto-for-effortless-youtube-to-mp4-conversions/a77e5eb2e812122237535581f065a7a4

### 2.5 종합 — 공통 패턴 5개
1. **하나의 큰 입력창** (화면 중앙 상단)
2. **클립보드 자동 감지 → 제안** (자동 추가 아님)
3. **스마트 기본값 + "다음부턴 이렇게" 체크**
4. **빈 상태 일러스트/마스코트로 친근함**
5. **진행률은 카드형 + 전체 요약 배지** (개별 + 합계 모두 보임)

---

## 3. 개선안 — 현재 UI → 제안 UI

각 항목: **문제 / 개선 / 예상 효과**

### 3.1 URL 입력
- **문제**: 현재 멀티라인 텍스트박스 + 우측에 "붙여넣기"·"작업 추가" 버튼 2개. 붙여넣기 버튼을 꼭 눌러야 하는지, 그냥 Ctrl+V 해도 되는지 애매. 드래그앤드롭 지원 표시 없음.
- **개선**:
  - 입력창에 placeholder "여기에 유튜브 링크를 붙여넣으세요 (여러 개 OK, 드래그해도 돼요)".
  - 앱 창 어디에 드롭해도 URL/브라우저 탭이면 감지 (DragOver 시 전체 창에 파란 오버레이 "여기에 놓으세요").
  - **클립보드 자동 감지**: 앱 포커스 획득 시 클립보드를 1회 스캔, 유튜브 URL이면 창 상단에 노란 배너 "`https://youtu.be/...` 를 추가할까요? [추가] [무시]".
  - "붙여넣기" 버튼 제거. "작업 추가" 버튼만 유지하되 Enter 키로도 동작.
- **효과**: 신규 사용자 평균 첫 큐 추가 시간 약 50% 단축 예상. 문의 "어떻게 붙여넣어요?" 제거.

### 3.2 작업 카드 디자인
- **문제**: 현재 한 줄 행에 [이모지] [포맷] [URL] [진행률바] [폴더] [중지] [X] 가 빽빽하게 나열. 썸네일 없어 어떤 영상인지 URL만으로 식별 어려움.
- **개선 — 카드 레이아웃**:
  ```
  ┌──────────────────────────────────────────────────┐
  │ [썸네일 80x45]  제목 (YoutubeExplode 로 미리 취득)   │
  │                 가수/채널 · 03:24 · MP3 · 5.2 MB    │
  │                 ━━━━━━━━━━━━━━━━━ 67%              │
  │                 ↓ 2.3 MB/s · 4초 남음    [⏸][🗂][✕]│
  └──────────────────────────────────────────────────┘
  ```
  - 썸네일 Image 컨트롤 (비동기 로딩, 실패 시 MP3/MP4 아이콘).
  - 상태별 좌측 테두리 색 4px: 대기(회색) / 진행(파랑) / 완료(초록) / 실패(빨강) / 일시정지(주황). **색 + 아이콘 + 텍스트** 3중 표현 (P2).
  - 진행률바는 굵게 (8px), 완료 시 체크 아이콘 오버레이.
- **효과**: 여러 작업 동시 수행 시 시각적 스캔 용이. 실수로 엉뚱한 영상 받았는지 바로 확인 가능.

### 3.3 진행 상태 가시성
- **문제**: 하단 "총 작업: 1" 숫자만 있음. 전체 진행 요약이 없어 "지금 몇 % 완료됐는지" 모름.
- **개선**:
  - **전체 진행률 요약 바**: 창 상단 고정. "5개 중 3개 완료 · 전체 62% · ↓ 평균 1.8 MB/s" 한 줄.
  - 앱 제목표시줄에도 반영: `YouTube Converter — 62% (3/5)`.
  - **Taskbar Progress** (Windows): TaskbarItemInfo.ProgressValue 를 세팅해 Windows 작업표시줄 아이콘 자체에 진행률이 차오르게. 다른 창을 써도 힐끔 확인 가능.
  - 애니메이션: 진행 중 카드는 진행률바에 은은한 shimmer (2초 주기). 완료 시 0.3초 scale pulse + 체크마크 fade-in.
- **효과**: 사용자가 "끝났는지 확인하러 앱 앞으로 자주 전환" 하는 빈도 감소.

### 3.4 완료 피드백
- **문제**: 작업 완료 시 로그에만 기록. 사용자가 눈치채기 어려움.
- **개선**:
  - **인-앱 Snackbar**: WPF-UI `Snackbar` 컨트롤로 우하단에서 3초 슬라이드. "✅ BTS - Dynamite.mp3 저장됨 [폴더 열기]".
  - **Windows 토스트** (ToastNotification API): 앱이 백그라운드일 때만. 액션 버튼 [폴더 열기] [파일 재생].
  - **완료 사운드**: 작고 짧은 ding (200ms 이하). 설정에서 끄기 가능, 기본 ON.
  - **숫자 배지**: 미확인 완료 건수를 작업 리스트 탭/헤더에 빨간 원 배지. 리스트를 보는 순간 0으로 초기화.
- **효과**: "다운로드 끝났는지 모르고 계속 기다리는" 케이스 제거.

### 3.5 빈 상태 (Empty State)
- **문제**: 현재 작업이 없을 때 빈 회색 박스. 사용자에게 다음 행동 안내가 없음.
- **개선**:
  ```
  ┌──────────────────────────────────────────────────┐
  │                                                    │
  │                   [일러스트]                       │
  │              (다운로드 아이콘 + 음표)               │
  │                                                    │
  │       아직 작업이 없어요                            │
  │       유튜브 링크를 위에 붙여넣거나                │
  │       이 창에 끌어다 놓으세요                       │
  │                                                    │
  │       [📋 클립보드에서 붙여넣기]                   │
  │                                                    │
  └──────────────────────────────────────────────────┘
  ```
  - 일러스트는 단색 SVG (DPI 자유). 라이선스 프리 (unDraw, Storyset).
  - CTA 버튼은 실제로 클립보드 → 파싱 → 큐 추가를 한 번에.
- **효과**: 첫 사용자 이탈률 감소, "뭘 해야 할지" 문의 제거.

### 3.6 포맷 / 동시성 설정 단순화
- **문제**: 첫 화면에 라디오(MP3/MP4) + 동시 작업 ComboBox(1~?) 노출. 초보자에게 "동시 작업이 뭐지" 부담.
- **개선**:
  - 포맷은 큰 **세그먼트 토글** 2개로: `[🎵 음악 MP3]  [🎬 영상 MP4]`. 전문 용어 대신 목적어.
  - 품질은 기본값 최고(MP3 192kbps / MP4 1080p 선호)로 자동. "고급 설정" 팝오버에서만 바꾸게.
  - 동시 작업 수는 숨기고 기본 3. 설정 화면(⚙️ 버튼 → 다이얼로그)에서만 조정. 앱 첫 화면에서 제거.
  - 저장 폴더는 하나의 얇은 바 "💾 저장 위치: ...\YtConverter [변경]". 첫 화면 세로 공간 절약.
- **효과**: 상단 컨트롤 영역 높이 약 40% 축소 → URL 입력과 작업 리스트에 공간 양보.

### 3.7 다크 모드 / Fluent Windows 11 스타일
- **문제**: 현재 네이티브 Win32 룩. 다크 모드 지원 없음. Win11 사용자 비중↑.
- **개선**:
  - **WPF-UI (lepoco)** 라이브러리 도입 (§4 참고). `ThemeType="Dark"/"Light"/"System"` 바인딩.
  - 상단 설정에 🌗 테마 토글. 기본값은 "시스템 따라가기".
  - 배경에 Mica-like 효과(완전한 Mica 는 WinUI만 지원, WPF는 유사 블러로 대체). 창 타이틀바에 Win11 스타일 캡션 버튼.
  - 버튼 모서리 8px radius, 그림자 대신 subtle elevation.
- **효과**: 2026년 Win11 사용자에게 "이 앱은 네이티브 같다" 인상. 다크모드 선호층 (개발자/야간 사용자) 만족.

---

## 4. WPF 구현 팁 — 라이브러리 비교

| 항목 | WPF-UI (lepoco) | ModernWpf (Kinnara) | MahApps.Metro | .NET 9 Fluent |
|------|-----------------|---------------------|---------------|---------------|
| 스타일 | Windows 11 Fluent | Win10 Fluent | Win8/10 Metro | Win11 Fluent (기본 컨트롤 한정) |
| 컨트롤 추가 | Snackbar, NavigationView, NumberBox, Dialog, ProgressRing, Card | 대부분 기본 컨트롤 재스타일 | Flyout, HamburgerMenu, Dialog 등 다수 | 추가 컨트롤 없음 |
| 다크 모드 | ✅ ThemeType API | ✅ ApplicationTheme | ✅ | ✅ ThemeMode |
| Mica | 유사 구현 | ❌ | ❌ | ❌ |
| 설치 | `Install-Package WPF-UI` | `Install-Package ModernWpfUI` | `Install-Package MahApps.Metro` | .NET 9 기본 |
| 라이선스 | MIT | MIT | MIT | MIT |
| 적용 난이도 | ★★☆ 중간 (App.xaml 리소스 추가 + 컨트롤 prefix 교체) | ★★☆ 중간 | ★★★ 높음 (Metro 전용 느낌 강해 커스터마이징 필요) | ★☆☆ 쉬움 (재컴파일만) |
| 2026년 유지보수 | 활발 | 저조 | 보통 | Microsoft 공식 |

**권장**: **.NET 9 Fluent 테마 + WPF-UI 부분 도입** 하이브리드.
- .NET 9 만으로는 Snackbar·Card·NavigationView 가 없어 P4/P5 만족 어려움.
- 풀 WPF-UI 마이그레이션은 공수 큼. 우선 Snackbar/ProgressRing/Card 3가지만 import.
- 참고: https://github.com/lepoco/wpfui
- 참고: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90

### 4.1 구현 순서 (엔지니어용)
1. `.csproj` 에 `<PackageReference Include="WPF-UI" Version="4.*" />` 추가.
2. `App.xaml` 에 `<ui:ThemesDictionary Theme="Dark"/><ui:ControlsDictionary/>` 머지.
3. `MainWindow` → `ui:FluentWindow` 로 변경, `ExtendsContentIntoTitleBar="True"` 로 Win11 타이틀바 스타일.
4. 기존 `Button` → `ui:Button` (Icon 속성 지원, `SymbolIcon="Play24"` 등).
5. 완료 피드백용 `ui:SnackbarPresenter` 를 MainWindow 루트에 배치 → ViewModel 에서 `ISnackbarService.Show(...)`.
6. 작업 카드 테두리는 `ui:Card` 로 감싸고 `ControlTemplate` 에 좌측 4px Border 바인딩.
7. Taskbar 진행률: `<Window.TaskbarItemInfo><TaskbarItemInfo ProgressState="..." ProgressValue="..."/></Window.TaskbarItemInfo>` 바인딩.

---

## 5. 접근성 체크리스트

WPF 앱은 UI Automation 기반으로 NVDA / 내레이터에 자동 노출되지만, 커스텀 컨트롤/장식 요소는 명시적으로 설정해야 한다.

### 5.1 키보드 네비게이션
- [ ] 모든 인터랙티브 컨트롤에 `TabIndex` 지정. 흐름: URL → 포맷 토글 → 폴더 → 시작 → 카드 리스트 → 로그.
- [ ] 포커스 표시(visual focus ring) 를 2px 강조색 outline 으로 커스터마이즈 (기본 점선보다 잘 보이게).
- [ ] **단축키**:
  - `Ctrl+V` : URL 입력창 외부에서도 붙여넣기 감지
  - `Enter` : 큐 추가
  - `F5` : 모두 시작
  - `Esc` : 현재 선택 작업 취소 (확인 다이얼로그)
  - `Delete` : 완료 작업 지우기
  - `Ctrl+,` : 설정
- [ ] `AccessKey` (밑줄 단축키): `_시작`, `_취소` 등 한글 버튼에도 설정.

### 5.2 스크린 리더 (NVDA / Narrator)
- [ ] 모든 아이콘 버튼에 `AutomationProperties.Name="폴더 열기"` 등 한국어 라벨.
- [ ] 진행률 카드는 `AutomationProperties.LiveSetting="Polite"` + 10% 단위 업데이트 시 `LiveRegion` 이벤트로 "67% 진행 중" 안내.
- [ ] 상태 이모지는 장식용 → `AutomationProperties.IsHiddenFromAutomationTree="True"`, 대신 텍스트 라벨을 screen reader 에 읽게.
- [ ] 토스트/스낵바는 `AutomationProperties.LiveSetting="Assertive"` 로 즉시 안내.
- 참고: https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/accessibility-best-practices

### 5.3 고 DPI
- [ ] `app.manifest` 에 `<dpiAwareness>PerMonitorV2</dpiAwareness>` 유지.
- [ ] 모든 아이콘을 **벡터** (SVG → XAML PathGeometry, 또는 WPF-UI SymbolIcon) 로. PNG 는 2x / 3x 버전 준비.
- [ ] 최소 폰트 13pt, 최소 클릭 타겟 32×32 px.
- [ ] 썸네일 Image 는 `RenderOptions.BitmapScalingMode="HighQuality"`.

### 5.4 색 대비
- [ ] 텍스트 vs 배경 대비 ≥ 4.5:1 (WCAG AA). 상태 색은 중립 배경에서 3:1 이상.
- [ ] 빨강(실패) / 초록(완료) 를 보는 사람 8% 는 구분 못함 → 반드시 아이콘(✅/⚠️) + 텍스트 동반.
- [ ] Windows "고대비" 모드 감지 시 `SystemColors.*` 리소스로 자동 전환.

---

## 6. Before / After 와이어프레임

### 6.1 현재 (Before)
```
┌──────────────────────────────────────────────────────────────┐
│ YouTube Converter — 병렬 큐                       _  □  ✕  │
├──────────────────────────────────────────────────────────────┤
│ URL(들) │ [                                    ] [붙여넣기] │
│         │ [                                    ] [작업추가] │
│ 포맷    │ (●) MP3  ( ) MP4    동시 작업 [ 3 ▾ ]            │
│ 저장폴더│ [C:\Users\...\YtConverter ]  [찾아보기] [폴더열기]│
│ [▶모두시작] [■모두취소] [🗑완료지우기]                         │
│ ┌ 변환 작업 ──────────────────────────────────────────────┐ │
│ │ ⌛MP3  https://youtube.com/...  [          ] [📁][■][✕] │ │
│ │       대기중                                              │ │
│ │                                                            │ │
│ │                                                            │ │
│ │                                                            │ │
│ └──────────────────────────────────────────────────────────┘ │
│ 총 작업: 1                                                   │
│ ┌ 로그 ──────────────────────────────────────────────────┐ │
│ │ 2026-04-18 22:45:58 INFO 앱 시작                        │ │
│ │ 2026-04-18 22:46:03 INFO 작업 추가: [MP3] https://...   │ │
│ └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```
문제: 좌측 라벨 열이 너무 넓다 / 포맷과 동시작업이 한 줄에 뭉쳐 있다 / 작업 카드는 한 줄뿐이라 썸네일·메타 표시 공간 없음 / 빈 상태 안내 없음 / 완료 피드백 없음.

### 6.2 제안 (After)
```
┌──────────────────────────────────────────────────────────────────┐
│ 🎵 YouTube Converter           🌗  ⚙  💾 ~\YtConverter   _ □ ✕│
├──────────────────────────────────────────────────────────────────┤
│ ▶ 5개 중 3개 완료 · 전체 62% ━━━━━━━━━━━━━━━━━━━━ ↓1.8MB/s     │ ← 전역 진행 요약
├──────────────────────────────────────────────────────────────────┤
│ ┌────────────────────────────────────────────────────────────┐ │
│ │  여기에 유튜브 링크를 붙여넣으세요                           │ │
│ │  (여러 개 OK · 끌어다 놓아도 돼요)                          │ │
│ │                                                              │ │
│ │                                      [📋 붙여넣기+추가]     │ │
│ └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│   [🎵 음악 MP3]   [🎬 영상 MP4]            [▶ 모두 시작]        │ ← 세그먼트 토글
│                                                                  │
│ 💡 클립보드에 유튜브 링크가 있어요: youtu.be/abc… [+ 추가] [X] │ ← 제안 배너
├──────────────────────────────────────────────────────────────────┤
│ ┃ ┌────┐ BTS - Dynamite                                         │
│ ┃ │썸넬│ HYBE LABELS · 03:19 · MP3 · 5.2 MB                    │
│ ┃ └────┘ ━━━━━━━━━━━━━━━━ 67%  ↓2.3MB/s · 4초 남음  [⏸][🗂][✕]│ ← 진행 (파란 바)
│ ┃ ┌────┐ NewJeans - Super Shy                                   │
│ ┃ │썸넬│ 완료 · 4.1 MB          ✅  [🗂 폴더 열기] [▶ 재생] [✕]│ ← 완료 (초록 바)
│ ┃ ┌────┐ Queen - Bohemian Rhapsody                              │
│ ┃ │썸넬│ 실패 · 네트워크 오류   ⚠️  [🔁 다시 시도] [로그] [✕] │ ← 실패 (빨강 바)
├──────────────────────────────────────────────────────────────────┤
│ 로그 펼치기 ▾                                                    │ ← 로그는 접힘
└──────────────────────────────────────────────────────────────────┘
   ┌─────────────────────────────────┐
   │ ✅ BTS - Dynamite.mp3 저장됨    │ ← 우하단 Snackbar (3초)
   │    [폴더 열기]                  │
   └─────────────────────────────────┘
```

### 6.3 빈 상태 (After)
```
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│                         ┌─────────┐                              │
│                         │  📥🎵   │     ← 일러스트                │
│                         └─────────┘                              │
│                                                                  │
│                  아직 작업이 없어요                               │
│         유튜브 링크를 위에 붙여넣거나 창에 끌어다 놓으세요        │
│                                                                  │
│                 [📋 클립보드에서 붙여넣기]                       │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

---

## 7. 로드맵 제안 (엔지니어 핸드오프)

작은 단위부터 적용해 회귀 리스크 최소화.

### Phase 1 — 비주얼 베이스라인 (1~2일)
- WPF-UI 패키지 도입, `FluentWindow` 마이그레이션
- 다크/라이트 토글
- 한국어 폰트 렌더링 확인 (Segoe UI Variable 대신 "Pretendard" 또는 "Noto Sans KR")

### Phase 2 — 핵심 친절함 (2~3일)
- 드래그앤드롭 + 클립보드 제안 배너
- 빈 상태 일러스트
- 세그먼트 토글 (MP3/MP4) + 설정 숨기기
- 작업 카드 리디자인 (썸네일 + 메타 + 좌측 색상 테두리)

### Phase 3 — 진행/완료 피드백 (1~2일)
- 전역 진행 요약 바 + Taskbar 진행률
- Snackbar + Windows 토스트 + 완료 사운드
- 숫자 배지

### Phase 4 — 접근성 하드닝 (1일)
- 키보드 단축키 전체 구현
- AutomationProperties 전 컨트롤 감사
- NVDA / 내레이터 실사용 테스트
- 고대비 모드 리소스 분기

### Phase 5 — 폴리싱
- 애니메이션 (shimmer, pulse, fade)
- "다음부턴 이 설정 기본값으로" 체크박스

---

## 8. 참고 자료 (출처)

- [4K Video Downloader Plus 26.1.0 Release Notes](https://www.4kdownload.com/blog/2026/04/13/4k-video-downloader-plus-26-1-0-is-out/)
- [4K Video Downloader Review 2026 (AddictiveTips)](https://www.addictivetips.com/software/4k-video-downloader-review/)
- [JDownloader Clipboard Observer Guide](https://technical-tips.com/blog/software/jdownloader-link-automatically-from-clipboard-2765)
- [JDownloader 2 Usage Guide (VideoProc)](https://www.videoproc.com/download-record-video/how-to-use-jdownloader-2.htm)
- [cobalt.tools](https://cobalt.tools/)
- [Y2Mate Effortless Conversion (OreateAI)](https://www.oreateai.com/blog/y2mate-your-goto-for-effortless-youtube-to-mp4-conversions/a77e5eb2e812122237535581f065a7a4)
- [WPF-UI (lepoco) GitHub](https://github.com/lepoco/wpfui)
- [ModernWpf (Kinnara) GitHub](https://github.com/Kinnara/ModernWpf)
- [MahApps.Metro](https://mahapps.com/)
- [What's New in WPF .NET 9 (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/whats-new/net90)
- [WinUI vs WPF 2026 Comparison (CTCO)](https://www.ctco.blog/posts/winui-vs-wpf-2026-practical-comparison/)
- [WPF Accessibility Best Practices (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/accessibility-best-practices)
- [WPF Drag and Drop Overview (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/drag-and-drop-overview)
- [4K Video Downloader NVDA Accessibility Guide](https://blindhelp.net/blog/how-use-4k-video-downloader-plus-windows-nvda)
- [Minimalist UI/UX Design (LogRocket)](https://blog.logrocket.com/ux-design/minimalism-ui-design-form-follows-function/)
- [A Pragmatic Approach to WPF Accessibility (CODE Magazine)](https://www.codemag.com/article/0810102/A-Pragmatic-Approach-to-WPF-Accessibility)
