# 0820 SOLID 리팩터링

## 범위와 판단 기준

`Assets/Scripts/**/*.cs`의 런타임 스크립트 102개를 대상으로 클래스별
메서드 군, Unity 생명주기, 저장 경로, 이벤트 구독, UI/오디오 의존성을
감사했다. 파일 길이 자체보다 다음 책임이 한 객체에서 함께 변경되는지를
분리 기준으로 삼았다.

- 게임 규칙 계산과 Unity 표현
- 런타임 상태와 저장 직렬화
- 입력 해석과 행동 실행
- 씬별 음악 선택과 오디오 재생
- 절차적 생성과 맵 UI/선택
- authored data와 per-run mutable data

공개 API, `[SerializeField]` 이름/타입, ScriptableObject 및 세이브 스키마,
씬·프리팹 GUID는 변경하지 않았다.

## 변경된 책임 지도

| 기존 진입점 | 현재 책임 | 분리된 협력 객체 |
|---|---|---|
| `SoundManager` | 음원 재생, 소스/볼륨, 사용자 설정 | `SoundtrackDirector`, `UiButtonFeedbackInstaller`, `UiButtonAudioFeedback`, `UiButtonSpriteHoverScale` |
| `PlayerShoot` | Unity 생명주기, 입력 게이트, 공개 전투 진입점 | `PlayerShootInputReader`, `PlayerShotRangePreview`, `BulletShotFeedbackView`, `PlayerAttackDamageCalculator`, `FiringSequenceController`, `DamagePreviewController`, `BulletEffectUtility`, `BulletDynamicCombatRules` |
| `EnemyController` | 적 상태와 턴 행동 조정 | `EnemyRunStateSerializer`, `EnemyTelegraphPresenter`, `EnemySupportTargetSelector`, `EnemyFrontlineTurnPolicy` |
| `NodeMapSystem` | 맵 화면 상태와 노드 선택 조정 | `NodeMapGenerator`, `NodeMapSaveSystem`, `NodeMapModels` |
| `GameStatistics` | 현재 런 통계 집계 | `RunDataModels`, `RunSaveSystem` |
| `EventDefinition` | authored event content | `EventRunContext`, `EventSelector` |
| `EventSceneController` | 이벤트 씬 생명주기, 상호작용, 효과·저장·이동 조정 | `EventChoiceAvailabilityEvaluator`, `EventChoiceAvailabilityContext`, `EventChoiceTextFormatter`, `EventChoiceButtonPresenter`, `EventResultPresenter`, `EventRuntimeRules` |
| `RelicData` | authored relic content | `RelicInstance` |
| `FirstRunGuideController` | 튜토리얼 진행, 입력 잠금과 대상 선택 조정 | immutable `FirstRunGuideContent`, `FirstRunGuideHighlightPresenter` |
| `PlayerCylinderUI` | 실린더 표시, 드래그, 애니메이션 | `CylinderBulletEffectPolicy` |
| `ShopManager` | 구매 흐름, 오퍼 UI, 새로고침 연출 | `ShopOfferGenerator` |
| `CombatPresentation` | 총구·기본 명중 표현과 공개 연출 진입점 | `CombatImpactSignaturePresenter` |
| `StateManager` | 전투 생명주기, 정산 커밋과 씬 전환 순서 조정 | `CombatReportRuntime`, `BattleClearRewardCalculator`, `BattleClearSettlement` |

`PlayerShoot`의 발사 실행과 데미지 미리보기는 같은 효과 분류와 계산기를
공유한다. 따라서 미리보기만 별도 규칙을 복제해 실제 발사 결과와 달라지는
경로를 줄였다. `PlayerShoot`의 기존 이벤트와 공개 메서드는 facade에 남겨
호출자 호환성을 유지한다.

골드·체력·약실·스택·보유 탄환 구성에 따라 달라지는 피해 및 치명타 보너스와
사거리·상태이상·선행 명중에 따른 대상별 배율은 plain C#
`BulletDynamicCombatRules`가 계산한다. 실제 발사, 확정 피해 미리보기,
런타임 툴팁은 각자의 현재 상태를 불변 값 컨텍스트로 캡처해 같은 규칙에
전달한다. 임시 보너스 소비, RNG, 탄환 이동과 이벤트 순서는 계속
`PlayerShoot`과 `BulletInstance`의 기존 소유 경계에 남는다.

