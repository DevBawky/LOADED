# 추가 피해 초과 피해 집계 및 WebGL UI·런타임 수정 기록

## 문서 정보

- 작성일: 2026-08-08
- 대상: 탄환 효과 추가 피해의 초과 피해 집계, `Image | FireStart` 반응형 UI, WebGL 커스텀 커서 크래시 대응
- 관련 코드: `PlayerShoot.cs`, `EnemyController.cs`, `CombatFeedbackController.cs`, `PlayerCylinderUI.cs`
- 관련 UI: `Assets/Prefabs/UI/Canvas.prefab`
- 관련 선행 문서: `0718_Combat_BulletEffects.md`, `0803_CombatFeedback_FullscreenImpact.md`, `0805_CombatFeedback_ComboGold_Kick_CameraShake.md`

## 작업 배경

2026-08-08 WebGL 데모 검증 중 다음 문제가 확인되었다.

1. 보스에게 베놈 버스트로 약 315만 피해를 주어도 스테이지 결과의 최대 초과 피해가 `0%`로 표시되었다.
2. `Image | FireStart`가 에디터 기준 해상도에서는 정상이나 WebGL 캔버스 크기가 변경되면 실린더와 다른 위치에 표시되거나 이미지 비율이 깨졌다.
3. WebGL 브라우저 로그에서 `Hidden/CoreSRP/CoreCopy shader is not supported` 이후 `RuntimeError: memory access out of bounds`가 발생했다.

최종 구현에서는 직접 공격뿐 아니라 플레이어 탄환에서 파생되는 추가 피해도 동일한 처치 성과 계산 규칙을 사용하도록 정리했다. WebGL UI는 초기 화면 좌표를 고정하지 않고 현재 실린더 위치를 추적하도록 변경했으며, WebGL 크래시와 연결된 커스텀 커서 기능은 프로젝트에서 완전히 제거했다.

## 추가 피해 초과 피해 집계

### 문제 원인

`CombatFeedbackController.RecordDefeat()`의 초과 피해율은 다음 값을 요구한다.

```text
초과 피해율 = max(0, 처치 피해 - 처치 직전 체력) × 100 / 대상 최대 체력
```

기존 직접 공격 처치 경로는 공격 피해, 처치 직전 체력과 최대 체력을 모두 전달했다. 반면 베놈 버스트 처치 경로는 `ApplyManagedTargetEffects()`에서 처치 여부만 `bool`로 반환했다. 이후 `RecordDefeat()`가 호출될 때 `targetHealthBeforeDamage`의 기본값 `-1`이 사용되었고, 유효한 직전 체력이 없는 처치는 안전 처리에 따라 초과 피해율 `0%`로 기록되었다.

넉백으로 두 적이 충돌하여 발생하는 피해도 플레이어 피해에는 포함되었지만, 간접 처치 성과를 완전한 피해 문맥과 함께 보고하지 않는 경로가 존재했다.

### 최종 처리 구조

`PlayerShoot`에 관리형 효과 처치 결과가 다음 정보를 보존하도록 구성했다.

- 효과가 대상을 처치했는지 여부
- 체력을 0으로 만든 효과의 전체 계산 피해
- 피해 적용 직전 대상 체력
- 대상 최대 체력
- 처치 월드 위치

베놈 버스트가 대상을 처치하면 독 폭발의 전체 피해와 폭발 직전 체력을 위 결과에 기록한다. 발사 후처리에서는 직접탄의 적용 피해가 아니라 이 효과 피해 문맥으로 `RecordDefeat()`를 호출한다.

넉백 충돌과 플레이어에게 귀속되는 간접 피해 처치는 `EnemyController.PlayerStatusDefeated` 경로로 상세 정보를 전달한다. 사격 중 발생한 간접 처치는 `PlayerShoot`이 발사 시퀀스 문맥에서 기록하고, `CombatFeedbackController`의 전역 상태 피해 처리와 중복 집계되지 않도록 분리했다.

### 피해 경로별 동작

| 피해 경로 | 초과 피해 기준값 | 처치 집계 주체 |
| --- | --- | --- |
| 직접 탄환 | 보정이 끝난 전체 공격 피해, 직접 공격 직전 체력 | `PlayerShoot` |
| 베놈 버스트 | 독 폭발 전체 피해, 폭발 직전 체력 | `PlayerShoot` 관리형 효과 결과 |
| 벽 충격 피해 전이 | 전이 전체 피해, 전이 대상의 피해 직전 체력 | `PlayerShoot.ApplyWallImpactDamageTransfer()` |
| 넉백 충돌 | 충돌 피해, 충돌 직전 체력 | 사격 중 간접 처치 이벤트를 받은 `PlayerShoot` |
| 독 등 턴 기반 상태 피해 | 상태 피해, 틱 적용 직전 체력 | `CombatFeedbackController` 간접 처치 처리 |
| 연쇄·탄피 추가 사격 | 각 추가 사격의 직접 공격 피해와 직전 체력 | 일반 직접 탄환 경로 |

