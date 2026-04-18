# YtConverter GitHub 노출/발견성 리서치 리포트

작성일: 2026-04-18
대상 저장소: https://github.com/vavibolivia/YtConverter
작성 근거: WebSearch/WebFetch 실측 (하단 출처 링크 모두 포함)

---

## 1. 경쟁/유사 도구 분석

### 1.1 주요 경쟁자 비교

| 도구 | 유형 | 가격 | 지원 OS | 강점 | 약점 |
| --- | --- | --- | --- | --- | --- |
| **4K Video Downloader** | GUI 데스크톱 | Freemium (Pro 유료) | Win/Mac/Linux | 4K/8K, 스마트 모드, 플레이리스트/채널 일괄, 자막 | 링크 파싱 실패·다운로드 정지 보고, 핵심 기능 페이월 뒤 |
| **JDownloader 2** | GUI 다운로드 매니저 | 무료 | Win/Mac/Linux(Java) | 대량 링크/플레이리스트, 플랫폼 변경 대응 빠름, 한도 없음 | UI 복잡·낡음, Java 런타임, 초보자 러닝커브 |
| **ClipGrab** | GUI 데스크톱 | 무료 오픈소스 | Win/Mac/Linux | "간단·빠름·안 복잡함", 포맷 변환 내장 | 고급 기능(스크립트·재생목록 세분화) 부족 |
| **yt-dlp** | CLI | 무료 오픈소스 | 크로스 플랫폼 | 수천 사이트 지원, 최신 유튜브 변경 즉시 대응, 포맷/메타데이터 제어 | CLI 전용, 일반 사용자 진입 장벽 |
| **cobalt.tools** | 웹 서비스 | 무료 | 브라우저 | 광고 없음·회원가입 없음, 30+ 플랫폼 | **2025년 중반 이후 유튜브 공식 인스턴스 차단** (네트워크 제한으로 거의 사용 불가) |
| **y2mate** | 웹 서비스 | 무료(광고) | 브라우저 | 브라우저만 있으면 됨, 오디오 추출 | SD 화질 한정, 공격적 팝업/가짜 버튼, 2시간 초과 영상 실패 보고 |

### 1.2 YtConverter 의 차별화 포인트 (3가지)

1. **WPF 네이티브 + 설치 불필요 단일 실행**
   데스크톱 GUI 경쟁군(4K Video Downloader, ClipGrab)이 인스톨러·런타임을 요구하는 반면, YtConverter 는 self-contained single-file exe (72MB). .NET 런타임 설치 불요, 더블클릭 즉시 실행.

2. **한국어 네이티브 UI + Unicode/이모지 파일명 안전 처리**
   대부분 해외 툴은 영문 UI 우선, 한글 파일명/경로에서 인코딩 문제 자주 발생. YtConverter 는 한국어 UI·한글·이모지·특수문자 모두 안전 처리 — 국내 사용자 대상 유일한 포지션.

3. **광고/번들웨어/회원가입 0, MIT 오픈소스**
   웹 기반(y2mate)은 광고 공격적, GUI 경쟁군 일부(4K VD)는 페이월. cobalt.tools 는 최근 유튜브 차단. YtConverter 는 MIT, 코드 공개, 광고 없음, 로컬 실행 (프라이버시 우위).

**부가**: yt-dlp 의 기술력에 GUI 편의성을 얹은 "한국인을 위한 yt-dlp 의 GUI 대안" 포지셔닝이 유효.

---

## 2. GitHub SEO 및 발견성 전략

### 2.1 추천 Topics (15개 — 단일 단어 위주, 정확 일치 검색용)

GitHub Topics 는 **정확 일치(exact match)** 로 검색되므로 단일 단어 또는 하이픈 결합어 사용.

