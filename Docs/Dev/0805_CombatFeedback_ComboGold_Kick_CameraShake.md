# 전투 피드백·처치 콤보·골드·발차기 통합 개발 기록

## 문서 정보

- 작성일: 2026-08-05
- 대상: 공격 등급별 연출, 대미지 텍스트, 한 번의 연속 발사 기준 처치 콤보, 골드 비행 UI, 전투 완료 순서, 발차기·밀치기, 통합 카메라 셰이크
- 관련 선행 문서: `0803_CombatFeedback_FullscreenImpact.md`
- 목적: 최근 추가·수정된 전투 피드백 기능의 최종 동작과 Inspector 조정 지점을 한 문서에서 확인할 수 있도록 정리한다.

## 최종 동작 요약

전투 피드백은 다음 네 단계로 구분된다.

| 우선순위 | 등급 | 판정 기준 | 주요 표현 |
| ---: | --- | --- | --- |
| 1 | 일반 공격 | 크리티컬 및 치명타 조건에 해당하지 않는 명중 | 가벼운 Fullscreen Impact, 명중음, 피해 비율 기반 셰이크 |
| 2 | 크리티컬 공격 | 크리티컬 판정 성공 | 일반 공격보다 강한 화면 효과, Bloom·Lens 계열 Volume Pulse, 짧은 슬로 모션, 크리티컬 사운드 |
| 3 | 치명적인 피해 | 한 발의 실제 적용 피해가 대상 최대 체력의 60% 이상 | 크리티컬보다 우선하여 더 강한 Volume Pulse, 슬로 모션, 충격파·RGB Split·Radial Zoom·Directional Tear |
| 4 | 적 처치 | 해당 피해로 적의 체력이 0 이하가 됨 | 가장 강한 처치 연출, 처치 텍스트, 콤보 연출, 처치음, 보너스 골드 |

`CombatImpactTierUtility.DevastatingDamageRatio`는 `0.6f`이다. 한 공격이 크리티컬이면서 최대 체력의 60% 이상을 가했다면 `Devastating`이 `Critical`보다 우선한다. 적을 처치했다면 표현 등급은 `Defeat`가 된다.

## 공격 등급별 연출

### 일반 공격

- 공격 위치를 중심으로 짧고 약한 Fullscreen Impact를 표시한다.
- 실제 적용 피해량을 대상 최대 체력으로 나눈 비율만큼 카메라를 흔든다.
- 일반 명중용 효과음과 대미지 텍스트를 사용한다.
- 적에게 적용된 피해가 없고 허공에 발사했을 때는 별도의 고정 반동을 사용한다.

### 크리티컬 공격

- 일반 공격보다 Fullscreen Impact의 지속 시간과 강도가 증가한다.
- Bloom, Lens Distortion, Chromatic Aberration, Vignette, Contrast를 묶은 Volume Pulse가 적용된다.
- 기본값 기준 `0.62`배 슬로 모션, `0.025`초 유지, `0.075`초 복귀를 사용한다.
- 크리티컬 전용 효과음과 크리티컬 대미지 프리팹을 사용한다.

### 치명적인 피해

- 실제 적용 피해가 최대 체력의 60% 이상이면 발생한다.
- 크리티컬 여부보다 우선하여 `Devastating` 연출을 선택한다.
- 기본값 기준 `0.5`배 슬로 모션, `0.045`초 유지, `0.11`초 복귀를 사용한다.
- Volume Pulse 강도는 기본 `0.72`이며, 크리티컬의 기본 `0.48`보다 강하다.
- 치명적인 피해 전용 대미지 프리팹이 없으면 크리티컬 프리팹으로 대체한다.

### 적 처치

- 처치 위치에서 처치용 화면 충격, Volume Pulse, 슬로 모션, 효과음과 처치 텍스트를 동시에 시작한다.
- 마지막 적 처치는 슬로 모션과 Fullscreen Impact 지속 시간을 추가로 강화한다.
- 한 번의 연속 발사 안에서 처치 수가 늘어나면 뒤의 처치일수록 연출 강도가 증가한다.
- 전투 완료 이벤트는 연속 발사가 끝나고 마지막 처치 피드백이 발생한 뒤 전달된다.

## 처치 시 대미지 텍스트 선택

처치 여부가 대미지 텍스트의 일반·크리티컬 구분을 덮어쓰지 않도록 구성했다.

