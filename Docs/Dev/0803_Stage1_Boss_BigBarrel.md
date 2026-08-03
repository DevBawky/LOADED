# 스테이지 1 보스: 폭약왕 빅 베럴

## 적용 범위

- 공용 `Enemy.prefab`과 `EnemyController.Initialize(EnemyData, ...)` 생성 흐름을 유지한다.
- 보스 전용 완성형 적 프리팹은 만들지 않는다.
- `BigBarrel.asset`을 `Stage 1 Boss.asset` 웨이브에 등록하고, 폭탄만 `BossBomb.prefab`으로 생성한다.
- 기존 행동 큐의 빈 슬롯 생성, 행동 타일 등록, 준비, 실행 단계를 그대로 사용한다.

## 고정 행동 순서

`BigBarrelStep`은 다음 순서만 반복하며 행동 선택에는 난수를 사용하지 않는다.

1. 플레이어 방향 회전 또는 방향 유지
2. 폭탄 공격 빈 큐 생성
3. `ExplosiveThrow` 타일 등록
4. 폭탄 대상 타일 선택 및 준비
5. 고정된 타일로 폭탄 투척
6. `preferredDistance` 기준 한 칸 거리 조정 또는 대기
7. 산탄 공격 빈 큐 생성
8. `ShotgunAttack` 타일 등록
9. 현재 보스 양옆 타일 고정 및 준비
10. 고정된 두 타일 동시 공격
11. `recoveryTurns`만큼 `BossReload` 타일을 표시하며 재장전

회전, 큐 생성, 타일 등록, 준비, 실행, 이동, 재장전은 모두 별도 적 턴을 소비한다. 준비 후에는 플레이어와 적이 이동해도 고정된 타일을 다시 계산하지 않는다.

## 페이즈

- 1페이즈는 폭탄 2개와 `bombFuseTurns`를 사용한다.
- 체력 비율이 `phaseTwoHealthRatio` 이하가 되면 현재 고수준 공격이 끝난 뒤 2페이즈로 전환한다.
- 2페이즈는 폭탄 3개와 `phaseTwoBombFuseTurns`를 사용한다.
- 준비가 끝난 행동은 페이즈 전환으로 취소하거나 다시 선택하지 않는다.
- 전환 시 Avatar가 주황색으로 점멸하고 행동 큐에도 같은 색의 등장 연출을 표시한다.
- 산탄 범위는 데이터와 관계없이 보스 좌우 인접 1칸으로 고정한다.

## 폭탄 대상과 Telegraph

- 보드 내부 타일 중 보스 점유 타일과 활성 폭탄 점유 타일을 제외한다.
- 한 번의 준비에서 중복 없는 타일을 Unity 난수로 선택한다. 프로젝트에는 별도 시드 서비스가 없으므로 기존 시스템과 같은 `UnityEngine.Random`을 사용한다.
- 후보가 요청 수보다 적으면 가능한 수만 준비하며 후보가 없으면 빈 공격으로 정상 완료한다.
- 준비 시 각 타일까지 포물선 LineRenderer를 만들고, 대상 중심 좌우 폭발 범위를 주황색 오버레이로 표시한다.
- 실행 시 타일이 여전히 유효하고 비어 있는 폭탄 타일만 개별 생성한다. 하나가 실패해도 나머지는 계속 처리한다.

## 거리 조정과 산탄

- 플레이어가 선호 거리보다 가까우면 반대 방향, 멀면 플레이어 방향으로 한 칸 이동한다.
- 폭탄 점유, 적 점유, 플레이어, 다음 웨이브 예약 타일은 이동할 수 없다.
- 퓨즈가 1 이하인 폭탄의 폭발 범위도 피하며 안전한 목표가 없으면 대기한다.
- 산탄 준비 시 유효한 좌우 인접 타일만 고정하고 붉은 오버레이를 표시한다.
- 실행 시 고정 타일에 현재 존재하는 플레이어와 일반 적에게 `shotgunDamage`를 적용한다. 보스 자신과 폭탄은 대상이 아니다.

