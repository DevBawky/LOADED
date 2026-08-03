# 전투 타격 피드백 및 Fullscreen Impact 개발 기록

## 기본 정보

- 작성일: 2026-08-03
- 대상 시스템: 플레이어 공격, 적 피해·처치, 콤보, 실린더 누적 대미지, URP Full Screen Pass, 전투 효과음
- 목적: 공격이 명중하는 순간마다 즉각적인 시각·청각 피드백을 제공하고, 일반 명중에서 처치까지 강도가 자연스럽게 상승하는 전투 연출을 구축한다.
- 최종 상태: 크리티컬 전체 화면 색상 반전은 사용자 피드백에 따라 제거되었다.

## 개발 목표

이번 작업은 단순히 효과를 많이 겹치는 것이 아니라 다음 원칙을 기준으로 진행했다.

1. 일반 명중, 크리티컬, 처치가 서로 다른 강도로 느껴져야 한다.
2. 짧은 공격 연출은 게임 진행을 방해하지 않으면서도 즉시 인지되어야 한다.
3. 관통이나 연쇄 공격처럼 같은 순간에 여러 적을 타격해도 각 충격 위치가 보존되어야 한다.
4. 화면 효과, UI, 슬로 모션, 카메라 흔들림과 사운드가 같은 전투 이벤트를 기준으로 동기화되어야 한다.
5. 모든 주요 강도와 지속 시간은 Player 프리팹의 Inspector에서 조정할 수 있어야 한다.
6. 일시정지와 연출 종료 후에는 `Time.timeScale`, Volume, UI Transform 및 셰이더 전역 값이 원래 상태로 복구되어야 한다.

## 요구사항 변화 과정

### 1단계: 처치 피드백과 전투 UI

초기 목표는 적 처치 시 다음 피드백을 한 번에 제공하는 것이었다.

- Main Camera의 Volume 효과
  - Chromatic Aberration
  - Bloom
  - Vignette
  - Lens Distortion
  - Contrast
- 짧은 슬로 모션과 부드러운 복귀
- 카메라 흔들림
- 처치 전용 효과음
- 콤보와 콤보 만료 시간 표시
- 현재 실린더의 누적 대미지 표시

UI는 기존 Canvas 구조를 그대로 사용했다.

| 용도 | GameObject 경로 또는 이름 | 표시 방식 |
| --- | --- | --- |
| 콤보 | `Canvas > Panel \| Feedback > Text \| Combo` | `combo <size=128>{콤보 수}</size>` |
| 콤보 타이머 | `Image \| Combo Timer` | 남은 시간 비율을 `Image.fillAmount`에 반영 |
| 실린더 누적 대미지 | `Text \| Current Damage` | `DMG <size=42>{누적 대미지}</size>` |

UI는 숫자만 즉시 교체하지 않고 스케일 오버슈트, 미세 회전, 알파 변화와 색상 전환을 사용해 충격을 전달하도록 구성했다.

### 2단계: Fullscreen Render 연출

Volume 효과만으로는 충격의 방향과 발생 위치를 표현하기 어려워 URP의 Full Screen Pass를 추가했다.

- 명중 위치를 중심으로 퍼지는 충격파
- 초반의 안쪽 압축파와 이후 바깥쪽 팽창파
- 방사형 줌
- 공격 방향에 따른 화면 찢김
- RGB 채널 분리
- 중심 발광과 방사형 광선
- 처치 및 크리티컬 여부에 따른 강도 변화

### 3단계: 회색 화면 문제 해결

첫 Full Screen Pass 적용 후 게임 화면이 원본 장면 대신 회색으로 덮이는 문제가 발생했다.

원인은 셰이더 자체의 색상 계산이 아니라 Full Screen Pass가 카메라 컬러 버퍼를 입력으로 받지 못한 것이었다. 렌더 피처 직렬화에 필요한 버전 값이 없으면 Unity의 마이그레이션 과정에서 `fetchColorBuffer`가 비활성화될 수 있었다.

최종 렌더 피처 설정은 다음과 같다.

```yaml
m_Version: 1
injectionPoint: 600
fetchColorBuffer: 1
requirements: 0
passIndex: 0
bindDepthStencilAttachment: 0
```

