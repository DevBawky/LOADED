# LOADED 작업 인계 기록 — Cylinder Tempo 및 입력 안정화

> 2026-10-06: 아래 내용은 이전 설계 기록이다. 현재 전투 규칙은 [행동 기반 전투 개편](1006_ActionCombat.md)을 따른다.

> 마지막 갱신: 2026-09-30 (KST)
>
> 다음 작업 시 사용 문구: **“이전 작업내역 불러와줘”**, **“0930 인계 문서 불러와줘”**
>
> 위 요청을 받으면 이 문서를 먼저 읽고 현재 Git 작업 트리 및 Unity 상태와 대조한 뒤 진행 상황을 설명한다.

## 1. 현재 방향과 핵심 결정

- Battle은 기존 2D에서 2.5D/3D 전장으로 전환 중이다.
- 렌더 파이프라인은 URP를 유지하면서 HD-2D 아트 디렉션을 강화한다. 현재 목표는 HDRP 전환보다 로우폴리 배경, 조명, 포스트 프로세싱, 스프라이트 캐릭터의 대비를 조합해 품질을 높이는 것이다.
- 기존 Duel Clock의 실시간 압박은 제거하고 **Cylinder Tempo** 방식으로 교체하는 중이다.
- 플레이어가 대기하는 동안 시간은 흐르지 않는다. 플레이어 행동이 Tempo 비용을 누적하고 6칸이 차면 적 행동 주기가 발생한다.
- 적 공격 회피는 턴제 안에서도 플레이어의 실시간 이동 타이밍을 요구하는 피지컬 요소로 유지한다.
- 탄환별 속도 시스템은 복잡도 증가 우려로 도입하지 않는다.
- 사운드는 Cylinder Tempo 구현 범위에 추가하지 않는다.

## 2. Cylinder Tempo 현재 규칙

행동별 비용:

| 행동 | Tempo 비용 |
| --- | ---: |
| 좌우/상하 이동 | 2 |
| 회전 | 1 |
| 대기 | 1 |
| 장전 | 3 |
| 발사 | 3 |

해결 규칙:

- Tempo가 6에 도달할 때마다 전장 속 적들이 1턴씩 행동한다.
- 6을 초과한 값은 적 행동이 모두 끝난 뒤 다음 주기로 이월한다.
- 적 행동 중에는 이동 회피만 즉시 허용한다. 회전, 대기, 장전, 사격은 적 행동 완료 전까지 실행하지 않는다.
- 최대 생존 적 수 미만이면 주기마다 적을 1명 스폰한다.
- 전장에 적이 한 명도 없으면 스테이지 타일 수의 10%를 올림한 최소 수만큼 즉시 스폰한다.
- 남은 스폰 풀이 최소 수보다 적으면 남은 수만 스폰한다.
- 스폰 예정 위치는 전장에 미리 표시하지 않는다.
- Combo Timer 셀 수는 8개에서 6개로 변경됐다.

UI 규칙:

- `Canvas/Panel | Cylinder Tempo`가 전용 HUD다.
- `Layout | Tempo`의 6개 이미지는 채워질 때 밝은 적색 태양풍 계열 Resolve 연출을 사용한다.
- 점등 순간 알파는 즉시 1이 된다. Lerp 없이 즉시 켜지고 꺼진다.
- 6칸이 모두 찬 뒤 적 행동이 끝나면 이월 칸을 제외한 점등이 한 번에 꺼진다.
- `Text | Spawn Left`는 스폰 풀 잔여 적 수를 표시한다.
- `Text | Left Enemy`는 현재 전장 생존 적 수를 실시간 표시한다.
- `Text | Phase`는 평상시에 청록색 `PLAYER PHASE`를 표시한다.
- 적 행동 주기가 예약된 동안에는 주황빛 적색의 굵은
  `ENEMY PHASE`와 느린 밝기·크기 펄스를 표시하고, 모든 적 행동 연출이
  끝나 이월 Tempo가 공개되는 즉시 `PLAYER PHASE`로 복귀한다.

주요 구현 파일:

- `Assets/Scripts/Manager/DuelClockController.cs`
- `Assets/Scripts/Manager/WaveManager.cs`
- `Assets/Scripts/Common/CylinderTempoHUD.cs`
- `Assets/Shaders/CylinderTempoSolarResolve.shader`
- `Assets/Materials/CylinderTempoSolarResolve.mat`
- `Assets/Editor/Tests/CylinderTempoTests.cs`
- `Assets/Scenes/Battle.unity`
- `Assets/Prefabs/UI/Canvas.prefab`

전장 작성 규칙(2026-10-07):

- `BattleData`의 `Minimum Board Count`와 `Maximum Board Count` 사이에서
  전투 시작 시 보드 칸 수를 한 번 선택한다.
- 선택된 칸 수는 런 저장 데이터에 포함되며 이어하기와 전투 종료 화면 복원에서
  다시 추첨하지 않는다.
- 적 구성은 `Duel Clock Enemy Spawn Count`와
  `Duel Clock Enemy Spawn Entries`만 사용한다.
