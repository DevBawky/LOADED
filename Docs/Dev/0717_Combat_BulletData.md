## AI-003: BulletData

> 260718 후속 변경: 상점용 `Price`, `Grade`, `Bullet Icon`, `Cylinder Icon`과 기존 `sprite` 직렬화 이전 규칙은 `0718_Shop_Reward_StageSystem.md`를 기준으로 한다.

> 260720 후속 변경: 크리티컬 확률은 플레이어 공통 스탯에서 탄환의 레벨별 스펙으로 이동했다. `Effects` 대상 지정, 조건부 이벤트와 자원 계열 이벤트의 현재 구조는 `0718_Combat_BulletEffects.md`를 기준으로 한다.

### Basic Information

* Date: 260717
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Combat

### Problem

탄환의 공격력, 사거리, 상태 효과, 관통 확률, 트레일 표현을 한 곳에서 관리할 데이터 구조가 필요했다.

특히 관통을 단순한 bool 또는 고정 최대 적중 수로 표현하지 않고, 현재 적을 맞힌 뒤 다음 적까지 진행할 확률을 단계별로 설정할 수 있어야 했다. 또한 기존 Bullet 프리팹의 `TrailRenderer`에 탄환별 Material과 Color를 적용하되 공유 에셋의 원본 데이터는 변경하지 않아야 했다.

### Why AI Was Used

관통 확률 리스트의 인덱스와 실제 적중 순서가 어긋나지 않도록 규칙을 정리하고, 첫 번째 적의 확정 적중과 이후 단계별 관통 판정을 재사용 가능한 API로 구현하기 위해 AI를 사용했다.

또한 기존 프로젝트의 `ScriptableObject` 작성 방식과 Inspector 직접 참조 방식을 유지하면서, 아직 구현되지 않은 발사 로직이 이후 연결할 수 있는 최소 투사체 초기화 지점을 구성하기 위해 도움을 받았다.

### Main Instructions

탄환 데이터를 `ScriptableObject`로 구현해주세요.

* 탄환 ID, 이름, 설명, 스프라이트를 저장합니다.
* 공격력, 탄환별 크리티컬 피해 배율과 최대 사거리 1~10칸을 저장합니다.
* 독, 기절, 표식, 밀치기, 위치 교환, 흡혈, 약화는 `Effects` 배열 항목으로 저장합니다.
* 각 효과에는 0~100% 발동 확률을 두고 디버프 스택과 밀치기 거리를 타입에 맞게 사용합니다.
* 표식 피해 증가는 탄환별 변수가 아닌 상태 시스템의 고정 50% 규칙을 사용합니다.
* Trail Material과 Trail Color를 저장합니다.
* 관통은 bool이나 고정된 `maxHitCount`가 아니라 `PenetrationChanceData` 리스트로 관리합니다.
* 첫 번째 적은 반드시 적중하고, 각 리스트 값은 현재 적을 맞힌 뒤 다음 적까지 관통할 확률로 사용합니다.
* 최대 적중 가능 수는 별도 필드 없이 관통 확률 리스트 개수 + 1로 계산합니다.
* 투사체가 탄환 데이터를 받으면 발사 전에 `TrailRenderer.Clear()`를 호출하고 Trail Material과 Trail Color를 적용합니다.
* `TrailRenderer`는 Inspector에서 직접 연결하며 런타임 자동 탐색을 사용하지 않습니다.
* 공유 Material Asset과 `ScriptableObject` 원본 데이터를 런타임 중 수정하지 않습니다.
* 조준, TargetTile, 폭발, 도탄, 분열, 당기기 기능은 추가하지 않습니다.
* 기존 투사체 또는 발사 구조가 없다면 이후 `PlayerShoot`이 호출할 수 있는 `Initialize(BulletData)` 진입점까지만 구현합니다.

작업 후 수정된 파일, 관통 확률 동작, 트레일 적용 방식, Inspector 연결 항목과 컴파일 오류 여부를 설명해주세요.

### Output Summary

