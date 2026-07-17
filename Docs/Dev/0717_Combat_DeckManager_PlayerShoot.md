## AI-004: DeckManager & PlayerShoot

### Basic Information

* Date: 260717
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Combat

### Problem

보유 탄환을 덱, 현재 장전된 탄환 목록, 무덤으로 구분해 순환시키고 키보드·마우스·UI에서 동일한 장전 및 발사 동작을 실행할 구조가 필요했다. 런타임에 현재 셔플 순서와 각 영역의 탄환을 Inspector에서도 확인할 수 있어야 했다.

발사 시 탄환별 턴 소비 여부와 카메라 반동을 적용해야 했으며, 기존 `BulletData`와 `PlayerMove`의 턴 이벤트 구조를 유지해야 했다. 최초 이동 투사체 방식은 후속 요청에 따라 즉시 표시되는 LineRenderer 방식으로 변경해야 했다. 현재 적 시스템이 없으므로 공격력과 상태 효과 데이터를 임의의 적 구현 없이 이후 적중 코드에 전달할 연결 지점도 필요했다.

### Why AI Was Used

덱이 비었을 때 무덤을 회수하고 다시 셔플하는 순환 규칙, 최대 장전 수량, 여러 탄환의 시간차 발사, 발사 묶음의 턴 소비 조건과 중복 입력 방어를 일관된 구조로 구현하기 위해 AI를 사용했다.

또한 설치된 Cinemachine 버전에 맞는 Basic Multi Channel Perlin API를 확인하고, 런타임 자동 탐색 없이 Inspector 참조만으로 발사선과 카메라 반동을 연결하기 위해 도움을 받았다.

### Main Instructions

기존 `BulletData`를 확장하고 `DeckManager`, 장전 및 발사 기능을 구현해주세요.

* `BulletData`에 `doesNotConsumeTurn`과 음수가 될 수 없는 `recoilStrength`를 추가합니다.
* Inspector의 보유 탄환 목록을 복사하여 런타임 덱을 만들고 Fisher-Yates 방식으로 셔플합니다.
* `maxReloadAmount`를 추가하고 기본값을 6으로 설정합니다.
* Reload 한 번에 덱의 가장 위 탄환 하나를 추가 장전하며 최대 장전 수량까지 반복 장전할 수 있습니다.
* 장전된 여러 탄환은 장전 순서대로 발사한 뒤 각각 무덤으로 이동합니다.
* 빈 덱에서 장전할 때 무덤 전체를 덱으로 이동하고 다시 셔플합니다.
* 덱과 무덤이 모두 비었거나 최대 장전 수량에 도달한 경우에는 장전하지 않습니다.
* 현재 덱 셔플 순서, 장전 탄환 목록, 무덤을 Inspector 직렬화 변수로 표시합니다.
* 덱, 장전 탄환, 무덤 상태를 UI가 읽을 수 있도록 읽기 전용 프로퍼티와 이벤트를 제공합니다.
* R 키는 장전, Space 키와 마우스 왼쪽 버튼은 발사에 사용합니다.
* UI 연결을 위해 `Reload`와 `Shoot`을 공개 메소드로 구현합니다.
* 성공한 장전은 턴을 소비하고 실패한 장전과 발사는 턴을 소비하지 않습니다.
* `shotInterval`을 추가하고 기본값을 0.2초로 설정합니다.
* Shoot 한 번에 장전된 탄환을 `shotInterval` 간격으로 전부 순차 발사합니다.
* 연속 발사 중 장전, 추가 발사, 이동, 회전, 대기를 포함한 모든 플레이어 행동을 실행하지 않습니다.
* 탄환 LineRenderer는 설정한 시간 동안 알파가 서서히 0으로 감소한 뒤 제거됩니다.
* 발사된 탄환 중 `doesNotConsumeTurn`이 false인 탄환이 하나라도 있으면 전체 발사 완료 후 턴을 한 번 소비합니다.
* 이동하는 Projectile 대신 LineRenderer를 즉시 표시합니다.
* LineRenderer 시작점은 Fire Point, 끝점은 바라보는 방향과 최대 사거리 안에서 관통 판정까지 통과한 마지막 적의 위치로 설정합니다.
* 유효 사거리 안에 적이 없어도 발사를 실행하고 탄환 및 턴 규칙을 정상 처리합니다.
* 최대 사거리가 보드 경계를 넘으면 마지막 유효 타일의 정중앙을 끝점으로 사용합니다.
* Line Material이 비어 있으면 프리팹의 Material을 유지하고 Primary/Secondary Line Color를 적용합니다.
* 공격력, 관통, 밀어내기, 기절, 표식 데이터는 이후 적중 코드가 사용할 수 있도록 발사선에 전달합니다.
* 설치된 Cinemachine 버전에 맞는 Basic Multi Channel Perlin의 Amplitude Gain과 Frequency Gain을 사용합니다.
* 발사 반동이 끝나면 두 Gain을 0으로 만들고 Main Camera를 원래 위치 `(0, 0, -10)`으로 복구합니다.
* Orthographic Size 5에서 과도하게 흔들리지 않도록 탄환 반동값에 작은 Recoil Scale을 적용합니다.
* 모든 참조는 Inspector에서 직접 연결하고 런타임 자동 탐색을 사용하지 않습니다.
* 별도의 적 시스템, 조준, 도탄, 분열, 폭발, 당기기, 대각선 공격은 추가하지 않습니다.

작업 후 Unity Inspector 설정, 입력 및 UI 연결, 턴 처리, Play Mode 테스트 순서와 컴파일 결과를 설명해주세요.

### Output Summary

실제로 수정한 파일은 `BulletData.cs`, `BulletLine.cs`, `DeckManager.cs`, `BoardManager.cs`, `WaveManager.cs`, `PlayerMove.cs`, `PlayerShoot.cs`, `ActorMotion.cs`, `Bullet.prefab`, `Player.prefab`, `Test Enemy.prefab`, `SampleScene.unity`, `Test Bullet.asset`, `BulletSmokeFlameLine.shader`, `BulletSmokeFlameLine.mat`, `0717_Combat_DeckManager_PlayerShoot.md`이며 기존 `BulletProjectile.cs`는 `BulletLine.cs`로 대체했다.

`BulletData`에 `doesNotConsumeTurn`과 `[Min(0f)]`가 적용된 `recoilStrength`를 추가했다. 두 필드는 기존 필드 뒤에 추가했으며, 후속 LineRenderer 변경에서 기존 `lineColor`를 `primaryLineColor`로 확장하고 `FormerlySerializedAs`를 사용해 기존 탄환 에셋 값을 유지했다. `secondaryLineColor`를 별도로 추가해 두 색을 탄환 SO마다 설정할 수 있다.

`DeckManager`는 `startingBullets`를 `Awake`에서 런타임 `deck`으로 복사하고 Fisher-Yates 방식으로 셔플한다. 리스트의 마지막 요소를 덱의 위로 사용한다. `maxReloadAmount`는 기본 6이며, `TryReload`를 호출할 때마다 덱 위 탄환 한 발을 `loadedBullets` 끝에 추가한다. 덱이 비어 있으면 `graveyard` 전체를 덱으로 옮겨 다시 셔플한다. 최대 장전 수량에 도달하거나 덱과 무덤이 모두 비면 장전하지 않는다.

현재 셔플 순서인 `deck`, 장전 순서인 `loadedBullets`, 발사 완료 순서인 `graveyard`는 `Runtime State` 아래의 직렬화 필드로 선언해 Play Mode Inspector에서 확인할 수 있다. 외부 UI에는 `Deck`, `LoadedBullets`, `Graveyard`, `MaxReloadAmount` 읽기 전용 프로퍼티와 `StateChanged` 이벤트를 제공한다. `TryFireLoadedBullet`은 `loadedBullets`의 첫 번째 탄환을 제거해 무덤 끝에 추가한다.

