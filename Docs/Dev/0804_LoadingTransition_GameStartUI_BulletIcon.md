# 로딩 전환, 전투 시작·결산 UI, 탄환 아이콘 정책

## 기본 정보

- 작성일: 2026-08-04
- 관련 기능: 비동기 씬 전환 / 전투·상점 전환 / `Canvas | Game Start` / 스테이지 리포트 / `BulletData` 아이콘
- 목적: 화면을 완전히 가린 상태에서만 게임 상태를 변경하고, 전투 시작과 종료 정보를 명확하게 전달하며, 모든 탄환 UI가 하나의 아이콘 규칙을 사용하도록 통일한다.

## 구현 파일

### 로딩 전환

- `Assets/Scripts/Manager/LoadingTransitionController.cs`
- `Assets/Resources/UI/Canvas _ Loading Transition.prefab`
- `Assets/Editor/LoadingTransitionPrefabBuilder.cs`
- `Assets/Scripts/Manager/StateManager.cs`

### 전투 시작 및 스테이지 리포트

- `Assets/Scripts/Manager/GameStartUI.cs`
- `Assets/Prefabs/UI/Canvas _ Game Start.prefab`
- `Assets/Scripts/Player/PlayerShoot.cs`
- `Assets/Scripts/Manager/StateManager.cs`

### 탄환 아이콘

- `Assets/Scripts/Bullet/BulletData.cs`
- `Assets/Editor/BulletDataEditor.cs`
- `Assets/Scripts/Bullet/BulletInstance.cs`
- `Assets/Scripts/Manager/ShopManager.cs`
- `Assets/Scripts/Item/InventoryTooltipUI.cs`
- `Assets/Scripts/Bullet/NextBulletUI.cs`
- `Assets/Scripts/Bullet/BulletManagementUI.cs`
- `Assets/Scripts/Bullet/SO/**/*.asset`

## 비동기 로딩 전환

`LoadingTransitionController`는 `DontDestroyOnLoad`로 유지되는 전역 로딩 Canvas를 관리한다. 프리팹은 반드시 다음 Resources 경로에 있어야 한다.

```text
Assets/Resources/UI/Canvas _ Loading Transition.prefab
```

런타임 접근 경로는 `UI/Canvas _ Loading Transition`이다. 게임 시작 전 자동 부트스트랩되며, 기존 인스턴스가 있으면 비활성 오브젝트까지 검색해 복구한다.

### 전환 순서

```text
입력 차단
  → 랜덤 탄환 6발 준비
  → 실린더 회전 + 탄환 순차 장전
  → 장전 진행도에 맞춰 배경 fillAmount 0 → 1
  → 화면이 100% 가려진 뒤 씬 로드 또는 상태 변경
  → 기본 2초 대기
  → 실린더 회전 + 탄환 순차 발사
  → 발사 진행도에 맞춰 배경 fillAmount 1 → 0
  → 입력 허용
```

탄환 한 발이 장전될 때마다 실린더가 기본 60도 회전한다. 로딩 종료 시에도 같은 순서와 속도로 실린더가 회전하며 탄환이 하나씩 빠져나간다.

배경은 `Image | Screen Fill`의 `fillAmount`를 `Mathf.Lerp`로 변경한다. 장전 중에는 각 탄환이 담당하는 `1 / 6` 구간만큼 증가하고, 발사 중에는 같은 구간만큼 감소한다. 마지막 장전이 끝나면 값은 정확히 `1`, 마지막 발사가 끝나면 정확히 `0`으로 보정된다.

### 배경 진행도와 UI 알파

다음 요소의 알파값은 배경 `fillAmount`와 같은 값을 사용한다.

- `Image | Cylinder`와 내부 Chamber 이미지
- `Text | Loading`
- `Text | Tip`

따라서 배경이 나타날 때 실린더와 텍스트도 함께 나타나며, 배경이 사라질 때 동일한 비율로 사라진다. 전체 Canvas의 `CanvasGroup`은 전환 중 입력 차단과 표시 여부를 담당한다.

### 랜덤 탄환과 팁

- `Bullet Sprites`에 스프라이트가 있으면 유효한 스프라이트를 섞어 6개 Chamber에 배치한다.
- 스프라이트가 비어 있으면 `Fallback Bullet Colors` 중 무작위 색상을 사용한다.
- `Tips`는 문자열 리스트로 관리하며 전환 시작 시 한 문장을 무작위로 선택한다.
- `Loading Label`의 기본값은 `LOADING`이다.

### Inspector 조정값

| 필드 | 기본값 | 설명 |
| --- | ---: | --- |
| `Bullet Load Duration` | 0.25초 | 탄환 한 발의 장전 및 발사 시간 |
| `Cylinder Rotation Duration` | 0.12초 | Chamber 한 칸 회전 시간 |
| `Cylinder Rotation Step` | 60도 | 탄환 한 발마다 회전하는 각도 |
| `Covered Hold Duration` | 2초 | 상태 변경 또는 씬 로드 완료 후 화면을 덮은 채 유지하는 시간 |
| `Use Unscaled Time` | true | 일시정지와 무관하게 전환 애니메이션 진행 |

