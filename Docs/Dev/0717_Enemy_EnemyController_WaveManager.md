## AI-006: EnemyController & WaveManager

### Basic Information

* Date: 260717
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Enemy Turn & Stage Wave

### Problem

기존 `EnemyData`에는 적의 고정 정보만 있고 실제 적 프리팹이 플레이어의 턴 소비에 맞춰 회전, 이동, 장전, 공격 준비, 발사를 수행하는 런타임 구조가 없었다.

또한 기존 행동 방식은 `Aggressive`, `KeepDistance`, `FixedPattern`, `Stationary`로 나뉘어 있었지만 현재 게임에서 필요한 구분은 근거리 적과 원거리 적 두 종류였다. 최초 구현은 스테이지별 적 풀과 최대 동시 등장 수를 기준으로 빈 자리를 보충했으나, 기획된 순서대로 적 종류와 수량을 구성하고 한 웨이브를 전멸시킨 뒤에만 다음 웨이브를 시작할 수 있는 구조가 추가로 필요했다.

### Why AI Was Used

플레이어가 실제로 턴을 소비한 경우에만 모든 적이 한 번씩 행동하도록 기존 `PlayerMove.TurnCompleted` 흐름을 재사용하고, 적 하나가 한 턴에 두 가지 행동을 수행하지 않도록 상태 전이를 정리하기 위해 AI를 사용했다.

근거리와 원거리 적의 장전 및 공격 준비 순서, 플레이어가 사거리 밖으로 이동한 경우의 처리, 여러 적의 타일 중복 방지, 새로 스폰된 적의 첫 행동 시점을 함께 정의하기 위해 도움을 받았다.

### Main Instructions

`EnemyBehaviorType`을 `Melee`와 `Range` 두 값으로 변경해주세요.

플레이어가 턴을 소비할 때마다 적도 이동, 회전, 대기, 장전, 발사 중 하나만 수행하도록 구현해주세요.

* Melee는 먼저 플레이어가 있는 방향을 바라보도록 회전합니다.
* 방향이 맞으면 `Image | Queue`를 활성화해 액션 큐를 생성합니다.
* Queue 생성 후 첫 번째 공격 스킬을 반드시 한 번 등록합니다.
* 플레이어와 거리가 1칸보다 멀고 `Preferred Distance` 안이면 첫 공격 이후 매 턴 50% 확률로 공격 스킬을 하나 추가 등록하거나 플레이어에게 접근합니다.
* 추가 등록 조건을 만족하지 않거나 Queue가 가득 찼으면 플레이어와 1칸 거리가 될 때까지 접근합니다.
* 플레이어와 거리가 1칸이면 해당 턴에는 공격 준비만 하고 다음 적 턴에 Queue를 시전합니다.
* 공격이 끝나면 `Preferred Distance`만큼 거리를 벌린 뒤 Queue 생성부터 반복합니다.
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
* 적 스폰 위치에 월드 좌표 오프셋을 적용할 수 있어야 합니다.
* 최초 구현에서는 적을 즉시 보충하지 않고 기본 2턴의 `Spawn Term`이 지난 뒤 스폰합니다.
* Melee 적은 공격 발사 후 `Preferred Distance`만큼 거리를 확보할 때까지 후퇴를 시도합니다.
* 적의 바라보는 방향이 바뀌어도 자식 Canvas는 뒤집히지 않아야 합니다.

추가 요청으로 기존 공격 등록과 발사 사이를 Shogun Showdown 방식의 가시적 큐로 확장합니다.

* 적 공격은 Queue 생성, 공격 등록, 공격 준비, 다음 적 턴 공격 순서로 진행합니다.
* Queue 생성 시 `Image | Queue`를 활성화합니다.
* 공격 행동은 최대 3개까지 한 턴에 하나씩 연속 등록하고 `Image _ Queue AttackIcon`을 Queue의 자식으로 표시합니다.
* 준비 턴에는 다른 행동을 하지 않고 Queue 색상을 기본 주황색으로 변경합니다.
* 준비된 행동은 다음 적 턴에 설정된 간격으로 순차 발동하며 모두 끝나면 Queue를 비활성화합니다.
* 이동과 회전은 공격 Queue에 등록하지 않고 적의 일반 턴 행동으로 독립 실행합니다.

추가 요청으로 기존 무작위 빈자리 보충 스폰을 배열 기반 웨이브 진행으로 변경합니다.

* `Waves` 배열의 각 원소에 적 프리팹 종류와 수량을 여러 항목으로 설정할 수 있어야 합니다.
* 첫 웨이브의 모든 적은 Play Mode 시작 시 한 번에 스폰합니다.
* 현재 웨이브의 적이 한 마리라도 살아 있는 동안에는 다음 웨이브 카운트다운을 진행하지 않습니다.
* 현재 웨이브를 전멸시킨 뒤 `Spawn Term`만큼 플레이어 소비 턴이 지나면 다음 웨이브 전체를 한 번에 스폰합니다.
* 마지막 웨이브를 전멸시키면 `Canvas > Panel | Stage Clear`를 활성화하고 `Canvas > Panel | MainGame`을 비활성화합니다.
* 관련 Markdown 문서가 이미 존재하므로 새 문서를 만들지 않고 현재 문서를 갱신합니다.

후속 밸런스 요청으로 Melee가 `Preferred Distance`에서 항상 최대 공격 수까지 모으느라 접근이 늦어지는 문제를 수정합니다.

* Queue 생성 후 첫 번째 MeleeAttack은 기존처럼 반드시 등록합니다.
* 첫 공격이 등록된 상태에서 추가 공격 등록 조건을 만족하면 매 턴 `추가 공격 등록 : 플레이어에게 접근`을 5:5 확률로 선택합니다.
* `Melee Additional Attack Chance`를 Inspector에 노출하고 기본값을 0.5로 설정합니다.
* 이미 1칸 거리이면 확률 판정 없이 공격을 준비하고, Queue가 가득 찼거나 Preferred Distance 밖이면 기존처럼 접근합니다.

후속 요청으로 다음 웨이브의 적이 생성될 타일을 미리 알려주는 Warning 표시를 추가합니다.

* Tile 프리팹 자식의 `Warning` 오브젝트는 기본 비활성 상태를 유지합니다.
* 현재 웨이브 전멸 시 다음 웨이브의 실제 스폰 타일을 미리 선택하고 해당 타일의 Warning을 활성화합니다.
* Warning이 표시된 타일은 실제 스폰 위치와 달라지지 않도록 예약하며 플레이어와 적의 이동 대상으로 사용할 수 없습니다.
* `Spawn Term`이 끝나 예약 타일에 적이 생성되면 Warning을 비활성화합니다.
* 첫 웨이브는 기존 요구대로 게임 시작 시 즉시 생성하므로 Warning 예고는 두 번째 웨이브부터 적용합니다.

작업 후 실제 Inspector 필드명에 맞춘 적용 방법과 Play Mode 테스트 순서를 Markdown 기록에 작성해주세요. 후속 웨이브 변경은 관련 문서가 이미 있으므로 이 문서를 업데이트합니다.

### Output Summary

`Assets/Scripts/Enemy/EnemyData.cs`의 `EnemyBehaviorType`을 `Melee = 0`, `Range = 1`로 변경했다. 기존 `Aggressive`의 직렬화 값 0은 Melee로, `KeepDistance`의 값 1은 Range로 유지되므로 이미 생성된 에셋의 해당 값은 손실되지 않는다. 사용하지 않는 `FixedPattern`과 `Stationary` 값은 제거했다.

`Assets/Scripts/Enemy/EnemyController.cs`를 생성해 적 프리팹의 런타임 체력, 최대 3개의 공격 행동 큐, 큐 생성 및 공격 준비 여부, 후퇴 여부, 마지막 턴 행동을 관리하도록 했다. 외부에서 `Initialize(BoardManager, PlayerMove, PlayerHealth, WaveManager)`를 호출해 Scene 참조를 명시적으로 전달하며 런타임 자동 탐색은 사용하지 않는다.

후속 연출 작업에서 `Assets/Scripts/Common/ActorMotion.cs`를 추가하고 Player와 Enemy 프리팹에 직접 연결했다. `Move Duration` 동안 타일 사이를 보간하면서 `sin(πt) * Jump Height`를 월드 Y에 더해 한 칸씩 폴짝 이동한다. `Rotate Duration` 동안 루트 X Scale을 현재 부호에서 반대 부호로 SmoothStep 보간한다. 플레이어와 적은 모션이 끝난 뒤에만 턴 완료를 전달한다.

적은 먼저 플레이어가 있는 방향을 확인하고 방향이 다르면 해당 턴에는 회전만 한다. Melee는 방향이 맞으면 거리와 관계없이 Queue를 생성하고 다음 적 턴에 첫 공격 스킬을 반드시 등록한다. 플레이어와 거리가 2칸 이상이면서 `Preferred Distance` 이내라면 이후 턴마다 `Melee Additional Attack Chance`를 판정한다. 기본값 0.5에서는 50% 확률로 공격 스킬을 하나 추가하고, 나머지 50%에서는 `Approach`의 `Movement Distance`만큼 플레이어에게 접근한다. 등록할 공격 에셋이 하나뿐이면 추가 등록 선택 시 같은 스킬을 반복하고, 여러 개면 `EnemyData.Actions`의 MeleeAttack 순서를 순환한다. Queue가 가득 찼거나 추가 등록 범위 밖이면 확률 판정 없이 플레이어와 1칸 거리가 될 때까지 접근한다.

Melee가 플레이어와 1칸 거리에 도달하면 Queue가 최대 수량에 도달하지 않았더라도 해당 턴에 `PrepareAttack`만 수행하고 Queue 색상을 기본 주황색으로 변경한다. 이 준비 상태는 바로 다음 적 턴의 첫 분기에서 큐 발사로 이어지므로 중간에 회전이나 이동을 끼우지 않는다. 큐의 공격 행동은 기본 0.2초 간격으로 앞에서부터 순차 실행되며, 각 아이콘도 실행 순서대로 제거되고 전부 끝나면 Queue를 비활성화한다. 공격 순간 플레이어가 방향 또는 개별 공격 사거리 밖에 있으면 해당 행동은 빗나가지만 큐에서는 정상 소비된다. Range는 기존처럼 등록 가능한 원거리 행동을 모두 큐에 넣고 유효 사거리 안에서 준비한다.

Melee 적은 발사 행동을 완료하면 `Is Retreating` 상태가 된다. 현재 타일 거리가 `Preferred Distance`보다 작으면 플레이어 반대 방향으로 한 번의 이동 행동을 수행하며, 목표 거리 이상이 될 때까지 다음 턴에도 후퇴를 재시도한다. `Retreat` 행동 에셋이 있으면 해당 `Movement Distance`를 사용하고, 없으면 기본 1칸을 사용한다. 한 번의 후퇴 거리는 남은 목표 거리 이하로 제한해 Preferred Distance를 불필요하게 초과하지 않는다. 보드 끝이나 다른 적 때문에 이동하지 못한 경우에는 대기하고 후퇴 상태를 유지한다.

Enemy 루트의 X Scale이 반전될 때 `ActorMotion > Orientation Locked Transform`으로 연결된 Canvas의 로컬 X Scale에도 매 프레임 같은 부호를 적용한다. 루트 반전과 Canvas 반전이 서로 상쇄되므로 회전 보간 도중에도 HP Canvas의 월드 방향은 정방향으로 유지된다.