`PlayerShoot`은 R, Space, 마우스 왼쪽 버튼을 새 Input System으로 처리하고 공개 `Reload`, `Shoot` 메소드를 제공한다. `shotInterval`은 기본 0.2초이며, `Shoot`은 Coroutine을 시작해 장전 목록의 첫 탄환부터 전부 순차 발사한다. 연속 발사 중에는 `isFiring`으로 R, Space, 마우스 발사와 UI의 Reload 및 Shoot 재호출을 막는다. 동시에 `PlayerMove.SetShooting(true)`를 전달해 키보드·마우스 및 UI에서 호출되는 이동, 회전, 대기도 모두 차단한다. 발사 묶음의 턴 완료 처리까지 끝난 뒤 잠금을 해제하며 PlayerShoot이 비활성화될 때도 잠금 상태를 복구한다. `Time.frameCount`를 이용한 같은 프레임 중복 방어도 유지했다. UI 위의 마우스 클릭은 Inspector에서 연결한 `EventSystem`으로 판별해 월드 발사 입력에서 제외한다.

성공한 한 발 장전은 `PlayerMove.CompleteTurn`을 호출한다. 연속 발사에서는 실제 발사된 탄환 중 `DoesNotConsumeTurn`이 false인 탄환이 하나라도 있을 때 모든 발사가 끝난 뒤 `CompleteTurn`을 한 번만 호출한다. 모든 탄환이 턴 비소비 탄환이면 턴을 소비하지 않는다. 실패한 장전과 한 발도 발사하지 못한 Shoot에서는 호출하지 않는다. 기존 `TurnCount`와 `TurnCompleted` 이벤트를 그대로 사용하기 위해 `CompleteTurn`의 접근 범위만 public으로 변경했다.

`BulletLine`은 전달된 `BulletData`를 `Data`에 보관한다. 후속 Enemy 시스템 연결에서 `PlayerShoot`이 정면 사거리 안의 적을 가까운 순서로 찾고 공격력과 관통 확률을 적용하도록 확장했다. 밀어내기, 기절, 표식 실행은 아직 구현하지 않았다. 이동하는 Projectile은 제거했으며 발사선은 `Fade Duration` 동안 시작 색상과 끝 색상의 알파를 SmoothStep으로 0까지 보간한 뒤 자동 제거된다. 일시정지 중에는 페이드 시간이 진행되지 않는다.

`WaveManager.GetEnemiesInDirection`은 플레이어 타일을 기준으로 바라보는 방향과 `MaxRange` 안의 적을 가까운 순서로 제공한다. 수집 시 각 적의 타일 거리와 타일 인덱스를 한 번만 저장하고 그 값으로 정렬해, 정렬 비교 도중 Transform 위치를 다시 조회하며 순서가 달라지는 경로를 제거했다. 첫 적은 확정 적중하고 이후 적은 `Penetration Chances`를 단계별로 판정한다. `PlayerShoot`은 가장 앞의 적에게 피해를 먼저 적용하며, 첫 피해 적용이 실패하면 뒤쪽 관통 대상의 피해도 중단한다. 적이 있으면 LineRenderer 끝점은 마지막 적중 적의 위치다. 적이 없으면 바라보는 방향의 최대 사거리 내 가장 먼 유효 타일 중앙까지 발사하고, 보드 끝에서 바깥을 바라보는 경우에만 Fire Point에서 사거리만큼 전방을 fallback 끝점으로 사용한다. 빗나간 탄환도 장전 목록에서 무덤으로 이동하고 반동과 `DoesNotConsumeTurn` 규칙을 정상 적용한다.

발사 초기화 시 LineRenderer의 위치 개수를 2로 설정하고 시작점과 끝점을 즉시 적용한다. Line Material이 null이 아닐 때만 `sharedMaterial` 참조를 교체하므로 null이면 프리팹 Material이 유지된다. Primary Line Color는 시작 색상과 끝 색상에 함께 적용하고 Secondary Line Color는 `MaterialPropertyBlock`으로 해당 LineRenderer 인스턴스에만 전달한다. 공유 Material 속성이나 `BulletData` 원본은 수정하지 않는다. 기존 `trailMaterial`, `trailColor`, `lineColor` 필드는 `FormerlySerializedAs`를 사용해 기존 에셋 값을 유지한다. `lineDuration`은 `FormerlySerializedAs`를 적용한 `fadeDuration`으로 변경해 기존 프리팹 값을 유지한다. Bullet 프리팹에는 `Line Renderer`와 `Fade Duration`이 직렬화되어 있다.