한 추가 피해가 이미 처치를 기록한 적을 같은 발사 후처리에서 다시 발견할 수 있으므로, 발사 중 기록된 효과 처치 대상은 별도로 추적한다. 이후 직접 처치 폴백에서는 해당 대상을 다시 `RecordDefeat()`하지 않는다. 이 규칙은 콤보, 한 실린더 처치 수, 최대 초과 피해가 한 처치당 한 번만 증가하도록 보장한다.

### 보스 계산 예시

최대 체력 5,000인 보스의 처치 직전 체력이 2,000이고 베놈 버스트 피해가 3,159,568이면 다음과 같이 계산한다.

```text
(3,159,568 - 2,000) × 100 / 5,000 = 63,151.36%
```

스테이지 결과에는 같은 스테이지에서 발생한 처치별 초과 피해율 중 최댓값이 저장된다. 표시 단계에서는 소수점 이하를 버리며, 메달 판정은 기존 기준인 25%, 75%, 150%를 유지한다.

## `Image | FireStart` WebGL 반응형 수정

### 문제 원인

`CylinderFireStartAnchor`는 실린더가 회전할 때 발사 시작 마커가 함께 회전하지 않도록 `FireStart`의 월드 위치와 회전을 초기화 시점에 저장했다. WebGL에서는 브라우저 레이아웃과 Canvas Scaler가 초기화된 뒤 실제 캔버스 크기가 다시 정해질 수 있다.

기존 방식에서는 다음 순서로 위치가 어긋났다.

1. 시작 해상도에서 `FireStart` 월드 위치를 저장한다.
2. 브라우저가 캔버스 크기를 변경한다.
3. 실린더는 RectTransform 앵커를 따라 새 위치로 이동한다.
4. `FireStart`는 초기 기준 공간 좌표에 계속 고정된다.

### 중간 구현에서 발견된 WebGL 스택 오버플로

첫 수정에서는 `CylinderFireStartAnchor.LateUpdate()`가 매 프레임 현재 실린더 중심과 초기 오프셋으로 `FireStart`의 월드 위치를 다시 설정했다. 이 방식은 화면 크기 변경에는 대응했지만 WebGL에서 RectTransform 레이아웃 갱신과 월드 Transform 재설정이 반복 호출되는 경로를 만들었다.

브라우저에서는 동일한 WASM 함수 묶음이 계속 반복된 뒤 다음 오류가 발생했다.

```text
Uncaught (in promise) RangeError: Maximum call stack size exceeded
```

Git 빌드 경계를 비교하면 커서 제거 이후에도 오류가 유지됐고, 직전 빌드와의 런타임 코드 차이는 `CylinderFireStartAnchor`의 매 프레임 좌표 재설정이었다. 따라서 동적 좌표 보정 방식 자체를 제거했다.

### 최종 처리 방식

`Image | FireStart`를 회전하는 `Image | Cylinder`의 자식에서 `Panel | MainGame`의 직접 자식으로 이동했다. 실린더와 마커는 동일한 화면 앵커 `(0.765, 0.135)`를 사용한다.

```text
Cylinder anchoredPosition = (0, 0), size = (250, 250)
FireStart anchoredPosition = (0, 150), size = (50, 25)
```

이 구조에서는 런타임에 Transform을 계속 수정할 필요가 없다.

- 브라우저 창과 캔버스 크기가 변경되면 두 UI가 같은 앵커를 따라 함께 이동한다.
- `FireStart`가 실린더의 자식이 아니므로 실린더 회전을 상속하지 않는다.
- `CylinderFireStartAnchor` 클래스와 `LateUpdate()` 좌표 보정을 제거했다.
- `PlayerCylinderUI`는 마커를 실린더의 자식 또는 같은 부모의 형제에서 찾을 수 있다.
- `Image.preserveAspect`를 활성화하여 스프라이트 종횡비를 유지한다.

프리팹의 `Image | FireStart`에도 `Preserve Aspect = true`를 직렬화했으며, 런타임 참조를 해석할 때도 같은 값을 보장한다.

### 실린더 표시 상태 동기화

`FireStart`가 실린더의 자식이 아니므로 잔탄이 0발일 때 자동으로 숨겨지지 않는 문제를 보완했다. `PlayerCylinderUI.SetCylinderVisible()`에서 실린더와 `FireStart`의 활성 상태를 함께 변경한다. 초기화, 재장전, 마지막 탄환 회전 연출 완료 경로가 모두 이 함수를 사용하므로 실린더가 비활성화되는 순간 `FireStart`도 함께 비활성화된다.

## WebGL 커스텀 커서 제거

### 로그 분석

브라우저 로그의 gzip 관련 문구는 압축 파일을 서비스할 때 `Content-Encoding: gzip` 응답 헤더를 추가하라는 시작 속도 경고다. 파일은 정상적으로 로드되었으므로 런타임 종료 원인이 아니다.