- 일반 공격으로 처치: `normalDamagePrefab`
- 크리티컬 공격으로 처치: `criticalDamagePrefab`

`EnemyDamageNumberDisplay.ShowAttackDamage()`는 `CombatImpactTier.Defeat`일 때도 `isCritical` 값을 확인하여 프리팹을 선택한다. 처치 등급은 색상만 적용하며 별도의 스케일 증가는 사용하지 않으므로, 처치 대미지 숫자는 같은 일반·크리티컬 공격과 동일한 크기로 표시된다.

### 연속 대미지 텍스트 겹침 방지

Damage Numbers Pro 프리팹의 Collision과 Push는 설정 반경과 배율을 사용하므로 프로젝트의 위치 오프셋과 함께 적용하면 숫자가 필요 이상으로 멀어질 수 있다. `EnemyDamageNumberDisplay`는 팝업을 생성하는 짧은 구간에만 프리팹의 Collision과 Push를 억제하고, 현재 살아 있는 대미지·상태 팝업의 로컬 위치를 함께 관리해 `minimumSpawnSeparation`만큼 떨어진 빈 위치를 예약한다. 프리팹 설정은 생성 직후 원래 값으로 복원되며 에셋에는 변경을 남기지 않는다.

일반, 크리티컬, 독, 표식 보너스와 상태 문구가 같은 위치 예약 목록을 사용한다. 기본 최소 간격은 `0.2` 월드 단위이고, `0`으로 설정하면 프로젝트의 강제 간격을 사용하지 않는다. 팝업이 제거되거나 비활성화되면 해당 위치는 자동으로 다시 사용할 수 있다.

## 한 번의 연속 발사 기준 처치 콤보

월드에 표시되는 `n연속 처치!` 문구의 처치 수는 `PlayerShoot`의 한 번의 발사 시퀀스를 기준으로 한다. HUD의 지속 콤보 수는 별도로 턴 제한 안에서 이어진다.

처치 문구의 연속 처치 수는 한 번의 발사 시퀀스를 기준으로 유지하지만, 전반적인 처치 연출 강도는 HUD의 지속 콤보 수를 기준으로 계산한다. 기본값에서는 콤보가 1 증가할 때마다 배율이 `0.2`씩 계속 증가한다. 이 배율은 처치 파티클 수와 크기, 방향 잔상, 화면 플래시, Volume Pulse, 슬로 모션, Fullscreen Impact, 콤보 문구에 적용된다. 카메라 셰이크는 접근성 및 화면 안정성을 위해 콤보와 무관한 고정 처치 세기를 사용한다.

`SFX_Combo_Die`의 피치는 콤보 수가 증가할 때마다 단조 증가하며, 높은 콤보에서도 갑자기 같은 값으로 고정되지 않도록 최대 피치에 점진적으로 접근하는 곡선을 사용한다.

1. 연속 발사를 시작할 때 `BeginFiringSequence()`가 기존 처치 수를 초기화한다.
2. 같은 시퀀스에서 적을 처치할 때마다 `RecordDefeat()`가 처치 수를 증가시킨다.
3. 실린더 발사가 끝나면 해당 시퀀스의 콤보도 종료된다.

### 처치 텍스트

| 같은 발사 내 처치 순서 | 표시 문구 | 색상 |
| ---: | --- | --- |
| 1 | `적 처치!` | 흰색 |
| 2 | `2연속 처치!` | 주황색 기본값 |
| 3 이상 | `n연속 처치!` | 빨간색 |

- 프리팹: `Assets/Prefabs/UI/Text _ Kill Combo.prefab`
- Player 프리팹의 `killComboTextPrefab`에 연결되어 있다.
- 등장 시 작은 크기에서 빠르게 팝업한 뒤 오버슈트와 흔들림을 거쳐 안정된다.
- 수평 드리프트, 회전 흔들림, 상승, 알파 페이드가 적용된다.
- 추가 처치마다 기본 크기의 2.5%씩 커지며 최대 20%까지 증가한다.
- 프리팹 참조가 없으면 런타임 TMP 3D Text를 생성하는 폴백이 있다.

> 현재 `Stage 1` 씬은 `secondKillTextColor`를 초록색 계열로 Override하고 있다. 요구 사양대로 주황색을 사용하려면 해당 Override를 제거하거나 주황색으로 변경해야 한다. 코드와 Player 프리팹의 기본값은 주황색이다.

