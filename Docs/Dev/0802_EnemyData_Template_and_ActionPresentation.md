# EnemyData 기반 단일 적 템플릿 및 행동 연출

> 현재 적 생성 구조와 적 행동 UI 연출의 기준 문서다. 기존 프리팹별 생성 방식은 더 이상 사용하지 않는다.

## 기본 정보

- 적용일: 260802
- 관련 기능: EnemyData, EnemyController, WaveManager, BattleData, 행동 큐 UI, 행동 툴팁, 투척병 투사체
- Unity 버전: 6000.3.15f1

## 변경 목적

기존에는 적 종류마다 별도 프리팹이 필요했고 다음 참조가 서로 연결되어 있었다.

`BattleData → 적 프리팹 → EnemyController.enemyData → EnemyData.prefab`

이 구조는 적을 추가할 때 ScriptableObject와 프리팹을 모두 만들고 양쪽 참조를 맞춰야 했다. 현재 구조는 공용 템플릿 하나에 `EnemyData`를 런타임으로 주입한다.

`BattleData → EnemyData → WaveManager → Enemy.prefab → EnemyController.Initialize(EnemyData, ...)`

## 최종 구조

| 구분 | 이전 구조 | 현재 구조 |
|---|---|---|
| 적 프리팹 | 적 종류별 프리팹 | `Assets/Prefabs/Enemy/Enemy.prefab` 하나 |
| 웨이브 항목 | `EnemyController` 프리팹 | `EnemyData` SO |
| EnemyData의 프리팹 참조 | 보유 | 제거 |
| EnemyController의 데이터 | 프리팹에 고정 직렬화 | 생성 시 런타임 주입 |
| 외형 적용 | 적 프리팹의 SpriteRenderer 값에 의존 | `EnemyData.Avatar`를 적 루트에 자동 생성 |
| 새 적 추가 | SO와 완성형 적 프리팹 모두 필요 | 공용 적 템플릿은 유지하고 `EnemyData`에 Avatar 연결 |

## 적 생성 흐름

1. `StateManager`가 `BattleData.Waves`의 모든 항목에 유효한 `EnemyData`와 양수 수량이 있는지 검사한다.
2. `WaveManager`가 현재 웨이브의 `EnemyData`와 수량을 읽는다.
3. 모든 적은 `WaveManager.enemyPrefabTemplate`에 연결된 공용 `Enemy.prefab`으로 생성된다.
4. 생성 직후 `EnemyController.Initialize`에 다음 참조가 전달된다.

   - `EnemyData`
   - `BoardManager`
   - `PlayerMove`
   - `PlayerHealth`
   - `WaveManager`

5. `EnemyController`는 전달받은 데이터로 체력, 지원 충전 수, AI 타입, 행동 목록, 투척물, 공격 예고선과 처치 보상을 초기화한다.
6. `EnemyData.Avatar` 프리팹을 생성해 적 루트 오브젝트의 직접 자식으로 배치한다. 위치와 회전은 루트 기준 0으로 초기화하며 Avatar 프리팹의 로컬 스케일은 유지한다.
7. Avatar에서 `Animator`와 `SpriteRenderer`를 자동 탐색한다. Avatar가 없거나 Animator가 없으면 경고를 남기며, 공용 적의 체력·AI·행동 처리는 계속 동작한다.
8. 런타임 GameObject 이름은 `EnemyData.DisplayName`을 사용하고, 이름이 비어 있으면 SO 에셋 이름을 사용한다.

## 에셋 마이그레이션

- `Melee Enemy.prefab`, `Range Enemy.prefab`, `Thrower.prefab`, `Porter.prefab`을 제거했다.
- `Melee Enemy.prefab`의 공용 UI와 컴포넌트 구성을 `Enemy.prefab` 템플릿으로 이관했다.
- `EnemyData.prefab` 필드와 Inspector 완성형 적 프리팹 검증을 제거했다.
- `EnemyData.Sprite`를 `EnemyData.Avatar` GameObject 참조로 교체했다.
- Gunner에는 `Avatar_Gunner.prefab`, 근접 적(Thief)에는 `Avatar_Thief.prefab`을 연결했다.
- Stage 1 Battle 01~05의 웨이브 항목 42개를 기존 적 프리팹에서 대응하는 `EnemyData` SO로 변경했다.
- `Assets/Scenes/Stage 1.unity`의 `WaveManager`에 공용 `Enemy.prefab`을 `Enemy Prefab Template`로 연결했다.

