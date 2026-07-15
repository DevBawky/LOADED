# LOADED Development Rules

## Commit Convention

- `feat:` 새로운 기능
- `fix:` 버그 수정
- `refactor:` 구조 개선
- `balance:` 밸런스 변경
- `docs:` 문서 변경
- `test:` 테스트 추가 또는 수정
- `build:` 빌드와 배포 변경
- `chore:` 기타 작업

## Commit Examples

```text
feat: implement shuffled bullet deck
balance: reduce base magazine size to four
test: add deterministic deck shuffle tests
docs: record bullet deck AI prompt
```

## Documentation Rules

- AI 사용 후 `Docs/AI/AI_USAGE_LOG.md` 갱신
- 중요한 프롬프트는 `Docs/AI/Prompts/`에 별도 기록
- 콘텐츠 변경은 `Docs/Game/CONTENT_SPEC.md` 갱신
- 밸런스 수치 변경은 `Docs/Game/BALANCE_LOG.md` 갱신
- 외부 에셋 추가 시 `Docs/References/THIRD_PARTY_ASSETS.md` 갱신
- 외부 라이브러리 추가 시 `Docs/References/OPEN_SOURCE_LICENSES.md` 갱신

## Code Rules

- 게임 로직과 UI 로직을 분리한다.
- 게임 판정과 연출을 분리한다.
- 핵심 로직은 가능한 한 `MonoBehaviour` 없이 테스트할 수 있게 한다.
- 공개 필드 남용을 피한다.
- null 상태를 방어한다.
- 코드를 생성할 때 테스트 가능성을 고려한다.
- 코드 주석에 이모지를 사용하지 않는다.

## 작성 지침

기능 변경과 같은 커밋에서 관련 문서를 함께 갱신한다. 미확정 정보는 추측하지 않고 `TBD` 또는 `준비 중`으로 표시한다.