`DamagePreviewController`는 실제 객체를 바꾸지 않는 가상 보유 탄환 목록을,
plain C# `PlayerCombatPreviewResources`는 가상 골드·현재/최대 체력을 소유한다.
체력 비용과 확정 탄환 파괴를 순서대로 반영하고,
`RelicLethalDamagePreviewState`는 죽음 방지 충전만 복제해 실제 유물 상태나
이벤트를 소비하지 않는다. 런타임 툴팁은 대기열 전체를 시뮬레이션하는 예측값이
아니라 현재 탄환 상태의 설명이며, 발사 결과 오버레이가 순차 예측의 권위다.
확정 흡혈·최대 체력 증가·골드 획득도 명중별 가상 자원에 커밋한 뒤 후속 탄환
컨텍스트를 만들므로 플레이어 자원 기반 효과가 실제 객체 변경 없이 이어진다.
`CurrencyManager.AddMoneyFromWorld`는 권위 골드를 호출 시점에 즉시 커밋하고
비행 코인은 표시만 담당한다. 따라서 HUD·카메라·프리팹 활성 여부와 관계없이
후속 도금탄, 소비, 저장이 같은 골드 상태를 본다.

`CombatPresentation`은 기존 직렬화 필드와 `PlayImpact` 공개 진입점을
유지한다. 새 상황별 시그니처의 절차적 오브젝트 수명과 애니메이션은 plain
C# `CombatImpactSignaturePresenter`가 소유하고, facade가 캡처한 적 스냅샷과
표현 등급만 전달한다. 이 협력 객체는 게임 판정, 시간 배율, 저장 상태를
소유하지 않는다.

`FirstRunGuideController`는 가이드 진행 상태, PlayerPrefs 완료 키, 입력 잠금과
강조할 대상 선택을 계속 소유한다. plain C# `FirstRunGuideHighlightPresenter`는
선택된 하나 또는 두 UI 대상의 화면 범위를 합성하고, 가이드 Canvas 좌표로
변환해 패딩과 펄스 알파를 적용한다. 강조 대상이 없거나 비활성화된 경우에는
표시만 숨기며 가이드 진행이나 게임 입력 상태를 변경하지 않는다.

전투 보고서의 누적 피해, COUNT 경계, 체력 변화, 발사 수와 처치 성과는
plain C# `CombatReportRuntime`이 소유한다. `StateManager`가 전투 시작·복원,
이벤트 전달, 저장 캡처와 전투 종료를 조정하고, `BattleClearRewardCalculator`가
동일 스냅샷에서 메달과 보너스 골드를 결정한다. `GameStartUI`는 전달받은
정산 결과를 표시하고 확인 의도만 반환한다. 실제 골드 커밋은 UI 코루틴이
끝난 직후 `StateManager`가 수행하므로 UI가 없거나 구성되지 않아도 결과가
달라지지 않으며, `BattleClearSettlement`이 같은 보너스의 중복 커밋을 막는다.
버전 3의 `RunCombatReportSaveData` 필드와 저장 키는 변경하지 않았다.

`EventSceneController`는 매니저가 소유한 현재 자원과 선택 대상 상태를
`EventChoiceAvailabilityContext`로 캡처한다. 선택지 요구 조건, 대상 수와
조합, 성공·실패 효과의 비용 및 보상 공간 판정은 plain C#
`EventChoiceAvailabilityEvaluator`가 담당한다. 반복 횟수에 따른 효과 범위,
효과량, 성공 확률 계산은 `EventRuntimeRules`가 공유하며, 컨트롤러는 효과
적용, 세이브 체크포인트와 씬 전환 순서를 계속 조정한다. 선택지 행동명,
키워드, 보상명과 비활성 사유의 rich text 조합은 plain C#
`EventChoiceTextFormatter`가 담당하고 직렬화된 색상은 컨트롤러가 전달한다.
선택지 버튼의 표시·비활성화·리스너 교체는 `EventChoiceButtonPresenter`가,
결과 텍스트와 슬롯 릴의 생성·상호 배타 표시는 `EventResultPresenter`가
담당한다. 두 프레젠터는 표시 상태만 투영하며 선택 결과, 보상, 저장 상태는
변경하지 않는다.

## 추가 분할하지 않은 대형 클래스

다음 파일은 길지만 현재 감사 기준에서 하나의 표현 또는 도메인 책임 안에
있어 기계적인 분할을 하지 않았다.