## 새 적 추가 방법

1. `Assets > Create > Loaded > Enemy > Enemy`로 `EnemyData`를 만든다.
2. ID, 표시 이름, 설명과 Avatar 프리팹을 입력한다.
3. 최대 체력과 처치 보상을 설정한다.
4. `Behavior Type`을 `Melee`, `Gunner`, `Thrower`, `Porter` 중 하나로 지정한다.
5. 해당 타입에 필요한 `EnemyActionData`를 `Actions`에 연결한다.
6. 원거리 또는 지원 적은 필요한 Telegraph Material을 연결한다.
7. `BattleData > Waves > Enemies > Enemy Data`에 새 SO와 수량을 등록한다.

Avatar 프리팹에는 `Animator`가 있어야 하며 Base Layer에 이름이 정확히 `Idle`, `Attack`인 상태를 둔다. 별도 완성형 적 프리팹 생성이나 `EnemyController.enemyData` 수동 연결은 필요하지 않다.

## Avatar 생성 및 애니메이션

공용 `Enemy.prefab`에는 캐릭터별 SpriteRenderer가 없다. `EnemyController.Initialize`가 호출될 때 연결된 Avatar를 적 루트 바로 아래에 생성하고, 이후 모든 외형 및 애니메이션 처리는 생성된 Avatar를 기준으로 수행한다.

### 현재 Avatar 연결

| EnemyData | Behavior | Avatar |
|---|---|---|
| `Test Gunner` | `Gunner` | `Avatar_Gunner.prefab` |
| `Test Enemy` (Thief) | `Melee` | `Avatar_Thief.prefab` |

Thrower와 Porter는 전용 Avatar가 준비될 때까지 참조가 비어 있으며, Inspector에 경고가 표시된다.

### 재생 규칙

- 적 생성 직후: `Base Layer.Idle`을 0초부터 재생한다.
- 근접 적: 큐에 공격 타일을 등록할 때는 Idle을 유지하고, 실제 피해를 적용하기 직전에 `Attack`을 한 번 재생한다.
- 원거리 적: `RangedAttack` 타일이 행동 슬롯에 추가되는 순간 `Attack`을 재생한다.
- Attack 상태의 클립 길이만큼 기다린 뒤 `Idle`을 0초부터 다시 재생한다. Attack 클립이 Loop여도 코드가 Idle로 복귀시킨다.
- 공격 애니메이션이 겹쳐 요청되면 마지막 요청만 Idle 복귀 권한을 가져, 먼저 시작한 코루틴이 새 Attack을 중간에 덮어쓰지 않는다.

### 공격 판정과 회피 키프레임

일반 근접·총격 공격과 빅 베럴 산탄은 공격 클립의 Animation Event로 회피 시작과 피격 구간을 작성한다. 회피 시작 당시 플레이어가 공격 범위 안에 있었다가 피격 구간이 시작되기 전에 안전한 타일로 이동하면, 범위를 벗어난 첫 프레임에 회피 성공을 확정하고 연출을 즉시 실행한다. 확정된 회피는 같은 공격 안에서 다시 범위에 들어와도 번복하지 않는다. 피격 구간 시작까지 범위를 벗어나지 못했다면 그 첫 프레임에 공격을 한 번만 판정한다.