`BulletSmokeFlameLine.mat`은 URP용 `Loaded/Bullet Smoke Flame Line` 셰이더 하나를 사용한다. 별도 텍스처 없이 다중 단계 노이즈와 domain warp로 끊어진 연기 밀도, 부드러운 양 끝, 밝은 중심부, 흐르는 불꽃과 스파크 알파 마스크를 만든다. LineRenderer 폭 곡선도 시작·중간·끝의 두께가 다른 테이퍼 형태로 변경했다. 셰이더는 vertex color의 Primary와 PropertyBlock의 Secondary를 노이즈 및 코어 마스크로 혼합한다. Material은 Bullet 프리팹의 기본 LineRenderer와 테스트 Bullet의 `Line Material`에 동일한 공유 참조로 연결했으며 런타임에는 Material 속성을 변경하지 않는다.

`PlayerShoot`의 프리팹 참조 필드는 `projectilePrefab`에서 `bulletLinePrefab`으로 변경했으며 `FormerlySerializedAs`를 적용해 기존 Inspector 참조가 유지되도록 했다. Inspector에서는 `Bullet Line Prefab`에 LineRenderer가 연결된 Bullet 프리팹을 할당한다.

Inspector에서 `DeckManager > Deck Settings > Max Reload Amount`로 최대 장전 수량을 조절하고, `PlayerShoot > Shot Interval`로 각 발사 사이의 시간을 조절한다. Bullet 프리팹의 `BulletLine > Fade Duration`으로 각 발사선이 완전히 투명해질 때까지의 시간을 조절한다. 연속 발사 도중 일시정지하면 발사 대기 시간과 LineRenderer 페이드 시간이 모두 진행하지 않으며 재개 후 계속된다.

프로젝트에 설치된 Cinemachine 3.1.7의 `CinemachineBasicMultiChannelPerlin`을 사용한다. `PlayerShoot`은 Inspector에서 `recoilNoise`와 `recoilCameraTransform`을 직접 참조하고 런타임 자동 탐색을 사용하지 않는다. Scene의 Main Camera에 연결된 Noise Profile을 유지하면서 `AmplitudeGain`과 `FrequencyGain`만 런타임에 조절한다.

Orthographic Size 5에서는 작은 월드 좌표 변화도 크게 보이므로 `cameraRecoilScale` 기본값을 `0.02`로 설정했다. 목표 Amplitude Gain은 `BulletData.RecoilStrength * Camera Recoil Scale`로 계산한다. 부드러운 흔들림을 위해 Frequency Gain 기본값은 0.8로 낮추고, Amplitude Gain은 0.1초 동안 목표값까지 상승한 뒤 0.45초 동안 0으로 감소한다. 새로운 탄환이 발사되면 현재 Gain에서 새 목표값으로 다시 보간하여 연속 발사 반동이 끊기지 않게 한다.

Gain과 Camera 위치 보간에는 기존 `Mathf.SmoothStep`보다 시작과 끝의 가속도 변화까지 부드러운 5차 SmootherStep 공식을 사용한다. Recovery 구간에는 Amplitude Gain 감소와 함께 현재 Camera 위치를 `(0, 0, -10)`으로 보간하며, 종료 시 정확한 원위치와 Gain 0을 다시 적용해 마지막 프레임의 튐을 방지한다.

`Awake`, 반동 Coroutine 종료, `PlayerShoot` 비활성화 시 `ResetCameraRecoil`을 호출한다. 이 메소드는 Amplitude Gain과 Frequency Gain을 모두 0으로 만들고 직렬화된 Camera Transform의 월드 위치를 `cameraRestPosition` 기본값 `(0, 0, -10)`으로 설정한다. Scene의 기존 Basic Multi Channel Perlin Gain도 idle 상태에서 0으로 저장했다.

### Decision

* [x] 그대로 채택
* [ ] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * Inspector 초기 목록과 런타임 덱이 별도 리스트인지 코드 확인
  * Fisher-Yates 반복 범위와 무작위 인덱스 범위 정적 검토
  * 덱, 장전 탄환, 무덤 사이의 이동 규칙 대입 확인
  * `MaxReloadAmount` 도달 전후의 추가 장전 조건 확인
  * 장전 목록의 첫 탄환부터 `ShotInterval` 간격으로 발사되는 Coroutine 정적 검토
  * 연속 발사 중 입력 차단과 일시정지 중 간격 정지 조건 확인
  * 발사 중 PlayerMove의 키보드·마우스·UI 공개 메소드 차단 확인
  * BulletLine의 시작/끝 알파 SmoothStep 보간과 종료 후 제거 확인
  * 장전 및 발사의 성공·실패별 `CompleteTurn` 호출 조건 확인
  * R, Space, 마우스 왼쪽 버튼과 한 프레임 중복 방어 코드 확인
  * 현재 타일, 바라보는 X축 방향, `MaxRange`, 보드 경계를 이용한 끝점 타일 인덱스 계산 확인
  * LineRenderer 시작점과 마지막 적중 Enemy 위치 끝점 적용 확인
  * 유효 적이 없을 때 최대 유효 사거리 타일 끝점, 탄환 소비, 반동 및 턴 조건 확인
  * 적의 타일 거리를 수집 시 캐시해 가까운 순서로 정렬하고 첫 적의 피해를 관통 대상보다 먼저 처리하는지 확인
  * null Line Material 유지, Primary/Secondary Line Color 적용, 공유 에셋 미수정 확인
  * URP Line 셰이더가 vertex color, MaterialPropertyBlock, 인스턴스 알파를 사용하고 공유 Material 속성을 수정하지 않는지 정적 확인
  * Cinemachine 3.1.7 패키지 소스에서 `CinemachineBasicMultiChannelPerlin.AmplitudeGain`, `FrequencyGain` API 확인
  * Orthographic Size 5 기준 Recoil Scale 0.02, Frequency Gain 0.8, Attack 0.1초, Recovery 0.45초 확인
  * 5차 SmootherStep Gain 보간과 Recovery 중 Camera 위치 보간 확인
  * 반동 완료와 컴포넌트 비활성화 시 Gain 0 및 Camera 위치 `(0, 0, -10)` 복구 코드 확인
  * 새 스크립트와 Cinemachine 어셈블리를 빌드 입력에 포함하여 `dotnet build LOADED.slnx --no-restore` 실행
  * Unity Play Mode 실제 입력, 덱 순환, 발사선 위치, 카메라 반동은 수동 검증 항목으로 남김

* 테스트 결과:

  * 초기 보유 탄환은 null 항목을 제외하고 런타임 덱에 복사되며 원본 `startingBullets`는 수정되지 않는다.
  * 장전 시 덱의 마지막 탄환 하나가 `LoadedBullets` 끝에 추가되며 기본 최대 6발까지 반복 장전된다.
  * 발사 시 `LoadedBullets`의 첫 탄환부터 순서대로 제거되어 `Graveyard` 끝에 추가된다.
  * 빈 덱에서 장전하면 무덤 전체가 덱으로 이동하고 셔플된 뒤 한 발이 장전된다.
  * 덱과 무덤이 모두 비어 있거나 장전 수량이 `MaxReloadAmount`에 도달한 경우 상태와 턴이 변경되지 않는다.
  * 기본 설정에서는 장전된 여러 탄환이 0.2초 간격으로 전부 발사된다.
  * 성공한 장전은 한 발마다 턴을 소비하며, 연속 발사 묶음은 턴 소비 탄환이 하나라도 있을 때 완료 후 턴을 한 번 소비한다.
  * 발사 코루틴 시작부터 턴 완료 처리까지 PlayerMove의 이동, 회전, 대기가 잠기고 코루틴 종료 또는 PlayerShoot 비활성화 시 잠금이 해제된다.
  * BulletLine은 Fade Duration 동안 알파가 0까지 감소하며 일시정지 중에는 페이드가 멈춘다.
  * 발사된 모든 탄환의 `DoesNotConsumeTurn`이 true이면 정상 발사와 무덤 이동 후에도 턴을 소비하지 않는다.
  * `RecoilStrength` 또는 `CameraRecoilScale`이 0이면 Perlin Gain을 변경하지 않는다.
  * 기본값에서 목표 Amplitude Gain은 `RecoilStrength * 0.02`, Frequency Gain은 0.8이다.
  * 반동은 0.1초 Attack과 0.45초 Recovery 및 5차 SmootherStep으로 처리된다.
  * Recovery 동안 Camera 위치도 원위치로 보간되고, 완료 후 두 Gain은 0이 되며 Camera 위치는 정확히 `(0, 0, -10)`으로 설정된다.
  * 정면 최대 사거리 안의 적이 거리순으로 선택되고 관통에 성공한 마지막 적의 위치가 끝점이 된다.
  * 정면에 적이 없어도 장전 탄환이 발사되어 무덤으로 이동하고 최대 유효 타일 중앙까지 발사선이 생성된다.
  * 정렬 중 적 위치를 재조회하지 않으며 가장 앞 적의 피해가 성공한 뒤에만 뒤쪽 관통 대상의 피해를 순서대로 적용한다.
  * Line Material이 null이면 프리팹 Material을 유지하며 Primary는 양 끝 vertex color, Secondary는 인스턴스 PropertyBlock에 적용된다.
  * Bullet 프리팹과 테스트 Bullet은 동일한 `BulletSmokeFlameLine` Material을 사용하며 Material 자체의 색상은 런타임에 변경하지 않는다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode 검증은 수행하지 않았다.