`fetchColorBuffer: 1`로 원본 카메라 화면을 `_BlitTexture`에 전달하고, 셰이더는 이 텍스처를 샘플링한 결과 위에 충격 연출을 합성한다. 이 설정으로 게임 화면이 회색으로 대체되는 현상을 해결했다.

### 4단계: 일반 피해까지 피드백 확장

처치에만 강한 효과가 발생하면 공격 과정이 밋밋하게 느껴질 수 있으므로 일반 피해에도 짧은 피드백을 추가했다.

| 이벤트 | 화면 효과 | 카메라 | 시간 효과 | 사운드 |
| --- | --- | --- | --- | --- |
| 일반 명중 | 짧은 압축파, 충격파, 약한 RGB 분리 | 작은 흔들림 | 없음 | Hit Accent |
| 크리티컬 | 일반 명중보다 강한 왜곡과 채널 분리 | 강화된 흔들림 | 없음 | Hit + Critical Accent |
| 처치 | 큰 충격파, 방사형 광선, Volume Pulse | 강한 흔들림 | 슬로 모션 | Hit + Kill Accent |
| 크리티컬 처치 | 처치 효과와 크리티컬 강조 결합 | 강한 흔들림 | 슬로 모션 | Hit + Critical + Kill Accent |
| 마지막 적 처치 | 가장 긴 충격파와 슬로 모션 | 최대 강조 | 연장된 슬로 모션 | 강화된 Kill Accent |

크리티컬 전용 1프레임 전체 색상 반전도 한 차례 구현했으나 최종 피드백에 따라 제거했다. 현재 크리티컬은 전용 크랙음, 강한 RGB 분리, 왜곡과 카메라 흔들림으로만 구분한다.

### 5단계: 사운드 강화

기존 단일 처치음은 다른 게임 사운드에 묻히기 쉬웠다. 이를 해결하기 위해 역할이 다른 세 레이어로 분리했다.

- `Hit Accent`: 짧은 노이즈 어택, 중저역 바디와 금속성 고역
- `Critical Accent`: 강한 크랙, 저역 펀치와 짧은 고역 링
- `Kill Accent`: 긴 서브베이스, 바디, 클릭과 짧은 차임

별도 AudioClip을 지정하지 않으면 위 특성을 가진 기본 모노 클립을 런타임에 생성한다. 프로젝트 전용 효과음을 Inspector에 할당하면 런타임 생성음 대신 해당 클립을 사용한다.

추가 적용 사항은 다음과 같다.

- 타격 위치의 화면 X 좌표에 따른 스테레오 패닝
- 현재 실린더의 발사 진행도에 따른 피치 상승
- 처치 및 콤보 단계에 따른 저역 피치와 볼륨 변화
- 타격 순간 기존 게임 오디오에 짧은 Low Pass Duck 적용
- 타격 피드백 AudioSource는 Listener Effect를 우회해 선명도를 유지
- 일반 명중, 크리티컬과 처치를 서로 다른 AudioSource에 배치해 레이어가 끊기지 않도록 처리

## 최종 시스템 구조

```text
PlayerShoot
  ├─ 실제 적용 대미지 계산
  ├─ CombatPresentation: 투사체·스파크·적 피격 표현
  └─ CombatFeedbackController
       ├─ RecordDamage: 실린더 누적 대미지/UI
       ├─ RecordHit: 일반 명중·크리티컬 피드백
       └─ RecordDefeat: 콤보·처치 피드백
            ├─ Camera Shake
            ├─ Fullscreen Impact Queue
            ├─ Volume Pulse
            ├─ Slow Motion
            └─ Layered Audio
```

`PlayerShoot`는 실제 적에게 적용된 대미지와 대상 최대 체력을 전달한다. 따라서 화면 강도는 공격의 이론상 대미지가 아니라 실제 적용 결과를 기준으로 계산된다.

독 폭발과 같은 관리형 상태 피해도 `EnemyController.ApplyStatusDamageAmount()`가 실제 적용량을 반환하도록 구성했다. 이를 통해 직접 공격과 상태 피해가 동일한 누적 대미지 및 명중·처치 피드백 경로를 사용할 수 있다.

## 이벤트별 데이터 흐름

### 실린더 시작

