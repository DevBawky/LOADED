# LOADED Test Plan

## Test Goals

핵심 규칙의 결정론, 탄환 보존, 턴 처리 순서, Web 및 모바일 입력 안정성을 검증한다.

## Test Environments

| 환경 | 브라우저 또는 플랫폼 | 상태 |
|---|---|---|
| PC Web | Chrome | Pending |
| PC Web | Edge | Pending |
| Mobile Web | Android Chrome | Pending |
| Mobile Web | iOS Safari | Pending |

## EditMode Tests

- 덱 셔플
- 고정 시드 재현성
- 버린 더미 재셔플
- 탄환 유실 검사
- 탄환 중복 생성 검사
- 탄환 효과 계산
- 턴 처리 순서

## PlayMode Tests

- 이동
- 방향 변경
- 장전
- 전체 발사
- 적 행동
- 승리
- 패배
- 보스 페이즈 전환
- UI 입력 차단

## Manual Test Cases

| ID | 기능 | 사전 조건 | 테스트 절차 | 예상 결과 | 상태 |
|---|---|---|---|---|---|
| MAN-000 | 작성 예시 | 테스트 시작 상태 | 번호를 붙여 재현 절차 기록 | 관찰 가능한 결과 | Pending |

## Regression Checklist

- [ ] 수정한 기능의 기존 성공 경로
- [ ] 수정한 기능의 실패 및 경계 경로
- [ ] 고정 시드 결과
- [ ] 저장 및 불러오기

## Web Build Checklist

- [ ] 빌드 로딩
- [ ] 브라우저 새로고침
- [ ] 포커스 손실과 복귀
- [ ] 로컬 저장 실패 처리

## Mobile Input Checklist

- [ ] 터치 영역 크기
- [ ] 중복 터치 차단
- [ ] 화면 회전 또는 비율 변경
- [ ] 텍스트 가독성
