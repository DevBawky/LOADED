# LOADED Game Rules

## Document Information

- Status: Draft
- Last Updated: 2026-07-14
- Related Systems: Turn, Bullet Deck, Magazine, Enemy

## Definitions

- Turn: 플레이어 행동과 그에 따른 처리 및 적 행동이 끝나는 단위.
- Bullet Deck: 전투에서 사용할 탄환 전체 집합.
- Draw Pile: 다음 장전 후보가 있는 더미.
- Discard Pile: 사용을 마친 탄환이 이동하는 더미.
- Magazine: 장전된 탄환의 순서가 유지되는 컨테이너.
- Loaded Bullet: Magazine에 배치된 런타임 탄환.
- Direction: 플레이어가 바라보고 발사하는 방향.
- Lane: 플레이어와 적이 배치되는 일자형 전장.

## Turn Sequence

1. 플레이어 입력
2. 플레이어 행동 처리
3. 탄환 효과 처리
4. 충돌 및 상태 효과 처리
5. 사망 판정
6. 적 행동 처리
7. 추가 사망 판정
8. 승리 및 패배 판정
9. 턴 종료

## Player Actions

### Move

- 이동 가능 조건: TBD
- 이동 불가능 조건: TBD
- 적과 같은 칸 처리: TBD
- 맵 끝 처리: TBD

### Turn

- 방향 변경 규칙: TBD
- 방향 변경이 소비하는 턴: TBD
- 방향과 발사 방향 관계: 현재 방향으로 발사하는 것을 기본안으로 두되 확정 전에는 TBD

### Load

- 덱에서 무작위 탄환 한 장 장전
- 탄창이 가득 찬 경우: TBD
- 덱이 빈 경우 버린 더미 재셔플
- 덱과 버린 더미가 모두 빈 경우: TBD
- 같은 탄환은 서로 다른 런타임 인스턴스로 처리
- 장전 순서는 발사 전까지 고정

### Fire

- 탄창이 빈 경우: TBD
- 앞에서부터 순서대로 탄환 처리
- 처리된 탄환은 버린 더미로 이동
- 적 사망 도중 다음 탄환의 타깃 처리: TBD
- 플레이어와 적의 동시 사망 처리: TBD

## Deck Rules

셔플 방식, 고정 시드, 전투 간 덱 유지 규칙은 `TBD`이다.

## Magazine Rules

기본 크기와 크기 변경 가능 여부는 `TBD`이다.

## Bullet Resolution Rules

효과 실행, 판정, 연출을 분리하고 각 결과를 순서대로 확정한다.

## Enemy Rules

적은 플레이어 행동 처리 후 한 번 행동한다. 행동 예고 변경 시점을 추가로 결정한다.

## Collision Rules

적과 적, 적과 플레이어, 맵 끝 충돌 규칙은 `TBD`이다.

## Status Effect Rules

적용 시점, 지속 턴, 중첩 규칙은 `TBD`이다.

## Encounter Rules

적 구성과 전투 시작 배치는 `TBD`이다.

## Reward Rules

전투 후 탄환 획득, 제거, 복제, 강화 중 제공 범위는 `TBD`이다.

## Victory Conditions

현재 전투의 필수 적을 모두 처치하는 안을 검토하며 최종 규칙은 `TBD`이다.

## Defeat Conditions

플레이어 체력이 0 이하가 되는 안을 검토하며 동시 사망 규칙은 `TBD`이다.

## Score Rules

`TBD`

## Daily Seed Rules

`TBD`

## Exception Rules

결정되지 않은 규칙은 `TBD`로 표시하고 임의로 확정하지 않는다. 예외를 추가할 때 재현 절차와 우선순위를 함께 기록한다.