1. `BeginCylinder()` 호출
2. 누적 대미지와 표시용 보간 값 초기화
3. 대미지 UI 활성화
4. 현재 실린더의 최초 장전 수 기록

### 피해 적용

1. `PlayerShoot`가 공격 대미지를 계산한다.
2. `EnemyController`가 실제 적용 대미지를 반환한다.
3. `RecordDamage(appliedDamage, wasOverkill)`로 누적값을 갱신한다.
4. 적이 생존하면 `RecordHit(...)`를 호출한다.
5. 적이 사망하면 `RecordDefeat(...)`를 호출한다.
6. 독 폭발 피해도 동일한 분기와 피드백을 사용한다.

### 실린더 진행도 반영

현재 발사 순번은 다음 비율로 정규화한다.

```text
cylinderBuild = bulletsFiredThisCylinder / initialLoadedBulletCount
```

실린더 후반으로 갈수록 화면 강도와 효과음 피치가 소폭 상승한다. 첫 발은 읽기 쉽고 마지막 발은 더 강하게 느껴지도록 하는 상승 곡선이다.

### 콤보 갱신

- 적 처치 시 콤보가 1 증가한다.
- `comboRemaining`이 `comboDuration`으로 초기화된다.
- `Image | Combo Timer.fillAmount`는 `comboRemaining / comboDuration`을 표시한다.
- 제한 시간 안에 추가 처치가 없으면 콤보와 타이머가 초기화된다.
- 3, 6, 10 콤보 구간을 기준으로 색상과 처치 강도가 단계적으로 상승한다.

### 누적 대미지 롤업

실제 누적값은 즉시 갱신하지만 화면에 보이는 값은 새 대미지의 약 72% 지점까지 먼저 점프한 뒤 빠르게 최종값으로 보간한다. 큰 숫자가 한 프레임에 교체되는 느낌을 줄이고 대미지가 누적되는 감각을 강조한다.

오버킬이 발생하면 잠시 주황 계열 강조색을 사용한다. 실린더 종료 후에는 짧게 결과를 유지한 뒤 UI를 감춘다.

## Fullscreen Impact 구현

### 다중 충격 상태

관통 및 연쇄 공격이 같은 프레임에 발생할 수 있으므로 단일 전역 충격값 대신 최대 4개의 상태를 관리한다.

각 상태는 다음 정보를 가진다.

- 화면 UV 기준 명중 중심점
- 공격 방향
- 진행 시간과 전체 지속 시간
- 강도
- 크리티컬 여부
- 마지막 적 처치 여부

빈 슬롯이 없으면 진행률이 가장 높은, 즉 종료에 가장 가까운 충격을 새 충격으로 교체한다. 이 방식은 오래된 약한 효과가 새 명중을 가리는 문제를 줄인다.

셰이더에는 다음 배열을 전달한다.

```hlsl
float4 _KillImpactCenters[4];
float4 _KillImpactDirections[4];
float4 _KillImpactParams[4];
```

`_KillImpactParams`의 구성은 다음과 같다.

| 채널 | 값 |
| --- | --- |
| X | 진행률 |
| Y | 현재 Envelope가 적용된 강도 |
| Z | 크리티컬 여부 |
| W | 마지막 적 처치 여부 |

셰이더는 네 충격의 UV 변형, RGB Offset과 발광을 누적한 뒤 원본 카메라 컬러와 합성한다.

### 시간 처리

화면 효과와 UI는 슬로 모션 중에도 일정한 실제 시간 길이를 유지해야 하므로 `Time.unscaledDeltaTime`을 사용한다. 일시정지 중에는 충격 진행과 콤보 타이머를 멈춘다.

슬로 모션은 기존 `Time.timeScale`을 저장한 뒤 Hold와 Recovery 구간을 거쳐 복원한다. 컴포넌트가 비활성화되는 경우에도 원래 값으로 복구한다.

연속 처치와 Hit Stop이 겹칠 때는 새 슬로 모션이 현재의 낮아진 배율을 원래 배율로 오인하지 않도록, 최초 슬로 모션이 소유권을 얻은 시점의 기준값을 중첩 연출이 모두 끝날 때까지 유지한다. 새 처치가 발생하면 진행 중인 Coroutine만 교체하고 기준값은 덮어쓰지 않는다.

