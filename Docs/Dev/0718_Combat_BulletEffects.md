## AI-008: Custom Bullet Effects

### Basic Information

* Date: 260718
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Bullet / Critical / Debuff / Push / Position Swap

### Goal

탄환마다 독, 기절, 표식, 밀치기, 위치 교환, 흡혈, 약화를 원하는 조합으로 설정하고 각 효과의 발동 확률을 독립적으로 조정할 수 있게 한다. 탄환별 크리티컬 배율과 플레이어 공통 크리티컬 확률도 같은 발사 흐름에 연결한다.

### Data Structure

`BulletData > Effects`에 `BulletEffectData` 항목을 원하는 수만큼 추가한다.

| 필드 | 의미 |
| --- | --- |
| `Effect Type` | `Poison`, `Stun`, `Mark`, `Knockback`, `PositionSwap`, `LifeSteal`, `Weakness` 중 하나 |
| `Activation Chance` | 해당 적중 대상에게 효과가 발동할 확률. `0`은 발동하지 않고 `100`은 확정 발동 |
| `Stack Count` | 독, 기절, 표식, 약화가 성공했을 때 추가할 스택 수 |
| `Knockback Distance` | 밀치기가 성공했을 때 최대 이동할 타일 수 |

`Stack Count`는 디버프 4종에서만 사용하고, `Knockback Distance`는 밀치기에서만 사용한다. 위치 교환과 흡혈은 두 수치를 사용하지 않는다.

`BulletData > Critical Damage Multiplier`는 해당 탄환의 크리티컬 피해 배율이며 기본값은 `2`다. `Player > PlayerShoot > Critical Chance`는 모든 탄환에 공통으로 적용되는 0~100% 발동 확률이며 기존 동작 보존을 위한 프리팹 기본값은 `0`이다.

Enum 값은 에셋 직렬화 호환성을 위해 다음 값으로 고정한다.

```text
Poison       = 0
Stun         = 1
Mark         = 2
Knockback    = 3
PositionSwap = 4
LifeSteal    = 5
Weakness     = 6
```

새 효과 타입을 추가할 때는 기존 값의 순서를 바꾸지 않고 마지막에 추가한다.

### Resolution Rules

한 발의 처리 순서는 다음과 같다.

1. 사거리와 관통 확률로 적중 대상 목록을 확정한다.
2. 발사에 성공한 탄환마다 크리티컬을 한 번 판정한다. 관통으로 맞은 모든 대상은 이 결과를 공유한다.
3. 각 대상의 직접 피해를 `기본 피해 -> 크리티컬 -> 공격자 약화 -> 대상 표식` 순으로 계산하고 실제 체력 감소량을 기록한다.
4. `Effects`를 위에서 아래 순서대로 처리하며, 각 효과 항목은 각 적중 대상마다 별도의 확률을 판정한다.
5. 직접 피해로 처치된 대상에는 흡혈만 처리하고 다른 디버프 및 공간 효과는 건너뛴다.
6. 밀치기와 위치 교환 모션이 끝날 때까지 기다린 뒤 다음 효과 또는 다음 탄환으로 진행한다.

동일한 효과를 배열에 여러 번 넣을 수 있다. 각 항목은 독립 판정되며 성공한 독, 기절, 표식, 약화 스택은 모두 누적된다. 흡혈 항목을 여러 개 넣으면 성공한 항목마다 직접 피해량을 각각 회복한다. 관통 탄환도 모든 적중 대상에게 같은 배열을 적용한다.

공간 효과 역시 배열 순서대로 실행한다. 예를 들어 `Knockback -> PositionSwap`은 밀쳐진 최종 위치의 적과 교환하고, `PositionSwap -> Knockback`은 교환된 뒤의 적 위치에서 발사 방향으로 밀친다. 발사 방향은 해당 연속 발사를 시작할 때 플레이어가 바라보던 방향을 유지한다.

### Critical Rule