`Assets/Scripts/Manager/BoardManager.cs`에는 타일 인덱스와 월드 위치를 변환하는 `TryGetTilePosition`, `TryGetTileIndex`, 두 위치의 타일 거리를 계산하는 `TryGetTileDistance`, 읽기 전용 `BoardCount`와 `BoardDistance`를 추가했다.

`Assets/Scripts/Manager/WaveManager.cs`는 `PlayerMove.TurnCompleted`를 구독하고 플레이어 입력을 잠근 뒤 `Enemy Turn Delay`만큼 기다린다. 이후 현재 활성 적의 스냅샷을 등록 순서대로 처리하며 각 적의 이동·회전 모션 완료를 기다리고 다음 적 전에 `Enemy Action Interval`을 적용한다. `Stage 1` 기본값은 각각 0.35초와 0.15초다. 모든 적 행동과 웨이브 카운트 처리가 끝난 뒤 플레이어 입력을 다시 허용한다.

기존 `Stage Enemy Pool`과 `Max Active Enemies` 기반의 무작위 보충 로직은 제거했다. 대신 직렬화 가능한 `EnemyWave[]`와 각 웨이브의 `EnemyWaveEntry[]`를 추가해 `Enemy Prefab + Count` 조합을 원하는 순서와 종류로 구성한다. 첫 웨이브는 게임 시작 시 일괄 생성하며, 현재 웨이브가 전멸하기 전에는 다음 웨이브 카운트다운을 시작하지 않는다. 전멸 후 플레이어가 소비한 턴마다 `Remaining Spawn Turns`가 감소하고 0이 되면 다음 웨이브의 모든 적을 같은 프레임에 생성한다. 전멸 공격으로 소비된 플레이어 턴도 카운트다운에 포함된다. 새로 생성된 적은 생성된 턴의 적 행동 스냅샷에는 포함되지 않으므로 다음 플레이어 소비 턴부터 행동한다.

웨이브를 생성하기 전에 모든 항목의 프리팹, `EnemyData`, 양수 수량과 현재 빈 타일 수를 검증한다. 생성 도중 초기화가 실패하면 그 웨이브에서 이미 만든 적도 제거해 일부만 등장한 상태로 진행하지 않는다. 마지막 웨이브를 전멸시키면 `Is Stage Cleared`를 켜고 `Canvas > Panel | Stage Clear`를 활성화하며 `Canvas > Panel | MainGame`을 비활성화한다. `Stage 1`에는 두 패널이 직접 참조되어 있고 Stage Clear 패널은 초기 비활성 상태다.

`Assets/Scripts/Manager/BoardTile.cs`를 추가하고 Tile 프리팹 루트에 연결했다. `Warning Object`에는 기존 자식 `Warning`을 직접 지정하며 `Awake`에서 항상 비활성화한다. `BoardManager`는 생성한 `BoardTile`을 타일 인덱스 순서대로 보관하고 `SetTileWarningActive`로 특정 타일의 경고 상태를 변경한다.

`WaveManager`는 현재 웨이브 전멸 시 다음 웨이브 수량만큼 서로 다른 빈 타일을 무작위로 예약하고 Warning을 활성화한다. 이후에는 다시 위치를 추첨하지 않고 예약된 타일 좌표에 적을 생성하며, 생성이 완료되면 모든 Warning과 예약 목록을 해제한다. 플레이어와 적의 이동 검사에도 예약 타일을 포함해 경고 위치가 점유되는 상황을 막았다. `Spawn Term = 0`이면 예약과 생성이 같은 프레임에 일어나 경고를 시각적으로 확인하기 어려우므로 예고가 필요하면 1 이상을 사용해야 한다.

스폰 위치에는 `Spawn Position Offset`을 월드 좌표로 더한다. `Stage 1` 설정값은 `(0, 0.7, 0)`이며 플레이어와 적 Sprite의 세로 기준을 맞춘다. 플레이어 타일과 기존 적 타일은 스폰 후보에서 제외하며 적 이동도 플레이어 및 다른 적의 타일을 침범하지 않는다.

`PlayerMove`는 WaveManager가 명시적으로 전달한 점유 정보를 사용해 적이 있는 타일로의 이동을 거부한다. 적 이동과 스폰도 플레이어 및 다른 적이 있는 타일을 제외하므로 플레이어와 모든 활성 적은 같은 타일을 공유할 수 없다. 점유 타일로 이동하려는 실패 행동은 턴을 소비하지 않는다. `Is Acting`과 `Is Enemy Turn Resolving` 잠금은 키보드 입력뿐 아니라 PlayerMove 및 PlayerShoot의 공개 UI 메소드에도 동일하게 적용된다.

`Assets/Scripts/Player/PlayerHealth.cs`를 생성해 `Max Health`, `Starting Health`, 현재 체력을 관리한다. 피해와 회복 시 `Canvas > Panel | MainGame > Player HP > Image | Fill Amount`의 `Image.fillAmount`와 `Text | Player HP`의 `현재/최대` 텍스트를 함께 갱신한다. 체력이 0이 되면 `Defeated` 이벤트를 제공한다.

`EnemyController`는 `EnemyData.MaxHealth`로 현재 체력을 초기화하고 피해를 받을 때 프리팹의 `HP_Value.fillAmount`를 갱신한다. 체력이 0이 되면 WaveManager 활성 목록에서 제거된 뒤 GameObject를 제거한다. 적 공격 발사 시 연결된 `PlayerHealth.ApplyDamage`에 `EnemyAttackData.Damage`를 전달한다.

`Assets/Scripts/Enemy/EnemyActionQueueUI.cs`를 추가했다. `Queue Image`, `Icon Parent`, `Attack Icon Prefab`을 Inspector에서 직접 연결하고 `Normal Queue Color`, `Prepared Queue Color`, `Missing Icon Color`를 제공한다. 공격 아이콘은 프리팹 인스턴스로만 생성해 원본 프리팹이나 행동 ScriptableObject를 변경하지 않는다. `Test Enemy.prefab`에는 기존 `Image | Queue`와 `Image _ Queue AttackIcon.prefab` 참조가 연결되어 있으며 Queue는 초기 비활성 상태다.

