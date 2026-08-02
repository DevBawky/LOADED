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
| 외형 적용 | 프리팹 SpriteRenderer 값에 의존 | `EnemyData.Sprite` 자동 적용 |
| 새 적 추가 | SO와 전용 프리팹 모두 필요 | `EnemyData` SO만 추가 |

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
6. `EnemyData.Sprite`를 공용 프리팹의 `SpriteRenderer`에 적용한다. Sprite가 비어 있으면 기존 적의 스프라이트를 재사용하지 않고 빈 Sprite를 적용한다.
7. 런타임 GameObject 이름은 `EnemyData.DisplayName`을 사용하고, 이름이 비어 있으면 SO 에셋 이름을 사용한다.

## 에셋 마이그레이션

- `Melee Enemy.prefab`, `Range Enemy.prefab`, `Thrower.prefab`, `Porter.prefab`을 제거했다.
- `Melee Enemy.prefab`의 공용 UI와 컴포넌트 구성을 `Enemy.prefab` 템플릿으로 이관했다.
- `EnemyData.prefab` 필드와 Inspector 프리팹 검증을 제거했다.
- Stage 1 Battle 01~05의 웨이브 항목 42개를 기존 적 프리팹에서 대응하는 `EnemyData` SO로 변경했다.
- `Assets/Scenes/Stage 1.unity`의 `WaveManager`에 공용 `Enemy.prefab`을 `Enemy Prefab Template`로 연결했다.

## 새 적 추가 방법

1. `Assets > Create > Loaded > Enemy > Enemy`로 `EnemyData`를 만든다.
2. ID, 표시 이름, 설명과 Sprite를 입력한다.
3. 최대 체력과 처치 보상을 설정한다.
4. `Behavior Type`을 `Melee`, `Gunner`, `Thrower`, `Porter` 중 하나로 지정한다.
5. 해당 타입에 필요한 `EnemyActionData`를 `Actions`에 연결한다.
6. 원거리 또는 지원 적은 필요한 Telegraph Material을 연결한다.
7. `BattleData > Waves > Enemies > Enemy Data`에 새 SO와 수량을 등록한다.

별도 적 프리팹 생성이나 `EnemyController.enemyData` 수동 연결은 필요하지 않다.

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

### 관련 시간 값

| EnemyData 필드 | 역할 |
|---|---|
| `Queue Element Reveal Duration` | Queue 배경 또는 행동 타일 하나가 등장하는 시간 |
| `Queued Action Interval` | 준비된 공격 여러 개를 실제 실행할 때 공격 사이의 간격 |

두 값은 서로 다른 용도다. Queue 등장 속도는 `Queue Element Reveal Duration`으로 조절한다. 기본값은 0.25초이며 0이면 기존처럼 즉시 표시된다.

## 투척병 투사체 연출

투척병은 준비 단계에서 고정한 목표 타일까지 포물선 궤적으로 투사체를 이동시킨 뒤 피해와 공격 이펙트를 적용한다.

### 표시 우선순위

1. `Thrown Projectile Prefab`이 있으면 해당 프리팹을 생성한다.
2. 프리팹이 없으면 런타임 `SpriteRenderer` 투사체를 자동 생성한다.
3. `Thrown Projectile Sprite`가 있으면 지정 Sprite를 사용한다.
4. Sprite도 비어 있으면 32×32 흰색 원형 Sprite를 런타임에 한 번 생성해 공유한다.

자동 생성 투사체는 적과 동일한 Sorting Layer를 사용하고 적보다 한 단계 높은 Sorting Order에 표시된다. 목표 도착 후 투사체를 제거한다.

### 관련 EnemyData 필드

| 필드 | 역할 | 기본값 |
|---|---|---|
| `Thrown Projectile Prefab` | 선택적인 완성형 투사체 프리팹 | 없음 |
| `Thrown Projectile Sprite` | 프리팹이 없을 때 사용할 Sprite | 없음, 원형 자동 생성 |
| `Thrown Projectile Color` | 자동 생성 Sprite 색상 | 흰색 |
| `Thrown Projectile Size` | 자동 생성 투사체 월드 크기 | 0.35 |
| `Thrown Projectile Duration` | 출발점에서 목표까지 이동 시간 | 0.5초 |
| `Thrown Projectile Arc Height` | 포물선 정점의 추가 높이 | 2 |

이동 위치는 선형 보간 위치에 `sin(progress × π) × Arc Height`를 더해 계산한다. 게임이 일시정지된 동안 투사체 이동 시간도 진행되지 않는다.

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
2. 생성된 적마다 Sprite, 체력, 행동 타입과 이름이 EnemyData에 맞게 적용되는지 확인한다.
3. Queue 생성 턴에 Queue 배경이 설정 시간 동안 서서히 나타나는지 확인한다.
4. 행동 등록 턴에 새 행동 타일만 설정 시간 동안 서서히 나타나는지 확인한다.
5. 행동 타일에 마우스를 올렸을 때 이름과 설명이 표시되고 포인터 이동과 화면 경계에 맞춰 위치가 갱신되는지 확인한다.
6. 행동 타일이 제거될 때 열려 있던 툴팁도 닫히는지 확인한다.
7. 투척병 데이터에 투사체 프리팹과 Sprite가 모두 없을 때 원형 투사체가 궤적을 따라 이동하는지 확인한다.
8. 투척병에 커스텀 Sprite 또는 프리팹을 지정했을 때 기본 원형 대신 지정 외형이 사용되는지 확인한다.
9. 일시정지 중 Queue 연출과 투사체 이동이 멈추고 재개 후 이어지는지 확인한다.

## 기존 문서와의 관계

다음 문서는 당시 구현 과정과 과거 설계를 보존하는 이력 문서다. 프리팹 연결과 BattleData 설정 방법이 이 문서와 충돌하면 현재 문서를 우선한다.

- `0717_Enemy_EnemyData.md`
- `0717_Enemy_EnemyController_WaveManager.md`
- `0727_EnemyAI.md`
