# P003 Bullet Deck

## Metadata

- Status: Initial
- Related AI Usage ID: 미사용

## Initial Prompt

```text
LOADED의 탄환 덱 시스템을 설계하고 구현한다.

필수 기능:
- 전투 시작 시 보유 탄환 덱 셔플
- Draw Pile 및 Discard Pile 관리
- 탄환 한 장을 뽑아 탄창에 추가
- Draw Pile이 비면 Discard Pile 재셔플
- 고정 시드를 사용하는 테스트 지원
- 탄환 유실 및 중복 생성 방지
- 빈 덱과 null 데이터 처리
- 동일한 탄환 데이터가 여러 장 존재하는 구조 지원

제약 조건:
- 런타임 탄환 인스턴스와 원본 탄환 데이터를 구분한다.
- UnityEngine.Random 전역 상태에 직접 의존하지 않는다.
- 덱 로직은 MonoBehaviour 없이 테스트 가능해야 한다.
- UI와 덱 로직을 분리한다.
- 재셔플 규칙을 명확하게 정의한다.

먼저 클래스 구조와 테스트 계획을 작성하라. 승인되지 않은 추가 기능은 구현하지 마라.
```

## Usage Notes

- Date:
- Tool and Model:
- Output Summary:
- Validation:
