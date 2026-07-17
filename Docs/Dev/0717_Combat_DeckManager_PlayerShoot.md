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
* 연속 발사 중 장전 또는 추가 발사를 실행하지 않습니다.
* 발사된 탄환 중 `doesNotConsumeTurn`이 false인 탄환이 하나라도 있으면 전체 발사 완료 후 턴을 한 번 소비합니다.
* 이동하는 Projectile 대신 LineRenderer를 즉시 표시합니다.
* LineRenderer 시작점은 Fire Point, 끝점은 바라보는 방향에서 최대 사거리 안의 가장 먼 유효 타일 정중앙으로 설정합니다.
* 최대 사거리가 보드 경계를 넘으면 마지막 유효 타일의 정중앙을 끝점으로 사용합니다.
* Line Material이 비어 있으면 프리팹의 Material을 유지하고 Line Color를 적용합니다.
* 공격력, 관통, 밀어내기, 기절, 표식 데이터는 이후 적중 코드가 사용할 수 있도록 발사선에 전달합니다.
* 설치된 Cinemachine 버전에 맞는 Basic Multi Channel Perlin의 Amplitude Gain과 Frequency Gain을 사용합니다.
* 발사 반동이 끝나면 두 Gain을 0으로 만들고 Main Camera를 원래 위치 `(0, 0, -10)`으로 복구합니다.
* Orthographic Size 5에서 과도하게 흔들리지 않도록 탄환 반동값에 작은 Recoil Scale을 적용합니다.
* 모든 참조는 Inspector에서 직접 연결하고 런타임 자동 탐색을 사용하지 않습니다.
* 별도의 적 시스템, 조준, 도탄, 분열, 폭발, 당기기, 대각선 공격은 추가하지 않습니다.

작업 후 Unity Inspector 설정, 입력 및 UI 연결, 턴 처리, Play Mode 테스트 순서와 컴파일 결과를 설명해주세요.

### Output Summary

실제로 수정한 파일은 `BulletData.cs`, `BulletLine.cs`, `DeckManager.cs`, `BoardManager.cs`, `PlayerMove.cs`, `PlayerShoot.cs`, `Bullet.prefab`, `0717_Combat_DeckManager_PlayerShoot.md`이며 기존 `BulletProjectile.cs`는 `BulletLine.cs`로 대체했다.

`BulletData`에 `doesNotConsumeTurn`과 `[Min(0f)]`가 적용된 `recoilStrength`를 추가했다. 두 필드는 기존 필드 뒤에 추가했으며, 후속 LineRenderer 변경에서 시각 필드는 `FormerlySerializedAs`를 사용해 기존 탄환 에셋 값을 유지했다.

`DeckManager`는 `startingBullets`를 `Awake`에서 런타임 `deck`으로 복사하고 Fisher-Yates 방식으로 셔플한다. 리스트의 마지막 요소를 덱의 위로 사용한다. `maxReloadAmount`는 기본 6이며, `TryReload`를 호출할 때마다 덱 위 탄환 한 발을 `loadedBullets` 끝에 추가한다. 덱이 비어 있으면 `graveyard` 전체를 덱으로 옮겨 다시 셔플한다. 최대 장전 수량에 도달하거나 덱과 무덤이 모두 비면 장전하지 않는다.

현재 셔플 순서인 `deck`, 장전 순서인 `loadedBullets`, 발사 완료 순서인 `graveyard`는 `Runtime State` 아래의 직렬화 필드로 선언해 Play Mode Inspector에서 확인할 수 있다. 외부 UI에는 `Deck`, `LoadedBullets`, `Graveyard`, `MaxReloadAmount` 읽기 전용 프로퍼티와 `StateChanged` 이벤트를 제공한다. `TryFireLoadedBullet`은 `loadedBullets`의 첫 번째 탄환을 제거해 무덤 끝에 추가한다.

`PlayerShoot`은 R, Space, 마우스 왼쪽 버튼을 새 Input System으로 처리하고 공개 `Reload`, `Shoot` 메소드를 제공한다. `shotInterval`은 기본 0.2초이며, `Shoot`은 Coroutine을 시작해 장전 목록의 첫 탄환부터 전부 순차 발사한다. 연속 발사 중에는 `isFiring`으로 R, Space, 마우스 발사와 UI의 Reload 및 Shoot 재호출을 막는다. `Time.frameCount`를 이용한 같은 프레임 중복 방어도 유지했다. UI 위의 마우스 클릭은 Inspector에서 연결한 `EventSystem`으로 판별해 월드 발사 입력에서 제외한다.

성공한 한 발 장전은 `PlayerMove.CompleteTurn`을 호출한다. 연속 발사에서는 실제 발사된 탄환 중 `DoesNotConsumeTurn`이 false인 탄환이 하나라도 있을 때 모든 발사가 끝난 뒤 `CompleteTurn`을 한 번만 호출한다. 모든 탄환이 턴 비소비 탄환이면 턴을 소비하지 않는다. 실패한 장전과 한 발도 발사하지 못한 Shoot에서는 호출하지 않는다. 기존 `TurnCount`와 `TurnCompleted` 이벤트를 그대로 사용하기 위해 `CompleteTurn`의 접근 범위만 public으로 변경했다.