복구 시점에는 진행 중인 슬로 모션 Coroutine을 명시적으로 취소하고, 현재 `Time.timeScale`이 0인지 여부와 관계없이 저장된 기준값을 직접 대입한다. 따라서 다음 경로에서도 정확한 원래 배율 복귀를 보장한다.

- 정상적인 Hold 및 Recovery 완료
- 연속 처치로 기존 슬로 모션이 교체된 경우
- Hit Stop 도중 새 처치가 발생한 경우
- 컴포넌트 비활성화
- GameObject 파괴

## Inspector 조정 위치

`Assets/Prefabs/Player/Player.prefab`의 `Combat Feedback Controller`에서 조정한다.

### 현재 적용값

| 그룹 | 필드 | 현재 값 | 용도 |
| --- | --- | ---: | --- |
| Combo | `Combo Duration` | 15 | 콤보 유지 시간 |
| Kill Motion | `Kill Slow Motion Scale` | 0.3 | 처치 시 목표 시간 배율 |
| Kill Motion | `Kill Slow Motion Hold` | 0.18 | 최저 배율 유지 시간 |
| Kill Motion | `Kill Slow Motion Recovery` | 0.18 | 정상 속도 복귀 시간 |
| Kill Motion | `Kill Camera Shake` | 0.055 | 처치 카메라 흔들림 |
| Volume Pulse | `Volume Pulse Duration` | 0.8 | 처치 후처리 지속 시간 |
| Volume Pulse | `Chromatic Boost` | 1 | 색수차 추가 강도 |
| Volume Pulse | `Bloom Boost` | 3 | Bloom 추가 강도 |
| Fullscreen Impact | `Fullscreen Impact Duration` | 0.42 | 처치 충격파 지속 시간 |
| Fullscreen Impact | `Shockwave Strength` | 1 | 충격파 UV 왜곡 |
| Fullscreen Impact | `RGB Split Strength` | 1 | RGB 분리 강도 |
| Fullscreen Impact | `Radial Zoom Strength` | 0.85 | 중심 줌 강도 |
| Fullscreen Impact | `Directional Tear Strength` | 0.72 | 공격 방향 찢김 강도 |
| Hit Feedback | `Hit Fullscreen Duration` | 0.14 | 일반 명중 화면 효과 시간 |
| Hit Feedback | `Minimum Hit Intensity` | 0.18 | 작은 대미지의 최소 피드백 |
| Hit Feedback | `Hit Camera Shake` | 0.018 | 일반 명중 카메라 흔들림 |
| Audio | `Hit Accent Volume` | 0.72 | 일반 명중음 볼륨 |
| Audio | `Critical Accent Volume` | 0.9 | 크리티컬음 볼륨 |
| Audio | `Kill Accent Volume` | 0.95 | 처치음 볼륨 |

### 조정 권장 순서

1. `Hit Camera Shake`와 `Minimum Hit Intensity`로 일반 공격의 피로도를 먼저 맞춘다.
2. `Fullscreen Impact Duration`과 `Shockwave Strength`로 처치의 크기를 결정한다.
3. `Chromatic Boost`와 `Bloom Boost`는 실제 게임 배경에서 과노출 여부를 확인하며 낮춘다.
4. 슬로 모션은 Hold보다 Recovery를 먼저 조정하면 조작 단절감을 줄이기 쉽다.
5. 효과음은 Kill, Critical, Hit 순으로 최대 볼륨을 정한 뒤 다른 전투 SFX와 비교한다.

## 구현 및 변경 파일

| 파일 | 역할 |
| --- | --- |
| `Assets/Scripts/Common/CombatFeedbackController.cs` | 콤보, 누적 대미지 UI, Volume, 슬로 모션, Fullscreen Impact, 사운드 총괄 |
| `Assets/Scripts/Player/PlayerShoot.cs` | 실제 피해·처치 이벤트 전달, 실린더 진행도 계산 |
| `Assets/Scripts/Enemy/EnemyController.cs` | 상태 피해의 실제 적용 대미지 반환 |
| `Assets/Shaders/KillImpactFullscreen.shader` | 카메라 컬러 기반 다중 충격파 후처리 |
| `Assets/Materials/KillImpactFullscreen.mat` | Full Screen Pass용 머티리얼 |
| `Assets/Settings/Renderer2D.asset` | Full Screen Pass Renderer Feature와 컬러 버퍼 입력 설정 |
| `Assets/Prefabs/Player/Player.prefab` | `CombatFeedbackController`와 기본 튜닝값 |
| `Assets/Prefabs/UI/Canvas.prefab` | 콤보, 타이머와 현재 대미지 UI |