1. Avatar 프리팹의 Animator Controller를 열고 실제 공격 상태가 사용하는 Animation Clip을 선택한다. 일반 적은 `Base Layer.Attack`, 빅 베럴 산탄은 `Base Layer.Attack_2`를 사용한다.
2. `Window > Animation > Animation`에서 클립을 열고 타임라인의 이벤트 추가 버튼으로 공격 예비 동작이 시작되는 프레임에 `BeginAttackDodgeWindow`를 추가한다.
3. 무기나 타격 이펙트가 실제 대상에 닿기 시작하는 첫 프레임에 `BeginAttackActiveWindow`를 추가한다. 이 이벤트 직전까지가 회피 성공 범위다.
4. 무기나 이펙트가 대상을 완전히 통과한 프레임에 `EndAttackActiveWindow`를 추가한다.
5. Avatar 프리팹의 Animator GameObject에 `EnemyAttackAnimationEvents`를 `EnemyAnimationSfx`와 나란히 추가한다. 세 이벤트는 매개변수 없이 설정하며, 이 전용 Action Window 컴포넌트가 생성된 Avatar 인스턴스에서 이벤트를 `EnemyController`의 판정으로 전달한다.

`EnemyController`는 Avatar 생성 뒤 프리팹에 작성된 Action Window를 찾아 현재 적 인스턴스와 연결할 뿐, 컴포넌트를 런타임에 자동 추가하지 않는다. 따라서 새로운 Avatar 프리팹을 만들 때 Animator와 `EnemyAnimationSfx`, `EnemyAttackAnimationEvents`를 함께 작성해야 한다.

`BeginAttackDodgeWindow`가 없으면 클립 시작부터 피격 시작까지를 회피 범위로 사용한다. `BeginAttackActiveWindow`와 `EndAttackActiveWindow`가 올바른 순서로 한 쌍을 이루지 않거나 공격 애니메이션 자체가 없으면 Animation Event를 사용하지 않고 `Attack Dodge Window Duration`만큼 기다린 뒤 공격한다. 기본값은 0.2초이며 이 시간 안에 공격 범위를 벗어나는 즉시 회피를 확정한다. 일시정지 중에는 모든 회피 시간이 진행되지 않는다.

투척병은 발사 애니메이션이 아니라 투사체 도착 시점을 기준으로 한다. 도착하기 `Attack Dodge Window Duration`초 전부터 도착 순간까지 목표 타일을 벗어나는 첫 프레임에 회피가 확정되고 연출이 시작된다. 그보다 일찍 빠져나간 경우 공격은 빗나가지만 회피 강조 연출은 재생하지 않는다.

회피 성공 연출은 판정과 분리된 `CombatFeedbackController`가 담당한다. 원래 피격 예정 위치에 이동 방향으로 흐르는 청백색 `회피!` 텍스트를 띄우고, 플레이어 잔상, 청백색 화면 굴절, 주변 비네트, 짧은 슬로 모션, 약한 카메라 반동과 `SFX_Evade`를 함께 재생한다. 판정 직후 `Time.timeScale`이 낮아지므로 `ActorMotion`으로 진행 중이던 플레이어 이동도 함께 느려지고, 슬로 모션이 회복되면서 남은 이동을 완료한다. 연출이나 접근성 효과를 비활성화해도 이미 확정된 회피 결과는 달라지지 않는다.

## 행동 타일 툴팁

적 머리 위 `Image | Queue`에 생성된 각 행동 타일은 마우스 포인터 이벤트를 받는다. 타일 생성 시 `EnemyActionTooltipTrigger`가 자동으로 추가되므로 행동 아이콘 프리팹에 별도 컴포넌트를 연결할 필요가 없다.

호버 시 기존 메인 Canvas의 다음 오브젝트를 자동 탐색해 사용한다.

- `Panel | Action Tooltip`
- `Text | Action Name`
- `Text | Action Description`

툴팁은 포인터를 따라 이동하고 화면 경계 밖으로 나가지 않도록 위치가 보정된다. 툴팁 자신의 Graphic Raycast는 비활성화해 포인터 이탈 이벤트를 방해하지 않는다. 행동 타일이 비활성화되거나 제거되면 해당 툴팁도 즉시 닫힌다.

### 이름 결정 순서

1. `EnemyActionData.DisplayName`
2. 연결된 `EnemyAttackData.DisplayName`
3. `EnemyActionData` 에셋 이름

### 설명 결정 순서

1. `EnemyActionData.Description`
2. 연결된 `EnemyAttackData.Description`
3. 행동 타입별 기본 설명

