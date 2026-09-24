# 장전·발사·명중·처치 연출

## 구현 범위

외부 이미지나 파티클 에셋을 요구하지 않는 코드 기반 전투 연출을 추가했다.
모든 색상은 현재 발사한 `BulletInstance.PrimaryLineColor`를 기준으로 자동
결정되므로 기존 특수 탄환 데이터가 그대로 시각 테마가 된다.

### 장전

- 실린더 UI가 확대됐다가 복귀하는 펀치 애니메이션
- 새로 들어온 탄환 아이콘을 흰색과 탄환 고유색으로 짧게 강조
- 기존 실린더 60도 회전 애니메이션과 동시에 재생
- 장전 시 화면 플래시는 사용하지 않음

### 발사

- Fire Point에서 절차적 셰이더 기반 총구 화염과 회전하는 에코 레이어 생성
- 총구 위치를 중심으로 화면 전체에 원형 충격파가 퍼짐
- 화염 셰이더가 코어, 전방 화염, 방사형 광선, 팽창 링을 한 번에 합성
- 총구 전방으로 탄환색 불씨와 사막색 연기가 흩어짐
- 0.012초의 발사 스냅으로 반동이 시작되는 순간을 강조
- 치명타 발사는 총구 화염, 충격파, 파편을 약 1.3배 강화
- 셰이더를 사용할 수 없는 환경에서는 기존 별 모양 스프라이트로 자동 폴백
- 기존 카메라 반동과 탄환 피드백 이미지는 그대로 유지
- 기존 탄도를 흰색 코어, 탄환색 본선, 외곽 글로우의 3겹으로 확장

### 명중

- 적 스프라이트의 흰색/탄환색 잔상 플래시
- 탄환 진행 방향으로 튀는 불꽃과 먼지 조각
- `VFX_EnemyHit` Particle System의 짧은 탄환색 스파크와 먼지
- 일반 명중은 짧은 방향성 관통선, 뒤쪽 충격 표식, 작은 중심점으로 타격 방향을 표시한다. 별도의 방사형 렌즈 반짝임은 사용하지 않는다.
- 일반 명중과 치명타에 서로 다른 길이의 히트 스톱

치명타는 `VFX_EnemyCriticalHit`을 사용한다. 일반 명중보다 긴 금빛
파편과 원형 파열 링을 추가해 파티클 수뿐 아니라 실루엣으로도 구분한다.

### 처치

- 일반 명중보다 긴 히트 스톱
- 적 스프라이트를 복제한 처치 잔상이 뒤로 밀리고 들리면서 회전 및 소멸
- 일반 명중보다 많은 탄환색 불꽃과 갈색 사막 먼지
- `VFX_EnemyDefeat` Particle System의 적 실루엣 파편, 불씨, 먼지 구름
- 강한 전체 화면 플래시
- 직접 피해뿐 아니라 치명타 조건부 효과 등 직접 피해 전에 발생한 처치도 처리

## 적용 상태

`Player.prefab` 루트에 `CombatPresentation`이 추가되고 `PlayerShoot`에
연결되어 있다. `Stage 1`은 해당 Player 프리팹 인스턴스를 사용하므로 별도
씬 작업 없이 Play하면 적용된다.

다른 플레이어 프리팹이나 별도 테스트 씬에서 `PlayerShoot`만 사용하는
경우에도 `Awake`에서 `CombatPresentation`을 자동으로 추가한다. 다만 이
경우 기본값으로 동작하며 값을 저장해 조절하려면 해당 GameObject에
`CombatPresentation`을 직접 추가하는 것이 좋다.

## Inspector에서 변경하기

### 전체 연출

Player 프리팹의 `Combat Presentation` 컴포넌트:

- `Presentation Enabled`: 모든 신규 연출을 한 번에 켜거나 끈다.
- `Intensity`: 파편 수, 히트 스톱, 화면 플래시 및 이동량의 전체 배율이다.
  `0.65`는 절제된 연출, `1`은 기본, `1.35` 이상은 강한 연출에 적합하다.

### 장전

`Player Cylinder UI > Reload Presentation`:

- `Reload Punch Scale`: 실린더가 커지는 최대 배율
- `Reload Punch Duration`: 확대와 복귀에 걸리는 전체 시간

### 발사

`Combat Presentation > Muzzle Flash`:

- `Muzzle Flash Duration`: 총구 섬광 수명
- `Muzzle Flash Size`: 총구 섬광의 월드 크기
- `Muzzle Ray Count`: 셰이더 방사형 광선 개수
- `Shot Screen Flash Alpha`: 발사 순간의 짧은 보조 플래시 밝기
- `Shot Screen Pulse Duration`: 원형 화면 충격파 지속 시간
- `Shot Screen Pulse Intensity`: 화면 충격파 밝기
- `Muzzle Ember Count`: 총구 불씨와 연기 개수
- `Shot Hit Stop Duration`: 발사 순간의 짧은 스냅 시간

`Muzzle Flash Material`과 `Screen Pulse Material`에는 기본 셰이더
머티리얼이 연결되어 있다. 다른 머티리얼로 교체해도 되며 비워두면
`Shader.Find`를 통해 기본 셰이더를 다시 찾는다.

`Bullet.prefab > Bullet Line > Layered Trail`:

- `Use Layered Trail`: 3겹 탄도 사용 여부
- `Core Width Multiplier`: 흰색 중심선 굵기
- `Glow Width Multiplier`: 외곽 발광선 굵기
- `Glow Alpha`: 외곽 발광선 불투명도

### 명중과 처치

`Combat Presentation > Hit`:

- `Hit Stop Duration`: 일반 명중 정지 시간
- `Critical Hit Stop Duration`: 치명타 정지 시간
- `Hit Flash Duration`: 적 잔상 플래시 시간
- `Hit Spark Count`: 일반 명중 파편 수

`Combat Presentation > Impact Particles`:

- `Normal Impact Particle Prefab`: 일반 피해용 파티클 프리팹
- `Critical Impact Particle Prefab`: 치명타와 대형 피해용 파티클 프리팹
- `Defeat Impact Particle Prefab`: 처치용 파티클 프리팹
- `Impact Particle Density`: 세 프리팹의 공통 방출량 배율
- `Normal Impact Particle Spawn Scale`: 일반 피해 파티클의 균일 크기 배율
- `Critical Impact Particle Spawn Scale`: 치명타와 대형 피해 파티클의 균일 크기 배율
- `Defeat Impact Particle Spawn Scale`: 처치 파티클의 균일 크기 배율

파티클은 피격 위치에 독립적으로 생성되며 적이 제거되어도 남는다. 전투
연출 접근성 설정의 파티클 밀도와 일시정지를 따르고, 게임의 피해·처치
판정에는 관여하지 않는다.

`Combat Presentation > Defeat`:

- `Defeat Hit Stop Duration`: 처치 정지 시간
- `Defeat Afterimage Duration`: 처치 잔상 소멸 시간
- `Defeat Knockback Distance`: 잔상이 밀리는 거리
- `Defeat Lift Height`: 잔상이 위로 뜨는 높이
- `Defeat Spark Count`: 처치 파편 수
- `Defeat Screen Flash Alpha`: 처치 화면 플래시 밝기
- `Defeat Dust Color`: 사막 먼지 색상

## 추천 프리셋

### 기본

- Intensity: `1`
- Hit Stop: `0.035`
- Critical Hit Stop: `0.055`
- Defeat Hit Stop: `0.075`
- Defeat Spark Count: `12`

### 더 화려하게

- Intensity: `1.3`
- Muzzle Flash Size: `0.62`
- Shot Screen Pulse Intensity: `1.7`
- Muzzle Ember Count: `13`
- Shot Screen Flash Alpha: `0.09`
- Defeat Hit Stop: `0.09`
- Defeat Spark Count: `16`
- Defeat Screen Flash Alpha: `0.25`

### 모바일/저사양

- Intensity: `0.7`
- Muzzle Ray Count: `4`
- Muzzle Ember Count: `4`
- Shot Screen Pulse Intensity: `0.8`
- Hit Spark Count: `3`
- Defeat Spark Count: `7`
- Glow Alpha: `0.18`

## 구현 파일

- `Assets/Scripts/Common/CombatPresentation.cs`
- `Assets/Scripts/Player/PlayerShoot.cs`
- `Assets/Scripts/Player/PlayerCylinderUI.cs`
- `Assets/Scripts/Bullet/BulletLine.cs`
- `Assets/Shaders/CombatMuzzleFlash.shader`
- `Assets/Shaders/CombatScreenPulse.shader`
- `Assets/Materials/CombatMuzzleFlash.mat`
- `Assets/Materials/CombatScreenPulse.mat`
- `Assets/Prefabs/Player/Player.prefab`

## 검증

- `Assembly-CSharp.csproj`: 오류 0개, 경고 0개
- `Assembly-CSharp-Editor.csproj`: 오류 0개, 경고 0개
- Unity가 새 파일을 프로젝트 파일에 반영하기 전에는 IDE 프로젝트에서 새
  타입을 찾지 못할 수 있다. Unity의 `Assets > Refresh`를 한 번 실행하면
  자동으로 프로젝트 파일이 재생성된다.