실제 종료 흐름은 다음과 같았다.

```text
Hidden/CoreSRP/CoreCopy shader is not supported
→ 브라우저 포커스 이벤트
→ RuntimeError: memory access out of bounds
→ WebAssembly 플레이어 중단
```

Git 커밋 `c5da156`에서 추가된 `CustomCursorController`는 `BeforeSceneLoad` 시점에 다음 작업을 수행했다.

1. `Graphics.Blit()`로 커서 텍스처를 임시 RenderTexture에 복사한다.
2. `ReadPixels()`로 읽기 가능한 런타임 Texture2D를 만든다.
3. 초기화 및 `OnApplicationFocus()`에서 `Cursor.SetCursor()`를 호출한다.

프로젝트의 커서 원본 텍스처는 이미 Read/Write가 활성화되어 있었으므로 GPU 복사는 불필요했다. 또한 로그의 CoreCopy 실패와 포커스 이벤트 종료 스택이 이 코드 경로와 일치했다.

### 최종 결정

WebGL에서만 우회 코드를 유지하는 대신 커스텀 커서 기능 전체를 제거했다. 삭제 대상은 다음과 같다.

- `Assets/Scripts/Common/CustomCursorController.cs`
- `Assets/Scripts/Common/CustomCursorTheme.cs`
- `Assets/Resources/Cursor/DefaultCustomCursorTheme.asset`
- `Assets/Sprites/Cursor/Cursor1_Standard.png`
- `Assets/Sprites/Cursor/Cursor1_Pressed.png`
- 위 파일과 전용 폴더의 `.meta` 파일

관련 GUID와 클래스명 검색 결과 씬, 프리팹 또는 다른 스크립트에 남은 참조는 없다. 제거 이후에는 운영체제와 브라우저의 기본 커서를 사용한다. 이로써 WebGL 시작 시 커서용 `Graphics.Blit`, `ReadPixels`, `Cursor.SetCursor`와 포커스 재적용 코드가 빌드에 포함되지 않는다.

## 검증

### 완료된 검증

- `dotnet build LOADED.slnx --no-restore`
- 결과: 경고 0개, 오류 0개
- 추가 피해 처치 경로의 `RecordDefeat()` 호출부 정적 검사
- 커스텀 커서 클래스명, 리소스명과 관련 GUID 잔존 참조 검색
- `CylinderFireStartAnchor`, `SetPositionAndRotation()` 잔존 호출 검색
- 수정 파일 `git diff --check`

### WebGL 재빌드 주의사항

검증 시 동일 프로젝트를 점유한 Unity 프로세스가 남아 있어 배치 WebGL 빌드는 시작 직후 종료되었다. 기존 `WebBuild` 폴더는 소스 삭제만으로 자동 갱신되지 않는다. 모든 Unity 에디터 프로세스를 종료한 뒤 WebGL을 새로 빌드해야 커서 제거 코드와 리소스가 실제 배포 산출물에 반영된다.

GitHub Pages에 새 빌드를 배포한 뒤 브라우저 캐시에 이전 `data.unityweb`이 남아 있다면 강력 새로고침 또는 사이트 데이터 삭제 후 확인한다.

## 최종 변경 파일

### 수정

- `Assets/Scripts/Player/PlayerShoot.cs`
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/Common/CombatFeedbackController.cs`
- `Assets/Scripts/Player/PlayerCylinderUI.cs`
- `Assets/Prefabs/UI/Canvas.prefab`

### 삭제

- 커스텀 커서 스크립트 2개와 메타 파일
- 커스텀 커서 테마와 메타 파일
- 커서 스프라이트 2개와 메타 파일
- 커서 전용 폴더 메타 파일

## 회귀 확인 항목

1. 직접 공격으로 적을 처치했을 때 기존 초과 피해율이 유지되는지 확인한다.
2. 베놈 버스트로 일반 적과 보스를 각각 처치하고 스테이지 결과가 `0%`가 아닌 계산값을 표시하는지 확인한다.
3. 벽 충격 전이와 넉백 충돌로 처치했을 때 콤보와 초과 피해가 한 번만 증가하는지 확인한다.
4. 독 틱으로 처치했을 때 발사 중 처치와 중복 집계되지 않는지 확인한다.
5. WebGL 창 너비와 높이를 변경하고 `FireStart`가 실린더 상단 위치와 스프라이트 비율을 유지하는지 확인한다.
6. 실린더 회전 및 재장전 펀치 중 `FireStart` 방향이 돌아가지 않는지 확인한다.
7. 새 WebGL 빌드의 브라우저 콘솔에 커서 초기화로 인한 CoreCopy 오류가 발생하지 않는지 확인한다.
8. 브라우저 탭 전환과 포커스 복귀를 반복해도 WebAssembly 플레이어가 중단되지 않는지 확인한다.
9. 브라우저 콘솔에 `Maximum call stack size exceeded`가 다시 발생하지 않는지 확인한다.