- 구형 `Board Count`, `Spawn Term`, `Waves`, `Combat Pacing`, 자연/행동
  진행도 및 레거시 적 풀 필드는 `BattleData`에서 제거됐다.

## 3. 2026-09-30 입력 안정화 수정

### 확인된 원인

1. Cylinder Tempo 적 행동 해결 중 이동 외 행동은 차단되는데, Input System의 `wasPressedThisFrame` 입력을 보존하는 큐가 없어 해제 직전 키 입력과 클릭이 영구적으로 사라졌다.
2. 마우스 사격은 `EventSystem.IsPointerOverGameObject()`를 사용해 실제 버튼뿐 아니라 적 체력바, Tempo HUD, 장식용 이미지 위 클릭까지 전부 UI 클릭으로 판정했다.
3. `PlayerShoot.TryBeginAction()`이 참조, 행동 가능 상태, 탄환 존재 여부보다 먼저 프레임 잠금을 소비해 실패한 입력이 같은 프레임의 정상 입력까지 막을 수 있었다.

### 적용한 수정

- 이동, 회전, 대기, 장전, 사격이 하나의 **최신 입력 버퍼**를 공유한다.
- 입력 보존 기본값은 0.3초다.
- 새 입력이 들어오면 이전 대기 입력을 교체한다. 여러 행동을 순서대로 쌓아 두지 않는다.
- 적 행동이나 짧은 애니메이션 잠금이 끝났을 때 아직 유효한 입력만 실행한다.
- 일시정지, 씬 전환, 명시적인 입력 잠금에서는 버퍼를 즉시 비운다.
- 적 행동 중 이동 회피는 버퍼 대기 없이 기존처럼 즉시 실행한다.
- 마우스 사격은 `Selectable` 또는 클릭·드래그·스크롤 핸들러가 실제로 연결된 UI에서만 차단한다.
- 적 체력바, Cylinder Tempo, 일반 HUD 같은 표시 전용 Graphic은 마우스 사격을 막지 않는다.
- 장전, 사격, 약실 제거는 유효성 검사를 통과한 뒤에만 같은 프레임 중복 방지 상태를 소비한다.

관련 파일:

- `Assets/Scripts/Player/PlayerActionInputBuffer.cs`
- `Assets/Scripts/Player/PlayerMove.cs`
- `Assets/Scripts/Player/PlayerShoot.cs`
- `Assets/Scripts/Player/PlayerShootInputReader.cs`
- `Assets/Editor/Tests/DeckManagerTests.cs`
- `Docs/Dev/0715_Player_PlayerMove.md`
- `Docs/Dev/0717_Combat_DeckManager_PlayerShoot.md`

### 후속 안정화 및 적 페이즈 템포 조정

- 전체 EditMode 테스트의 기존 실패 11개를 현재 에셋과 런타임 규칙에 맞게 정리했다.
- 실제 Play Mode 전환을 포함하는 Cylinder Tempo 통합 테스트를 추가했다. 6칸 도달, 적 페이즈 중 이동 회피 허용, 비이동 입력 차단, 분리된 공격 연출 완료 대기, 이월 공개, 버퍼 장전 1회 실행을 한 흐름으로 검증한다.
- 적 이동·회전·대기의 불필요한 순차 대기는 제거된 기존 병렬 처리를 유지한다. 공격 연출과 회피 가능 시간은 줄이지 않고, 완료된 공격 뒤 다음 적이 있을 때의 간격만 0.05초로 줄였다.
- 적 페이즈는 모든 공격·폭탄 연출이 끝날 때까지 유지하며 최소 표시 시간은 0.1초다.
- Treasure UI는 기존 빌더로 재생성해 선택 버튼의 비활성 Transition과 Relic Panel 외곽선을 복구했다.

## 4. 이전 Battle 연출 및 3D 전환 작업 요약

다음 항목은 이전 대화에서 지속적으로 조정한 영역이다. 후속 수정 전에는 씬과 현재 코드 상태를 다시 조회한다.

- 일반/크리티컬/처치 피격 연출을 구분했다.
- 처치 위치에서 충격파가 항상 발생하도록 월드 좌표와 화면 좌표 변환을 조정했다.
- 처치 시 갈색 먼지 파티클을 제거하고 탄환색 기반 불꽃·별빛·파편 연출을 강화했다.
- 일반 및 크리티컬 명중에도 처치 대비 낮은 강도의 별빛·파편을 적용했다.
- 연속 처치 텍스트를 처치된 적 위치에 표시하고 연속 처치 단계별 색상·셰이더 피드백을 사용했다.
- 대미지 텍스트의 크기와 분산 범위를 조정하고 Perspective 카메라에 맞춘 월드 위치 계산을 보완했다.
- 적 UI와 대미지 텍스트를 카메라 정면으로 보이게 하고, 레인 변경·피격 시 한 프레임 늦게 따라오는 현상을 수정했다.
- Battle 카메라의 씬 위치, 회전, FOV를 런타임 초기값으로 사용하도록 조정했다.
- 캐릭터 발 위치와 그리드/지형 접지를 맞추고 스프라이트형 그림자 방향을 조정했다.
- URP 환경 조명, Fog, Terrain, 로우폴리 배경의 HD-2D 스타일링을 진행했다.
- Terrain은 젖은 반사 느낌을 줄이고 밝은 황토색 황무지 방향으로 조정했다.
- `##--ENVIRONMENT--##/Train` 왕복 이동을 구성했다. 이동 시간 `n`의 마지막 요청값은 35초, 대기 시간 `m`은 10초다.

