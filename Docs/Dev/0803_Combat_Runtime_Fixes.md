# 전투 런타임 보완: 탄환 상태, 적 애니메이션, 적 턴 시간

## 기본 정보

- 작성일: 2026-08-03
- 관련 기능: 충전탄 / 연쇄 사격 / 클론탄 / 적 애니메이션 / `WaveManager`
- 목적: 실린더 안의 탄환 런타임 상태와 적 행동 연출 시간을 실제 전투 규칙에 맞게 일치시킨다.

## 충전탄 발사 카운트

충전탄의 카운트는 해당 탄환이 실린더에 들어가는 순간부터 시작한다.

- `DeckManager`가 탄환을 장전할 때 `BeginCylinderShotTracking()`으로 해당 탄환의 관측 발사 횟수를 0으로 초기화한다.
- 사격 한 발이 실제로 성공할 때마다 실린더에 남아 있는 모든 탄환에 `RecordShotWhileLoaded()`를 호출한다.
- 따라서 충전탄보다 먼저 발사된 탄환뿐 아니라 충전탄 자신의 발사 직전까지 성공한 모든 발사가 충전량에 반영된다.
- 연쇄탄처럼 한 탄환이 여러 번 실제 사격을 발생시키면 각 사격 성공을 개별 1회로 계산한다.
- `Charge` 효과는 `min(관측 발사 횟수, Stack Count)`를 충전 스택으로 사용해 피해 배율을 계산한다.
- 일시정지 중에는 사격 진행과 카운트가 진행되지 않는다.

## 클론탄 런타임 상태 상속

클론탄이 직전 발사를 복제하면 직전 탄환이 발사 직전에 보유했던 런타임 값을 함께 상속한다.

`BulletRuntimeStateSnapshot`에 저장되는 값은 다음과 같다.

| 값 | 설명 |
| --- | --- |
| `AbilityStacks` | 집중, 축전 등 능력 스택 |
| `PermanentStacks` | 전투 중 누적된 영구 스택 |
| `StoredDamageBonus` | 저장형 추가 피해 |
| `TemporaryCriticalChanceBonus` | 임시 치명타 확률 보너스 |
| `TemporaryDamageBonus` | 임시 피해 보너스 |
| `ShotsObservedWhileLoaded` | 실린더 안에서 관측한 발사 횟수, 즉 충전량의 원본 |

실제 사격은 직전 탄환의 발사 직전 스냅샷을 저장한다. 다음 탄환이 `ClonePreviousShot`으로 해석되면 그 스냅샷을 클론탄 인스턴스에 적용한 뒤 피해와 효과를 계산한다. 이에 따라 스택형 및 충전형 탄환을 복제했을 때 직전 탄환의 발사 직전 값이 그대로 사용된다. 실린더 호버 피해 예측도 동일한 스냅샷 흐름을 사용해 실제 사격과 결과를 맞춘다.

## 적 공격 애니메이션

### Thrower

- 행동 큐 생성, 타일 등록, 준비 단계에서는 `Attack` 애니메이션을 재생하지 않는다.
- 실제 투척 공격을 실행하는 순간에만 Animator의 `Base Layer.Attack` 상태를 재생한다.
- Attack 종료 후 Idle 복귀는 Animator Controller 전이로 처리하므로 별도 복귀 코루틴을 실행하지 않는다.

### Gunner

- 공격 타일이 큐에 하나 이상 등록되면 Animator bool 파라미터 `isReloaded`를 `true`로 설정한다.
- 공격 타일 큐가 비거나 초기화되면 `false`로 되돌린다.
- Avatar 생성, 큐 추가·제거·초기화 시점마다 상태를 다시 동기화한다.

## 적 턴 시간 규칙

`WaveManager.ResolveEnemyTurns()`는 `Enemy Turn Delay`를 적마다 반복하지 않고 한 번의 적 턴에 참여하는 모든 적이 공유하는 기본 시간 예산으로 사용한다.

### 기본 시간에 포함되는 행동

- 이동
- 회전
- 행동 큐 생성과 아이콘 공개
- Telegraph 등록 및 준비 UI
- 재장전 표시와 대기
- 그 밖의 비공격 연출

위 행동이 진행되는 동안 흐른 시간은 남은 `Enemy Turn Delay`에서 차감한다. 여러 적이 아무 행동도 하지 않으면 적 수와 무관하게 전체 적 턴은 한 번의 `Enemy Turn Delay`만 기다린다. 비공격 연출 시간이 기본 예산보다 길면 연출이 끝날 때까지 기다리되 추가 기본 대기는 하지 않는다.

### Acting으로 판정하는 행동

- `EnemyController.LastTurnAction == EnemyTurnActionType.Fire`인 실제 공격만 추가 Acting으로 판정한다.
- 공격 애니메이션, 투사체 비행, 피해 처리 등 공격 코루틴의 실제 길이는 `Enemy Turn Delay`에서 차감하지 않고 별도 시간으로 보장한다.
- `Enemy Action Interval`은 실제 공격이 끝난 뒤 다음 적으로 넘어갈 때만 적용한다.
- 이동, 회전, UI 연출 뒤에는 `Enemy Action Interval`을 추가하지 않는다.
- 게임 일시정지 동안의 시간은 기본 예산과 행동 경과 시간 어디에도 포함하지 않는다.

개념적인 전체 시간은 다음과 같다.

```text
max(Enemy Turn Delay, 비공격 행동의 실제 총시간)
  + 실제 공격들의 재생 시간
  + 공격 사이에 적용된 Enemy Action Interval
```

Scene Inspector에서 조정하는 필드는 다음과 같다.

| 필드 | 용도 |
| --- | --- |
| `WaveManager > Enemy Turn Delay` | 모든 적이 공유하는 비공격 기본 턴 시간 |
| `WaveManager > Enemy Action Interval` | 실제 공격 이후에만 붙는 다음 적 행동 전 간격 |

## 확인 목록

1. 적이 여러 마리여도 모두 대기하면 적 턴 총 대기가 `Enemy Turn Delay × 적 수`가 되지 않는지 확인한다.
2. 이동, 회전, 큐/UI 연출이 기본 딜레이 안에서 처리되고 그 뒤 별도 간격이 붙지 않는지 확인한다.
3. 실제 공격 애니메이션과 투사체 시간이 잘리지 않고 끝까지 재생되는지 확인한다.
4. 공격 이후에만 `Enemy Action Interval`이 적용되는지 확인한다.
5. Thrower가 실제 투척 순간에만 Attack을 재생하는지 확인한다.
6. Gunner의 공격 타일 유무와 `isReloaded` 값이 일치하는지 확인한다.
7. 충전탄 앞의 연쇄 사격이 실제 발사 횟수만큼 충전 스택을 올리는지 확인한다.
8. 클론탄이 직전 스택형·충전형 탄환의 발사 직전 값을 상속하는지 확인한다.

## 검증 결과

- `dotnet build LOADED.slnx --no-restore`: 경고 0개, 오류 0개
- `git diff --check`: 공백 오류 없음
- 실제 애니메이션 전이와 체감 시간은 Unity Play Mode에서 위 확인 목록으로 최종 검증한다.
