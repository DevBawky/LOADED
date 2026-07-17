## AI-006: EnemyController & WaveManager

### Basic Information

* Date: 260717
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Enemy Turn & Stage Wave

### Problem

기존 `EnemyData`에는 적의 고정 정보만 있고 실제 적 프리팹이 플레이어의 턴 소비에 맞춰 회전, 이동, 장전, 공격 준비, 발사를 수행하는 런타임 구조가 없었다.

또한 기존 행동 방식은 `Aggressive`, `KeepDistance`, `FixedPattern`, `Stationary`로 나뉘어 있었지만 현재 게임에서 필요한 구분은 근거리 적과 원거리 적 두 종류였다. 스테이지별 적 풀과 최대 동시 등장 수를 설정하고, 빈 자리를 보드의 무작위 타일에서 보충하는 Wave 관리 구조도 필요했다.

### Why AI Was Used

플레이어가 실제로 턴을 소비한 경우에만 모든 적이 한 번씩 행동하도록 기존 `PlayerMove.TurnCompleted` 흐름을 재사용하고, 적 하나가 한 턴에 두 가지 행동을 수행하지 않도록 상태 전이를 정리하기 위해 AI를 사용했다.

근거리와 원거리 적의 장전 및 공격 준비 순서, 플레이어가 사거리 밖으로 이동한 경우의 처리, 여러 적의 타일 중복 방지, 새로 스폰된 적의 첫 행동 시점을 함께 정의하기 위해 도움을 받았다.

### Main Instructions

`EnemyBehaviorType`을 `Melee`와 `Range` 두 값으로 변경해주세요.

플레이어가 턴을 소비할 때마다 적도 이동, 회전, 대기, 장전, 발사 중 하나만 수행하도록 구현해주세요.

* Melee는 플레이어를 바라보고, 바라보는 방향이 맞으면 접근합니다.
* Melee는 플레이어가 `Preferred Distance` 안에 들어오면 공격 타일을 장전합니다.
* 장전 후 공격 사거리 안에 플레이어가 들어오면 공격을 준비하고 다음 턴에 발사합니다.
* Range는 공격 타일이 없으면 먼저 장전합니다.
* Range는 플레이어가 공격 사거리 밖에 있으면 접근하고, 사거리 안에 있으면 공격을 준비합니다.
* Range도 플레이어를 바라본 상태에서만 다음 행동을 수행합니다.
* 모든 적은 한 번의 플레이어 턴에 한 가지 행동만 수행합니다.

스테이지에 등장할 적 프리팹 풀과 최대 동시 등장 수를 설정하고, 현재 적 수가 최대 수보다 적으면 플레이어와 기존 적이 없는 무작위 보드 타일에 적을 스폰하는 `WaveManager`를 구현해주세요.

후속 요청으로 다음 전투 연결을 추가합니다.

* 플레이어와 모든 적은 같은 타일을 둘 이상이 점유할 수 없습니다.
* 플레이어 체력은 `Image | Fill Amount`와 `Text | Player HP`에 반영합니다.
* 적 체력은 프리팹의 `HP_Value.fillAmount`에 반영합니다.
* 적 공격력은 플레이어 체력에, 플레이어 탄환 공격력은 적 체력에 적용합니다.
* 플레이어 탄환은 최대 사거리 타일이 아니라 정면 유효 사거리 안의 적을 향해 발사합니다.
* 기존 관통 확률에 따라 다음 적까지 순서대로 피해를 적용합니다.

작업 후 실제 Inspector 필드명에 맞춘 적용 방법과 Play Mode 테스트 순서를 작성하고 새 Markdown 기록 파일을 생성해주세요.

### Output Summary

`Assets/Scripts/Enemy/EnemyData.cs`의 `EnemyBehaviorType`을 `Melee = 0`, `Range = 1`로 변경했다. 기존 `Aggressive`의 직렬화 값 0은 Melee로, `KeepDistance`의 값 1은 Range로 유지되므로 이미 생성된 에셋의 해당 값은 손실되지 않는다. 사용하지 않는 `FixedPattern`과 `Stationary` 값은 제거했다.