`BulletLine`은 전달된 `BulletData`를 `Data`에 보관하여 공격력, 관통 확률, 밀어내기, 기절, 표식 정보를 이후 적중 코드에서 읽을 수 있게 한다. 현재 적 시스템이 없어 실제 피해와 상태 효과 적용은 구현하지 않았다. 이동하는 Projectile은 제거했으며 발사선은 `lineDuration` 동안 표시된 뒤 자동 제거된다.

`BoardManager.TryGetRangedTilePosition`은 플레이어의 현재 타일 인덱스에서 바라보는 방향으로 `MaxRange`칸 떨어진 인덱스를 계산하고 보드 범위로 제한한다. LineRenderer의 시작점은 `firePoint.position`, 끝점은 계산된 타일의 로컬 `(x, 0, 0)`을 월드 좌표로 변환한 정중앙 위치다. 바라보는 방향에 유효한 다음 타일이 전혀 없으면 발사는 실패하며 탄환과 턴을 소비하지 않는다.

발사 초기화 시 LineRenderer의 위치 개수를 2로 설정하고 시작점과 끝점을 즉시 적용한다. Line Material이 null이 아닐 때만 `sharedMaterial` 참조를 교체하므로 null이면 프리팹 Material이 유지된다. Line Color는 시작 색상과 끝 색상에 함께 적용한다. 공유 Material 속성이나 `BulletData` 원본은 수정하지 않는다. 기존 `trailMaterial`, `trailColor` 필드는 `FormerlySerializedAs`를 사용해 `lineMaterial`, `lineColor`로 변경하여 기존 에셋 값을 유지했다. Bullet 프리팹에는 `lineRenderer`와 `lineDuration`이 직렬화되어 있다.

`PlayerShoot`의 프리팹 참조 필드는 `projectilePrefab`에서 `bulletLinePrefab`으로 변경했으며 `FormerlySerializedAs`를 적용해 기존 Inspector 참조가 유지되도록 했다. Inspector에서는 `Bullet Line Prefab`에 LineRenderer가 연결된 Bullet 프리팹을 할당한다.

Inspector에서 `DeckManager > Deck Settings > Max Reload Amount`로 최대 장전 수량을 조절하고, `PlayerShoot > Shot Interval`로 각 발사 사이의 시간을 조절한다. 연속 발사 도중 일시정지하면 대기 시간도 진행하지 않으며 재개 후 남은 간격부터 계속된다.

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
  * 장전 및 발사의 성공·실패별 `CompleteTurn` 호출 조건 확인
  * R, Space, 마우스 왼쪽 버튼과 한 프레임 중복 방어 코드 확인
  * 현재 타일, 바라보는 X축 방향, `MaxRange`, 보드 경계를 이용한 끝점 타일 인덱스 계산 확인
  * LineRenderer 시작점과 유효 사거리 타일 정중앙 끝점 적용 확인
  * null Line Material 유지, Line Color 적용, 공유 에셋 미수정 확인
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
  * 발사된 모든 탄환의 `DoesNotConsumeTurn`이 true이면 정상 발사와 무덤 이동 후에도 턴을 소비하지 않는다.
  * `RecoilStrength` 또는 `CameraRecoilScale`이 0이면 Perlin Gain을 변경하지 않는다.
  * 기본값에서 목표 Amplitude Gain은 `RecoilStrength * 0.02`, Frequency Gain은 0.8이다.
  * 반동은 0.1초 Attack과 0.45초 Recovery 및 5차 SmootherStep으로 처리된다.
  * Recovery 동안 Camera 위치도 원위치로 보간되고, 완료 후 두 Gain은 0이 되며 Camera 위치는 정확히 `(0, 0, -10)`으로 설정된다.
  * 코드 계산상 최대 사거리 타일이 보드 안에 있으면 해당 타일 정중앙이 끝점이 되고, 보드를 벗어나면 바라보는 방향의 마지막 타일 정중앙으로 제한된다.
  * Line Material이 null이면 프리팹 Material을 유지하고 Line Color는 양 끝 색상에 적용된다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode 검증은 수행하지 않았다.

* 발견한 문제:

  * 현재 적과 적중 처리 시스템이 없어 공격력, 관통, 밀어내기, 기절, 표식의 실제 적용 대상이 없다. 이번 범위에서는 `BulletLine.Data` 연결까지만 구현했다.
  * UI 버튼 클릭은 마우스 왼쪽 발사와 동시에 해석될 수 있으므로 `eventSystem` 참조를 추가하고 UI 위의 포인터 입력을 발사에서 제외했다.
  * 연속 발사 중 추가 입력이 들어오면 장전 목록과 Coroutine 상태가 충돌할 수 있으므로 `isFiring` 동안 모든 장전 및 발사 진입을 차단했다.
  * IDE용 Unity 생성 프로젝트 파일이 변경된 스크립트 파일명을 즉시 반영하지 않아 컴파일 검증 때 `BulletLine`과 Cinemachine 참조를 빌드 입력에 명시했다. 검증 후 생성 프로젝트 파일 변경은 원상 복구했다.