`Queued Attack Actions`에는 `MeleeAttack` 또는 `RangedAttack`만 들어간다. 회전은 플레이어 방향이 다를 때 `ActorMotion`으로 즉시 선택되는 일반 턴 행동이며, 접근과 후퇴는 각각 `Approach`, `Retreat` 타입의 `EnemyActionData.MovementDistance`를 읽어 실행한다. 유효한 Attack Data가 하나도 없어 공격 Queue를 만들 수 없는 설정에서도 Queue를 초기화한 뒤 `Approach` 이동을 시도하므로 공격 데이터 누락이 이동과 회전을 막지 않는다.

`PlayerShoot`은 WaveManager에서 플레이어가 바라보는 방향과 탄환 `Max Range` 안의 적을 가까운 순서로 받아 첫 적을 확정 적중시키고, 이후 적은 `Penetration Chances`를 순서대로 판정한다. 적이 있으면 LineRenderer 길이는 마지막으로 적중한 적의 X 거리로 계산하고 적중한 모든 적에게 `BulletData.Damage`를 적용한다. 발사선은 Fire Point Y를 수평 기준으로 보정한 뒤 설정된 최대 각도 안에서 탄환별로 무작위 회전한다. 유효한 적이 없어도 최대 사거리 안의 가장 먼 유효 타일 X 거리까지 발사를 실행하고 탄환을 무덤으로 이동하며 기존 턴 소비 규칙을 적용한다.

사용자가 이름을 변경한 `Assets/Prefabs/Enemy/Test Enemy.prefab` 루트에는 `EnemyController`와 `ActorMotion`을 유지하고 `Enemy Data`, 루트 SpriteRenderer, `HP_Value` Image, Actor Motion을 직접 연결했다. Actor Motion의 `Actor Transform`은 Enemy 루트, `Orientation Locked Transform`은 Canvas다. Player 프리팹에도 `ActorMotion`을 추가해 `PlayerMove > Actor Motion`과 연결했다. Player 프리팹에는 `PlayerHealth`를 추가했으며 `Stage 1`의 Player 인스턴스에서 `Health Fill Image`, `Health Text`를 지정된 HUD 오브젝트에 직접 연결했다. WaveManager의 `Player Health`와 PlayerShoot의 `Wave Manager` 참조도 Scene에 연결했다.

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
  * 첫 MeleeAttack 강제 등록 후 Preferred Distance 안에서 추가 등록/접근 50:50 분기 확인
  * Melee 발사 후 후퇴 시작, 거리 달성, 이동 실패 유지 조건 확인
  * Enemy 루트 양방향 Scale에서 Canvas 최종 X Scale 부호 확인
  * 첫 웨이브 즉시 생성, 웨이브 생존 중 카운트다운 정지, 전멸 후 Spawn Term 2의 카운트다운 순서 확인
  * 웨이브 항목의 프리팹·EnemyData·수량과 빈 타일 수 사전 검증 및 실패 시 생성 롤백 확인
  * 마지막 웨이브 전멸 시 MainGame/Stage Clear 패널 활성 상태 전환 확인
  * 다음 웨이브 예약 타일 수와 Warning 활성 수가 웨이브 적 수와 일치하는지 확인
  * 예약 타일 이동 거부, 예약 좌표 스폰 및 스폰 완료 후 Warning 해제 확인
  * Spawn Position Offset의 월드 위치 적용 확인
  * 한 `TakeTurn` 호출에서 하나의 `CompleteAction` 경로만 선택되는지 확인
  * Queue 생성, 공격별 아이콘 등록, 준비 색상 변경, 다음 적 턴 발사 상태 전이 확인
  * 최대 3개 제한과 `Queued Action Interval` 대기 및 마지막 아이콘 제거 후 Queue 비활성화 확인
  * 이동과 회전이 `Queued Attack Actions`에 추가되지 않고, Attack Data 누락 시에도 Approach가 실행되는지 확인
  * Enemy Turn Delay 후 적이 목록 순서대로 행동하고 각 Is Acting 종료까지 기다리는 Coroutine 확인
  * Player와 Enemy의 타일별 Sin 점프 및 X Scale 회전 보간식 확인
  * 플레이어 및 활성 적 타일의 스폰 제외 조건 확인
  * Player 이동 목표와 Enemy 이동 목표의 점유 타일 거부 조건 확인
  * PlayerHealth의 Fill Amount 및 HP 텍스트 계산 확인
  * Enemy 프리팹의 `EnemyController`, `Enemy Data`, `Sprite Renderer`, `Health Fill Image` 직렬화 참조 확인
  * 탄환 대상의 방향, 사거리, 거리 정렬, 관통 순서와 LineRenderer의 X 거리 및 랜덤 각도 확인
  * `Find`, `FindObjectOfType`, `FindAnyObjectByType`, `Resources.Load`, `GetComponent` 미사용 확인
  * 새 스크립트를 빌드 입력에 포함한 뒤 `dotnet build LOADED.slnx --no-restore`로 C# 컴파일 확인
  * Unity Play Mode에서의 동작 검증은 수동 테스트 항목으로 남김

* 테스트 결과:

  * `Aggressive = 0`에서 `Melee = 0`, `KeepDistance = 1`에서 `Range = 1`로 숫자가 유지됐다.
  * 이동, 회전, 대기, Queue 생성, 공격 등록, 공격 준비, 발사는 각각 하나의 `EnemyTurnActionType`만 완료한다.
  * Melee 공격 행동은 `CreateQueue -> 첫 RegisterAttack -> 조건부 추가 RegisterAttack 또는 Move -> 거리 1칸 PrepareAttack -> 다음 턴 Fire` 순서로 진행된다.
  * Melee는 첫 스킬을 거리와 관계없이 등록하고, 거리 2 이상 및 Preferred Distance 이내에서는 기본 50% 확률로 추가 스킬 등록 또는 접근을 선택한다.
  * 거리 1칸에서는 Queue가 덜 찼어도 즉시 준비하며, Queue가 가득 찬 상태에서 거리가 멀면 접근한다.
  * 이동과 회전은 공격 Queue를 거치지 않으며, 유효한 공격 행동이 없어도 회전 및 Approach 이동은 계속 수행한다.
  * 준비된 Queue는 다음 적 턴에 기본 0.2초 간격으로 순차 실행되고 마지막 행동 후 UI가 비활성화된다.
  * 플레이어 이동·회전 연출이 끝난 뒤 0.35초 후 첫 적이 행동하며, 각 적 행동 사이에는 기본 0.15초 간격이 적용된다.
  * 적 이동은 여러 칸이어도 타일마다 `Move Duration`과 `sin(πt)` 점프를 반복하고 회전 완료 전에는 다음 적 행동으로 넘어가지 않는다.
  * 실패한 플레이어 행동과 `doesNotConsumeTurn` 탄환 발사는 `PlayerMove.TurnCompleted`를 발생시키지 않으므로 적 턴도 실행되지 않는다.
  * WaveManager는 플레이어 타일과 이미 활성 적이 있는 타일을 제외한 후보에서 무작위 스폰 위치를 선택한다.
  * PlayerMove는 활성 적이 있는 타일로 이동하지 않으며 적도 플레이어 또는 다른 적의 타일로 이동하지 않는다.
  * PlayerHealth는 체력 비율을 0~1의 Fill Amount로, 현재 체력을 `현재/최대` 문자열로 반영한다.
  * 적 피해는 `HP_Value.fillAmount`에 반영되고 0이 되면 적이 제거된다.
  * 탄환은 정면 사거리 안의 가장 가까운 적부터 적중하며 관통 성공 시 다음 적까지 피해를 적용하고 마지막 적중 적을 선의 끝점으로 사용한다. 적이 없으면 최대 유효 사거리 타일까지 발사된다.
  * 첫 웨이브는 시작 시 일괄 생성되고 현재 웨이브의 적이 남아 있는 동안에는 `Remaining Spawn Turns`가 진행되지 않는다.
  * `Spawn Term = 2`에서는 현재 웨이브를 전멸시킨 공격의 소비 턴을 포함해 두 번의 플레이어 소비 턴이 완료된 시점에 다음 웨이브 전체가 생성된다.
  * `Stage 1`의 스폰 위치에는 `(0, 0.7, 0)` 오프셋이 적용된다.
  * 마지막 웨이브의 마지막 적이 제거되는 즉시 `Panel | Stage Clear`가 활성화되고 `Panel | MainGame`이 비활성화된다.
  * 다음 웨이브 대기 중 예약된 각 타일의 `Warning`이 활성화되고 플레이어와 적은 해당 타일로 이동할 수 없다.
  * 다음 웨이브는 Warning이 표시된 좌표에 생성되며 생성 완료와 함께 Warning이 비활성화된다.
  * Melee 발사 후 거리가 Preferred Distance보다 작으면 반대 방향 이동을 시도하며, Retreat 에셋이 없는 현재 테스트 데이터에서는 1칸 fallback을 사용한다.
  * Enemy 루트가 좌우 반전될 때 Canvas Transform이 반대 반전을 적용해 HP UI가 정방향을 유지한다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode는 실행하지 않았다.

* 발견한 문제:

  * 플레이어와 적의 기본 Damage 및 체력 감소는 연결했지만 Knockback, Stun, Mark 상태 효과 실행 로직은 아직 없다.
  * 플레이어 체력이 0이 되면 `Defeated` 이벤트가 발생하지만 Game Over UI, 입력 차단, Scene 전환은 이번 요청에 정의되지 않아 추가하지 않았다.
  * 탄환 발사는 즉시 판정되는 한 줄 전투를 기준으로 하며, 빗나간 탄환도 발사·소비된다. 보드 끝에서 바깥을 바라봐 유효한 전방 타일이 없을 때는 Fire Point 기준 사거리 거리의 전방 위치를 시각적 끝점으로 사용한다.
  * Spawn Position Offset은 실제 Transform 위치에 더해지므로 X 오프셋은 `Board Distance / 2`보다 작게 유지해야 같은 논리 타일로 판정된다. 일반적으로 세로 보정용 Y 값만 설정한다.
  * 한 웨이브의 총 적 수는 플레이어가 점유한 타일을 제외한 빈 보드 타일 수 이하여야 한다. 초과하거나 항목의 프리팹·EnemyData·수량이 유효하지 않으면 해당 웨이브를 생성하지 않고 오류를 출력한다.
  * 첫 웨이브는 시작 시 즉시 생성되므로 Warning이 표시되지 않는다. `Spawn Term = 0`인 이후 웨이브도 Warning 활성화와 스폰이 같은 프레임에 처리된다.
  * MeleeAttack이 하나뿐이면 같은 행동을 최대 `Max Queued Attacks`까지 반복 등록하고, 여러 개면 목록 순서를 순환한다. Range는 등록된 RangedAttack을 목록 순서대로 최대 수량까지 한 번씩 사용한다. 이동용 `Approach`와 `Retreat`은 해당 타입에서 가장 먼저 발견한 행동을 사용한다.

### Human Modifications

사용자가 직접 생성한 뒤 이름을 `Test Enemy.prefab`으로 변경한 적 프리팹과 `Test Enemy`, `New Enemy Action`, `New Enemy Attack` 에셋은 삭제하거나 새 에셋으로 교체하지 않았다.

Enemy 프리팹에는 Codex가 `EnemyController`를 추가하고 기존 `Test Enemy`, SpriteRenderer, `HP_Value`를 연결했다. Player 프리팹에는 `PlayerHealth`를 추가하고 사용자 Scene의 기존 HUD 오브젝트를 직렬화 참조로 연결했다. 테스트 데이터의 수치와 행동 목록은 사용자가 원하는 밸런스로 설정할 수 있도록 변경하지 않았다. `Stage 1`의 기존 테스트 적 2마리 설정은 `Waves[0] > Enemies[0]`의 `Test Enemy.prefab`, `Count = 2`로 이관했다.

### Final Result

#### Enemy 데이터 설정

Melee 적은 `Enemy Data > Behavior Type`을 `Melee`로 설정하고 `Preferred Distance`를 추가 공격을 Queue에 등록할 거리이자 공격 후 확보할 거리로 설정한다. `Actions`에는 다음 에셋이 필요하다.

* `Action Type = Approach`, `Movement Distance`는 한 번의 이동 행동으로 전진할 칸 수
* `Action Type = MeleeAttack`, `Attack Data`에는 사거리가 1 이상인 `EnemyAttackData`. 행동 에셋이 하나면 반복 등록하고 여러 개면 등록 순서대로 순환한다.

Range 적은 `Behavior Type`을 `Range`로 설정하고 다음 행동 에셋을 `Actions`에 등록한다.

* `Action Type = Approach`, `Movement Distance`는 사거리 밖에서 전진할 칸 수
* `Action Type = RangedAttack`, `Attack Data`에는 원하는 유효 `Range`가 설정된 `EnemyAttackData`. 연속 공격은 이 타입의 행동 에셋을 원하는 순서로 최대 3개 등록한다.

`Range` 값이 0이면 같은 타일 외에는 유효 공격 대상이 없으므로 일반적인 근거리 공격은 1, 원거리 공격은 2 이상의 값을 권장한다. 공격 시각 효과가 필요하면 `EnemyAttackData > Attack Effect Prefab`을 연결한다.

#### Enemy 프리팹 설정

기존 `Assets/Prefabs/Enemy/Test Enemy.prefab`에는 이미 `EnemyController`가 추가되어 있다.

* `Enemy Data`: 해당 프리팹이 사용할 `EnemyData`
* `Sprite Renderer`: 적 외형을 표시할 SpriteRenderer
* `Health Fill Image`: 프리팹의 `Canvas > HP_Bar > HP_Value` Image
* `Canvas Transform`: 프리팹의 자식 `Canvas` RectTransform
* `Actor Motion`: 같은 루트에 추가한 `ActorMotion`
* `Action Queue UI`: 같은 루트의 `EnemyActionQueueUI`
* `Attack Queue > Max Queued Attacks`: 연속 등록 상한, 1~3, 기본값 3
* `Attack Queue > Queued Action Interval`: 준비된 공격 행동 사이의 실행 간격, 기본값 0.2초
* `Attack Queue > Melee Additional Attack Chance`: 첫 MeleeAttack 이후 추가 공격을 등록할 확률, 0~1, 기본값 0.5. 등록하지 않으면 해당 턴에는 플레이어에게 접근한다.
* `Runtime State > Current Health`, `Queued Attack Actions`, `Is Queue Created`, `Is Attack Prepared`, `Is Retreating`, `Last Turn Action`, `Is Acting`: Play Mode에서 현재 상태를 확인하는 필드

같은 루트의 `EnemyActionQueueUI`에는 다음 참조를 연결한다.

* `Queue Image`: 자식 `Image | Queue`의 Image
* `Icon Parent`: 자식 `Image | Queue`의 RectTransform
* `Attack Icon Prefab`: `Assets/Prefabs/UI/Image _ Queue AttackIcon.prefab`
* `Normal Queue Color`: 일반 등록 상태의 Queue 색상
* `Prepared Queue Color`: 공격 준비 상태의 Queue 색상, 기본값 주황색 `(1, 0.5, 0, 1)`
* `Missing Icon Color`: 행동 아이콘 Sprite가 null일 때 표시할 색상

새 적 프리팹을 만들 때도 루트에 `EnemyController`와 `ActorMotion`을 추가한다. `ActorMotion > Actor Transform`에는 적 루트, `Orientation Locked Transform`에는 Canvas를 연결한다. `Move Duration`, `Jump Height`, `Rotate Duration`으로 타일당 이동 시간, 점프 높이, 회전 시간을 설정한다. `EnemyData > Prefab`에도 완성된 적 프리팹을 등록해 데이터 에셋의 프리팹 참조를 유지한다.

#### PlayerHealth 설정

Player 프리팹 루트에는 `PlayerHealth`가 추가되어 있다.

* `Max Health`: 플레이어 최대 체력, 기본값 100
* `Starting Health`: 게임 시작 체력, 기본값 100이며 Max Health 안으로 제한됨
* `Health Fill Image`: `Canvas > Panel | MainGame > Player HP > Image | Fill Amount`
* `Health Text`: `Canvas > Panel | MainGame > Player HP > Text | Player HP`

`Stage 1`의 두 UI 참조는 이미 Player 프리팹 인스턴스에 연결되어 있다. 피해를 받으면 Fill Amount는 `Current Health / Max Health`, 텍스트는 `CurrentHealth/MaxHealth` 형식으로 갱신된다.

#### WaveManager 설정

Scene에 빈 GameObject `@_WaveManager`와 선택적인 적 부모 Transform `@_Enemies`를 만든 뒤 `WaveManager`를 추가한다.

* `Waves`: 진행 순서대로 배치하는 웨이브 배열
* `Waves > Enemies`: 해당 웨이브에서 함께 생성할 적 구성 배열
* `Enemy Prefab`: `EnemyController`와 유효한 `EnemyData`가 연결된 적 프리팹
* `Count`: 해당 프리팹을 이 웨이브에 생성할 수량, 1 이상
* `Spawn Position Offset`: 타일 중앙 위치에 더할 월드 좌표 오프셋, `Stage 1` 설정값 `(0, 0.7, 0)`
* `Spawn Term`: 현재 웨이브 전멸 후 다음 웨이브까지 기다릴 플레이어 소비 턴 수, 기본값 2
* `Enemy Turn Delay`: 플레이어 행동 완료 후 첫 적 행동까지 기다릴 시간, `Stage 1` 기본값 0.35초
* `Enemy Action Interval`: 한 적의 행동 완료 후 다음 적 행동까지 기다릴 시간, `Stage 1` 기본값 0.15초
* `Board Manager`: Scene의 `@_BoardManager`
* `Player Move`: Player 오브젝트의 `PlayerMove`
* `Player Health`: Player 오브젝트의 `PlayerHealth`
* `Enemy Parent`: 생성된 적을 정리할 `@_Enemies` Transform
* `Main Game Panel`: `Canvas > Panel | MainGame`
* `Stage Clear Panel`: `Canvas > Panel | Stage Clear`
* `Active Enemies`: Play Mode에서 현재 활성 적을 확인하는 런타임 목록
* `Current Wave Index`: 현재 진행 중인 웨이브의 0 기반 인덱스. 시작 전에는 -1
* `Remaining Spawn Turns`: 현재 웨이브 전멸 후 다음 웨이브까지 남은 소비 턴 수
* `Is Waiting For Next Wave`: 현재 웨이브를 전멸시키고 다음 웨이브를 기다리는지 여부
* `Is Stage Cleared`: 모든 웨이브를 완료했는지 여부

`WaveManager`는 시작 시 첫 웨이브 전체를 즉시 생성한다. 현재 웨이브의 모든 적이 제거되면 `Spawn Term` 카운트다운을 시작하고 이후 `PlayerMove.TurnCompleted`마다 1씩 감소시킨다. 값이 0이 되면 다음 웨이브 전체를 생성한다. `Spawn Term`이 0이면 전멸 시점에 다음 웨이브가 즉시 생성된다. 마지막 웨이브를 전멸시키면 카운트다운 없이 Stage Clear UI로 전환한다. `Waves`가 비어 있어도 시작 시 완료된 스테이지로 처리한다.

한 웨이브에는 서로 다른 적을 여러 항목으로 섞을 수 있다. 예를 들어 `Enemies[0] = Melee Prefab, Count 2`, `Enemies[1] = Range Prefab, Count 1`이면 해당 웨이브 시작 프레임에 세 마리가 서로 다른 빈 타일에 생성된다. 웨이브 총 수량은 플레이어 타일을 제외한 사용 가능한 보드 타일 수 이하여야 한다.

#### Tile Warning 설정

`Assets/Prefabs/Tiles/Tile.prefab` 루트에는 `BoardTile`이 추가되어 있다.

* `Warning Object`: Tile 자식의 `Warning` GameObject
* `Warning`: 프리팹 기본 상태는 비활성
* `BoardManager > Tile Prefab`: GameObject가 아니라 Tile 프리팹 루트의 `BoardTile` 컴포넌트 참조

현재 `Stage 1`의 `BoardManager > Tile Prefab`과 Tile 프리팹의 `Warning Object` 참조는 연결되어 있다. 다음 웨이브 대기 중 `WaveManager > Reserved Spawn Tile Indices`에서 실제 예약된 0 기반 타일 인덱스를 확인할 수 있다.

Player의 `PlayerShoot > Wave Manager`에도 Scene의 `@_WaveManager`를 연결한다. 발사 시 WaveManager가 정면 사거리 안의 적을 가까운 순서로 제공하며, 유효한 적이 없어도 최대 유효 사거리 타일까지 발사한다.

#### Play Mode 테스트 순서