`Assets/Scripts/Enemy/EnemyController.cs`를 생성해 적 프리팹의 런타임 체력, 장전된 공격 행동, 공격 준비 여부, 마지막 턴 행동을 관리하도록 했다. 외부에서 `Initialize(BoardManager, PlayerMove, WaveManager)`를 호출해 Scene 참조를 명시적으로 전달하며 런타임 자동 탐색은 사용하지 않는다.

적은 먼저 플레이어가 있는 방향을 확인한다. 방향이 다르면 해당 턴에는 회전만 한다. Melee는 공격 행동의 사거리와 `Preferred Distance` 중 큰 값 안에 플레이어가 들어오면 장전하고, 장전 후 실제 공격 사거리 안에 들어오면 해당 턴을 대기하면서 공격 준비 상태가 된다. 다음 플레이어 소비 턴에도 사거리 안이면 공격을 발사한다. Range는 방향을 맞춘 뒤 공격이 없으면 장전하고, 사거리 밖이면 접근하며 사거리 안이면 공격을 준비한다.

공격 준비는 한 턴에 다른 행동과 함께 실행되지 않도록 `EnemyTurnActionType.Wait`로 기록한다. 준비 후 플레이어가 사거리 밖으로 이동하면 준비 상태만 해제하고 장전 상태는 유지한 채 접근한다. 발사 후에는 장전된 공격 행동을 소비한다.

`Assets/Scripts/Manager/BoardManager.cs`에는 타일 인덱스와 월드 위치를 변환하는 `TryGetTilePosition`, `TryGetTileIndex`, 두 위치의 타일 거리를 계산하는 `TryGetTileDistance`, 읽기 전용 `BoardCount`와 `BoardDistance`를 추가했다.

`Assets/Scripts/Manager/WaveManager.cs`를 생성했다. `PlayerMove.TurnCompleted`를 구독해 현재 활성 적의 스냅샷을 순서대로 한 번씩 처리한 다음, 최대 동시 등장 수보다 부족한 만큼 적을 보충한다. 플레이어 타일과 기존 적 타일은 스폰 후보에서 제외하며 적 이동도 플레이어 및 다른 적의 타일을 침범하지 않는다. 턴 처리 뒤에 생성된 적은 같은 턴에는 행동하지 않고 다음 플레이어 소비 턴부터 행동한다.

`PlayerMove`는 WaveManager가 명시적으로 전달한 점유 정보를 사용해 적이 있는 타일로의 이동을 거부한다. 적 이동과 스폰도 플레이어 및 다른 적이 있는 타일을 제외하므로 플레이어와 모든 활성 적은 같은 타일을 공유할 수 없다. 점유 타일로 이동하려는 실패 행동은 턴을 소비하지 않는다.

`Assets/Scripts/Player/PlayerHealth.cs`를 생성해 `Max Health`, `Starting Health`, 현재 체력을 관리한다. 피해와 회복 시 `Canvas > Panel | MainGame > Player HP > Image | Fill Amount`의 `Image.fillAmount`와 `Text | Player HP`의 `현재/최대` 텍스트를 함께 갱신한다. 체력이 0이 되면 `Defeated` 이벤트를 제공한다.

`EnemyController`는 `EnemyData.MaxHealth`로 현재 체력을 초기화하고 피해를 받을 때 프리팹의 `HP_Value.fillAmount`를 갱신한다. 체력이 0이 되면 WaveManager 활성 목록에서 제거된 뒤 GameObject를 제거한다. 적 공격 발사 시 연결된 `PlayerHealth.ApplyDamage`에 `EnemyAttackData.Damage`를 전달한다.

`PlayerShoot`은 더 이상 최대 사거리 타일을 LineRenderer 끝점으로 사용하지 않는다. WaveManager에서 플레이어가 바라보는 방향과 탄환 `Max Range` 안의 적을 가까운 순서로 받아 첫 적을 확정 적중시키고, 이후 적은 `Penetration Chances`를 순서대로 판정한다. LineRenderer 끝점은 마지막으로 적중한 적의 위치이며 적중한 모든 적에게 `BulletData.Damage`를 적용한다. 유효한 적이 없으면 발사가 실패하고 탄환과 턴을 소비하지 않는다.

