# YtConverter — 즉시 실행 가능한 SEO/발견성 체크리스트

작성일: 2026-04-18
근거: `docs/research.md`
원칙: 각 항목은 **"오늘 하나씩 실행하면 끝"** 단위로 쪼갰음. 소요 시간·명령·검증 순서 포함.

---

## 체크리스트 10개

### 1. Repository Topics 15개 일괄 설정 (5분)

GitHub Topics 는 검색 정확 일치로 작동 — 최대 20개 가능, 15개 추천.

```bash
gh repo edit vavibolivia/YtConverter \
  --add-topic youtube-downloader \
  --add-topic youtube-dl \
  --add-topic youtube-mp3 \
  --add-topic youtube-mp4 \
  --add-topic mp3-converter \
  --add-topic mp3-downloader \
  --add-topic wpf \
  --add-topic dotnet \
  --add-topic csharp \
  --add-topic ffmpeg \
  --add-topic windows \
  --add-topic desktop-app \
  --add-topic youtubeexplode \
  --add-topic video-converter \
  --add-topic korean
```

검증: `gh repo view vavibolivia/YtConverter --json repositoryTopics`

---

### 2. Repository Description 키워드 최적화 (2분)

현재 About 를 검색 친화로 재작성 (5~15 단어, 주요 키워드 앞쪽).

```bash
gh repo edit vavibolivia/YtConverter \
  --description "YouTube to MP3/MP4 converter — WPF desktop app for Windows (.NET 8, FFmpeg, Korean UI, no ads)" \
  --homepage "https://github.com/vavibolivia/YtConverter/releases/latest"
```

검증: 리포 카드 상단에 description·homepage 링크 노출.

---

### 3. "Releases" 탭 유도용 Homepage 링크 확인 (1분)

`--homepage` 에 Releases 링크 설정(위 2번에 포함). About 의 아이콘 링크로 바로 노출되어 **다운로드 전환율 상승**.

---

### 4. README 상단 "즉시 다운로드" CTA 강화 (3분)

현재 README 는 스크린샷 뒤에 Releases 링크. **배지 위**로 CTA 블록 이동 권장:

```markdown
<p align="center">
  <a href="https://github.com/vavibolivia/YtConverter/releases/latest">
    <img src="https://img.shields.io/badge/Download-latest%20.exe-blue?style=for-the-badge&logo=github" alt="Download"/>
  </a>
</p>
```

이유: Shields.io 베스트 프랙티스 — "첫눈에 목적 달성 CTA 가 와야 전환율 최대".

---

### 5. Social Preview 이미지 업로드 (5분)

GitHub 리포 Settings → Options → Social preview → 1280×640 PNG 업로드.
- 없으면 Twitter/Reddit/Slack 공유 시 텍스트만 노출 → CTR 낮음.
- 앱 스크린샷 + 제목 + "MP3/MP4 converter — Windows WPF" 문구 권장.

검증: `curl -I https://opengraph.githubassets.com/…/vavibolivia/YtConverter` 로 미리보기 URL 확인.

---

### 6. awesome-wpf PR 제출 (20분)

Fork: https://github.com/Carlos487/awesome-wpf
- `README.md` 의 `Sample Apps` 섹션에 추가:

```markdown
- [YtConverter](https://github.com/vavibolivia/YtConverter) - YouTube to MP3/MP4 converter. WPF + .NET 8 + FFmpeg. Single-file self-contained. Korean UI, MIT license.
```

- Commit → PR 제목: `Add YtConverter to Sample Apps`
- PR 본문에 스크린샷·라이선스(MIT)·빌드 상태 언급.

같은 요령으로 `quozd/awesome-dotnet` 의 `Media` 또는 `Sample Projects` 섹션에도 PR 시도 (경쟁 심함, 거절 가능성 있음).

---

### 7. GeekNews Show 등록 (10분)

https://news.hada.io/ 로그인 → "글쓰기" → Show 카테고리.

템플릿:
```
제목: [Show GN] YtConverter — 유튜브 MP3/MP4 변환기 (WPF, 오픈소스)

본문:
- 광고/회원가입/설치 전혀 없음, 더블클릭 실행 (self-contained 72MB)
- 한국어 UI, 한글·이모지 파일명 안전 처리
- 실시간 대비 20~25배 변환 속도 (Mariah Carey 3:42 → 10초)
- 기술 스택: .NET 8 + WPF + YoutubeExplode + FFmpeg 자동 설치
- MIT 라이선스, 코드 전체 공개
- 다운로드: https://github.com/vavibolivia/YtConverter/releases/latest
- 코드/이슈: https://github.com/vavibolivia/YtConverter
```

