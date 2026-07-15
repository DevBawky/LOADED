# LOADED Game Overview

## Document Information

- Status: Draft
- Last Updated: 2026-07-14
- Owner: TBD

## One-Line Pitch

무작위로 장전되는 탄환의 순서와 발사 타이밍을 관리하는 턴제 탄창 빌딩 로그라이크.

## Core Fantasy

예측할 수 없는 탄환을 받아들이고, 불완전한 탄창으로 최선의 발사 타이밍을 만드는 경험.

## Design Goals

1. 플레이 영상 10초 안에 핵심 규칙 전달
2. 한 번의 입력으로 명확한 결과 제공
3. 더 장전할지 발사할지 반복해서 고민하게 할 것
4. 최소한의 그래픽으로 강한 연쇄 피드백 제공
5. 모바일과 PC에서 같은 조작 경험 제공
6. 짧은 세션과 높은 재도전성 확보

## Non-Goals

- 복잡한 캐릭터 조작
- 넓은 맵 탐험
- 실시간 액션 전투
- 대규모 멀티플레이
- 많은 캐릭터 애니메이션
- 긴 스토리 중심 진행

## Core Loop

장전 -> 위험 판단 -> 이동 또는 방향 변경 -> 전체 발사 -> 적 처치 -> 보상 선택 -> 탄환 덱 수정 -> 다음 전투

## Player Actions

### Move

일자형 전장에서 위치를 한 칸 변경한다. 세부 제한은 [Game Rules](GAME_RULES.md)에 기록한다.

### Turn

플레이어의 방향을 변경한다. 턴 소비 여부는 `TBD`이다.

### Load

보유 탄환 덱에서 무작위 탄환을 뽑아 탄창 끝에 장전한다.

### Fire

탄창의 모든 탄환을 앞에서부터 순서대로 발동한다.

## Combat Overview

플레이어 행동 뒤 적이 행동하는 일자형 턴제 전투다. 적의 다음 행동을 예고해 위치와 발사 타이밍 판단을 지원한다.

## Deckbuilding Overview

전투 후 탄환 획득, 제거, 복제, 강화를 통해 보유 덱을 수정한다. 세부 보상 규칙은 `TBD`이다.

## Run Structure

전투, 보상, 덱 수정의 반복 구조를 사용한다. 전투 수와 보스 배치는 `TBD`이다.

## Score and Ranking

일일 시드 또는 점수 랭킹을 향후 확장 대상으로 둔다. 산식과 동점 규칙은 `TBD`이다.

## Target Platforms

- PC Web
- Mobile Web

## Target Session Length

10분에서 15분

## Scope

### Prototype

핵심 행동 4개, 기본 적, 승리 및 패배 판정을 검증한다.

### Submission Build

탄환 덱빌딩, 콘텐츠, 모바일 UI, Web 배포와 제출 자료를 포함한다.

### Future Expansion

일일 시드, 랭킹, 추가 탄환, 적, 보스를 검토한다.

## Reference Games

참고 작품과 참고한 요소만 기록하며, 직접적인 복제는 피한다.

| 작품 | 참고 요소 | 차별화 방향 |
|---|---|---|
| Shogun Showdown | 일자형 턴제 전투, 예고된 적 행동 | 무작위 탄창과 전체 발사 |
| Vampire Crawlers | 순서 기반 조합과 빌드 성장 | 탄환 장전 순서와 위험 관리 |