### 콤보 보너스 골드

첫 번째 처치에는 콤보 보너스가 없다.

```text
현재 처치의 콤보 보너스 = (현재 처치 수 - 1) × comboGoldPerKill
comboGoldPerKill 기본값 = 10
```

따라서 같은 발사에서 첫 번째 처치는 0, 두 번째 처치는 10, 세 번째 처치는 20 골드를 추가로 생성한다. 보너스 골드도 처치된 적의 월드 좌표에서 출발한다.

## 마지막 적 처치와 전투 완료 순서

마지막 두 적을 한 발사에서 처치할 때 마지막 적이 `적 처치!`로 다시 표시되던 원인은, 마지막 적 사망 즉시 `BattleCompleted`가 발생하여 발사 시퀀스와 콤보 상태가 먼저 정리되었기 때문이다.

현재 처리 순서는 다음과 같다.

```text
마지막 적 사망
  → 처치 및 콤보 피드백 기록
  → WaveManager가 전투 완료를 Pending으로 보관
  → 남은 연속 발사와 발사 종료 정리
  → NotifyFiringSequenceCompleted()
  → BattleCompleted 발생
```

- 플레이어가 발사 중이면 `WaveManager.isBattleCompletionPending`만 설정한다.
- `PlayerShoot`가 연속 발사를 완전히 종료하고 `NotifyFiringSequenceCompleted()`를 호출한다.
- 이 시점에 실제 `BattleCompleted` 이벤트를 발생시킨다.
- 발사 중이 아닌 다른 원인으로 마지막 적이 사망했다면 즉시 완료할 수 있다.

이 순서로 마지막 적의 처치 텍스트와 콤보 보너스가 먼저 처리되고, 사용자에게 적 전멸 피드백이 보인 뒤 상점·스테이지 완료 흐름으로 넘어간다.

## 골드 비행 UI

골드를 월드에서 획득할 때 `CurrencyManager.AddMoneyFromWorld(amount, sourceWorldPosition)`를 사용한다.

### 생성과 출발 위치

- 획득량이 `N`이면 UI 골드 이미지도 `N`개 생성한다.
- UI 프리팹: `Assets/Resources/UI/Flying Gold.prefab`
- 이미지 리소스: `Assets/Sprites/UI/FlyingGold.png`
- 총알 능력으로 획득: 플레이어 월드 좌표에서 출발한다.
- 적 피해·처치 또는 콤보 보너스로 획득: 해당 적의 월드 좌표에서 출발한다.
- 월드 좌표를 루트 Canvas의 로컬 UI 좌표로 변환한다.
- 도착지는 `Canvas > Panel | Money`이다.

### 이동

- 생성 간격 기본값: `0.045`초
- 기본 비행 시간: `0.65`초
- 개별 비행 시간: 기본 시간의 `0.8`배에서 `1.2`배 사이 무작위
- Sin 파동 진폭: `18~52`
- Sin 파동 주기 수: `0.75~1.5`
- 파동 방향과 이동 편차를 개별로 무작위화하여 여러 골드가 한 덩어리처럼 겹치지 않게 한다.
- 직선 진행에는 SmoothStep을 사용하고, Sin 오프셋은 출발·도착 지점에서 자연스럽게 0으로 줄어든다.

### 실제 재화 반영 시점

- 골드 이미지를 생성할 때는 `pendingAnimatedMoney`만 증가한다.
- 각 이미지가 `Panel | Money`에 도착한 순간에만 `CommitMoney(1)`을 호출한다.
- 도착할 때 Money 패널에 짧은 펀치 스케일을 적용한다.
- 프리팹, Canvas, 카메라 또는 Money 패널을 찾지 못하면 재화 유실 방지를 위해 즉시 반영한다.

### 게임 클리어 후 상점 진입

`ShopManager.OpenShop()`는 상품 생성 전에 `CurrencyManager.FlushPendingMoney()`를 호출한다.

- 진행 중인 골드 코루틴을 중단한다.
- 화면에 남은 비행 골드 오브젝트를 제거한다.
- 아직 도착하지 않은 `pendingAnimatedMoney`를 한 번에 실제 재화로 반영한다.
- Money 패널의 스케일을 원래 값으로 복원한다.