### 공개 API

```csharp
LoadingTransitionController.RunTransition(coveredAction, completed);
LoadingTransitionController.LoadScene(sceneName, completed);
LoadingTransitionController.LoadScene(buildIndex, completed);
```

- `RunTransition`: 화면이 완전히 덮였을 때 `coveredAction`을 실행한다.
- `LoadScene`: 화면이 완전히 덮였을 때 비동기 씬 로드를 시작한다.
- `completed`: 배경이 완전히 사라지고 입력이 다시 허용된 뒤 실행된다.
- 이미 전환 중이면 새로운 전환을 시작하지 않는다.

`StateManager`의 전투 종료 → 상점, 상점 → 다음 전투 흐름은 `RunTransition`을 사용하므로 실제 상태 변경은 배경이 100% 채워졌을 때만 실행된다.

### 인스턴스 생성 오류 수정

Resources 프리팹 루트가 비활성 상태이면 `Instantiate` 직후 `Awake()`가 실행되지 않아 `Instance`가 등록되지 않는다. 이 경우 다음 오류가 발생했다.

```text
LoadingTransitionController could not be created.
```

현재는 다음과 같이 보완되어 있다.

- 프리팹 루트 `Canvas _ Loading Transition`을 활성 상태로 저장한다.
- 생성한 오브젝트를 명시적으로 활성화한다.
- 비활성 상태의 기존 컨트롤러도 검색한다.
- 도메인 리로드 등으로 정적 `Instance`만 초기화된 경우 기존 오브젝트를 다시 등록한다.

화면을 숨길 때는 GameObject를 비활성화하지 않고 루트 `CanvasGroup.alpha = 0`, `interactable = false`, `blocksRaycasts = false`를 사용한다.

## Canvas | Game Start

하나의 Canvas 안에서 전투 시작 패널과 전투 결과 패널을 관리한다.

```text
Canvas _ Game Start
├─ Panel | Stage Notice
│  ├─ Text | Stage Info
│  ├─ Text | Stage Sub Title
│  └─ Text | Click to Play
├─ Panel | Stage Report
│  ├─ Text | Stage Info
│  ├─ Text | Stage Report
│  └─ Text | Click to Play
└─ Text | Fight
```

패널은 페이드인하지 않고 필요한 시점에 즉시 활성화한다. `Text | Click to Play`는 기본 0.3초 간격으로 알파값을 0과 1 사이에서 전환해 점멸한다.

### 전투 시작 흐름

1. 전투 준비 시 일반 게임 HUD를 숨기고 카메라 Follow를 해제한다.
2. `Panel | Stage Notice`를 즉시 표시한다.
3. 패널 전체에 연결된 Button 클릭을 기다린다.
4. 클릭 즉시 Stage Notice를 숨기고 `Text | Fight`를 표시한다.
5. 동시에 게임 HUD와 카메라 Follow를 복원하고 실제 전투를 시작한다.
6. `Text | Fight`를 기본 0.7초 유지한 뒤 0.3초 동안 페이드아웃한다.

`Fight Hold Duration`, `Fight Fade Duration`, `Click Text Blink Interval`은 Inspector에서 수정할 수 있다. UI 시간은 기본적으로 Unscaled Time을 사용한다.

### 전투 종료 흐름

1. 모든 적 처치 및 전투 행동 종료를 기다린다.
2. 통계 수집을 종료하고 `Panel | Stage Report`를 즉시 표시한다.
3. 일반 게임 HUD를 숨긴다.
4. Stage Report 패널 클릭을 기다린다.
5. 클릭하면 `StateManager`가 로딩 전환을 통해 상점으로 이동한다.

## 스테이지 리포트

`Text | Stage Report`에는 다음 아홉 항목만 표시한다.

| 항목 | 계산 방식 |
| --- | --- |
| 총 대미지 | `PlayerShoot.DamageDealt`로 보고된 적용 피해의 합계 |
| 최고 누적 대미지 | 한 턴 안에서 누적된 적용 피해 중 가장 높은 값 |
| 최고 한 방 대미지 | 한 번의 `DamageDealt` 이벤트로 보고된 가장 높은 피해 |
| 입은 피해 | 전투 중 플레이어 체력이 감소한 양의 합계 |
| 회복량 | 전투 중 플레이어 체력이 증가한 양의 합계 |
| 소모 턴 | 전투 종료 TurnCount에서 전투 시작 TurnCount를 뺀 값 |
| 총 발사 수 | `PlayerShoot.BulletFired` 이벤트 발생 횟수 |
| 턴 당 평균 대미지 | 총 대미지 / 소모 턴 |
| 평균 발 당 대미지 | 총 대미지 / 총 발사 수 |

나눗셈 기준값이 0이면 평균값은 `0.0`으로 표시한다. 평균값은 소수점 첫째 자리까지 표시한다.

현재 총 대미지 통계는 `PlayerShoot.DamageDealt`가 발생시키는 실제 적용 공격 피해를 기준으로 한다. 별도의 이벤트를 발생시키지 않는 환경 피해나 다른 시스템의 직접 피해를 통계에 포함하려면 해당 피해 경로에서도 통계 이벤트를 전달해야 한다.