타이밍: **평일 오전 9~10시 KST** (개발자 트래픽 피크).

---

### 8. OKKY 커뮤니티 프로젝트 글 (15분)

https://okky.kr/community → "커뮤니티" 게시판.

- 제목 예: `유튜브 → MP3/MP4 변환기 만들었습니다 (WPF, 오픈소스)`
- GeekNews 와 동일 내용 + **개발 중 배운 점** (YoutubeExplode 사용기, FFmpeg 자동 프로비저닝, WPF MVVM) 1~2단락 추가.
- 주의: 단순 홍보 글은 비호감. "기술 공유" 앵글로 작성.

---

### 9. GitHub Actions 릴리스 자동화 워크플로 추가 (30분)

파일: `.github/workflows/release.yml`
참고 템플릿: https://github.com/ghost1372/Dotnet-Publish-Action/blob/main/dotnet-release.yml

핵심 단계:
1. 트리거: `on: push: tags: ['v*']`
2. `actions/checkout@v4`
3. `actions/setup-dotnet@v4` with `dotnet-version: 8.0.x`
4. `dotnet publish src/YtConverter.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish`
5. SHA256 파일 생성: `Get-FileHash publish/YtConverter.App.exe -Algorithm SHA256 > publish/YtConverter.App.exe.sha256`
6. `softprops/action-gh-release@v2` 로 `publish/*` 업로드.

이득: 이후 태그 `git tag v1.0.1 && git push --tags` 만으로 Release 자산 자동 생성 → **winget/Chocolatey 등록 기반** 확보.

---

### 10. README 하단 "면책 + Badges Row" 보강 (5분)

현재 면책 문구 앞에 **경고 박스** 추가 (research.md §4.3 초안 활용):

```markdown
> ⚖️ **면책**: 개인 합법적 사용(타임시프트·백업) 전용. YouTube 약관·저작권법 준수는 사용자 책임입니다.
> ⚖️ **Disclaimer**: Educational and personal use only. You bear all legal responsibility.
```

그리고 CI/CodeQL 배지 추가 (빌드 자동화 완료 후):

```markdown
[![Release Build](https://github.com/vavibolivia/YtConverter/actions/workflows/release.yml/badge.svg)](https://github.com/vavibolivia/YtConverter/actions/workflows/release.yml)
[![CodeQL](https://github.com/vavibolivia/YtConverter/actions/workflows/codeql.yml/badge.svg)](https://github.com/vavibolivia/YtConverter/actions/workflows/codeql.yml)
```

---

## 실행 우선순위 요약

| 순서 | 항목 | 소요 | 기대 효과 |
| --- | --- | --- | --- |
| 1 | #1 Topics 설정 | 5분 | GitHub 내부 검색 즉시 노출 |
| 2 | #2 Description 최적화 | 2분 | About 랭킹 요소 강화 |
| 3 | #4 README CTA 강화 | 3분 | 전환율 직접 상승 |
| 4 | #10 면책 + 배지 | 5분 | 신뢰·법적 위험 완화 |
| 5 | #5 Social Preview | 5분 | 외부 공유 CTR 상승 |
| 6 | #9 릴리스 자동화 | 30분 | 이후 배포 작업 제로화, winget 준비 |
| 7 | #6 awesome-wpf PR | 20분 | 백링크·노출 |
| 8 | #7 GeekNews Show | 10분 | 한국 개발자 초기 star 유입 |
| 9 | #8 OKKY 글 | 15분 | 지속 노출 |
| 10 | #3 Homepage | 1분 | CTA 경로 단축 (#2에 포함) |

**1일차 목표**: 1~5번 (총 20분) → 즉시 노출 개선.
**1주차 목표**: 6~10번 — 외부 백링크·자동화 인프라 구축.

---

## 실행 후 측정

- GitHub Insights → Traffic: views/clones 주간 추이
- `gh repo view vavibolivia/YtConverter --json stargazerCount,forkCount,watchers`
- Google `site:github.com YtConverter` 및 `"youtube mp3 wpf" korean` 검색 순위
- Release 다운로드 수: `gh release list`, `gh release view <tag>`