## BossBomb과 관리자

`BossBombManager`는 `WaveManager`에 한 번 연결되며 활성 폭탄 목록과 타일별 사전을 유지한다. `FindObjectsByType` 같은 전역 검색은 사용하지 않는다.

- 폭탄 생성 성공 시 목록과 타일 사전에 등록한다.
- 제거 또는 폭발 시작 시 점유를 즉시 해제한다.
- 플레이어 이동, 적 이동, 밀어내기 경로와 다음 웨이브 스폰 후보는 같은 점유 조회를 사용한다.
- `WaveManager.EnemyTurnCycleCompleted`에서 퓨즈를 처리한다.
- 생성 시 현재 적 턴 라운드 번호를 저장하고 같은 라운드 종료 이벤트는 건너뛴다.
- 이후 라운드 종료마다 1 감소하며 0이면 즉시 폭발한다.
- 런타임 퓨즈는 항상 1~3으로 제한한다.
- 퓨즈 1에서는 폭탄과 범위 오버레이가 함께 점멸한다.

폭발은 큐와 중복 방지 집합으로 처리한다. 범위 안의 다른 폭탄을 큐에 넣고 각 폭탄이 한 번씩 독립 피해를 적용하므로 겹치는 범위의 피해도 각각 적용된다. 컬렉션 순회에는 스냅샷을 사용한다.

피해 범위는 설치 타일 중심 좌우 `bombExplosionRadius`칸이다. 플레이어와 일반 적은 `bombDamage`, 빅 베럴은 `bossSelfExplosionDamage`를 받는다. 적 보호막은 `EnemyController.ApplyDamageInternal`을 통해 먼저 흡수한다. 현재 PlayerHealth에는 보호막 시스템이 없으므로 플레이어는 기존 `ApplyDamage` 흐름을 사용한다. 폭탄 피해에는 밀어내기나 상태 이상을 적용하지 않는다.

## 플레이어 탄환

현재 플레이어 사격은 물리 충돌체가 아니라 타일 정렬 히트스캔이다. `IPlayerBulletBlocker`를 추가하고 `BossBomb`이 이를 구현한다.

- 사선에서 가장 가까운 폭탄 뒤의 적은 히트 후보에서 제외한다.
- 폭탄 앞의 적은 기존 관통 확률과 최대 타격 수 규칙대로 처리한다.
- 관통에 성공해 폭탄까지 도달해도 폭탄에서 탄환이 종료된다.
- 폭탄은 탄환 피해, 상태 이상, 밀어내기를 받지 않고 폭발하지 않는다.
- 피해 미리보기 역시 폭탄 뒤 적에게 피해를 표시하지 않는다.

## 사망과 전투 종료

- 보스 체력이 0이면 실행 코루틴, 큐 UI와 모든 Telegraph를 즉시 정리한다.
- `WaveManager.NotifyBigBarrelDefeated`가 폭탄 퓨즈와 연쇄 폭발 처리를 먼저 정지한다.
- 전투 완료 직전에 `BossBombManager.ClearAll`이 점유, 표시, 오브젝트를 모두 제거한다.
- 전투 완료 호출은 기존 `WaveManager.HandleWaveCleared` 흐름만 사용한다.
- 영구 해금 시스템은 현재 프로젝트에 없다. 후속 시스템은 `WaveManager.BigBarrelDefeated` 이벤트에 구독해 최초 처치 시 `ExplosiveBullet.asset`을 추가할 수 있다. 임시 저장 데이터는 만들지 않았다.

## 데이터와 기본 에셋

`EnemyData.BigBarrel`에서 다음 값을 설정한다.

| 필드 | 기본값 |
|---|---:|
| Phase Two Health Ratio | 0.5 |
| Bomb Damage | 20 |
| Boss Self Explosion Damage | 10 |
| Bomb Explosion Radius | 1 |
| Bomb Fuse Turns | 3 |
| Phase Two Bomb Fuse Turns | 2 |
| Shotgun Damage | 15 |
| Bomb Arc Height | 2 |
| Explosion Camera Shake | 0.2 |
| Boss Hit Camera Shake | 0.12 |

