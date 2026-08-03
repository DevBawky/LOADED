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
- 일반 명중과 치명타에 서로 다른 길이의 히트 스톱

### 처치

- 일반 명중보다 긴 히트 스톱
- 적 스프라이트를 복제한 처치 잔상이 뒤로 밀리고 들리면서 회전 및 소멸
- 일반 명중보다 많은 탄환색 불꽃과 갈색 사막 먼지
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