실제 화면의 최종 강도는 해상도, 카메라 Orthographic Size, 탄환 머티리얼에
영향을 받으므로 Play Mode에서 위 값만 미세 조정하면 된다.

## 관통 및 오버킬 강조 (2026-09-24)

- 확률 관통 성공에 의존하는 사거리 타일은 확률·관통 횟수·탄환 색상의 원래 알파와 무관하게 알파 0.5로 표시한다. 확정 도달 구간의 색상은 유지하고, 관통 확률 0 뒤는 표시하지 않는다.
- `PlayerShoot`가 이미 확정한 명중 목록에서 다음 대상 또는 탄환 차단물까지 관통하는 경우에만 `CombatPresentation`이 출구 빛줄기를 표시한다. 전장 전체 공격은 관통으로 취급하지 않는다. 기존 흰 픽셀 Sprite를 사용한 두 겹 탄환색 궤적은 0.16초 동안 전방으로 움직이며 사라진다. 추가 관통 추첨은 없다.
- `SoundManager`는 기존 `SFX_Enemy_Hit`을 높은 피치의 관통 강조음으로, `SFX_Enemy_Die`를 낮은 피치의 오버킬 강조음으로 재사용한다. 강조음별 0.08초 제한과 고정 클립 조회로 소리 중첩·추가 난수 소비를 제한한다.
- 오버킬 연출 강도는 `(피해 - 처치 직전 HP - 방어막 흡수량) / 처치 직전 HP`를 0~1로 제한한다. HP를 알 수 없는 처치에는 추정 강도를 넣지 않는다. 기존 최대 HP 기준 통계는 바꾸지 않는다.
- `CombatFeedbackController`의 처치 큐가 강도를 전달하며, `CombatImpactSignaturePresenter`는 처치 잔상 이동 배율을 최대 1.75배 강화한다. 오버킬 강조음은 처치 큐와 같은 시점에 재생한다. 추가 슬로우·히트스톱은 없으며 마지막 적의 0.05배속과 색 반전을 유지한다. 회피·무방비 지속 규칙은 변경하지 않는다.

## 마지막 적 처치 마무리 (2026-09-24)

- `WaveManager.FinalEnemyDefeated`는 살아 있는 적이 없고 Duel Clock의 미등장 풀 또는 레거시 후속 웨이브도 없을 때 전투당 한 번 발생한다. 화면에 잠깐 적이 없는 상태를 최종 처치로 보지 않는다.
- `CombatFeedbackController`는 기존 시간·볼륨 연출 소유자를 통해 짧은 히트스톱 뒤 정확히 0.05배속을 실제 시간 0.5초 동안 유지하고, 0.12초의 빠른 ease-out 복귀로 이전 배속을 회복한다. 최종 연출 중 일반 명중·처치·회피의 시간 효과와 추가 히트스톱은 이를 덮어쓰지 않는다. 시간 효과 비활성화 설정은 유지한다.
- 기존 `KillImpactFullscreen` 셰이더에서 색 반전이 0.3으로 시작해 슬로우 유지와 복귀가 끝나는 0.62초 동안 부드럽게 0으로 감소한다. 기본 연출 강도(0.5) 이상에서 최대 0.3이며, 낮은 강도와 섬광 감소 설정에서는 약해진다. 일시정지·비활성화·파괴 시 배속과 반전 값을 복구한다.
- 최종 승리 판정은 기존 경로와 미결 투척 공격 정산을 유지한다. `StateManager`는 플레이어 행동 정산 후 골드 흡수 표시와 최종 슬로우 복귀를 최대 1.25초 기다린 뒤 기존 클리어 결산 화면을 표시한다. 실제 골드와 승리 상태는 이미 확정되며, UI 누락이나 중단으로 승리가 멈추지 않는다.

## 4회 이상 연속 처치 텍스트

- 기존 발사 시퀀스 내 처치 횟수를 기준으로 1~3회는 현재 색상을 유지한다. 4회부터 표시마다 밝은 랜덤 색상을 선택하며, 전투용 `UnityEngine.Random` 대신 별도 `System.Random`을 사용한다.
- `BulletTypeTextEffect`의 공통 TMP 셰이더를 일반 모드로 재사용해 같은 반짝임 효과를 적용한다. 런타임 머티리얼의 생성·정리는 기존 텍스트 효과 컴포넌트가 담당하며 폰트·프리팹 에셋을 수정하지 않는다.
