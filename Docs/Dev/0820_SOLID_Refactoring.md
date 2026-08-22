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
| `PlayerShoot` | Unity 생명주기, 입력 게이트, 공개 전투 진입점 | `PlayerShootInputReader`, `PlayerShotRangePreview`, `BulletShotFeedbackView`, `PlayerAttackDamageCalculator`, `FiringSequenceController`, `DamagePreviewController`, `BulletEffectUtility` |
| `EnemyController` | 적 상태와 턴 행동 조정 | `EnemyRunStateSerializer`, `EnemyTelegraphPresenter` |
| `NodeMapSystem` | 맵 화면 상태와 노드 선택 조정 | `NodeMapGenerator`, `NodeMapSaveSystem`, `NodeMapModels` |
| `GameStatistics` | 현재 런 통계 집계 | `RunDataModels`, `RunSaveSystem` |
| `EventDefinition` | authored event content | `EventRunContext`, `EventSelector` |
| `RelicData` | authored relic content | `RelicInstance` |
| `FirstRunGuideController` | 튜토리얼 진행과 UI 조정 | immutable `FirstRunGuideContent` |
| `PlayerCylinderUI` | 실린더 표시, 드래그, 애니메이션 | `CylinderBulletEffectPolicy` |
| `ShopManager` | 구매 흐름, 오퍼 UI, 새로고침 연출 | `ShopOfferGenerator` |
| `CombatPresentation` | 총구·기본 명중 표현과 공개 연출 진입점 | `CombatImpactSignaturePresenter` |

`PlayerShoot`의 발사 실행과 데미지 미리보기는 같은 효과 분류와 계산기를
공유한다. 따라서 미리보기만 별도 규칙을 복제해 실제 발사 결과와 달라지는
경로를 줄였다. `PlayerShoot`의 기존 이벤트와 공개 메서드는 facade에 남겨
호출자 호환성을 유지한다.

`CombatPresentation`은 기존 직렬화 필드와 `PlayImpact` 공개 진입점을
유지한다. 새 상황별 시그니처의 절차적 오브젝트 수명과 애니메이션은 plain
C# `CombatImpactSignaturePresenter`가 소유하고, facade가 캡처한 적 스냅샷과
표현 등급만 전달한다. 이 협력 객체는 게임 판정, 시간 배율, 저장 상태를
소유하지 않는다.

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

- `EnemyController`: Melee/Gunner/Thrower/BigBarrel/Porter 턴 결정을 행동
  전략으로 분리. 적 행동별 PlayMode 테스트를 먼저 추가한다.
- `FirstRunGuideController`: 런타임 UI 생성과 영상/하이라이트 표현을
  presenter로 분리. 씬 오브젝트 이름 기반 연결을 테스트로 고정한 뒤 진행한다.
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