공격 데이터에도 설명이 없으면 피해량과 사거리를 조합한 기본 설명을 표시한다.

## 행동 큐 등장 연출

`Image | Queue` 배경과 각각의 행동 타일은 즉시 나타나지 않는다. 생성된 UI에 `CanvasGroup`을 자동으로 추가하고 다음 연출을 실행한다.

- Alpha: 0에서 1
- Scale: 원래 크기의 75%에서 100%
- Easing: 끝으로 갈수록 느려지는 cubic ease-out
- 일시정지: `GamePauseController.IsPaused` 동안 연출 시간도 정지

연출이 끝날 때까지 해당 적의 행동 완료를 보류한다. 따라서 여러 적의 행동 처리와 큐 UI 연출이 겹쳐 순서가 깨지지 않는다.

준비된 행동 타일이 없는 적은 한 행동 안에서 `Image | Queue`를 생성하고 첫 행동 타일을 추가한다. 다음 행동부터 기존 사거리 및 목표 조건을 확인하며, 조건을 만족하면 공격 준비 상태로 전환하고 그 다음 행동에 공격한다. 빅 베럴도 각 폭탄·산탄 Queue를 시작할 때 Queue 생성과 첫 타일 등록을 같은 행동에서 처리한다.

준비된 행동 타일은 적 턴 전체나 공격 애니메이션 종료를 기다리지 않는다. 근접·총잡이는 실제 공격 판정 시점, 투척병은 투사체를 던지는 시점, 지게꾼은 지원 효과 적용 시점에 해당 타일을 즉시 제거하고 `Image | Queue` 전체를 비활성화한다. 빅 베럴의 산탄 판정도 같은 시점에 Queue 전체를 숨긴다.

### 관련 시간 값

| EnemyData 필드 | 역할 |
|---|---|
| `Queue Element Reveal Duration` | Queue 배경 또는 행동 타일 하나가 등장하는 시간 |
| `Queued Action Interval` | 준비된 공격 여러 개를 실제 실행할 때 공격 사이의 간격 |
| `Attack Dodge Window Duration` | 키프레임이 없는 공격의 지연 및 투척·폭탄의 회피 가능 시간 |

두 값은 서로 다른 용도다. Queue 등장 속도는 `Queue Element Reveal Duration`으로 조절한다. 기본값은 0.25초이며 0이면 기존처럼 즉시 표시된다.

## 투척병 투사체 연출

투척병은 준비 단계에서 고정한 목표 타일까지 포물선 궤적으로 투사체를 이동시킨 뒤 피해와 공격 이펙트를 적용한다.

### 표시 우선순위

1. `Thrown Projectile Prefab`이 있으면 해당 프리팹을 생성한다.
2. 프리팹이 없으면 런타임 `SpriteRenderer` 투사체를 자동 생성한다.
3. `Thrown Projectile Sprite`가 있으면 지정 Sprite를 사용한다.
4. Sprite도 비어 있으면 32×32 흰색 원형 Sprite를 런타임에 한 번 생성해 공유한다.

자동 생성 투사체는 적과 동일한 Sorting Layer를 사용하고 적보다 한 단계 높은 Sorting Order에 표시된다. 목표 도착 후 투사체를 제거한다.

투척이 시작된 뒤에는 `WaveManager`가 비행 중 공격의 정산을 소유한다. 투척병이 도중에 사망해도 투사체는 고정된 목표 타일까지 계속 이동하여 폭발하고, 그 시점의 타일 점유자에게 기존 피해와 상태 효과를 적용한다. 마지막 적이 투척병이라도 투사체 충돌 전에는 전투 완료를 확정하지 않는다. 회피 성공 시 무방비는 살아 있는 원래 투척병에게만 적용하며, 원래 투척병이 이미 사망했다면 같은 종류의 다른 적을 포함해 아무에게도 이전하지 않는다.

### 관련 EnemyData 필드