사용자가 이름을 변경한 `Assets/Prefabs/Enemy/Test Enemy.prefab` 루트에는 `EnemyController`를 유지하고 `Enemy Data`, 루트 SpriteRenderer, `HP_Value` Image를 직접 연결했다. Player 프리팹에는 `PlayerHealth`를 추가했으며 SampleScene의 Player 인스턴스에서 `Health Fill Image`, `Health Text`를 지정된 HUD 오브젝트에 직접 연결했다. WaveManager의 `Player Health`와 PlayerShoot의 `Wave Manager` 참조도 Scene에 연결했다.

### Decision

* [x] 그대로 채택
* [ ] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * `EnemyBehaviorType`의 기존 직렬화 숫자 유지 여부 확인
  * `PlayerMove.CompleteTurn`과 `TurnCompleted` 호출 조건 확인
  * Melee와 Range 상태 전이를 정적 검토
  * 한 `TakeTurn` 호출에서 하나의 `CompleteAction` 경로만 선택되는지 확인
  * 플레이어 및 활성 적 타일의 스폰 제외 조건 확인
  * Player 이동 목표와 Enemy 이동 목표의 점유 타일 거부 조건 확인
  * PlayerHealth의 Fill Amount 및 HP 텍스트 계산 확인
  * Enemy 프리팹의 `EnemyController`, `Enemy Data`, `Sprite Renderer`, `Health Fill Image` 직렬화 참조 확인
  * 탄환 대상의 방향, 사거리, 거리 정렬, 관통 순서와 LineRenderer 끝점 확인
  * `Find`, `FindObjectOfType`, `FindAnyObjectByType`, `Resources.Load`, `GetComponent` 미사용 확인
  * 새 스크립트를 빌드 입력에 포함한 뒤 `dotnet build LOADED.slnx --no-restore`로 C# 컴파일 확인
  * Unity Play Mode에서의 동작 검증은 수동 테스트 항목으로 남김

* 테스트 결과:

  * `Aggressive = 0`에서 `Melee = 0`, `KeepDistance = 1`에서 `Range = 1`로 숫자가 유지됐다.
  * 이동, 회전, 대기 및 공격 준비, 장전, 발사는 각각 하나의 `EnemyTurnActionType`만 완료한다.
  * 실패한 플레이어 행동과 `doesNotConsumeTurn` 탄환 발사는 `PlayerMove.TurnCompleted`를 발생시키지 않으므로 적 턴도 실행되지 않는다.
  * WaveManager는 플레이어 타일과 이미 활성 적이 있는 타일을 제외한 후보에서 무작위 스폰 위치를 선택한다.
  * PlayerMove는 활성 적이 있는 타일로 이동하지 않으며 적도 플레이어 또는 다른 적의 타일로 이동하지 않는다.
  * PlayerHealth는 체력 비율을 0~1의 Fill Amount로, 현재 체력을 `현재/최대` 문자열로 반영한다.
  * 적 피해는 `HP_Value.fillAmount`에 반영되고 0이 되면 적이 제거된다.
  * 탄환은 정면 사거리 안의 가장 가까운 적부터 적중하며 관통 성공 시 다음 적까지 피해를 적용하고 마지막 적중 적을 선의 끝점으로 사용한다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode는 실행하지 않았다.

* 발견한 문제:

  * 플레이어와 적의 기본 Damage 및 체력 감소는 연결했지만 Knockback, Stun, Mark 상태 효과 실행 로직은 아직 없다.
  * 플레이어 체력이 0이 되면 `Defeated` 이벤트가 발생하지만 Game Over UI, 입력 차단, Scene 전환은 이번 요청에 정의되지 않아 추가하지 않았다.
  * 탄환 발사는 즉시 판정되는 한 줄 전투를 기준으로 하며 유효한 적이 없으면 남은 장전 탄환을 유지하고 연속 발사를 중단한다.
  * `EnemyData.Actions`에 동일한 타입의 행동이 여러 개 있으면 현재는 목록에서 가장 먼저 발견한 행동을 사용한다.

### Human Modifications

사용자가 직접 생성한 뒤 이름을 `Test Enemy.prefab`으로 변경한 적 프리팹과 `Test Enemy`, `New Enemy Action`, `New Enemy Attack` 에셋은 삭제하거나 새 에셋으로 교체하지 않았다.