- `CombatFeedbackController`: 전투 결과의 화면/카메라 피드백 표현
- `InventoryTooltipUI`: 인벤토리·상점 툴팁 표현과 위치 계산
- `RelicManager`: 유물 런타임 규칙과 유물 이벤트 조정

이 파일을 이후 변경할 때는 새로운 규칙이나 저장 책임을 UI에 추가하지
말고, 실제로 독립 변경되는 경계가 생길 때 협력 객체로 추출한다.

## 후속 리팩터링 후보

현재 검증을 깨지 않고 즉시 분리할 필요는 없지만 다음 경계는 기능 개발과
함께 단계적으로 추출할 가치가 있다.

- `EnemyController`: Porter의 지원 대상 선택과 Melee의 전열 우선순위
  결정은 각각 `EnemySupportTargetSelector`, `EnemyFrontlineTurnPolicy`로
  분리했다. 남은 Melee/Gunner/Thrower/BigBarrel 턴 결정을 행동 전략으로
  분리하려면 적 행동별 PlayMode 테스트를 먼저 추가한다. 현재 런타임의
  전열 제한은 Melee에만 적용되지만 `0727_EnemyAI.md`는 Gunner도 포함하므로,
  Gunner 적용 여부는 리팩터링이 아닌 별도 게임 규칙 변경으로 결정한다.
- `FirstRunGuideController`: 하이라이트 좌표와 펄스 표현은
  `FirstRunGuideHighlightPresenter`로 분리했다. 남은 런타임 UI 생성과 영상
  표현은 씬 오브젝트 이름 기반 연결을 테스트로 고정한 뒤 단계적으로 분리한다.
- `RelicManager`: 효과 타입별 이벤트 처리를 독립 handler로 분리. 현재의
  18개 유물 회귀 테스트를 효과별로 확장한 뒤 작은 묶음부터 이동한다.

`CombatFeedbackController`와 `InventoryTooltipUI`는 길지만 현재 각각 전투
피드백 표현과 툴팁 표현이라는 단일 변경 이유를 유지하므로 우선순위가 낮다.

## 검증 기록

- 생성된 모든 `.cs` 파일에 대응 `.meta`를 추가했다.
- IDE용 `Assembly-CSharp.csproj`에 신규 파일을 임시로 포함해 전체 런타임
  어셈블리를 컴파일했으며 경고 0개, 오류 0개를 확인했다.
- `PlayerAttackDamageCalculator`의 경계값, 올림, overflow, NaN 동작과
  오퍼 중복/용량, 실린더 효과 표시 정책에 대한 EditMode 테스트를 추가했다.
- 기존 테스트는 일반 런타임 폴더에서 Unity Test Framework가 발견하지
  못하고 있었다. GUID를 보존해 `Assets/Editor/Tests`로 이동하고 런타임
  내부 타입은 `InternalsVisibleTo("Assembly-CSharp-Editor")`로 제한해
  실제 테스트 어셈블리에서 실행되도록 수정했다.
- Unity 6000.3.21f1 EditMode Test Runner 결과: 총 55, 성공 55, 실패 0,
  건너뜀 0.
- 활성 빌드 씬 7개를 실제로 열어 씬 파일 유실과 Missing Script를 검사하는
  `SceneIntegrityTests`도 통과했다.
- 테스트 실행 중 `RelicEffectData`의 `[Serializable]` 누락 회귀를 발견해
  복원했다.

## Unity Editor 수동 회귀 체크리스트

1. 메인 메뉴, 노드 맵, 전투, 상점, 보물, 이벤트 씬을 순서대로 진입한다.
2. 씬마다 BGM 전환, 버튼 hover/click SFX, 볼륨 저장을 확인한다.
3. 이동/재장전/사격 입력 잠금과 턴 완료 이벤트가 한 번씩 발생하는지 본다.
4. 일반·치명타·광역·벽 충돌·조건부 효과의 미리보기와 실제 피해를 비교한다.
5. 탄환 파괴, 마지막 적 처치, 탄환 고갈이 같은 발사에서 겹칠 때 전투 종료
   우선순위를 확인한다.
6. 적 공격 예고, 보호막 표시, Big Barrel 예고 및 저장 후 복원을 확인한다.
7. 노드 맵 생성/선택을 저장하고 새 씬에서 이어하기가 같은 맵으로 복원되는지
   확인한다.
8. 데스크톱 새 게임/이어하기 및 WebGL 저장 분기를 각각 확인한다.