## 모범 작업 요청 예시

다음은 같은 기능을 다시 구현하거나 개선할 때 사용할 수 있는 작업 요청 예시다. 실제 대화 전문이나 내부 추론을 기록하는 대신 재현 가능한 요구사항 형태로 정리했다.

> 적에게 피해를 주거나 처치했을 때 시각·청각 피드백을 단계적으로 제공해줘. 일반 명중은 짧고 약하게, 크리티컬은 명확하게, 처치는 가장 강하게 표현해줘. 관통 공격에서는 여러 명중 위치가 동시에 남아야 하며 일시정지와 슬로 모션에서도 효과 시간이 안정적이어야 해.

> 콤보는 `Text | Combo`에 `combo <size=128>{n}</size>` 형식으로 표시하고, `Image | Combo Timer.fillAmount`로 남은 시간을 보여줘. `Text | Current Damage`에는 현재 실린더에서 실제로 적용된 누적 대미지를 표시하고 숫자 롤업과 펀치 애니메이션을 적용해줘.

> URP Full Screen Pass가 원본 카메라 컬러를 안전하게 샘플링하도록 구성하고, 화면 전체가 단색으로 덮이는 경우 Renderer Feature의 컬러 버퍼 입력과 직렬화 버전을 점검해줘.

> 효과음은 일반 명중, 크리티컬, 처치를 별도 레이어로 나누고 타격 위치 패닝과 실린더 진행도 기반 피치 변화를 적용해줘. 전용 클립이 없을 때도 테스트할 수 있는 런타임 대체음을 제공해줘.

## 검증 결과

자동 검증 명령:

```powershell
dotnet build Assembly-CSharp.csproj --no-restore
git diff --check
```

최종 C# 검증 결과:

- 경고 0개
- 오류 0개
- 공백 오류 없음
- 크리티컬 전체 화면 색상 반전 관련 C# 및 Shader 프로퍼티 제거 확인

## Play Mode 확인 체크리스트

1. 일반 명중 시 짧은 충격파와 Hit Accent가 한 번 발생하는지 확인한다.
2. 크리티컬 시 화면 전체 색상이 반전되지 않는지 확인한다.
3. 크리티컬 전용 크랙음과 강화된 왜곡은 유지되는지 확인한다.
4. 처치 시 Volume Pulse, 슬로 모션, 카메라 흔들림과 Kill Accent가 함께 발생하는지 확인한다.
5. 관통 공격으로 여러 적을 맞혔을 때 최대 4개의 충격 중심이 각각 보이는지 확인한다.
6. 콤보 텍스트와 타이머가 처치마다 갱신되고 제한 시간 후 초기화되는지 확인한다.
7. 현재 실린더 누적 대미지가 실제 적용 대미지만 합산하고 부드럽게 롤업되는지 확인한다.
8. 독 폭발 피해와 독 폭발 처치도 동일한 UI와 피드백을 발생시키는지 확인한다.
9. 일시정지 도중 콤보, 슬로 모션과 화면 효과가 비정상적으로 진행되지 않는지 확인한다.
10. 컴포넌트 비활성화 또는 전투 종료 후 Volume과 `Time.timeScale`이 원래 상태로 복원되는지 확인한다.

## 후속 개선 후보

- 실제 제작된 Hit, Critical, Kill 사운드 에셋으로 런타임 대체음 교체
- 무기 또는 탄환 타입에 따른 Impact Color와 음색 프리셋
- 보스, 엘리트와 일반 적에 따른 처치 피드백 등급 분리
- 동일 프레임 다수 타격 시 사운드 보이스 수 제한 및 Loudness 정규화
- 옵션 메뉴에 화면 흔들림, 화면 왜곡, 슬로 모션 강도 접근성 설정 추가
- 저사양 환경을 위한 Fullscreen Impact 동시 개수 및 RGB Sample 품질 옵션