Enemy 프리팹에는 Codex가 `EnemyController`를 추가하고 기존 `Test Enemy`, SpriteRenderer, `HP_Value`를 연결했다. Player 프리팹에는 `PlayerHealth`를 추가하고 사용자 Scene의 기존 HUD 오브젝트를 직렬화 참조로 연결했다. 테스트 데이터의 수치와 행동 목록은 사용자가 원하는 밸런스로 설정할 수 있도록 변경하지 않았다.

### Final Result

#### Enemy 데이터 설정

Melee 적은 `Enemy Data > Behavior Type`을 `Melee`로 설정하고 `Preferred Distance`를 공격 타일을 장전하기 시작할 거리로 설정한다. `Actions`에는 다음 에셋이 필요하다.

* `Action Type = Approach`, `Movement Distance`는 한 번의 이동 행동으로 전진할 칸 수
* `Action Type = MeleeAttack`, `Attack Data`에는 사거리가 1 이상인 `EnemyAttackData`

Range 적은 `Behavior Type`을 `Range`로 설정하고 다음 행동 에셋을 `Actions`에 등록한다.

* `Action Type = Approach`, `Movement Distance`는 사거리 밖에서 전진할 칸 수
* `Action Type = RangedAttack`, `Attack Data`에는 원하는 유효 `Range`가 설정된 `EnemyAttackData`

`Range` 값이 0이면 같은 타일 외에는 유효 공격 대상이 없으므로 일반적인 근거리 공격은 1, 원거리 공격은 2 이상의 값을 권장한다. 공격 시각 효과가 필요하면 `EnemyAttackData > Attack Effect Prefab`을 연결한다.

#### Enemy 프리팹 설정

기존 `Assets/Prefabs/Enemy/Test Enemy.prefab`에는 이미 `EnemyController`가 추가되어 있다.

* `Enemy Data`: 해당 프리팹이 사용할 `EnemyData`
* `Sprite Renderer`: 적 외형을 표시할 SpriteRenderer
* `Health Fill Image`: 프리팹의 `Canvas > HP_Bar > HP_Value` Image
* `Runtime State > Current Health`, `Loaded Attack Action`, `Is Attack Prepared`, `Last Turn Action`: Play Mode에서 현재 상태를 확인하는 필드

새 적 프리팹을 만들 때도 루트에 `EnemyController`를 추가하고 위 세 참조를 직접 연결한다. `EnemyData > Prefab`에도 완성된 적 프리팹을 등록해 데이터 에셋의 프리팹 참조를 유지한다.

#### PlayerHealth 설정

Player 프리팹 루트에는 `PlayerHealth`가 추가되어 있다.

* `Max Health`: 플레이어 최대 체력, 기본값 100
* `Starting Health`: 게임 시작 체력, 기본값 100이며 Max Health 안으로 제한됨
* `Health Fill Image`: `Canvas > Panel | MainGame > Player HP > Image | Fill Amount`
* `Health Text`: `Canvas > Panel | MainGame > Player HP > Text | Player HP`

SampleScene의 두 UI 참조는 이미 Player 프리팹 인스턴스에 연결되어 있다. 피해를 받으면 Fill Amount는 `Current Health / Max Health`, 텍스트는 `CurrentHealth/MaxHealth` 형식으로 갱신된다.

#### WaveManager 설정

Scene에 빈 GameObject `@_WaveManager`와 선택적인 적 부모 Transform `@_Enemies`를 만든 뒤 `WaveManager`를 추가한다.

* `Stage Enemy Pool`: 이 스테이지에 등장할 `EnemyController`가 포함된 Enemy 프리팹 목록
* `Max Active Enemies`: 동시에 존재할 최대 적 수
* `Board Manager`: Scene의 `@_BoardManager`
* `Player Move`: Player 오브젝트의 `PlayerMove`
* `Player Health`: Player 오브젝트의 `PlayerHealth`
* `Enemy Parent`: 생성된 적을 정리할 `@_Enemies` Transform
* `Active Enemies`: Play Mode에서 현재 활성 적을 확인하는 런타임 목록

