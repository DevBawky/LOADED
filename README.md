## github Commit ConventionPermalink

|Type	|설명|
|------|---|
|Feat	|새로운 기능 추가|
|Fix|	버그 수정 또는 typo|
|Refactor	|리팩토링|
|Design|	CSS 등 사용자 UI 디자인 변경|
|Comment|	필요한 주석 추가 및 변경|
|Style	|코드 포맷팅, 세미콜론 누락, 코드 변경이 없는 경우|
|Test	|테스트(테스트 코드 추가, 수정, 삭제, 비즈니스 로직에 변경이 없는 경우)|
|Chore|	위에 걸리지 않는 기타 변경사항(빌드 스크립트 수정, assets image, 패키지 매니저 등)|
|Init	|프로젝트 초기 생성|
|Rename|	파일 혹은 폴더명 수정하거나 옮기는 경우|
|Remove|	파일을 삭제하는 작업만 수행하는 경우|
|Add|	코드나 테스트, 예제, 문서등의 추가 생성이 있는경우|
|Improve|	향상이 있는 경우. 호환성, 검증 기능, 접근성 등이 될수 있습니다.|
|Move|	코드의 이동이 있는경우|
|Updated	|계정이나 버전 업데이트가 있을 때 사용. 주로 코드보다는 문서나, 리소스, 라이브러리등에 사용합니다.|

# LOADED

무작위로 장전되는 탄환의 순서와 발사 타이밍을 관리하는 턴제 탄창 빌딩 로그라이크.

## Play

- Web Build: 준비 중
- Gameplay Video: 준비 중

## Core Concept

플레이어는 이동, 방향 변경, 무작위 장전, 전체 발사 중 하나를 선택한다.
탄환은 보유 덱에서 무작위로 장전되며 순서를 변경할 수 없다.
플레이어는 위험을 감수하고 더 장전할지, 현재 탄창을 발사할지 결정한다.

## Player Actions

1. Move
2. Turn
3. Load
4. Fire

## Controls

PC와 모바일의 실제 입력 키 및 버튼 배치는 구현 후 기록한다.

## Project Goals

- 10초 안에 이해되는 플레이
- PC와 모바일에서 동일한 조작
- 짧고 반복 가능한 로그라이크 세션
- 최소한의 그래픽 리소스
- 일일 도전 및 랭킹 확장 가능성

## How to Run

- Unity Version: 6000.3.15f1
- Start Scene: 미정. 현재 후보는 `Assets/Scenes/SampleScene.unity`
- Web Build URL: 준비 중

Unity Hub에서 프로젝트를 Unity 6000.3.15f1로 열고, 시작 Scene이 확정되면 이 절을 갱신한다.

## Repository Structure

- `Assets/`: Unity 게임 코드와 리소스
- `Docs/Game/`: 게임 설계 문서
- `Docs/Development/`: 일정, 테스트, 이슈 기록
- `Docs/AI/`: AI 활용 및 프롬프트 기록
- `Docs/Submission/`: NAN 2026 제출 자료 초안
- `Docs/References/`: 외부 에셋과 오픈소스 출처
- `Tools/BalanceSimulation/`: 밸런스 분석 도구

## Development Environment

- Engine: Unity
- Editor Version: 6000.3.15f1
- Target Platform: PC Web, Mobile Web

## Documentation

- [Game Overview](Docs/Game/GAME_OVERVIEW.md)
- [Game Rules](Docs/Game/GAME_RULES.md)
- [Content Specification](Docs/Game/CONTENT_SPEC.md)
- [AI Usage Overview](Docs/AI/AI_USAGE_OVERVIEW.md)
- [AI Usage Log](Docs/AI/AI_USAGE_LOG.md)
- [Submission Checklist](Docs/Submission/SUBMISSION_CHECKLIST.md)

## AI Usage

AI는 시스템 설계, 코드 구현 보조, 테스트 케이스 도출, 밸런스 시뮬레이션, 문서화에 활용한다.

AI 출력은 그대로 신뢰하지 않고 코드 리뷰, 테스트, 플레이 테스트를 통해 검증한다.

자세한 내용은 [AI Usage Overview](Docs/AI/AI_USAGE_OVERVIEW.md)를 참고한다.

## Third-Party Assets

외부 에셋과 오픈소스 사용 내역은 아래 문서에서 관리한다.

- [Third-Party Assets](Docs/References/THIRD_PARTY_ASSETS.md)
- [Open Source Licenses](Docs/References/OPEN_SOURCE_LICENSES.md)