따라서 게임 클리어 직후 상점에 진입해도 획득한 골드를 기다리지 않고 즉시 사용할 수 있다.

## 발차기·밀치기

### 이동 시간

기존의 타일당 이동 시간 방식 대신, 밀려나는 전체 거리에 관계없이 정해진 시간 안에 최종 위치까지 이동한다.

- 설정 필드: `pushFlightDuration`
- 기본값: `0.1`초
- 이전 `pushTileDuration` 직렬화 값은 `FormerlySerializedAs`로 마이그레이션된다.
- 충돌이 발생하면 충돌 반동을 정리하는 짧은 추가 시간이 적용될 수 있다.

### 타격 시점과 연출

- 애니메이터가 있으면 발차기 애니메이션의 정규화 시간 `0.5` 지점에서 실제 충격을 발생시킨다.
- 애니메이터 상태를 찾지 못하면 폴백 시간을 사용한다.
- 충격 순간에 Lens Distortion, Bloom, Chromatic Aberration, Vignette, Contrast를 강화한다.
- 기본값 기준 `0.42`배 슬로 모션, `0.045`초 유지, `0.13`초 복귀가 적용된다.
- Low-pass cutoff를 충격 강도에 따라 약 `5200Hz`에서 `2600Hz` 방향으로 낮춘다.
- 밀치는 방향을 반영한 Fullscreen Impact와 카메라 셰이크를 재생한다.
- 실제 충돌까지 발생한 발차기는 충돌 없이 밀어낸 발차기보다 강하게 표현한다.
- 총알 능력에 의한 넉백은 직접 발차기 연출과 구분한다.

## 통합 카메라 셰이크

탄환별 고정 반동 대신, 명중 시에는 적 최대 체력 대비 실제 적용 피해 비율을 사용한다.

```text
damageRatio = Clamp01(실제 적용 피해 / 대상 최대 체력)
shakeStrength = maximumDamageShakeStrength × damageRatio
shakeDuration = maximumDamageShakeDuration × damageRatio
```

예를 들어 최대 세기가 10, 최대 지속 시간이 2초이고 최대 체력의 50%를 피해로 주었다면 세기 5, 지속 시간 1초의 셰이크가 발생한다.

`EnemyController.DamageApplied(appliedDamage, maxHealth)` 이벤트가 실제 피해 적용 뒤 발생하고, `CombatFeedbackController`가 이를 받아 `CombatCameraShake`를 실행한다. 보호막이 흡수한 양과 실제 체력 감소량을 합친, 적에게 실제로 적용된 피해를 기준으로 한다.

### 허공 발사

적 대상이 없는 발사는 최대 셰이크 세기와 지속 시간의 40%를 사용한다.

```text
emptyShotStrength = maximumDamageShakeStrength × 0.4
emptyShotDuration = maximumDamageShakeDuration × 0.4
```

### 부드러운 복귀와 순간이동 방지

모든 셰이크는 흔들림 구간 뒤에 별도의 복귀 구간을 가진다.

- 요청 복귀 시간은 셰이크 지속 시간과 같게 시작한다.
- 너무 짧은 셰이크도 부드럽게 보이도록 최소 복귀 시간 `0.35`초를 보장한다.
- 프레임이 큰 환경에서도 즉시 끝나지 않도록 최소 `12` 프레임의 복귀를 보장한다.
- 복귀 시작 시 Cinemachine Noise의 `FrequencyGain`을 `0`으로 만들어 노이즈 목표점이 계속 움직이지 않게 한다.
- `AmplitudeGain`을 SmoothStep 보간으로 원래 값까지 낮춘다.
- 회전은 `Quaternion.Slerp`로 `(0, 0, 0)`에 해당하는 `Quaternion.identity`까지 복귀시키고 마지막 프레임에 정확히 고정한다.
- `LateUpdate`와 높은 실행 순서를 사용하여 Cinemachine 업데이트 뒤에 회전 복귀를 적용한다.
- 복귀 중 새 셰이크가 들어오면 기존 복귀를 즉시 중단하고 현재 상태에서 새 셰이크를 시작한다.
- Cinemachine을 사용하는 경로에서는 카메라 Transform 위치를 마지막에 강제로 덮어쓰지 않는다. 위치 소유권 충돌로 발생하던 마지막 순간의 점프를 방지하기 위한 처리다.
- Cinemachine Noise를 찾지 못한 폴백 경로에서만 로컬 위치와 회전을 직접 보간한다.