| 필드 | 역할 | 기본값 |
|---|---|---|
| `Thrown Projectile Prefab` | 선택적인 완성형 투사체 프리팹 | 없음 |
| `Thrown Projectile Sprite` | 프리팹이 없을 때 사용할 Sprite | 없음, 원형 자동 생성 |
| `Thrown Projectile Color` | 자동 생성 Sprite 색상 | 흰색 |
| `Thrown Projectile Size` | 자동 생성 투사체 월드 크기 | 0.35 |
| `Thrown Projectile Duration` | 출발점에서 목표까지 이동 시간 | 0.5초 |
| `Thrown Projectile Arc Height` | 포물선 정점의 추가 높이 | 2 |
| `Attack Dodge Window Duration` | 도착 직전 회피 판정 시간 | 0.2초 |

이동 위치는 선형 보간 위치에 `sin(progress × π) × Arc Height`를 더해 계산한다. 게임이 일시정지된 동안 투사체 이동 시간도 진행되지 않는다.

## 적 피격 및 사망 효과음

피격 및 사망 효과음은 적 종류별 SO가 아니라 공용 `Enemy.prefab > EnemyController > Audio`에서 한 번만 설정한다. 따라서 모든 EnemyData와 Avatar가 같은 효과음 목록을 공유한다. 각 묶음은 비어 있는 항목을 제외한 `Clips` 중 하나를 같은 확률로 선택하며, 독립적인 `Volume`, `Min Pitch`, `Max Pitch` 값을 사용한다.

| EnemyController 필드 | 재생 시점 |
|---|---|
| `Normal Hit Sfx` | 일반 플레이어 공격의 피해 또는 보호막 피해가 적용되고 적이 살아남았을 때 |
| `Critical Hit Sfx` | 치명타 플레이어 공격의 피해 또는 보호막 피해가 적용되고 적이 살아남았을 때 |
| `Death Sfx` | 직접 공격, 상태 피해 또는 충돌 피해로 체력이 0이 되었을 때 |

치명적인 일반/치명타 공격은 피격음과 사망음이 겹치지 않도록 `Death Sfx`만 재생한다. 빠른 연속 피격에서는 재생 중인 AudioSource를 덮어쓰지 않고 풀에서 별도 Source를 사용하므로 각 클립의 무작위 피치가 끝까지 유지된다. 사망음은 적 GameObject 제거 전에 독립 오디오 오브젝트로 분리하여 적이 즉시 사라져도 클립이 끊기지 않으며, 재생 완료 후 자동 제거된다.

`Sfx Audio Source`는 출력 설정의 기준이다. 비워 두면 2D AudioSource가 런타임에 자동 생성된다. Audio Mixer 또는 3D 공간 음향이 필요하면 공용 프리팹에 AudioSource를 추가해 Output, Spatial Blend, Rolloff와 Distance를 설정한 뒤 이 필드에 연결한다. 피격음 풀과 독립 사망음도 해당 설정을 복사한다.

### 적용 방법

1. `Assets/Prefabs/Enemy/Enemy.prefab`을 선택한다.
2. `EnemyController > Audio`에서 `Normal Hit Sfx`, `Critical Hit Sfx`, `Death Sfx`를 펼친다.
3. 각 `Clips`의 Size를 늘리고 상황별 AudioClip을 등록한다. 목록이 비어 있으면 해당 상황에서는 소리를 재생하지 않는다.
4. 각 묶음의 Volume과 Min/Max Pitch를 조절한다. 두 피치 값을 같게 두면 고정 피치로 재생된다.
5. `Sfx Audio Source`에서 공통 Audio Mixer와 공간 음향 설정을 조절한다.

기존 `Test Enemy`와 `Porter` EnemyData에 임시로 들어 있던 일반 피격음 4개는 공용 Enemy 프리팹의 `Normal Hit Sfx`로 이전했으며 SO의 효과음 직렬화 데이터는 제거했다.

## 변경 파일