## 5. 자동 검증 결과

2026-09-30 기준:

- Unity 6000.3.21f1 재컴파일: 성공
- Unity Console 컴파일 오류: 0
- `PlayerActionInputBufferTests`: 2/2 통과
- `PlayerShootInputReaderTests`: 5/5 통과
- `CylinderTempoTests`: 27/27 통과
- `DeckManagerTests`: 27/27 통과
- 전체 EditMode: 720개 중 678개 통과, 0개 실패, 42개 의도적 Skip

현재 콘솔의 남은 경고는 이번 입력 수정에서 새로 발생한 것이 아니다.

- 사용되지 않는 `DuelClockController.SpawnCyclesCommitted`
- 사용되지 않는 `CurrencyManager` 골드 연출 시간 필드
- Train 하위 Door 오브젝트의 음수 스케일 `BoxCollider` 경고가 이전 Play 기록에 존재한다.

## 6. 다음 세션에서 우선 확인할 수동 항목

1. 정상 게임 흐름으로 Battle에 진입한다.
2. 적 행동 종료 직전 `Q`, `E`, `R`, `Space`, 마우스 좌클릭을 각각 눌러 0.3초 안의 입력이 실행되는지 확인한다.
3. 적 행동 중 `A/D/W/S` 이동 회피가 즉시 반응하는지 확인한다.
4. 적 체력바, Tempo HUD, Combo HUD 위를 좌클릭해도 사격되는지 확인한다.
5. 실제 Button, 실린더 드래그 영역, 적 행동 Queue 툴팁 위 클릭은 사격을 막는지 확인한다.
6. 한 입력으로 행동이 두 번 실행되거나 오래된 입력이 뒤늦게 실행되지 않는지 확인한다.
7. 0.3초가 짧거나 길게 느껴지면 `PlayerMove > Action Timing > Input Buffer Duration`을 조정한다.
8. Train이 방향 전환할 때 Door의 `BoxCollider` 음수 스케일 경고가 실제 충돌 문제를 만드는지 확인한다.

## 7. 현재 Git 작업 트리 주의사항

현재 변경 사항은 커밋되지 않은 상태다. 다음 파일들은 기존 Cylinder Tempo 작업과 이번 입력 수정이 함께 포함돼 있으므로 임의로 되돌리거나 전체 복원하지 않는다.

수정된 파일:

- `Assets/Editor/Tests/DeckManagerTests.cs`
- `Assets/Editor/Tests/DuelClockStateTests.cs`
- `Assets/Prefabs/UI/Canvas.prefab`
- `Assets/Scenes/Battle.unity`
- `Assets/Scripts/Manager/BattleData.cs`
- `Assets/Scripts/Manager/DuelClockController.cs`
- `Assets/Scripts/Manager/FirstRunGuideContent.cs`
- `Assets/Scripts/Manager/WaveManager.cs`
- `Assets/Scripts/Player/PlayerMove.cs`
- `Assets/Scripts/Player/PlayerShoot.cs`
- `Assets/Scripts/Player/PlayerShootInputReader.cs`
- `Docs/Dev/0715_Player_PlayerMove.md`
- `Docs/Dev/0717_Combat_DeckManager_PlayerShoot.md`
- `Docs/Dev/0805_CombatFeedback_ComboGold_Kick_CameraShake.md`
- `Docs/Dev/0808_Conversation_Implementation_Log.md`
- `Docs/Dev/0823_DuelClockPrototype.md`

새 파일:

- `Assets/Editor/Tests/CylinderTempoTests.cs`
- `Assets/Materials/CylinderTempoSolarResolve.mat`
- `Assets/Scripts/Common/CylinderTempoHUD.cs`
- `Assets/Scripts/Player/PlayerActionInputBuffer.cs`
- `Assets/Shaders/CylinderTempoSolarResolve.shader`

각 Unity Asset의 `.meta` 파일도 함께 보존한다.

## 8. 다음 작업 시작 절차

1. 이 문서를 완전히 읽는다.
2. `git status --short`로 현재 변경 파일을 다시 확인한다.
3. Unity Editor 연결 상태와 활성 씬을 확인한다.
4. Console 컴파일 오류를 확인한다.
5. 위 수동 항목의 결과를 사용자에게 물어보거나 직접 재현한 뒤 필요한 부분만 수정한다.
6. 기존 Scene, Prefab, `.meta` 파일을 전체 복원하거나 생성기로 재작성하지 않는다.
