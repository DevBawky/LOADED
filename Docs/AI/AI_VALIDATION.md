# LOADED AI Output Validation

각 AI 결과에 해당하는 항목을 확인하고 결과를 [AI Usage Log](AI_USAGE_LOG.md)에 기록한다.

## Code Validation

- [ ] 컴파일 성공 여부 확인
- [ ] Null 처리 확인
- [ ] 책임 분리 확인
- [ ] 불필요한 싱글턴과 전역 상태 확인
- [ ] Unity 생명주기 의존성 확인
- [ ] Web 플랫폼 호환성 확인
- [ ] 테스트 코드 작성 여부 확인

## Deterministic Validation

- [ ] 동일한 시드에서 동일한 덱 순서가 나오는지 확인
- [ ] 실행 순서에 따라 결과가 달라지지 않는지 확인
- [ ] 발사 결과를 데이터 형태로 재현할 수 있는지 확인

## Design Validation

- [ ] 핵심 행동 4개를 벗어나지 않는지 확인
- [ ] 새로운 기능이 핵심 선택을 흐리지 않는지 확인
- [ ] 10초 안에 규칙이 전달되는지 확인
- [ ] 특정 탄환이 필수가 되지 않는지 확인

## Test Evidence Template

- AI Usage ID:
- Date:
- Reviewer:
- Build or Commit:
- Validation Method:
- Expected Result:
- Actual Result:
- Pass or Fail:
- Follow-up:

예시: `AI-003`, 고정 시드 100회 실행 결과가 모두 동일하면 Pass로 기록한다.
