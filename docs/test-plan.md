# 테스트 계획 — YouTube → MP3/MP4 WPF 변환기
_최종 갱신: 2026-04-18_

## 1. 20개 테스트 에이전트 분담표
| # | 에이전트 | 영역 | 시나리오 | 합격 기준 | 자동화 |
| --- | --- | --- | --- | --- | --- |
| A01 | Short-Video | 기능 | 3분 이내 영상 MP3 변환 | 파일 생성, 재생 가능, 비트레이트≥128kbps | 부분(ffprobe) |
| A02 | Long-Video | 기능/성능 | 1시간 이상 영상 MP4 | 끝까지 진행, CPU·메모리 정상 | 수동 |
| A03 | Live-Stream | 예외 | 라이브 URL 입력 | "라이브는 변환 불가" 메시지 | 자동 |
| A04 | Private-Video | 예외 | 비공개/삭제 URL | 명확한 에러 토스트 | 자동 |
| A05 | Age-Restricted | 예외 | 연령 제한 URL | 적절한 에러 메시지, 크래시 없음 | 자동 |
| A06 | Region-Blocked | 예외 | 지역 제한 URL | 에러 매핑 확인 | 수동 |
| A07 | Playlist-URL | 기능 | `?list=` URL | 첫 영상 또는 "재생목록 미지원" 안내 | 자동 |
| A08 | Invalid-URL | 예외 | 랜덤/오타 URL | 시작 버튼 비활성 또는 검증 오류 | 자동(UI Test) |
| A09 | Network-Drop | 스트레스 | 다운로드 중 Wi-Fi OFF | 진행률 멈춤 후 재시도, 실패 시 에러 | 반자동 |
| A10 | Concurrent-Jobs | 스트레스 | 동시 3건 변환 | 모두 완료, UI 반응성 유지 | 자동 |
| A11 | Low-Disk | 환경 | 남은 공간 50MB | 사전 경고, 파일 파손 없음 | 수동 |
| A12 | Unicode-Title | 호환 | 한국어/이모지 제목 | 파일명 유효, 재생 가능 | 자동 |
| A13 | Special-Chars | 호환 | `/ \ : * ? " < > |` 포함 제목 | 치환 규칙 준수 | 자동(유닛) |
| A14 | Cancel-Flow | UX | 변환 50% 에서 취소 | 2초 내 중단, 임시 파일 삭제 | 자동 |
| A15 | Restart-Flow | UX | 실패 후 재시작 | 임시파일 정리, 정상 완료 | 반자동 |
| A16 | Korean-UI | 현지화 | ko-KR 라벨/메시지 | 잘림/오타 없음 | 수동 |
| A17 | DPI-Scaling | UX | 100/150/200% | 레이아웃 정상 | 수동 |
| A18 | Accessibility | UX | 키보드 탭/스크린리더 | 모든 컨트롤 접근 가능 | 반자동 |
| A19 | Memory-Leak | 코드 품질 | 50회 반복 변환 | 누수 < 30MB, GC 안정 | 자동 |
| A20 | Stress-Loop | 무한 회귀 | 대화 없을 때 무한 테스트 | 24h 내 크래시 0, 실패율 <1% | 자동 |

## 2. 사용성(UX) 체크리스트
- [ ] URL 붙여넣기 버튼 동작
- [ ] 저장 폴더 선택/"열기" 버튼
- [ ] 변환 중 Start disable, Cancel enable
- [ ] 진행률 + 상태 텍스트 의미 있음
- [ ] 로그창 자동 스크롤 & 복사 가능
- [ ] 에러 메시지는 사용자 언어로

## 3. 코드 품질 체크리스트
- [ ] MVVM 경계: code-behind 50줄 이하
- [ ] 모든 `async` 에 `CancellationToken`
- [ ] `ConfigureAwait(false)` 서비스 레이어에 일관 적용
- [ ] `IDisposable`/`using` 누락 없음
- [ ] Nullable 경고 0
- [ ] `dotnet format` 통과
- [ ] 단위 테스트: FfmpegProvisioner, sanitize, URL 검증

## 4. 스트레스 시나리오
1. 동일 URL 20회 순차 변환 → 캐시 재사용 확인
2. 10개 URL 큐 + 임의 취소/재시작
3. `taskkill` → 임시 파일 자동 정리
4. 저속 네트워크 타임아웃·재시도
5. 변환 중 FFmpeg 강제 종료 → 복구

## 5. 자동화 vs 수동
- **자동**: 단위 테스트(xUnit), FlaUI UI 테스트, ffprobe 산출물 검증, dotnet-counters
- **수동**: DPI/접근성/현지화/지역 제한

## 6. 합격 기준
- MP3: `codec_name=mp3`, `bit_rate ≥ 128000`, duration ±1초
- MP4: 비디오=h264/vp9→remux, 오디오=aac/opus, 재생 가능
- CRC 손상 없음
- 예외는 Logger + UI 사용자 메시지
- 24h 스트레스 크래시 0