| 우선순위 | Topic | 근거 |
| --- | --- | --- |
| Tier 1 | `youtube-downloader` | 최상위 카테고리, 월간 검색 최다 |
| Tier 1 | `youtube-dl` | 키워드 연관성 (상위 기대) |
| Tier 1 | `youtube-mp3` | MP3 추출 검색어 직격 |
| Tier 1 | `youtube-mp4` | MP4 변환 검색어 직격 |
| Tier 1 | `mp3-converter` | 변환 카테고리 |
| Tier 2 | `mp3-downloader` | 포맷 다운로더 |
| Tier 2 | `wpf` | 프레임워크 필터 사용자 |
| Tier 2 | `dotnet` | 생태계 발견 |
| Tier 2 | `csharp` | 언어 필터 |
| Tier 2 | `ffmpeg` | 기술 스택 |
| Tier 2 | `windows` | OS 필터 |
| Tier 2 | `desktop-app` | 애플리케이션 유형 |
| Tier 3 | `youtubeexplode` | 핵심 라이브러리 |
| Tier 3 | `video-converter` | 변환 일반 |
| Tier 3 | `korean` | 한국어 UI 포지셔닝 |

> 주의: 한글 Topic (예: `유튜브다운로드`) 은 GitHub 공식 정규화되지 않아 검색 효율 낮음 → README 본문 `Keywords` 섹션에 남기되 Topic 은 영문만.

### 2.2 README 배지/구조 권장사항

- **상단 배지는 2~4개로 제한** (과다하면 가독성 저하). 핵심 상태만:
  - 릴리스 버전 (`img.shields.io/github/v/release`)
  - 다운로드 수 (`github/downloads/.../total`)
  - 라이선스 (MIT)
  - 플랫폼/런타임 (.NET 8, WPF/Windows)
- 현재 YtConverter README 는 이미 5개 적용 — **적절**.
- 추가 권장: **GitHub Actions 빌드 상태 배지**, **CodeQL 배지** (신뢰성 시그널).
- 구조: 제목 → 배지 → 1줄 요약 → 스크린샷 → 기능 → 빠른 시작 → FAQ → 라이선스. 현재 README 는 이 구조 충족.

### 2.3 GitHub 검색 랭킹 요소 (확인된 것들)

- **Name / About / Topics** 가 검색 랭킹의 핵심. About 는 5~15 단어, 주요 키워드를 **앞쪽**에 배치.
- Topics 는 **정확 일치**, Name/About 는 부분/변형 일치.
- Stars, Watchers, Forks, **최근 활동성**이 엔진 가중치에 반영 (공식 공개는 안 됨, 커뮤니티 분석 기반).
- 알고리즘은 "환경별" 다중 — Topic 페이지 랭킹 ≠ 검색 결과 랭킹. Topic 페이지가 Google SEO 에 더 유리.

### 2.4 GitHub Trending 진입 조건

- **공식 비공개 알고리즘**. 커뮤니티 관찰 요약:
  - 단순 star 총합이 아니라 **"최근 star 증가 속도"** 가 핵심 (rolling window).
  - Reddit/HN 에서 흔히 인용되는 벤치마크: **24시간 내 500 star** 가 트렌딩 진입 패턴.
  - 언어별 임계 다름(JS·Python 은 더 높음, 틈새 언어는 낮음). **C# 은 상대적으로 낮은 임계**로 진입 가능.
  - Fork, Issue, PR, 코멘트 등 **참여 지표** 도 같이 고려.
