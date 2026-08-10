> [!WARNING]
> 이 저장소는 `NAN 2026` 및 `OpenAI GAME BUILDERS SEOUL` 출품작의 심사 목적으로 공개되어 있습니다.
> 오픈소스 소프트웨어가 아니며, 코드와 에셋의 복제·수정·재배포·상업적 이용 및 다른 프로젝트에서의 사용을 금지합니다.
> Copyright © 2026 Bawky Studio. All rights reserved.

# LOADED

상점에서 탄환을 선택하고 강화해 덱을 구성한 뒤, 전투에서 무작위로 장전되는 탄환의 조합과 발사 타이밍으로 적의 예고 공격을 돌파하는 턴제 불릿 빌딩 로그라이크입니다.

## 플레이

- [LOADED WebGL 데모](https://devbawky.github.io/LOADED/)
- 플랫폼: PC WebGL

## 핵심 플레이

- 이동, 방향 전환, 대기, 장전, 발사 중 하나를 선택하면 턴이 진행됩니다.
- 보유한 탄환은 무작위로 실린더에 장전되며, 탄환 효과와 발사 순서의 조합이 전투 결과를 결정합니다.
- 전투 보상과 상점을 통해 탄환을 구매하고 강화해 덱을 발전시킵니다.
- 적의 다음 행동과 예상 피해를 확인하고 장전과 발사 사이의 위험을 판단합니다.

## 조작법

| 입력 | 동작 |
| --- | --- |
| `A` / `D` | 왼쪽 / 오른쪽 이동 |
| `W` 또는 마우스 가운데 버튼 | 방향 전환 |
| `S` | 대기 |
| `R` | 탄환 장전 |
| `Space` 또는 마우스 왼쪽 버튼 | 장전된 탄환 발사 |

게임 화면의 액션 버튼으로도 조작할 수 있습니다.

## 실행 방법

- Unity: `6000.3.21f1`
- 시작 씬: `Assets/Scenes/MainMenu.unity`
- 빌드 씬: `MainMenu` → `Stage 1` → `Ending`

1. Unity Hub에서 저장소 루트를 Unity `6000.3.21f1`로 엽니다.
2. 서드파티 에셋이 없는 환경에서는 `Assets/DamageNumbersPro`, `Assets/OldMovie`, `Assets/Package`에 필요한 에셋을 다시 임포트합니다.
3. `Assets/Scenes/MainMenu.unity`를 열고 Play Mode를 실행합니다.

`main` 브랜치의 `WebBuild/` 변경 사항은 GitHub Actions를 통해 GitHub Pages에 배포됩니다.

## 저장소 구조

- `Assets/Scripts/`: 게임플레이 및 UI 코드
- `Assets/Scenes/`: 메인 메뉴, 전투, 엔딩 씬
- `Assets/Tests/`: Unity 테스트
- `Assets/StreamingAssets/`: 튜토리얼 및 연출 영상
- `Docs/Art/`: 아트 및 UI 기록
- `Docs/Dev/`: 날짜별 구현·수정 기록
- `Docs/Submission/`: 출품 및 AI 활용 문서
- `Tools/BalanceSimulation/`: 밸런스 분석 도구
- `WebBuild/`: 배포용 WebGL 빌드

## 문서

- [탄환 덱 생명주기](Docs/BulletDeckLifecycle.md)
- [첫 실행 가이드 구현 기록](Docs/Dev/0809_FirstRunGuide.md)
- [WebGL 저장 및 로딩 구현 기록](Docs/Dev/0809_WebGL_Save_And_Loading.md)
- [AI 활용 기술 문서](Docs/Submission/AI_USAGE_TECHNICAL_DOCUMENT.md)
- [개발 및 커밋 규칙](CONTRIBUTING.md)

## 기술 환경

- Unity `6000.3.21f1`
- Universal Render Pipeline 2D
- Unity Input System
- WebGL / GitHub Pages

## 라이선스

이 프로젝트는 평가 목적으로만 사용할 수 있습니다. 자세한 내용은 [LICENSE](LICENSE)를 확인하세요. 포함되거나 참조된 서드파티 패키지와 에셋에는 각각의 라이선스가 적용됩니다.