- `Assets/Scripts/Enemy/EnemyData.cs`
- `Assets/Scripts/Enemy/Editor/EnemyDataEditor.cs`
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/Enemy/EnemyActionData.cs`
- `Assets/Scripts/Enemy/EnemyActionQueueUI.cs`
- `Assets/Scripts/Enemy/EnemyActionTooltipTrigger.cs`
- `Assets/Scripts/Manager/WaveManager.cs`
- `Assets/Scripts/Manager/StateManager.cs`
- `Assets/Prefabs/Enemy/Enemy.prefab`
- `Assets/Prefabs/Enemy/Enemy_Avatar/Gunner/Avatar_Gunner.prefab`
- `Assets/Prefabs/Enemy/Enemy_Avatar/Gunner/Animation/Avatar_Gunner.controller`
- `Assets/Prefabs/Enemy/Enemy_Avatar/Thief/Avatar_Thief.prefab`
- `Assets/Scripts/Enemy/Enemy SO/Test Gunner.asset`
- `Assets/Scripts/Enemy/Enemy SO/Test Enemy.asset`
- `Assets/Scenes/Stage 1.unity`
- `Assets/Scripts/Manager/Battle SO/Stage 1 Battle 01.asset`~`05.asset`

## 검증 결과

- `dotnet build LOADED.slnx --no-restore`: 경고 0, 오류 0
- BattleData 웨이브의 EnemyData 참조 42개 검사: 누락 0
- `Assets/Prefabs/Enemy`의 적 프리팹: `Enemy.prefab` 하나
- Unity Editor 최근 컴파일 로그: C# 컴파일 오류 0
- 행동 아이콘 프리팹의 Raycast Target과 적 월드 Canvas의 GraphicRaycaster 연결 확인
- 기존 `Panel | Action Tooltip`, 행동 이름 Text, 행동 설명 Text 오브젝트 탐색 확인

## Play Mode 확인 항목

1. BattleData 웨이브에서 서로 다른 EnemyData가 모두 공용 프리팹으로 생성되는지 확인한다.
2. 생성된 적 루트의 직접 자식으로 EnemyData에 연결된 Avatar가 하나만 생성되고, 체력·행동 타입·이름도 데이터에 맞게 적용되는지 확인한다.
3. Queue 생성 턴에 Queue 배경이 설정 시간 동안 서서히 나타나는지 확인한다.
4. 행동 등록 턴에 새 행동 타일만 설정 시간 동안 서서히 나타나는지 확인한다.
5. 행동 타일에 마우스를 올렸을 때 이름과 설명이 표시되고 포인터 이동과 화면 경계에 맞춰 위치가 갱신되는지 확인한다.
6. 행동 타일이 제거될 때 열려 있던 툴팁도 닫히는지 확인한다.
7. 투척병 데이터에 투사체 프리팹과 Sprite가 모두 없을 때 원형 투사체가 궤적을 따라 이동하는지 확인한다.
8. 투척병에 커스텀 Sprite 또는 프리팹을 지정했을 때 기본 원형 대신 지정 외형이 사용되는지 확인한다.
9. 일시정지 중 Queue 연출과 투사체 이동이 멈추고 재개 후 이어지는지 확인한다.
10. Gunner가 공격 타일을 슬롯에 추가하는 순간 Attack을 재생하고, 클립 종료 후 Idle로 돌아가는지 확인한다.
11. Thief가 공격 타일을 등록할 때는 Idle을 유지하고, 실제 공격 시 Attack을 재생한 뒤 Idle로 돌아가는지 확인한다.
12. 일반 공격과 치명타 공격에서 각각 대응하는 Clips 목록만 사용하고, 연속 피격 시 소리가 중간에 끊기거나 피치가 변경되지 않는지 확인한다.
13. 치명적인 공격에서는 피격음 대신 사망음만 재생되고, 적 GameObject가 제거된 뒤에도 사망음이 끝까지 들리는지 확인한다.
14. 독 또는 충돌 피해로 사망했을 때도 사망음이 재생되는지 확인한다.

## 기존 문서와의 관계

다음 문서는 당시 구현 과정과 과거 설계를 보존하는 이력 문서다. 프리팹 연결과 BattleData 설정 방법이 이 문서와 충돌하면 현재 문서를 우선한다.

- `0717_Enemy_EnemyData.md`
- `0717_Enemy_EnemyController_WaveManager.md`
- `0727_EnemyAI.md`