> 260718 후속 변경: 개별 `Knockback Distance`, `Stun Duration Turns`, `Mark Duration Turns`, `Poison Duration Turns`, `Mark Damage Multiplier` 필드는 `Effects` 배열로 대체했다. 이후 탄환별 `Critical Damage Multiplier`와 `LifeSteal`, `Weakness` 효과를 추가했다. 현재 구조와 적용 규칙은 `0718_Combat_BulletEffects.md`를 기준으로 한다.

> 260720 후속 변경: 기본 레벨과 +1~+3의 `BulletLevelData`에 각각 `Critical Chance`와 `Conditional Events`를 추가했다. 상세 툴팁은 `크리티컬 확률: {n}%` 형식으로 현재 레벨의 값을 표시한다.

`Assets/Scripts/Bullet/BulletData.cs`에 `PenetrationChanceData`, `BulletEffectData`, `BulletConditionalEventData`, `BulletData`를 구현했다. `BulletData`는 요청된 식별 정보, 전투 수치, 0~100으로 제한되는 크리티컬 확률, 최소 1로 보정되는 크리티컬 배율, 효과 및 조건부 이벤트 배열, 관통 확률 리스트, Line Material과 색상을 직렬화 필드로 보관하며 읽기 전용 프로퍼티로 제공한다.

`MaxRange`에는 1~10, 각 관통 및 효과 발동 확률에는 0~100의 Inspector 범위 제한을 적용했다. `MaxHitCount`는 별도 직렬화 필드를 만들지 않고 `penetrationChances.Count + 1`로 계산한다.

관통 판정은 현재까지 적중한 적 수에서 1을 뺀 값을 리스트 인덱스로 사용한다. `RollPenetrationAfterHit`은 런타임 확률 판정을 제공하고, `CanPenetrateAfterHit`은 외부에서 전달한 0 이상 100 미만의 roll 값으로 동일한 규칙을 검증할 수 있도록 구성했다.

`Assets/Scripts/Bullet/BulletProjectile.cs`에는 `Initialize(BulletData)`를 구현했다. 초기화 시 Inspector에서 할당된 `TrailRenderer`를 먼저 Clear하고, 전달된 데이터의 `TrailMaterial`을 `sharedMaterial` 참조에 할당한 뒤 `TrailColor`를 시작 색상과 끝 색상에 적용한다. Material의 속성이나 `BulletData`의 필드는 변경하지 않는다.

기존 `Assets/Prefabs/Player/Bullet.prefab` 루트에 `BulletProjectile`을 추가하고 자식 Trail 오브젝트의 `TrailRenderer`를 직렬화 참조로 직접 연결했다. `PlayerShoot`은 빈 플레이스홀더 상태이고 실제 발사 흐름이 없으므로 이번 범위에서는 수정하지 않았다.

### Decision

* [x] 그대로 채택
* [ ] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * `BulletData`의 모든 요청 필드와 Inspector 범위 속성 확인
  * 모든 기존 BulletData 에셋에 `Critical Damage Multiplier = 2`가 직렬화됐는지 확인
  * 관통 리스트 인덱스 계산과 `MaxHitCount` 계산식 정적 검토
  * 빈 리스트, `[100]`, `[100, 50]`에 대한 적중 순서 대입 확인
  * `BulletProjectile.Initialize`의 null 방어, Clear 호출 순서, Material 및 Color 적용 코드 확인
  * Bullet 프리팹 직렬화 데이터에서 `BulletProjectile` 컴포넌트와 `TrailRenderer` 직접 참조 확인
  * 새 스크립트 두 개를 빌드 입력에 포함한 뒤 `dotnet build LOADED.slnx --no-restore`로 C# 컴파일 확인
  * Unity Play Mode에서의 실제 발사와 트레일 출력은 수동 검증 항목으로 남김

