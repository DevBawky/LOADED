# P002 Turn System

## Metadata

- Status: Initial
- Related AI Usage ID: 미사용

## Initial Prompt

```text
LOADED의 턴 처리 시스템을 설계한다.

한 턴은 플레이어 입력, 플레이어 행동, 탄환 효과, 충돌과 상태 효과, 사망 판정, 적 행동, 추가 사망 판정, 승패 판정, 턴 종료 순서로 처리한다.

요구사항:
- 플레이어 행동은 Move, Turn, Load, Fire만 지원한다.
- 적은 플레이어 행동 이후 한 번 행동한다.
- 입력, 게임 판정, 애니메이션을 분리한다.
- 처리 중 사망이나 보스 페이즈 변경이 발생해도 순서를 보존한다.
- 애니메이션 중 중복 입력을 차단한다.
- 동일한 입력과 시드는 동일한 결과를 만들어야 한다.
- Web 플랫폼에서 안정적으로 동작해야 한다.

먼저 상태 전이, 데이터 흐름, 책임 분리 구조를 제안하라. 코드 작성 전 예상되는 엣지 케이스도 함께 작성하라. 미확정 규칙은 TBD로 표시하라.
```

## Usage Notes

- Date:
- Tool and Model:
- Output Summary:
- Validation:
