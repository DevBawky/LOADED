## AI-005: EnemyData

### Basic Information

* Date: 260717
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Enemy Data

### Problem

적의 기본 정보와 행동 패턴, 공격 수치를 Scene 오브젝트나 실행 코드에 직접 작성하지 않고 에셋 단위로 관리할 데이터 구조가 필요했다.

적의 이동과 공격 AI는 아직 구현 범위가 아니므로, 현재 체력이나 현재 행동 인덱스 같은 런타임 상태를 `ScriptableObject`에 저장하지 않으면서 이후 실행 시스템이 읽을 수 있는 고정 데이터 관계만 정의해야 했다.

### Why AI Was Used

기존 프로젝트의 `ScriptableObject` 작성 방식과 읽기 전용 프로퍼티 규칙을 유지하면서, 적 정보와 행동, 공격 데이터를 서로 독립적으로 재사용할 수 있는 참조 구조를 설계하기 위해 AI를 사용했다.

또한 음수가 허용되지 않는 전투 수치의 Inspector 제한, 기존 클래스명 충돌 여부, 구현 범위 밖인 적 AI와 턴 처리의 분리를 함께 점검하기 위해 도움을 받았다.

### Main Instructions

적 데이터를 세 종류의 `ScriptableObject`로 구현해주세요.

* `EnemyData`는 적 ID, 이름, 설명, 스프라이트 또는 프리팹, 최대 체력, 처치 보상, 행동 방식, 선호 거리, 보유 행동 목록, 시작 행동 인덱스 랜덤 여부를 저장합니다.
* 행동 방식은 `Aggressive`, `KeepDistance`, `FixedPattern`, `Stationary` Enum으로만 정의합니다.
* `EnemyActionData`는 행동 타입, 이동 칸 수, 사용할 `EnemyAttackData`, 행동 아이콘, 행동 설명을 저장합니다.
* 행동 타입은 `Approach`, `Retreat`, `Rotate`, `MeleeAttack`, `RangedAttack`, `Wait` Enum으로 정의합니다.
* `EnemyAttackData`는 스킬 ID, 이름, 설명, 공격력, 사거리, 밀어내기 거리, 기절 지속 턴, 표식 지속 턴, 표식 피해 배율, 공격 이펙트를 저장합니다.
* 근거리와 원거리는 별도 bool 없이 `EnemyActionData`의 행동 타입과 공격 사거리로 구분합니다.
* 직렬화 필드는 private으로 유지하고 외부에는 읽기 전용 프로퍼티를 제공합니다.
* 현재 체력, 현재 행동 인덱스 같은 런타임 값과 적 이동, 공격 실행, 행동 선택, 턴 연결은 구현하지 않습니다.
* 런타임 자동 탐색, 불필요한 싱글톤, 별도의 적 AI 프레임워크를 추가하지 않습니다.

작업 후 에셋 생성과 연결 방법, 테스트용 구성 예시, 생성 파일과 컴파일 결과를 기록해주세요.

### Output Summary

`Assets/Scripts/Enemy/EnemyData.cs`를 생성하고 `EnemyBehaviorType` Enum과 `EnemyData`를 구현했다. `EnemyData`는 식별 정보, 선택적으로 사용할 `Sprite`와 `GameObject` 프리팹, 최대 체력, 처치 보상, 행동 방식, 선호 거리, `EnemyActionData` 목록, 시작 행동 인덱스 랜덤 여부를 보관한다.

`Assets/Scripts/Enemy/EnemyActionData.cs`를 생성하고 `EnemyActionType` Enum과 `EnemyActionData`를 구현했다. 행동 에셋은 이동 칸 수와 선택적인 `EnemyAttackData` 참조, 아이콘, 설명을 보관한다. 공격하지 않는 행동은 `Attack Data`를 비워둘 수 있다.

`Assets/Scripts/Enemy/EnemyAttackData.cs`를 생성해 근거리와 원거리에서 공통으로 사용할 공격 데이터를 구현했다. 공격 에셋은 피해와 사거리, 밀어내기, 기절, 표식, 공격 이펙트 프리팹을 보관하며 근거리 여부를 나타내는 별도 bool은 두지 않았다.

모든 직렬화 필드는 private과 `[SerializeField]`를 사용했고 외부에는 읽기 전용 프로퍼티만 제공했다. 최대 체력은 최소 1, 그 외 음수가 허용되지 않는 정수와 표식 피해 배율은 최소 0으로 Inspector 입력을 제한했다. 현재 체력이나 현재 행동 인덱스와 같은 런타임 데이터 및 실행 로직은 추가하지 않았다.

새 스크립트와 Enemy 폴더를 Unity가 안정적으로 추적하도록 해당 `.meta` 파일을 함께 생성했다.

### Decision

* [x] 그대로 채택
* [ ] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * 기존 `BulletData`, `ItemData`의 직렬화 필드와 읽기 전용 프로퍼티 작성 방식 비교
  * 프로젝트 전체에서 `EnemyData`, `EnemyActionData`, `EnemyAttackData`, 관련 Enum 이름의 기존 정의 여부 검색
  * 세 데이터 에셋의 참조 방향과 요청 필드 정적 검토
  * `[Min]` 속성을 통한 음수 입력 제한 확인
  * 새 스크립트 세 개를 빌드 입력에 포함한 뒤 `dotnet build LOADED.slnx --no-restore`로 C# 컴파일 확인
  * Unity Play Mode 실행 및 적 행동 테스트는 이번 데이터 구조 범위에 포함하지 않음

* 테스트 결과:

  * 기존 Enemy 계열 클래스 및 Enum과의 이름 충돌은 발견되지 않았다.
  * `EnemyAttackData`를 `EnemyActionData.AttackData`에 연결하고, 해당 행동 에셋을 `EnemyData.Actions` 목록에 등록할 수 있다.
  * `EnemyData.Actions`는 `IReadOnlyList<EnemyActionData>`로, 나머지 값은 setter가 없는 프로퍼티로 외부에 제공된다.
  * 근거리와 원거리를 구분하는 추가 bool은 없으며 `Action Type`과 `Range` 조합으로 표현한다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode는 실행하지 않았다.

* 발견한 문제:

  * Unity가 생성한 C# 프로젝트 파일에 새 스크립트가 즉시 포함되지 않아, 컴파일 검증 중에만 세 파일을 빌드 입력에 추가했다. 검증 후 생성 프로젝트 파일 변경은 원상 복구했다.
  * 현재 적 실행 시스템이 없으므로 현재 체력, 행동 인덱스, 이동, 공격 실행, 행동 선택, 턴 연결은 검증하거나 임의 구현하지 않았다.
  * `EnemyData`의 `Sprite`와 `Prefab`은 둘 다 선택적으로 제공되며 실제 표시 또는 생성 시 어느 참조를 사용할지는 이후 적 실행 시스템에서 결정해야 한다.

### Human Modifications

사용자가 직접 수정한 코드는 없다.

기존 Scene과 Prefab은 수정하지 않았으며 Inspector 참조도 변경하지 않았다. 이번 작업은 이후 실행 시스템이 사용할 데이터 에셋 타입과 연결 구조만 추가했다.

### Final Result

Unity Project 창에서 `Assets > Create > Loaded > Enemy > Attack`을 선택해 `EnemyAttackData` 에셋을 만든다. `Skill Id`, `Display Name`, `Description`, `Damage`, `Range`, `Knockback Distance`, `Stun Duration Turns`, `Mark Duration Turns`, `Poison Duration Turns`, `Weakness Duration Turns`, `Attack Effect Prefab`을 설정한다. 표식 피해 증가는 상태 시스템의 고정 50% 규칙을 사용하므로 공격별 배율 필드는 없다. `Weakness Duration Turns`가 1 이상이면 적중한 플레이어의 직접 공격 피해가 남은 스택 동안 30% 감소한다.

다음으로 `Assets > Create > Loaded > Enemy > Action`을 선택해 `EnemyActionData` 에셋을 만든다. `Action Type`, `Movement Distance`, `Attack Data`, `Icon`, `Description`을 설정한다. `MeleeAttack` 또는 `RangedAttack` 행동이라면 앞에서 만든 공격 에셋을 `Attack Data`에 드래그한다. `Approach`, `Retreat`, `Rotate`, `Wait`처럼 공격을 사용하지 않는 행동은 `Attack Data`를 비워둘 수 있다.

마지막으로 `Assets > Create > Loaded > Enemy > Enemy`를 선택해 `EnemyData` 에셋을 만든다. `Enemy Id`, `Display Name`, `Description`, `Sprite`, `Prefab`, `Max Health`, `Defeat Reward`, `Behavior Type`, `Preferred Distance`, `Actions`, `Randomize Starting Action Index`를 설정한다. 외형만 필요하면 `Sprite`를, 완성된 적 오브젝트 구성이 있으면 `Prefab`을 연결하며 필요에 따라 둘 다 등록할 수 있다. `Actions`의 Size를 늘린 뒤 실행 순서대로 행동 에셋을 등록한다.

테스트용 근접 적은 다음과 같이 구성할 수 있다.

* 공격 에셋 `Bandit Slash`: `Skill Id`는 `enemy_bandit_slash`, `Damage`는 3, `Range`는 1, `Knockback Distance`는 1, 나머지 상태 효과는 0으로 설정한다.
* 행동 에셋 `Bandit Approach`: `Action Type`은 `Approach`, `Movement Distance`는 1, `Attack Data`는 비워둔다.
* 행동 에셋 `Bandit Melee`: `Action Type`은 `MeleeAttack`, `Movement Distance`는 0, `Attack Data`에는 `Bandit Slash`를 연결한다.
* 적 에셋 `Bandit`: `Enemy Id`는 `enemy_bandit`, `Max Health`는 10, `Defeat Reward`는 5, `Behavior Type`은 `Aggressive`, `Preferred Distance`는 1로 설정하고 `Actions`에 `Bandit Approach`, `Bandit Melee`를 순서대로 등록한다. 고정된 첫 행동부터 시작하려면 `Randomize Starting Action Index`를 끈다.

데이터 구조의 C# 빌드는 경고와 오류 없이 완료됐다. Unity Play Mode에서 에셋 생성 메뉴, Inspector 표시, 실제 적 행동을 확인하는 작업은 별도로 필요하다.

### Lessons Learned

적의 고정 데이터와 현재 체력, 현재 행동 인덱스 같은 실행 상태를 분리해야 여러 적 인스턴스가 하나의 `ScriptableObject`를 안전하게 공유할 수 있다.

공격 수치를 독립된 에셋으로 분리하면 여러 행동이나 적이 같은 공격을 재사용할 수 있고, 행동 목록의 순서만 바꿔 Fixed Pattern 같은 패턴 데이터를 표현할 기반을 만들 수 있다.

행동 방식 Enum은 데이터의 의도를 나타낼 뿐 실제 AI를 자동으로 제공하지 않는다. 다음 작업에서는 런타임 적 상태의 소유자, 행동 선택 규칙, 행동 실행 주체, 턴 완료 시점을 별도 컴포넌트로 명확히 정의해야 한다.