* 테스트 결과:

  * 빈 관통 리스트에서는 첫 번째 적만 맞힐 수 있고 `MaxHitCount`는 1이다.
  * `[100]`에서는 첫 번째 적 적중 후 다음 적까지 확정 관통하며 `MaxHitCount`는 2이다.
  * `[100, 50]`에서는 두 번째 적까지 확정 적중하고, 두 번째 적 적중 후 세 번째 적까지는 50% 확률로 관통하며 `MaxHitCount`는 3이다.
  * 리스트 범위를 벗어난 단계와 null인 리스트 항목은 관통 실패로 처리된다.
  * `Initialize`은 Trail을 Clear한 뒤 공유 Material 참조와 인스턴스별 Trail 색상을 설정하며, 공유 Material의 속성과 `BulletData` 원본을 수정하지 않는다.
  * Bullet 프리팹의 `trailRenderer` 필드는 자식 Trail 오브젝트의 `TrailRenderer`를 참조한다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode 실행을 통한 실제 발사, 관통, 트레일 출력 검증은 수행하지 않았다.

* 발견한 문제:

  * 기존 `PlayerShoot`에는 발사 코드가 없고 Bullet 프리팹에도 동작 스크립트가 없었다. 따라서 즉시 발사, 충돌 감지, 피해 적용까지 확장하지 않고 `Initialize(BulletData)` 진입점까지만 구현했다.
  * Unity가 생성한 C# 프로젝트 파일에 새 스크립트가 즉시 포함되지 않아, 컴파일 검증 시 두 파일을 빌드 입력에 명시적으로 포함했다. 검증 후 생성 프로젝트 파일 변경은 원상 복구했다.
  * 실제 적 충돌 시스템이 없어 관통 확률에 따른 다중 적중은 코드 규칙과 계산 결과만 검증했다.

### Human Modifications

사용자가 직접 수정한 코드는 없다.

기존에 생성되어 있던 Bullet 프리팹과 자식 Trail 오브젝트를 유지하고, Codex가 루트에 `BulletProjectile` 컴포넌트를 추가해 기존 `TrailRenderer` 참조를 연결했다.

실제 발사 구조가 없는 상태이므로 `PlayerShoot`에 임의의 발사 입력이나 프리팹 생성 로직을 추가하지 않았다. 이후 발사 시스템에서는 Bullet 프리팹을 생성한 뒤 `BulletProjectile.Initialize(BulletData)`를 호출하면 된다.

### Final Result

탄환별 식별 정보, 전투 수치, 상태 효과, 관통 단계, Trail Material과 Trail Color를 하나의 `BulletData` 에셋으로 관리할 수 있게 되었다.

첫 번째 적은 관통 판정 없이 적중 대상으로 처리하며, 이후 관통은 적중한 적 수에 대응하는 확률 리스트 값으로 판정한다. 최대 적중 가능 수는 항상 리스트 개수 + 1로 계산되어 별도 필드와 불일치할 가능성이 없다.

Bullet 프리팹은 `Initialize(BulletData)` 호출 시 기존 Trail을 지우고 데이터의 Material 참조와 Color를 적용할 수 있다. 필요한 `TrailRenderer` 참조는 프리팹 Inspector 데이터에 직접 연결되어 있으며 런타임 자동 탐색은 사용하지 않는다.

C# 빌드에서 경고와 오류가 발생하지 않았다. Unity Play Mode에서 실제 발사와 시각 결과를 확인하는 작업은 별도로 필요하다.

### Lessons Learned

관통 확률 리스트는 각 값이 몇 번째 적의 적중 확률인지가 아니라, 현재 적을 맞힌 뒤 다음 적까지 진행할 확률이라는 점을 명확히 해야 인덱스 해석 오류를 피할 수 있다.

최대 적중 수처럼 다른 데이터에서 바로 계산할 수 있는 값은 중복 저장하지 않아야 Inspector 값 불일치와 밸런스 수정 실수를 줄일 수 있다.

공유 Material을 사용하는 시각 효과에서는 Material 속성을 직접 변경하지 않고 Renderer의 참조와 인스턴스별 색상 값을 설정해야 다른 탄환이나 에셋 원본에 의도하지 않은 변경이 전파되지 않는다.

다음 작업에서는 실제 적 충돌 순서, 사거리 종료 조건, 피해와 상태 효과 적용 주체, 발사 행동의 턴 소비 시점을 함께 정의해야 한다.