1. Melee용 `Approach`, `MeleeAttack`, `EnemyAttackData`를 만들고 Melee `EnemyData.Actions`에 연결한다.
2. Range용 `Approach`, `RangedAttack`, `EnemyAttackData`를 만들고 Range `EnemyData.Actions`에 연결한다.
3. 각 Enemy 프리팹의 `Enemy Data`, `Sprite Renderer`, `Health Fill Image`, `Canvas Transform`, `Actor Motion`을 확인하고 Actor Motion에 루트와 Canvas가 연결됐는지 확인한다.
4. Player의 `PlayerHealth`에서 `Health Fill Image`, `Health Text`를 확인한다.
5. `@_WaveManager`의 `Waves`에 웨이브 순서대로 `Enemies > Enemy Prefab, Count`를 설정하고 `Board Manager`, `Player Move`, `Player Health`, `Enemy Parent`, `Main Game Panel`, `Stage Clear Panel`을 연결한다.
6. PlayerShoot의 `Wave Manager`를 연결한다.
7. Play Mode 시작 직후 첫 웨이브에 설정한 모든 적이 오프셋이 적용된 서로 다른 무작위 타일에 한 번에 생성되는지 확인한다.
8. 플레이어와 적이 서로가 있는 타일로 이동할 수 없고 적끼리도 겹치지 않는지 확인한다.
9. 정면 사거리 안에 적이 있을 때 LineRenderer가 마지막 적중 적까지 표시되고 적 HP_Value가 감소하는지 확인한다.
10. 관통 확률이 성공하면 다음 적도 피해를 받고, 실패하면 해당 적 앞에서 적중이 종료되는지 확인한다.
11. 유효한 적이 없을 때 최대 유효 사거리 타일까지 발사선이 표시되고 탄환과 설정에 따른 턴이 소비되는지 확인한다.
12. Melee와 Range 적의 공격으로 Player HP Fill과 텍스트가 함께 감소하는지 확인한다.
13. Melee가 발사한 뒤 `Is Retreating`이 켜지고 Preferred Distance 이상이 될 때까지 플레이어 반대 방향으로 이동하는지 확인한다.
14. Enemy가 좌우로 회전해도 HP Canvas와 Fill 방향이 뒤집히지 않는지 확인한다.
15. A, D, S, 회전, 정상 장전, 턴을 소비하는 발사를 실행하고 적마다 `Last Turn Action`이 한 번만 변경되는지 확인한다.
16. 실패한 이동 및 장전, 미장전 발사, `doesNotConsumeTurn` 탄환 발사에는 적 상태 및 Spawn Term이 진행되지 않는지 확인한다.
17. 플레이어 이동과 회전이 폴짝 이동 및 Scale 보간으로 완료된 뒤 `Enemy Turn Delay`만큼 지나 첫 적이 행동하는지 확인한다.
18. 적들이 `Enemy Action Interval` 간격으로 목록 순서대로 행동하고, 이동·회전 중에는 다음 적이 시작하지 않는지 확인한다.
19. 웨이브의 일부 적만 제거했을 때 카운트다운이 시작되지 않고, 전멸시킨 뒤에만 `Spawn Term`이 감소하며 0이 되는 턴에 다음 웨이브 전체가 한 번에 생성되는지 확인한다.
20. Melee가 플레이어를 바라보지 않으면 회전만 하고, 다음 적 턴에 `Image | Queue`만 활성화하는지 확인한다.
21. Queue 생성 다음 턴에 거리와 관계없이 첫 MeleeAttack 아이콘이 반드시 하나 등록되는지 확인한다.
22. 거리가 2칸 이상이면서 Preferred Distance 이내일 때 턴마다 50% 확률로 공격 아이콘이 하나 추가되거나 플레이어 쪽으로 접근하는지 반복 확인한다.
23. MeleeAttack 에셋이 하나일 때 같은 스킬이 반복되고 여러 개일 때 목록 순서가 순환하는지 확인한다.
24. 거리가 1칸이면 Queue가 3개 미만이어도 준비 턴이 별도로 종료되고 `Image | Queue`가 주황색으로 변경되는지 확인한다.
25. 바로 다음 적 턴에 큐의 공격이 `Queued Action Interval` 간격으로 순차 발동하고 아이콘이 앞에서부터 제거되는지 확인한다.
26. 마지막 공격 후 `Image | Queue`가 비활성화되고 Melee가 Preferred Distance까지 후퇴한 뒤 Queue 생성부터 반복하는지 확인한다.
27. 마지막 웨이브의 마지막 적을 제거하는 즉시 `Panel | Stage Clear`가 활성화되고 `Panel | MainGame`이 비활성화되는지 확인한다.
28. 한 웨이브의 수량을 빈 보드 타일보다 크게 설정했을 때 일부 적만 생성되지 않고 Console에 설정 오류가 출력되는지 확인한다.
29. 첫 웨이브를 전멸시키면 다음 웨이브 적 수와 같은 수의 Tile `Warning`이 활성화되는지 확인한다.
30. Warning 타일로 플레이어가 이동할 수 없고, `Spawn Term`이 끝나면 표시된 위치마다 적이 생성된 뒤 Warning이 모두 비활성화되는지 확인한다.

C# 컴파일은 경고와 오류 없이 완료됐다. Unity Play Mode를 통한 실제 스폰, 상태 진행, 이펙트 출력 검증은 수행하지 않았다.

### Lessons Learned

동시 턴 규칙을 유지하면서 시각 연출을 순차적으로 보여주려면 플레이어의 유효 행동 하나에 대해 각 적의 상태는 정확히 한 단계만 진행시키되, 중앙 `WaveManager`가 각 행동의 완료를 기다려야 한다. 입력 잠금도 첫 지연부터 마지막 적 행동까지 유지해야 Coroutine 도중 새 플레이어 턴이 겹치지 않는다.

장전과 공격 준비를 분리하면 플레이어에게 공격을 예고할 턴이 생긴다. 준비 상태를 별도 런타임 값으로 두고 공격 타일 자체는 유지해야, 플레이어가 사거리 밖으로 피했을 때 다시 장전하지 않고 추적을 계속할 수 있다.

체력 변경과 UI 갱신을 하나의 컴포넌트가 함께 관리하면 공격 주체가 UI 계층을 알 필요가 없다. 적은 `PlayerHealth.ApplyDamage`만 호출하고 PlayerHealth가 Fill Amount와 텍스트를 일관되게 갱신한다.

한 줄 전투에서는 물리 충돌 대신 보드 타일 인덱스로 방향과 거리를 비교하는 것이 더 명확하다. 동일한 타일 인덱스를 점유 검사, 탄환 대상 정렬, 스폰 제외에 공통으로 사용하면 겹침과 적중 순서 불일치를 줄일 수 있다.

공통 `ActorMotion`에서 이동 경로를 타일 단위로 나누면 여러 칸 이동에도 각 타일마다 같은 `sin(πt)` 점프를 반복할 수 있다. 회전 중 부모 Scale이 0을 지나 반전될 때 Canvas에도 같은 부호를 매 프레임 적용하면 UI의 월드 좌우 방향은 유지된다.

턴 기반 게임의 스폰 지연은 초 단위 Coroutine보다 `TurnCompleted`에서 카운트다운을 감소시키는 편이 일시정지와 입력 실패 규칙을 자연스럽게 따른다. 다음 웨이브 대기 상태를 별도로 두면 현재 적 수가 최대치보다 적다는 이유로 웨이브 도중 적이 보충되는 문제도 막을 수 있다. 시각 오프셋은 논리 타일 선택 이후에 적용해야 무작위 스폰과 점유 판정 규칙을 그대로 유지할 수 있다.