일반 `EnemyData`의 `preferredDistance`와 `recoveryTurns`는 각각 2로 설정했다.

추가된 기본 에셋:

- `Assets/Scripts/Enemy/Enemy SO/BigBarrel.asset`
- `Assets/Scripts/Enemy/Enemy Action SO/ExplosiveThrow.asset`
- `Assets/Scripts/Enemy/Enemy Action SO/ShotgunAttack.asset`
- `Assets/Scripts/Enemy/Enemy Action SO/BossReload.asset`
- `Assets/Prefabs/Enemy/BossBomb.prefab`
- `Assets/Materials/Enemy/BigBarrelBombTelegraph.mat`
- `Assets/Materials/Enemy/BigBarrelShotgunTelegraph.mat`
- `Assets/Scripts/Manager/Battle SO/Stage 1 Boss.asset`

실제 보스 Avatar, 행동 아이콘, 오디오 리소스는 제공되지 않아 슬롯을 비워 두었다. Avatar는 연결 전까지 공용 적 로직만 동작하며, 폭탄은 런타임 원형 Sprite와 퓨즈 TextMeshPro를 자동 생성한다. 행동 아이콘은 기존 누락 아이콘 색상으로 표시된다.

에셋을 삭제했거나 다시 만들 필요가 있으면 Unity 메뉴 `Tools > Loaded > Create Stage 1 Big Barrel Assets`를 실행한다. 생성기는 이미 존재하는 에셋을 덮어쓰지 않는다.

## Inspector 검증

`EnemyDataEditor`는 `Behavior Type = BigBarrel`일 때 다음을 경고로 표시한다.

- 폭탄, 산탄, 재장전 Action 누락
- BossBomb 프리팹 또는 루트 컴포넌트 누락
- 폭탄/산탄 Telegraph Material 누락
- 잘못된 폭탄 피해, 폭발 범위, 퓨즈, 2페이즈 체력 비율

검증은 경고만 표시하며 런타임 강제 예외를 만들지 않는다. 런타임은 퓨즈 등 안전 범위를 다시 제한한다.

## Play Mode 확인 목록

1. `Stage 1 Boss.asset`으로 전투를 시작했을 때 공용 `Enemy.prefab`에 `BigBarrel.asset`이 주입되는지 확인한다.
2. 고정 행동 단계가 각기 다른 적 턴을 소비하는지 확인한다.
3. 준비 후 이동해도 폭탄과 산탄 대상 타일이 바뀌지 않는지 확인한다.
4. 1/2페이즈에서 각각 폭탄 수와 퓨즈가 2/3개 및 대응 값으로 바뀌는지 확인한다.
5. 생성 라운드에 퓨즈가 줄지 않고 이후 적 턴 라운드 종료마다 줄어드는지 확인한다.
6. 플레이어, 적, 보스, 보호막에 폭발 피해가 올바르게 적용되는지 확인한다.
7. 폭탄 연쇄 폭발과 겹친 피해가 각각 한 번씩 적용되는지 확인한다.
8. 일반탄과 관통탄이 폭탄에서 끝나며 폭탄은 즉시 폭발하지 않는지 확인한다.
9. 보스 사망 직후 폭탄이 멈추고 승리 처리 전에 모두 제거되는지 확인한다.

## 검증 결과

- 새 런타임 및 Editor 스크립트를 포함한 `dotnet build LOADED.slnx --no-restore`: 경고 0, 오류 0
- `git diff --check`: 공백 오류 없음
- Unity 6000.3.15f1 배치 실행은 프로젝트가 이미 다른 Unity Editor 프로세스에서 열려 있어 동일 프로젝트의 두 번째 Editor 검증을 진행하지 못했다. 열린 Editor에서 스크립트 임포트 후 위 Play Mode 목록을 확인한다.