* 발견한 문제:

  * 후속 Enemy 시스템에서 공격력과 관통은 실제 적 체력에 연결했다. 밀어내기, 기절, 표식은 실행 대상 상태 컴포넌트가 없어 아직 적용하지 않는다.
  * UI 버튼 클릭은 마우스 왼쪽 발사와 동시에 해석될 수 있으므로 `eventSystem` 참조를 추가하고 UI 위의 포인터 입력을 발사에서 제외했다.
  * 연속 발사 중 추가 입력이 들어오면 장전 목록과 Coroutine 상태가 충돌할 수 있으므로 `isFiring` 동안 장전과 발사 진입을 차단하고 PlayerMove에는 별도의 사격 잠금을 전달했다.
  * IDE용 Unity 생성 프로젝트 파일이 변경된 스크립트 파일명을 즉시 반영하지 않아 컴파일 검증 때 `BulletLine`과 Cinemachine 참조를 빌드 입력에 명시했다. 검증 후 생성 프로젝트 파일 변경은 원상 복구했다.

### Human Modifications

사용자가 직접 수정한 코드는 없다.

Cinemachine 3.1.7 패키지와 Scene의 Cinemachine Brain 및 Cinemachine Camera는 작업 시작 전에 이미 존재했다. 작업 중이던 `SampleScene.unity`, `Packages/manifest.json`, `Packages/packages-lock.json` 변경은 보존했으며 Codex가 수정하지 않았다.

Player 프리팹과 Scene에는 프로젝트별 BulletData 목록과 카메라 설정이 필요하다. 기존 Bullet 프리팹의 TrailRenderer는 LineRenderer로 교체하고 `BulletLine`의 `Line Renderer` 필드에 직접 연결했으며, 기존 Sprite 오브젝트는 비활성화했다. Scene의 기존 Basic Multi Channel Perlin과 Main Camera Transform은 `PlayerShoot`에 직접 연결했다.

### Final Result

보유 탄환은 게임 시작 시 복사·셔플된 덱, 최대 수량까지 누적되는 장전 탄환 목록, 발사된 탄환이 쌓이는 무덤으로 관리된다. 덱이 소진되면 다음 장전 시 무덤 전체가 다시 덱으로 순환한다. 세 목록은 Play Mode의 DeckManager Inspector에서 현재 순서대로 확인할 수 있다.

R 키와 공개 `Reload` 메소드는 최대 장전 수량까지 한 발씩 추가 장전한다. Space 키·마우스 왼쪽 버튼·공개 `Shoot` 메소드는 장전된 모든 탄환을 설정된 시간 간격으로 플레이어가 바라보는 X축에 발사한다. 기본값은 최대 6발과 발사 간격 0.2초다. 발사 중에는 이동, 회전, 대기를 포함한 모든 플레이어 행동이 잠긴다. 성공 여부와 발사 묶음의 탄환 설정에 따라 기존 `PlayerMove`의 턴 카운트와 이벤트가 갱신된다.