접근성 옵션의 `CameraShakeMultiplier`가 0이면 카메라 셰이크는 재생되지 않는다.

### 현재 직렬화 값 주의

| 위치 | 최대 세기 | 최대 지속 시간 |
| --- | ---: | ---: |
| Player 프리팹 기본값 | `0.055` | `0.18`초 |
| Stage 1 씬 Override | `1` | `1`초 |

위 값들은 구현 공식의 최대값이며 게임 감각에 맞춰 Inspector에서 조정할 수 있다. “최대 세기 10 / 최대 시간 2초” 예시는 비율 계산을 설명하기 위한 값이고, 현재 Stage 1 직렬화 값은 위 표와 같다.

## 주요 파일과 역할

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/Common/CombatCameraShake.cs` | 공격 등급 판정 상수, Cinemachine 셰이크, 부드러운 위치·회전 복귀, 접근성 배율 |
| `Assets/Scripts/Common/CombatFeedbackController.cs` | 공격 등급별 시각·청각 연출, 처치 콤보, 콤보 골드, 허공 반동 |
| `Assets/Scripts/Enemy/EnemyController.cs` | 실제 피해 계산, `DamageApplied` 이벤트, 대미지·처치 피드백 호출 |
| `Assets/Scripts/Enemy/EnemyDamageNumberDisplay.cs` | 일반·크리티컬·치명적·처치 대미지 텍스트 프리팹과 스타일 선택 |
| `Assets/Scripts/Player/PlayerShoot.cs` | 한 번의 연속 발사 수명 주기, 허공 발사 판정, 발사 종료 통지 |
| `Assets/Scripts/Player/PlayerMove.cs` | 발차기 타격 시점, 고정 총 비행 시간 기반 밀치기 |
| `Assets/Scripts/Manager/WaveManager.cs` | 마지막 적 처치 후 전투 완료 Pending 및 발사 종료 뒤 완료 처리 |
| `Assets/Scripts/Manager/CurrencyManager.cs` | 월드 좌표 기반 골드 UI 생성·이동·도착 반영·즉시 Flush |
| `Assets/Scripts/Manager/RewardManager.cs` | 피해·처치 보상에 적 월드 좌표 전달 |
| `Assets/Scripts/Manager/ShopManager.cs` | 상점 진입 전 대기 중 골드 즉시 반영 |
| `Assets/Prefabs/UI/Text _ Kill Combo.prefab` | 3D TMP 처치 콤보 텍스트 |
| `Assets/Resources/UI/Flying Gold.prefab` | 골드 비행 UI 이미지 |

## Play Mode 확인 항목

1. 일반 공격, 크리티컬, 최대 체력 60% 이상 피해, 처치가 서로 다른 강도로 표현되는지 확인한다.
2. 크리티컬 처치에서는 크리티컬 대미지 프리팹, 일반 처치에서는 일반 대미지 프리팹이 나오는지 확인한다.
3. 한 번의 연속 발사로 적 두 명 이상을 처치했을 때 `2연속 처치!`, `3연속 처치!`가 순서대로 표시되는지 확인한다.
4. 첫 처치에는 보너스 골드가 없고, 두 번째 처치부터 공식대로 보너스가 발생하는지 확인한다.
5. 마지막 두 적을 같은 발사로 처치했을 때 마지막 텍스트까지 콤보 수에 포함된 뒤 전투 완료가 발생하는지 확인한다.
6. 획득량만큼 골드 이미지가 생성되고, 각 이미지가 Money 패널에 도착할 때 수치가 1씩 증가하는지 확인한다.
7. 골드가 비행 중인 상태로 상점에 진입하면 남은 금액이 즉시 한 번만 반영되는지 확인한다.
8. 먼 거리로 밀어내도 발차기 이동이 `pushFlightDuration` 안에 끝나는지 확인한다.
9. 최대 체력의 25%, 50%, 100% 피해에서 카메라 셰이크 세기와 지속 시간이 선형 비율로 변하는지 확인한다.
10. 허공 발사에서 최대값의 40% 셰이크가 발생하는지 확인한다.
11. 짧은 셰이크와 연속 셰이크 뒤에도 카메라 위치가 점프하지 않고, 회전이 `(0, 0, 0)`으로 부드럽게 돌아오는지 확인한다.

