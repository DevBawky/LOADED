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