각 BulletLine은 프리팹의 `Fade Duration` 동안 RGB를 유지한 채 알파만 부드럽게 0으로 줄어든 뒤 제거된다. SampleScene에서 사용하는 Bullet 프리팹의 기본 Fade Duration은 0.2초다.

발사 시 Fire Point에서 관통 판정을 통과한 마지막 적까지 LineRenderer가 즉시 표시된다. 적중한 적은 `BulletData.Damage`만큼 체력이 감소한다. 유효한 적이 없어도 최대 유효 사거리 타일까지 발사선이 표시되고 장전 탄환은 무덤으로 이동하며, 탄환 데이터에 따라 턴을 소비한다.

Cinemachine Basic Multi Channel Perlin의 Gain은 탄환의 `RecoilStrength`와 `Camera Recoil Scale`에 비례해 5차 SmootherStep으로 부드럽게 상승·감소한다. 낮은 Frequency Gain과 긴 Recovery를 사용하며 Camera 위치도 Recovery 동안 함께 원위치로 보간한다. 반동이 끝나면 Gain을 모두 0으로 만들고 Main Camera를 정확히 `(0, 0, -10)`으로 복구한다. C# 빌드에서 경고와 오류가 발생하지 않았다. Unity Play Mode 최종 검증은 별도로 필요하다.

### Lessons Learned

덱의 위가 리스트의 어느 방향인지 명시해야 장전 순서와 제거 비용을 일관되게 관리할 수 있다. 이번 구현에서는 마지막 요소를 위로 정해 한 발 장전을 간단하게 처리했다.

입력 처리와 UI 버튼이 같은 공개 메소드를 호출하더라도 UI 클릭 자체가 마우스 발사 입력이 될 수 있으므로 포인터가 UI 위에 있는지 별도로 구분해야 한다.

턴 소비는 입력 발생 여부가 아니라 실제 상태 변경의 성공 여부를 기준으로 처리해야 빈 덱, 미장전, 누락 참조 상황에서 잘못된 턴 증가를 막을 수 있다.

여러 탄환을 하나의 Shoot 행동으로 발사할 때는 개별 탄환마다 턴을 올리지 않고, 발사 묶음 안에 턴 소비 탄환이 포함되어 있는지를 누적한 뒤 완료 시 한 번 처리해야 한다.

Cinemachine Basic Multi Channel Perlin은 Noise Profile을 교체하지 않고 Amplitude Gain과 Frequency Gain만 조절해 발사 순간의 흔들림을 만들 수 있다.

Orthographic Size가 작은 2D 카메라에서는 탄환의 수치 데이터를 Amplitude Gain으로 직접 사용하지 않고 별도의 작은 Recoil Scale을 두어야 연속 발사에서도 화면이 과도하게 튀지 않는다. 또한 반동 종료 시 Gain과 기준 Camera 위치를 함께 복구하면 누적 흔들림이나 위치 이탈을 방지할 수 있다.

행동 잠금은 입력을 처리하는 컴포넌트 하나만 막아서는 충분하지 않다. 공개 UI 메소드가 있는 PlayerMove에도 명시적인 사격 상태를 전달해야 키보드와 버튼이 동일한 규칙을 따를 수 있다. 짧은 발사선은 공유 Material의 알파를 변경하지 않고 LineRenderer 인스턴스의 시작·끝 색상 알파를 보간해야 다른 발사선에 영향을 주지 않는다.

관통 대상 정렬에서는 비교 함수 안에서 Transform을 다시 조회하지 않고 수집 순간의 타일 거리 값을 캐시해야 첫 대상 순서를 안정적으로 유지할 수 있다. 또한 Primary는 LineRenderer vertex color, Secondary는 MaterialPropertyBlock으로 전달하면 공용 Material 하나를 유지하면서 탄환별 두 색을 ScriptableObject에서 독립적으로 관리할 수 있다. 직사각형 LineRenderer는 셰이더 알파만 바꾸는 것보다 폭 곡선의 테이퍼, 양 끝 마스크, 밀도 breakup을 함께 적용해야 연기 형태가 분명해진다.
