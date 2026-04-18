# Phase 2 — 수정 내역
_적용: 2026-04-18 / 엔지니어_

## 수정
| ID | 심각도 | 문제 | 수정 |
| --- | --- | --- | --- |
| **I-03** | High | HttpClient Timeout=Infinite, per-request 워치독 없음 → 네트워크 단절 시 무한 매달림 | 청크별 `CancellationTokenSource` 60초 타이머 + exponential backoff 재시도 (2s/4s/8s, 최대 3회). 사용자 취소와 타임아웃 구분 |
| **I-01** | Medium | Failed 작업을 단독으로 재시도할 UI 없음 (B-05 fix 부수효과) | `JobViewModel.RetryCommand` + 🔁 버튼 추가, MainViewModel 의 `AttachJob` 에서 `RequestRetry = RunJobAsync` 연결 |
| **I-02** | Medium | `CleanupStaleArtifacts` 가 0바이트 파일을 무조건 삭제 → 사용자 데이터 손실 위험 | 0바이트 삭제에도 age > 1h 조건 추가 |
| **I-08** | Medium | StatusGlyph 이모지에 접근성 라벨 없음 → 스크린리더가 "Unicode 9203" | `AutomationProperties.Name="{Binding StatusText}"` + `LiveSetting="Polite"` |
| **I-12** | Low | MinHeight=620 DPI 200% 에선 1080p 에서 세로 짤림 | MinHeight=480, MinWidth=640 |

## 회귀 검증 메모
Phase 2 PL 검증: B-01/02/03/07/10/12 = PASS (fix verified), B-05 = PARTIAL (Idle+Failed 혼재 시 StartAll 에서 Failed도 시작됨 — 의도한 동작이나 I-01 로 보완됨)

## 미해결 / 다음 Phase
- I-04 `.tmp` 삭제 실패 silently swallowed (Low) — 로깅만 보강하면 됨
- I-05 work/ 루트 오래된 dir 축적 — 7일 purge 로직 필요
- I-06 Playlist 전개 UX — 기능 요구
- I-07 디스크 공간 precheck — `DriveInfo`
- I-09 Queue schema version — SchemaVersion 필드
- I-10 Drop FileDrop/ANSI URL 미지원
- I-11 AppLogger DetachSink — 테스트 하네스 누수
