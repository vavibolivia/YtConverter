# Phase 1 — 수정 내역
_적용: 2026-04-18 / 엔지니어_

Phase 1 리포트의 P0/P1 이슈를 엔지니어가 수정. 커밋: TBD (이 파일과 함께 push)

## 수정된 이슈
| ID | 심각도 | 문제 | 수정 |
| --- | --- | --- | --- |
| **B-01** | High | 동시 작업 수 변경 시 `_slots` 교체 → SemaphoreFullException | RunJobAsync 가 로컬 참조로 획득·Release. 실행 중이면 교체 지연, 유휴 시 재설정 |
| **B-02** | High | URL 전체 hash → `&t=`, `&list=` 다르면 같은 영상도 별도 work | `video.Id.Value` 로 hash — 같은 videoId+format 은 work 공유 |
| **B-03** | High | 읽기전용 폴더에서 UnauthorizedAccessException 미매핑 | `MapException` 에 케이스 추가, UI에 "저장 폴더에 쓰기 권한이 없습니다" |
| **B-05** | Medium | Failed/Canceled 도 자동 재개 — 영구 실패 URL 반복 시도 | `Status == Idle` 만 자동 재개. Failed/Canceled 는 사용자가 수동 트리거 |
| **B-07** | Medium | `queue.json.tmp` 파편 남을 수 있음 | `Load()` 시작부에서 `.tmp` 선제 삭제 |
| **B-10** | High | 사용자 폴더에 `.stream-*.tmp`, `.part` 고아 파일 (210MB) | `DownloadService.CleanupStaleArtifacts` — 앱 기동 시 1h+ 방치된 `*.part`, `*.stream-*.tmp`, 0바이트 파일 자동 정리 |
| **B-12** | High | `PersistQueue` 가 비-UI 스레드에서 ObservableCollection 열거 → InvalidOperationException | UI Dispatcher 에서 스냅샷 생성 후 Task.Run 으로 저장 |

## 유지 (다음 Phase 검토)
- B-04 (Med) 1KB 임계로 재사용 판정 — ffprobe 검증 고려
- B-06 (Med) Clipboard 정규식 느슨 — watch/youtu.be/shorts/embed 만 허용
- B-08 (Med) placeholder race — GUID suffix 항상 or coalesce
- B-09 (Low) FFmpeg stderr grep 필터
- B-11 (Med) FFmpeg stream 순서 가정
- B-13 (Low) LocalizationService 저장 실패 조용한 스왈로우
- B-14 (Low) `{0:P0}` culture — `Loc.Language` 기반
- B-15 (Low) JSON 쓰기 디바운스