### Human Modifications

사용자가 직접 수정한 코드는 없다.

Cinemachine 3.1.7 패키지와 Scene의 Cinemachine Brain 및 Cinemachine Camera는 작업 시작 전에 이미 존재했다. 작업 중이던 `SampleScene.unity`, `Packages/manifest.json`, `Packages/packages-lock.json` 변경은 보존했으며 Codex가 수정하지 않았다.

Player 프리팹과 Scene에는 프로젝트별 BulletData 목록과 카메라 설정이 필요하다. 기존 Bullet 프리팹의 TrailRenderer는 LineRenderer로 교체하고 `BulletLine`의 `Line Renderer` 필드에 직접 연결했으며, 기존 Sprite 오브젝트는 비활성화했다. Scene의 기존 Basic Multi Channel Perlin과 Main Camera Transform은 `PlayerShoot`에 직접 연결했다.

### Final Result

보유 탄환은 게임 시작 시 복사·셔플된 덱, 최대 수량까지 누적되는 장전 탄환 목록, 발사된 탄환이 쌓이는 무덤으로 관리된다. 덱이 소진되면 다음 장전 시 무덤 전체가 다시 덱으로 순환한다. 세 목록은 Play Mode의 DeckManager Inspector에서 현재 순서대로 확인할 수 있다.

R 키와 공개 `Reload` 메소드는 최대 장전 수량까지 한 발씩 추가 장전한다. Space 키·마우스 왼쪽 버튼·공개 `Shoot` 메소드는 장전된 모든 탄환을 설정된 시간 간격으로 플레이어가 바라보는 X축에 발사한다. 기본값은 최대 6발과 발사 간격 0.2초다. 성공 여부와 발사 묶음의 탄환 설정에 따라 기존 `PlayerMove`의 턴 카운트와 이벤트가 갱신된다.

발사 시 Fire Point에서 유효 사거리의 마지막 타일 정중앙까지 LineRenderer가 즉시 표시된다. 모든 전투 데이터는 발사선의 `Data`에서 사용할 수 있으나 적 시스템이 없어 실제 적중 효과는 구현하지 않았다.

Cinemachine Basic Multi Channel Perlin의 Gain은 탄환의 `RecoilStrength`와 `Camera Recoil Scale`에 비례해 5차 SmootherStep으로 부드럽게 상승·감소한다. 낮은 Frequency Gain과 긴 Recovery를 사용하며 Camera 위치도 Recovery 동안 함께 원위치로 보간한다. 반동이 끝나면 Gain을 모두 0으로 만들고 Main Camera를 정확히 `(0, 0, -10)`으로 복구한다. C# 빌드에서 경고와 오류가 발생하지 않았다. Unity Play Mode 최종 검증은 별도로 필요하다.

### Lessons Learned

덱의 위가 리스트의 어느 방향인지 명시해야 장전 순서와 제거 비용을 일관되게 관리할 수 있다. 이번 구현에서는 마지막 요소를 위로 정해 한 발 장전을 간단하게 처리했다.

입력 처리와 UI 버튼이 같은 공개 메소드를 호출하더라도 UI 클릭 자체가 마우스 발사 입력이 될 수 있으므로 포인터가 UI 위에 있는지 별도로 구분해야 한다.

턴 소비는 입력 발생 여부가 아니라 실제 상태 변경의 성공 여부를 기준으로 처리해야 빈 덱, 미장전, 누락 참조 상황에서 잘못된 턴 증가를 막을 수 있다.

여러 탄환을 하나의 Shoot 행동으로 발사할 때는 개별 탄환마다 턴을 올리지 않고, 발사 묶음 안에 턴 소비 탄환이 포함되어 있는지를 누적한 뒤 완료 시 한 번 처리해야 한다.

Cinemachine Basic Multi Channel Perlin은 Noise Profile을 교체하지 않고 Amplitude Gain과 Frequency Gain만 조절해 발사 순간의 흔들림을 만들 수 있다.

Orthographic Size가 작은 2D 카메라에서는 탄환의 수치 데이터를 Amplitude Gain으로 직접 사용하지 않고 별도의 작은 Recoil Scale을 두어야 연속 발사에서도 화면이 과도하게 튀지 않는다. 또한 반동 종료 시 Gain과 기준 Camera 위치를 함께 복구하면 누적 흔들림이나 위치 이탈을 방지할 수 있다.

다음 작업에서는 발사선 위 적의 정렬 순서, 관통 성공 후 다음 적 판정, 피해와 상태 효과 적용 인터페이스를 정의해야 한다.