`PlayerShoot.Critical Chance`가 성공하면 해당 탄환의 직접 피해에 `BulletData.Critical Damage Multiplier`를 곱하고 올림한다. 배율은 최소 `1`로 보정한다. 한 탄환의 관통 대상들은 모두 같은 크리티컬 결과를 사용하지만, 실린더의 다음 탄환은 다시 독립 판정한다.

크리티컬은 독 피해와 밀치기 충돌 피해에는 적용하지 않는다. 크리티컬 결과가 약화의 30% 감소를 거친 뒤 대상의 표식 1.5배가 적용된다.

### Mark Rule

표식이 있는 대상이 일반 공격 피해를 받으면 피해가 항상 1.5배로 올림 처리된다. 탄환별 `Mark Damage Multiplier`는 존재하지 않는다.

탄환 기본 피해를 먼저 적용하고 효과를 나중에 적용하므로 표식을 새로 부여한 탄환은 자기 피해를 증폭하지 않는다. 같은 실린더에서 이후에 발사되는 탄환부터 고정 50% 추가 피해를 받는다. 독 피해와 밀치기 충돌 피해는 일반 공격 피해가 아니므로 표식 배율을 적용하지 않는다.

### Knockback Rule

탄환 밀치기는 플레이어 직접 밀치기와 같은 경로 및 충돌 처리를 사용한다.

* 발사 방향으로 `Knockback Distance`만큼 진행하거나 보드 끝에서 멈춘다.
* 이동 범위 안에서 다른 적을 만나면 그 적의 앞 타일이 최종 정지 위치가 된다.
* 충돌 접점까지 한 번에 날아간 뒤 최종 타일로 떨어진다.
* 충돌한 두 적은 각각 자신의 최대 체력에 대한 `Push Collision Damage Ratio`만큼 피해를 받는다.
* 비행 시간과 충돌 연출은 Player 프리팹 `PlayerMove`의 기존 Push 설정을 공유한다.
* 탄환 밀치기에서는 플레이어 Avatar 충돌 모션, 플레이어 위치 이동, 직접 밀치기 쿨다운 변경이 발생하지 않는다.

### Position Swap Rule

위치 교환은 명중한 적이 기본 피해 후 생존했을 때 플레이어와 적의 논리 타일을 서로 바꾼다. 플레이어와 적은 동시에 포물선 이동하며 각자의 기존 세로 및 깊이 오프셋을 유지한다.

교환 모션 자체가 별도 턴을 소비하지는 않는다. 전체 장전 탄환 발사가 끝난 뒤 기존 `Does Not Consume Turn` 규칙에 따라 발사 묶음의 턴 소비 여부를 결정한다.

### LifeSteal Rule

흡혈은 지속 상태가 아니라 적중 순간 실행되는 탄환 효과다. 크리티컬, 공격자의 약화, 대상의 표식, 남은 체력 제한까지 모두 반영한 `실제로 감소한 체력`만큼 플레이어를 회복한다. 따라서 남은 체력이 3인 적에게 20 피해를 주어도 회복량은 3이고, 처치 공격에서도 정상적으로 회복한다.

회복은 `PlayerHealth.Heal`을 사용하므로 최대 체력을 넘지 않는다. 독 피해와 밀치기 충돌 피해는 흡혈량에 포함하지 않는다.

### Weakness Rule

약화는 플레이어와 적이 공통으로 사용하는 `StatusEffectType.Weakness`다. 약화 상태에서 직접 공격 피해를 줄 때 최종 공격 피해를 `floor(피해 * 0.7)`로 계산하고 최소 1을 보장한다. 독과 밀치기 충돌 피해에는 적용하지 않는다.

약화 스택은 턴 종료마다 1 감소한다. 플레이어의 연속 탄환 발사와 적의 공격 큐는 각각 하나의 턴 행동이므로 해당 묶음의 모든 직접 공격에 약화가 적용된 뒤 한 번 감소한다. 약화 아이콘도 기존 `Image _ Debuff` 프리팹을 사용하지만 전용 스프라이트 에셋은 아직 없으므로 Player와 Enemy 프리팹의 `Weakness Sprite`에 제작된 스프라이트를 연결해야 한다.