`WaveManager`는 시작 시 빈 자리를 채우고, 이후 `PlayerMove.TurnCompleted`가 발생할 때 기존 적의 행동을 모두 처리한 다음 다시 빈 자리를 채운다. 별도의 버튼이나 UnityEvent 연결은 필요하지 않다.

Player의 `PlayerShoot > Wave Manager`에도 Scene의 `@_WaveManager`를 연결한다. 발사 시 WaveManager가 정면 사거리 안의 적을 가까운 순서로 제공하며, 유효한 적이 없으면 발사하지 않는다.

#### Play Mode 테스트 순서

1. Melee용 `Approach`, `MeleeAttack`, `EnemyAttackData`를 만들고 Melee `EnemyData.Actions`에 연결한다.
2. Range용 `Approach`, `RangedAttack`, `EnemyAttackData`를 만들고 Range `EnemyData.Actions`에 연결한다.
3. 각 Enemy 프리팹의 `Enemy Data`, `Sprite Renderer`, `Health Fill Image`를 확인한다.
4. Player의 `PlayerHealth`에서 `Health Fill Image`, `Health Text`를 확인한다.
5. `@_WaveManager`의 `Stage Enemy Pool`, `Max Active Enemies`, `Board Manager`, `Player Move`, `Player Health`, `Enemy Parent`를 연결한다.
6. PlayerShoot의 `Wave Manager`를 연결한다.
7. Play Mode 시작 시 플레이어와 겹치지 않는 무작위 타일에 최대 수만큼 적이 생성되는지 확인한다.
8. 플레이어와 적이 서로가 있는 타일로 이동할 수 없고 적끼리도 겹치지 않는지 확인한다.
9. 정면 사거리 안에 적이 있을 때 LineRenderer가 마지막 적중 적까지 표시되고 적 HP_Value가 감소하는지 확인한다.
10. 관통 확률이 성공하면 다음 적도 피해를 받고, 실패하면 해당 적 앞에서 적중이 종료되는지 확인한다.
11. 유효한 적이 없을 때 탄환과 턴을 소비하지 않는지 확인한다.
12. Melee와 Range 적의 공격으로 Player HP Fill과 텍스트가 함께 감소하는지 확인한다.
13. A, D, S, 회전, 정상 장전, 턴을 소비하는 발사를 실행하고 적마다 `Last Turn Action`이 한 번만 변경되는지 확인한다.
14. 실패한 이동 및 장전, 미장전 발사, `doesNotConsumeTurn` 탄환 발사에는 적 상태가 진행되지 않는지 확인한다.
15. 적을 제거한 뒤 다음 플레이어 소비 턴 종료 시 빈 타일에 새 적이 보충되는지 확인한다.

C# 컴파일은 경고와 오류 없이 완료됐다. Unity Play Mode를 통한 실제 스폰, 상태 진행, 이펙트 출력 검증은 수행하지 않았다.

### Lessons Learned

동시 턴 게임에서 중요한 것은 모든 오브젝트를 실제 같은 프레임에 이동시키는 것보다, 플레이어의 유효 행동 하나에 대해 각 적의 상태를 정확히 한 단계만 진행시키는 것이다. 중앙 `WaveManager`가 턴 이벤트를 한 번 구독하면 새 적의 행동 시점과 적 처리 순서를 일관되게 관리할 수 있다.

장전과 공격 준비를 분리하면 플레이어에게 공격을 예고할 턴이 생긴다. 준비 상태를 별도 런타임 값으로 두고 공격 타일 자체는 유지해야, 플레이어가 사거리 밖으로 피했을 때 다시 장전하지 않고 추적을 계속할 수 있다.

체력 변경과 UI 갱신을 하나의 컴포넌트가 함께 관리하면 공격 주체가 UI 계층을 알 필요가 없다. 적은 `PlayerHealth.ApplyDamage`만 호출하고 PlayerHealth가 Fill Amount와 텍스트를 일관되게 갱신한다.

한 줄 전투에서는 물리 충돌 대신 보드 타일 인덱스로 방향과 거리를 비교하는 것이 더 명확하다. 동일한 타일 인덱스를 점유 검사, 탄환 대상 정렬, 스폰 제외에 공통으로 사용하면 겹침과 적중 순서 불일치를 줄일 수 있다.