### 수치 색상

TMP Rich Text의 `<color>` 태그를 사용해 라벨은 기본색으로 두고 값만 전용 색상으로 표시한다.

| Inspector 필드 | 적용 대상 |
| --- | --- |
| `Damage Value Color` | 총 피해, 최고 피해, 평균 피해 |
| `Damage Taken Value Color` | 입은 피해 |
| `Summary Value Color` | 회복량, 소모 턴, 총 발사 수 |

## 탄환 아이콘 정책

탄환 UI 스프라이트는 이제 `Cylinder Icon` 하나만 사용한다.

### BulletData 변경

- `bulletIcon` 직렬화 필드를 제거했다.
- `BulletIcon` 공개 프로퍼티를 제거했다.
- Custom Inspector에서 `Bullet Icon` 입력란을 제거했다.
- 아이콘 검증은 `Cylinder Icon`이 비어 있는지만 확인한다.
- 기존 BulletData 에셋 45개에서 `bulletIcon` YAML 데이터를 제거했다.
- 모든 BulletData 에셋에 기존 `cylinderIcon` 값이 존재하는 것을 확인했다.

### UI 적용 범위

| UI | 표시 규칙 |
| --- | --- |
| 상점 탄환 슬롯 | `BulletData.CylinderIcon`만 표시 |
| `Panel | Bullet Tooltip` | 기존 Bullet Sprite 이미지는 숨기고 Bullet Cylinder Sprite에 Cylinder Icon 표시 |
| `Next Chip → Image | Next Chip` | 다음 탄환의 Cylinder Icon 표시 |
| `NextBulletUI` | 다음 탄환의 Cylinder Icon 표시 |
| 탄환 관리 목록 | Cylinder Icon 표시 |
| 탄환 관리 상세 정보 | 기존 Bullet Sprite를 숨기고 Cylinder Icon 표시 |
| 로딩 화면의 랜덤 탄환 후보 | BulletData의 Cylinder Icon 사용 가능 |

`BulletInstance`에도 `BulletIcon` 프로퍼티가 없으며 `CylinderIcon`만 외부에 제공한다.

## 프리팹 및 Inspector 확인 사항

### Canvas _ Loading Transition

1. 프리팹이 `Assets/Resources/UI` 아래에 있는지 확인한다.
2. 프리팹 루트 GameObject는 활성 상태로 둔다.
3. `Image | Screen Fill`은 Filled 타입과 원하는 Fill Method를 사용한다.
4. `Bullet Images`에는 Chamber 1부터 Chamber 6까지 정확히 6개를 연결한다.
5. Cylinder와 Loading Copy에 각각 CanvasGroup을 연결한다.
6. 팁 문구는 `Tips` 리스트에서 수정한다.

### Canvas _ Game Start

1. `Panel | Stage Notice`, `Panel | Stage Report`, `Text | Fight` 이름을 유지한다.
2. 두 패널 루트에는 클릭을 받을 Button이 있어야 한다.
3. `Gameplay Canvas`는 Game Start Canvas와 별개의 Canvas여야 한다.
4. Cinemachine Camera와 Player Tracking Target을 연결한다.
5. `PlayerShoot`, `PlayerMove`, `PlayerHealth`는 런타임 자동 탐색이 가능하지만 명시적 연결을 권장한다.
6. Stage Report 본문 TMP는 Rich Text를 활성화한다.

## Play Mode 확인 목록

1. 게임 시작 시 Stage Notice가 즉시 나타나는지 확인한다.
2. Click to Play가 기본 0.3초 간격으로 점멸하는지 확인한다.
3. Stage Notice 클릭 즉시 Fight 텍스트와 실제 전투가 함께 시작되는지 확인한다.
4. 전투 종료 시 Stage Report에 지정된 아홉 항목만 표시되는지 확인한다.
5. 리포트 값의 색상이 항목 분류에 맞게 적용되는지 확인한다.
6. Stage Report 클릭 후 로딩 배경이 100% 채워진 시점에 상점이 열리는지 확인한다.
7. 실린더가 회전하며 탄환 여섯 발이 순차 장전 및 발사되는지 확인한다.
8. Loading과 Tip의 알파값이 배경 fillAmount와 일치하는지 확인한다.
9. 배경이 완전히 사라지기 전까지 게임 입력이 차단되는지 확인한다.
10. 상점, Bullet Tooltip, Next Chip에서 모두 Cylinder Icon만 표시되는지 확인한다.

## 검증 결과

- `dotnet build Assembly-CSharp-Editor.csproj --no-restore`: 경고 0개, 오류 0개
- C# 참조에서 `BulletData.BulletIcon` 및 `BulletInstance.BulletIcon` 사용 제거
- BulletData 에셋의 `bulletIcon` 직렬화 항목 잔여 수: 0개
- 실제 화면 비율, 애니메이션 체감 속도와 클릭 영역은 Unity Play Mode에서 최종 확인한다.