### Inspector Examples

50% 확률로 독 5스택을 부여하는 항목:

```text
Effect Type        : Poison
Activation Chance  : 50
Stack Count        : 5
Knockback Distance : 1 (미사용)
```

30% 확률로 3칸 밀치는 항목:

```text
Effect Type        : Knockback
Activation Chance  : 30
Stack Count        : 1 (미사용)
Knockback Distance : 3
```

독과 표식을 함께 가진 탄환은 `Effects` Size를 2로 만들고 각 타입, 확률, 스택을 별도로 설정한다.

크리티컬과 흡혈을 사용하는 예시는 다음과 같다.

```text
PlayerShoot > Critical Chance       : 25
BulletData > Critical Damage Multiplier : 2
Effects[0] > Effect Type            : LifeSteal
Effects[0] > Activation Chance      : 100
```

이 설정은 탄환마다 25% 확률로 직접 피해가 2배가 되며, 적중할 때마다 실제 체력 감소량만큼 플레이어를 회복한다.

### Existing Asset Migration

* 모든 기존 탄환: `Critical Damage Multiplier = 2`
* `Ghost`: 효과 없음, 턴 비소비 설정 유지
* `Pierce`: 효과 없음, 기존 관통 확률 유지
* `Stun`: `Stun / 70% / 1 Stack`
* `Venom`: `Poison / 70% / 5 Stacks`
* `Power`: `Poison / 100% / 5 Stacks`

### Validation Checklist

1. `Activation Chance = 0`인 효과가 여러 번 적중해도 발동하지 않는지 확인한다.
2. `Activation Chance = 100`인 효과가 매번 발동하는지 확인한다.
3. 독 50%, 5스택 항목이 성공했을 때 정확히 5스택이 추가되는지 확인한다.
4. 같은 디버프 항목을 두 개 추가했을 때 성공한 항목의 스택이 누적되는지 확인한다.
5. 표식을 부여한 탄환의 다음 탄환부터 기본 피해가 1.5배로 적용되는지 확인한다.
6. 탄환 밀치기 중 플레이어 루트와 Avatar가 움직이지 않고 직접 밀치기 쿨다운도 변하지 않는지 확인한다.
7. 밀쳐진 적이 다른 적과 충돌할 때 둘 다 각자 최대 체력 비례 피해를 받는지 확인한다.
8. 위치 교환 후 플레이어와 적의 타일 인덱스가 정확히 바뀌고 같은 타일에 겹치지 않는지 확인한다.
9. 관통 탄환의 모든 생존 대상이 효과별 독립 판정을 수행하는지 확인한다.
10. 공간 효과가 끝나기 전에 다음 탄환이나 적 턴이 시작하지 않는지 확인한다.
11. `Critical Chance = 100`, 배율 2인 관통 탄환의 모든 대상이 같은 크리티컬 결과를 받는지 확인한다.
12. 흡혈이 초과 피해가 아닌 실제 체력 감소량만 회복하고 최대 체력을 넘지 않는지 확인한다.
13. 처치 공격과 효과 배열 뒤쪽의 흡혈도 정상 회복하는지 확인한다.
14. 약화 상태의 플레이어와 적이 직접 공격 피해를 30% 감소시키고 최소 피해 1을 유지하는지 확인한다.
15. 약화가 독·충돌 피해를 줄이지 않으며 턴 종료마다 1스택 감소하는지 확인한다.
16. `Weakness Sprite`를 연결한 뒤 Player와 Enemy의 Status 아래에 아이콘과 남은 스택이 갱신되는지 확인한다.

### Future Effect Ideas

* `Burn`: 고정 수치의 턴 종료 피해
* `Pull`: 발사자 방향으로 강제 이동
* `Chain`: 적중 대상에서 가까운 다른 적에게 연쇄 피해
* `Execute`: 특정 체력 비율 이하의 적에게 추가 피해
* `Ricochet`: 첫 적중 후 반대 방향 또는 무작위 대상에게 재발사