- **현실적 전략**: 언어별 트렌딩(https://github.com/trending/c%23) 먼저 목표. 전체 트렌딩은 Reddit r/csharp + HN + Korean GeekNews 동시 배포 시 가능성.

---

## 3. 웹 노출 채널

### 3.1 Awesome Lists (실제 URL)

| 리스트 | URL | 등록 방법 | 적합 섹션 |
| --- | --- | --- | --- |
| **awesome-dotnet** (quozd) | https://github.com/quozd/awesome-dotnet | Fork + PR, CONTRIBUTING.md 준수, 상용/프로프라이어터리도 수용 | `SDK and API Clients` 또는 `Media` (YouTube 관련 기존 예: YoutubeExplode) |
| **awesome-wpf** (Carlos487) | https://github.com/Carlos487/awesome-wpf | Fork + PR, 기존 포맷(섹션별 정렬) 따름 | `Sample Apps` 또는 `Utilities` |
| **awesome-dotnet-core** (thangchung) | https://github.com/thangchung/awesome-dotnet-core | Fork + PR | 유틸리티/툴 섹션 |
| **extra-awesome-dotnet** (ara3d) | https://github.com/ara3d/extra-awesome-dotnet | Fork + PR | .NET 리포지토리 큐레이션 |
| **awesome-dotnet-ranked** (jefersonsv) | https://github.com/jefersonsv/awesome-dotnet-ranked | 자동 랭킹 — star 수에 연동 | 수동 등록 불필요 |

> 주의: `.NET 8 + WPF` 프로젝트이므로 awesome-wpf 가 가장 적합. awesome-dotnet 은 경쟁이 치열하지만 노출 이득 크다.

### 3.2 Reddit 서브레딧 (공유 주의사항 포함)

- **규칙 핵심**: Reddit 은 구 9:1 규칙은 폐기, 현재는 **"진정성 있는 참여자"** 기준. 핵심 테스트: "제품 언급을 빼도 유용한 글인가?"
- **서브레딧 가이드 (sidebar 필독)**:
  - `r/dotnet` — 프로젝트 공유 가능하나 단순 홍보는 비추. **빌드 과정·기술 선택 이유** 에세이 형태 권장.
  - `r/csharp` — 유사. 코드 스니펫·패턴 중심 글이 더 환영받음.
  - `r/Windows` — 일반 Windows 유저 타겟. "무료 YouTube → MP3 툴" 앵글, 단 YouTube ToS 이슈 민감 가능.
  - `r/coolgithubprojects` — 자기 프로젝트 공유 공식 허용.
  - `r/opensource` — 오픈소스 프로젝트 런칭 적합.
  - `r/SideProject` — 사이드 프로젝트 런칭 적합.
- **공유 시 안전 패턴**: 먼저 2~3주간 타인 글에 성실히 댓글/답변 → 첫 공유 시 기술적 비화/빌드 로그 포함 → "저는 개발자이고 여기 코드 공개했습니다" 투명성.

### 3.3 한국 커뮤니티

| 채널 | URL | 적합성 |
| --- | --- | --- |
| **OKKY** | https://okky.kr/community | 국내 최대 개발자 커뮤니티(17만+). "커뮤니티" 게시판 → 프로젝트 공유 글. **C#/WPF 인구 상대적 적음**이라 차별화 유리. |
| **GeekNews (Hada)** | https://news.hada.io/ | "한국판 Hacker News". **Show GN** 섹션이 자기 프로젝트 공개용 공식 공간. 한국 개발자 발견성 최상. |
| **공개SW 포털** | https://www.oss.kr/ | 오픈소스 등록·수상작 소개. 제출 양식 제공. 국가 지원 생태계. |
| **Disquiet (옵션)** | https://disquiet.io/ | 메이커/프로덕트 커뮤니티. 유저 발견 가능. |

**권장 동선**: GeekNews Show → OKKY 커뮤니티 글 → 공개SW 포털 등록.

### 3.4 Product Hunt / Hacker News 적합성

- **Hacker News** (적합 ★★★★☆): DevTool·오픈소스는 HN 적합. 프론트페이지 3시간 시 6,000~8,000 방문 사례. **Show HN** 포맷 권장. C# + WPF + 한국어 UI는 틈새 감도. 단, YouTube 다운로더 자체는 ToS 논쟁 가능성.
- **Product Hunt** (적합 ★★☆☆☆): PH 는 주로 B2B SaaS 친화. 개발자 툴이라도 Freemium/SaaS 에 가중. 데스크톱 OSS 단일 바이너리는 후광 효과 제한. 발사 원할 시 **4~6주 사전 대기열·화요일/수요일 오전 12:01 PST** 패턴 준수.
- **권장**: HN Show 먼저, PH 는 선택사항.

---

## 4. YouTube 정책 / 법적 고려

### 4.1 YouTube ToS (2026 현재)

- **Section 5.B** — "다운로드·복제·배포·전송·방송·표시·판매·라이선스 허용 안 됨, 단 서비스가 그런 기능을 제공하거나 콘텐츠 소유자의 사전 서면 허가가 있는 경우 제외."
- 또한 "서비스의 비디오 재생 페이지, 임베드 플레이어, 또는 YouTube 가 명시적으로 승인한 수단 외의 기술로 콘텐츠 접근 금지".
- **공식 오프라인 허용 경로**: YouTube Premium 다운로드 (모바일·선택 데스크톱 브라우저, 29일 온라인 검증).
- **CC 라이선스 영상**: 다운로드·재사용·개작 명시적 허용.

### 4.2 DMCA / 저작권 — 개인 용도 vs 재배포

- 2026년 초 **캘리포니아 연방 법원 판결**: 스트림리퍼(ripping) 도구로 리액션/코멘터리 클립 다운 시 DMCA § 1201 **기술적 보호조치 우회 조항** 위반 가능성.
- **Fair Use 방어 한계**: 공정 이용 인정되어도 § 1201 우회 자체는 별개 위반 — 즉 개인 용도라도 기술적 보호 우회 시 책임 가능.
- DMCA 예외는 Library of Congress 가 3년마다 개정, YouTube 개인 다운로드 명시적 커버 없음 **회색 지대**.
- **결론**: 개인 타임시프트·백업은 여러 법역에서 관례상 관용되지만 법적 안전망은 좁다. 도구 배포자는 **면책 명시** 필수.

### 4.3 README 면책 문구 초안

**한국어 (50자 내외, 2종)**:

1. "개인 합법적 사용(타임시프트·백업) 전용. YouTube 약관·저작권법 준수는 사용자 책임입니다."  *(46자)*
2. "본 도구는 교육·개인 보관용. 재배포·상업 이용은 금지되며 각국 법을 따르십시오."  *(39자)*

**영문 (50자 내외, 2종)**:

1. "For personal lawful use only. Respect YouTube ToS and local copyright law."  *(74 chars — 약간 초과, 영문 50자는 너무 짧아 실용성 낮음)*
2. "Educational and personal use only. You bear all legal responsibility."  *(69 chars)*

> 권장: 현재 README "⚖️ 면책 / 라이선스" 섹션 유지 + 위 한국어 1번 문장을 상단 경고 박스로 추가.

---

## 5. 배포 채널 확장

### 5.1 winget 등록 절차

- 저장소: https://github.com/microsoft/winget-pkgs
- 제출 문서: https://learn.microsoft.com/en-us/windows/package-manager/package/manifest
- **최소 요구사항**:
  - 패키지 식별자 **Unique**. 버전당 PR 1건, 패키지당 PR 1건.
  - **Silent install 지원 필수** (무인 설치 불가 시 등록 불가).
  - 매니페스트 경로: `manifests/<소문자 첫글자>/<Publisher>/<Package>/<Version>/`
  - 설치자 유형: MSIX / MSI / APPX / MSIXBundle / APPXBundle / **.exe**
  - 메타데이터 최대한 채울 것 (PackageIdentifier, Version, Publisher, Name, License, ShortDescription, Installers[Architecture, InstallerType, InstallerUrl, InstallerSha256]).
- **YtConverter 실행 가능성**: self-contained single-file exe 이고, CLI 플래그로 `--silent` 가 없으면 인스톨러가 아니기에 `InstallerType: zip` 또는 `portable` 옵션 활용 가능. 실제로는 portable 매니페스트가 가장 간단.
- **도구**: Microsoft 제공 `wingetcreate` 또는 커뮤니티 `Komac` 사용 추천. Windows Sandbox 로 로컬 검증 후 PR.
- 응답 기한: BOT 이슈 재할당 시 7일 내 처리 안 하면 자동 close.

### 5.2 Chocolatey 등록 절차

- 저장소: https://community.chocolatey.org
- 패키지 요소 최소: **.nuspec** (XML) — 4개 요소 중 nuspec 만 필수.
- **.nuspec 필수 필드** (개요):
  - `id` (소문자, 하이픈), `version`, `title` (공식 대소문자/공백 유지), `authors`, `owners`, `projectUrl`, `licenseUrl`, `iconUrl`, `tags`, `summary`, `description`, **`packageSourceUrl`** (CPMR0040 룰: nuspec 소스 위치 필수), `releaseNotes`.
- **인코딩**: `.nuspec` / `.ps1` UTF-8. PowerShell 스크립트는 **BOM 포함 UTF-8** 필수.
- **검증 프로세스**: 모든 버전은 커뮤니티 모더레이션 통과 후 공개.
- 일반 명령: `choco pack`, `choco apikey`, `choco push YtConverter.<version>.nupkg --source https://push.chocolatey.org/`.

### 5.3 GitHub Release 자동화 (Actions)

공식 및 검증된 템플릿:

1. **Microsoft 공식 WPF 샘플**: https://github.com/microsoft/github-actions-for-desktop-apps (태그 푸시 시 릴리스 생성·자산 업로드)
2. **.NET publish 가이드**: https://learn.microsoft.com/en-us/dotnet/devops/dotnet-publish-github-action
3. **Dotnet-Publish-Action (ghost1372)**: https://github.com/ghost1372/Dotnet-Publish-Action
   - WPF 포함 즉시 사용 가능
   - Matrix (x64/x86/arm64), self-contained 지원
   - auto-changelog + `ncipollo/release-action` 사용
   - 템플릿 YAML: https://github.com/ghost1372/Dotnet-Publish-Action/blob/main/dotnet-release.yml

**추천 최소 워크플로 요소** (YtConverter 에 적용):
- 트리거: 태그 푸시 (`v*`)
- `actions/checkout@v4` → `actions/setup-dotnet@v4` (.NET 8)
- `dotnet publish src/YtConverter.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`
- `actions/upload-artifact@v4` (빌드 잡 간 전달)
- `softprops/action-gh-release@v2` 또는 `ncipollo/release-action@v1` 로 Release + 자산 첨부
- SHA256 체크섬 파일 동봉 (winget 매니페스트 요구사항 충족)

---

## 출처 (모두 2026-04-18 조회)

### 경쟁 도구
- [Top 10 Free Working yt-dlp Alternatives 2026 — WinX](https://www.winxdvd.com/streaming-video/yt-dlp-alternatives.htm)
- [Best YouTube Downloaders of 2026: Top Tools Compared — RedSider](https://www.redsider.com/best-youtube-downloaders-of-2026-top-tools-compared/)
- [4K Video Downloader Alternatives — AlternativeTo](https://alternativeto.net/software/4k-video-downloader/)
- [Cobalt.tools Deep Review and Top 5 Alternatives 2026 — Wondershare](https://videoconverter.wondershare.com/video-converters/cobalt-tools-alternative.html)
- [cobalt tools YouTube block issue #1356](https://github.com/imputnet/cobalt/issues/1356)
- [Cobalt Tools Review 2026: Is It Safe](https://ytsaver.net/review/cobalt-tools/)
- [Top 5 JDownloader Alternatives in 2026](https://www.winxdvd.com/streaming-video/jdownloader-alternatives.htm)

### GitHub SEO / Trending
- [GitHub SEO: Rank your repo — Nakora](https://nakora.ai/blog/github-seo)
- [The Ultimate Guide to GitHub SEO for 2025 — DEV](https://dev.to/infrasity-learning/the-ultimate-guide-to-github-seo-for-2025-38kl)
- [GitHub Search Engine Optimization — MarkePear](https://www.markepear.dev/blog/github-search-engine-optimization)
- [GitHub Project Visibility and SEO Guide — Codemotion](https://www.codemotion.com/magazine/dev-life/github-project/)
- [How to trend on Github — Medium](https://medium.com/@manoj.radhakrishnan/how-to-trend-on-github-dcdda9055f8)
- [GitHub Trending Algorithm discussion — community #163970](https://github.com/orgs/community/discussions/163970)
- [GitHub Trending explore](https://github.com/trending)
- [youtube-downloader topic](https://github.com/topics/youtube-downloader)
- [youtube-mp3 topic](https://github.com/topics/youtube-mp3)
- [youtube-mp4-downloader topic](https://github.com/topics/youtube-mp4-downloader)

### Awesome Lists
- [awesome-dotnet (quozd)](https://github.com/quozd/awesome-dotnet)
- [awesome-wpf (Carlos487)](https://github.com/Carlos487/awesome-wpf)
- [awesome-dotnet-core (thangchung)](https://github.com/thangchung/awesome-dotnet-core)
- [extra-awesome-dotnet (ara3d)](https://github.com/ara3d/extra-awesome-dotnet)
- [awesome-dotnet-ranked (jefersonsv)](https://github.com/jefersonsv/awesome-dotnet-ranked)

### Reddit / 커뮤니티
- [Reddit Self-Promotion Rules 2026 — KarmaGuy](https://karmaguy.io/en/blog/reddit-self-promotion-rules)
- [What Are Reddit Self-Promotion Rules — Conbersa](https://www.conbersa.ai/learn/reddit-self-promotion-rules)
- [Self-Promote on Reddit — JetThoughts](https://jetthoughts.com/blog/self-promote-on-reddit-without-getting-banned-promotion/)
- [OKKY 커뮤니티](https://okky.kr/community)
- [GeekNews (Hada)](https://news.hada.io/)
- [공개SW 포털](https://www.oss.kr/)

### Product Hunt / Hacker News
- [Product Hunt Launch Guide 2026 — Calmops](https://calmops.com/indie-hackers/product-hunt-launch-guide/)
- [How to launch a developer tool on PH 2026 — HackMamba](https://hackmamba.io/developer-marketing/how-to-launch-on-product-hunt/)
- [Product Hunt vs Hacker News — DoWhatMatter](https://dowhatmatter.com/guides/product-hunt-vs-hacker-news)
- [Lessons launching a developer tool on HN vs PH — Medium](https://medium.com/@baristaGeek/lessons-launching-a-developer-tool-on-hacker-news-vs-product-hunt-and-other-channels-27be8784338b)

### YouTube ToS / DMCA
- [YouTube Terms of Service](https://www.youtube.com/static?gl=GB&template=terms)
- [YouTube ToS Explained — TLDRLegal](https://www.tldrlegal.com/license/youtube-terms-of-service)
- [YouTube Copyright Policies](https://www.youtube.com/howyoutubeworks/policies/copyright/)
- [Fair use on YouTube — YouTube Help](https://support.google.com/youtube/answer/9783148?hl=en)
- [DMCA on YouTube in 2026 — DMCADesk](https://dmcadesk.com/blogs/dmca-on-youtube-copyright-strikes-and-takedowns/)
- [Federal Court Ruling on YouTube Ripping Tools — WebProNews](https://www.webpronews.com/federal-court-ruling-on-youtube-ripping-tools-reshapes-digital-copyright-enforcement-under-dmca/)
- [How third-party YouTube downloads create copyright risks — MediaNama](https://www.medianama.com/2026/02/223-dmca-ruling-third-party-youtube-downloads-legal-risks-creators/)
- [Court Rules Ripping YouTube Clips Can Violate DMCA — Slashdot](https://news.slashdot.org/story/26/02/05/1924252/court-rules-that-ripping-youtube-clips-can-violate-the-dmca)

### 배포 채널
- [winget Create your package manifest — MS Learn](https://learn.microsoft.com/en-us/windows/package-manager/package/manifest)
- [Submit packages to Windows Package Manager — MS Learn](https://learn.microsoft.com/en-us/windows/package-manager/package/)
- [microsoft/winget-pkgs repo](https://github.com/microsoft/winget-pkgs)
- [Chocolatey NuGet packages and Nuspec — Chocolatey Community](https://community.chocolatey.org/courses/creating-chocolatey-packages/nuget-packages-and-nuspec)
- [Chocolatey Create Packages docs](https://docs.chocolatey.org/en-us/create/create-packages/)
- [CPMR0040 PackageSourceUrl rule](https://docs.chocolatey.org/en-us/community-repository/moderation/package-validator/rules/cpmr0040/)
- [GitHub Actions for Desktop Apps — microsoft](https://github.com/microsoft/github-actions-for-desktop-apps)
- [dotnet-publish-github-action — MS Learn](https://learn.microsoft.com/en-us/dotnet/devops/dotnet-publish-github-action)
- [ghost1372/Dotnet-Publish-Action](https://github.com/ghost1372/Dotnet-Publish-Action)
- [dotnet-release.yml 템플릿](https://github.com/ghost1372/Dotnet-Publish-Action/blob/main/dotnet-release.yml)

### 배지
- [shields.io](https://shields.io/)
- [Readme Badges GitHub Best Practices — daily.dev](https://daily.dev/blog/readme-badges-github-best-practices)
- [badges/awesome-badges](https://github.com/badges/awesome-badges)
