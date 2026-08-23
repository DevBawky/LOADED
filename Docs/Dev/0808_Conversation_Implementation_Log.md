# 2026-08-08 대화 기반 통합 구현 기록

## 문서 목적

이 문서는 2026-08-08까지 이어진 작업 대화에서 요청하고 구현한 전투, UI, 상점, 저장, 결과 정산 기능을 한곳에 정리한 개발 기록이다.

- 대상 프로젝트: `LOADED`
- 대상 씬: 전투 스테이지, 상점, 메인 메뉴
- 작성 기준: 현재 프로젝트에 남아 있는 런타임 코드와 최종 확정된 UX
- 주의: 기존 기능별 문서는 유지하며, 이 문서를 대화 작업의 통합 색인으로 사용한다.

## 1. 빅 베럴 폭탄

### 최종 동작

- 빅 베럴이 투척하여 생성하는 `BossBomb(Clone)`은 `Sprites/Boss/Boss_Weapon_Bomb` 스프라이트를 사용한다.
- 프리팹의 `SpriteRenderer` 참조가 비어 있어도 런타임에 렌더러와 스프라이트를 복구한다.
- 폭탄의 최종 스폰 Y 오프셋은 `-0.3`이다.
- 퓨즈 텍스트의 로컬 스케일은 `(1, 1, 1)`이다.
- 퓨즈 텍스트 폰트는 `Galmuri9`을 사용한다.

### 주요 코드

- `Assets/Scripts/Enemy/BossBomb.cs`
- `Assets/Scripts/Enemy/BossBombManager.cs`
- `Assets/Scripts/Enemy/Editor/BigBarrelAssetBuilder.cs`
- `Assets/Scripts/Enemy/EnemyData.cs`

## 2. 발차기 준비 상태

- 새로운 전투 스테이지에 진입하면 발차기 쿨타임을 초기화한다.
- 플레이어는 전투 시작 직후 발차기를 사용할 수 있다.
- 발차기가 다시 준비되면 `발차기 준비!` 전투 피드백을 출력한다.

주요 코드:

- `Assets/Scripts/Player/PlayerMove.cs`
- `Assets/Scripts/Common/CombatFeedbackController.cs`

## 3. 전투 조작법 패널과 일시정지

### 전투 전용 조작법 컨트롤러

- 전투의 `Panel | Control`에는 `CombatControlPanelController`를 사용한다.
- 사전/도감 UI를 담당하는 `DictInfoPanelController`와 독립적으로 동작한다.
- `Paused -> Button _ How To Play`을 누르면 전투용 `Panel | Control`과 그 콘텐츠가 강제로 활성화된다.
- 외부 컨트롤러가 동일한 이름의 자식 패널을 비활성화해도 전투용 컨트롤러가 표시 상태를 유지한다.

### ESC 우선순위

1. 조작법이 열려 있으면 ESC는 조작법만 닫는다.
2. 조작법이 닫힌 상태에서 다시 ESC를 눌러야 일시정지 패널이 닫힌다.

### 일시정지 연출 안정화

- 일시정지 UI 애니메이션과 버튼 호버는 `Time.timeScale`과 무관한 시간 기준으로 동작한다.
- 적 처치 직후 일시정지해도 카메라 셰이크가 고정된 채 남지 않도록 일시정지 진입 시 카메라 피드백을 정리한다.
- 일시정지 화면은 전투 후처리 효과 때문에 과도하게 어둡거나 왜곡되지 않도록 별도 처리한다.

주요 코드:

- `Assets/Scripts/Manager/CombatControlPanelController.cs`
- `Assets/Scripts/Manager/GamePauseController.cs`
- `Assets/Scripts/Common/CombatCameraShake.cs`

## 4. 탄환 및 실린더 관련 수정

### 탄피 수집 추가 사격

- 탄피 수집 탄의 추가 사격 횟수는 현재 누적된 탄피 수를 기준으로 계산한다.
- 발사 처리 도중 스택이 바뀌어 누적량과 실제 추가 사격 횟수가 어긋나는 문제를 수정했다.

### 탄환 강화 사운드

- 한 번에 여러 탄환을 강화해도 강화 사운드는 중복 재생하지 않는다.
- 다른 종류의 효과음 중복 재생 정책에는 영향을 주지 않는다.

### 탄 파괴 피드백

- 탄환이 파괴되면 `탄 파괴됨..` 텍스트를 출력한다.
- 텍스트는 검회색 계열을 사용한다.

### Next Chip 정보

- `Next Chip -> Image | Next Chip -> Text | Level`에 다음 탄환의 강화 단계를 표시한다.
- 강화 단계 텍스트 색상은 탄환 강화 등급을 따른다.
- `Text | Stack`에는 다음 탄환의 누적 스택 수를 표시한다.
- 스택 텍스트는 프리팹에 설정된 고유 색상을 유지한다.
- 다음 탄환의 스택이 0이 아니면 비활성 계층에 묻히지 않고 정상 표시된다.

### 연출 기본값

- 관련 전투 연출의 초기 기본 강도는 기존 값의 50% 수준으로 조정했다.

주요 코드:

- `Assets/Scripts/Player/PlayerShoot.cs`
- `Assets/Scripts/Bullet/BulletInstance.cs`
- `Assets/Scripts/Bullet/NextBulletUI.cs`
- `Assets/Scripts/Item/InventoryTooltipUI.cs`
- `Assets/Scripts/Common/CombatFeedbackController.cs`

## 5. 적 체력 UI와 예상 피해

### 체력 수치 표시

- 일반 적은 `HP_Value -> Text | HP`에 `{현재 체력}/{최대 체력}`을 표시한다.
- 보스는 `Panel | Boss -> Text | HP`에 같은 형식을 표시한다.

### 피해 연출

- 체력이 감소하면 텍스트가 빨간색으로 바뀌고 약간 확대된다.
- 표시 체력은 즉시 바뀌지 않고 실제 감소량을 따라 서서히 내려간다.
- 연출이 끝나면 원래 색상과 스케일로 복귀한다.

### 탄환 호버 예상 피해

- 실린더 탄환에 마우스를 올리면 예상 피해 이후의 체력을 체력 수치 텍스트에도 반영한다.
- 선택한 탄환 이전에 발사될 탄환들의 피해도 순서대로 누적하여 계산한다.
- 예상 수치 표시 중에는 원래 색과 빨간색을 번갈아 표시한다.
- 호버가 끝나면 실제 체력 수치와 원래 색상으로 복구한다.

주요 코드:

- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/Enemy/EnemyHealthBarFeedback.cs`
- `Assets/Scripts/Player/PlayerCylinderUI.cs`
- `Assets/Scripts/Item/InventoryTooltipUI.cs`

## 6. 디버프 툴팁과 디버프 처치

### 디버프 아이콘 툴팁

- 일반 적과 보스의 디버프 아이콘에 마우스를 올리면 기본 설명과 현재 스택 기반 설명을 표시한다.
- 기본 설명이 끝난 뒤 한 줄을 비우고 현재 스택 설명을 표시한다.
- COUNT 수, 공격력, 감소율, 피해 수치 등 중요한 값은 Rich Text 색상으로 강조한다.

스택 설명 예시:

- 기절: `N COUNT 동안 행동 불가`
- 약화: `N COUNT 동안 공격력이 N% 감소합니다.`
- 독: `N COUNT 동안 독의 대미지를 받습니다. 이번 COUNT 피해량: N`
- 표식: `N COUNT 동안 50%의 추가 대미지를 받습니다.`

### 지속 피해 예상 체력

- 독처럼 피해를 주는 디버프를 호버하면 이번 COUNT 예상 피해를 적 체력 수치에도 반영한다.

### 디버프 처치 콤보

- 독 등 플레이어가 부여한 디버프로 적이 사망해도 일반 처치와 동일하게 콤보에 포함한다.
- 직접 공격 처리와 중복 집계되지 않도록 상태 효과 처치 이벤트 경로를 분리한다.

주요 코드:

- `Assets/Scripts/Common/StatusEffectController.cs`
- `Assets/Scripts/Enemy/EnemyActionTooltipTrigger.cs`
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/Common/CombatFeedbackController.cs`
- `Assets/Scripts/Player/PlayerShoot.cs`

## 7. 적 액션 툴팁

- 적 액션 호버 시 `BG | Action Damage Range -> Text | Action Description`에 공격 정보를 표시한다.
- 출력 형식은 다음과 같다.

```text
대미지: <color=red> 15 </color> 사거리: <color=yellow>4</color>
```

- 실제 값은 `EnemyActionData.AttackData.Damage`와 `Range`를 사용한다.
- 같은 이름을 가진 본문 설명 텍스트와 피해/사거리 텍스트를 부모 계층 기준으로 구분한다.
- 이동, 대기, 지원처럼 공격 데이터가 없는 액션은 `BG | Action Damage Range`를 숨긴다.
- 디버프 툴팁에서도 공격 정보 영역을 숨긴다.

주요 코드:

- `Assets/Scripts/Enemy/EnemyActionTooltipTrigger.cs`
- `Assets/Scripts/Enemy/EnemyActionData.cs`

## 8. 카운트 기반 콤보 시스템

### 콤보 유지 규칙

- 기존 실시간 타이머나 플레이어 행동 횟수 대신 완료된 Duel Clock 카운트를 기준으로 콤보 제한을 계산한다.
- 콤보 제한은 8카운트다.
- `WaveManager.EnemyTurnCycleCompleted`가 발생하면 `Image | Combo Timer BG`의 게이지가 오른쪽 끝부터 감소한다. 기존 `Turn` 자식 이름도 프리팹 호환을 위해 인식한다.
- 각 칸은 기본 0.2초 동안 `fillAmount 1 -> 0`으로 감소한다.
- 적을 처치한 카운트 자체에는 콤보 잔여 카운트를 감소시키지 않는다.
- 8카운트 안에 다시 적을 처치하면 잔여 카운트를 8로 초기화하고 콤보 수를 올린다.
- 콤보가 유효한 동안 `Text | Combo`를 계속 표시한다.

### 콤보 색상

- 1~4 콤보: 흰색
- 5~9 콤보: 주황색
- 10 콤보 이상: 빨간색

### 실린더 연속 처치 문구

- `적 처치!`, `N연속 처치!` 문구는 전체 콤보 수를 사용하지 않는다.
- 현재 한 실린더의 연속 발사 과정에서 처치한 적만 집계한다.
- 디버프 처치는 전체 COUNT 콤보에는 포함하지만, 해당 발사 시퀀스 소속이 아니면 실린더 연속 처치 문구에는 포함하지 않는다.

### 저장 대상

- 현재 콤보 수와 남은 콤보 제한 카운트를 저장하고 복원한다. 버전 3 JSON의 기존 필드 이름은 호환성을 위해 유지한다.

주요 코드:

- `Assets/Scripts/Common/CombatFeedbackController.cs`
- `Assets/Scripts/Manager/GameStatistics.cs`

## 9. 전투 결과 및 정산 보고서

### 패널 흐름

1. 스테이지 클리어 시 `Image | Stage Report`가 팝업된다.
2. 첫 보고서의 골드 항목 이름은 `획득한 골드`이다.
3. 이어서 `Image | Stage Result`가 팝업된다.
4. 정산 결과가 표시되어도 첫 전투 결과 보고서는 유지된다.
5. 보고서가 열린 동안 아무 곳이나 클릭해서 상점으로 넘어갈 수 없다.
6. `Button | Gain Gold`로 보너스 골드를 받은 뒤에만 상점 진행이 가능하다.

### 결과 카운트업과 메달

- `Layout | Combo Kill`, `Layout | Cylinder Kill`, `Layout | Executor`의 `Text | My Result`는 0부터 스테이지 결과까지 증가한다.
- 카운트가 끝나면 위에서부터 메달 이미지를 차례로 표시한다.
- 메달은 반짝이며 튀어나오는 팝업 연출을 사용한다.
- 보고서 팝업 시간, 시작 스케일, 오버슈트, 결과 증가 시간, 메달 팝업 시간과 간격을 인스펙터에서 설정할 수 있다.

### 전투 보고서 COUNT 기준

- `최고 누적 대미지`는 한 번의 완료된 적 행동 COUNT 경계 안에서 누적된 피해를 기준으로 한다.
- `완료 COUNT`는 전투 시작 이후 `WaveManager.EnemyTurnCycleCompleted`가 완료된 횟수다.
- `COUNT 당 평균 대미지`는 총 대미지를 완료 COUNT로 나눈다.
- 버전 3 저장 DTO의 `currentTurnDamage`, `startingTurnCount` 필드명은 이전 저장 호환성을 위해 유지하되 값의 의미는 COUNT 기준이다.

### 메달 기준

| 항목 | 동 | 은 | 금 |
| --- | ---: | ---: | ---: |
| 연쇄 사냥꾼 | 스테이지 전체 적의 25% | 50% | 70% |
| 실린더 청소부 | 한 실린더 2킬 | 3킬 | 4킬 이상 |
| 완벽한 처형 | 최대 체력의 25% 초과 피해 | 75% | 150% |

- 연쇄 사냥꾼 기준은 전체 적 수에 비례하며 소수 결과는 필요한 처치 수 기준으로 올림한다.
- `Panel | Result Post -> Layout | Combo Kill`의 동/은/금 텍스트에 계산된 숫자만 표시한다. `1콤보` 같은 접미사는 붙이지 않는다.
- 완벽한 처형 판정은 `25%`, `75%`, `150%`와 정확히 같은 값도 포함하는 `이상(>=)` 조건이다.
- 부동소수점 오차 때문에 정확한 경계값이 누락되지 않도록 작은 허용 오차를 사용한다.
- `Panel | Result Post -> Layout | Executor` 기준 텍스트는 `25%`, `75%`, `150%`처럼 수치만 표시하며 `이상` 문구는 붙이지 않는다.
- 완벽한 처형의 실제 결과 텍스트는 소수점 이하를 내림한다. 예: `49.99% -> 49%`.

### 보너스 골드

- 메달 점수는 각 항목 0~3점, 총 9점이다.
- 총점 표시는 `메달 총점: <color=orange>{내 점수}</color>/9` 형식을 사용한다.
- 보너스 비율은 다음과 같다.

| 메달 총점 | 추가 골드 |
| ---: | ---: |
| 1~2점 | 5% |
| 3~5점 | 10% |
| 6~8점 | 20% |
| 9점 | 30% |

- 보너스는 이번 스테이지에서 획득한 골드 합계에 비율을 적용한다.
- `Text | Bonus Result`에는 추가 비율, 메달 총점, 계산식을 표시한다.
- 모든 결과 연출이 끝난 뒤 `Button | Gain Gold`를 활성화한다.
- 버튼 자식 `Text | Amount`에는 최종 정산 금액을 표시한다.
- 메달 스프라이트는 `bronzeMedalSprite`, `silverMedalSprite`, `goldMedalSprite` 직렬화 필드에 연결한다.
- 정산 버튼의 클릭 리스너는 버튼을 표시하고 팝업 애니메이션을 시작하기 전에 등록해, 표시 직후의 첫 클릭도 정산으로 처리한다.

주요 코드:

- `Assets/Scripts/Manager/GameStartUI.cs`
- `Assets/Scripts/Manager/GameStatistics.cs`
- `Assets/Scripts/Common/CombatFeedbackController.cs`

## 10. 런 저장 및 이어하기

### 저장 시점

- 전투 중 ESC 메뉴의 `Button _ Exit`로 나갈 때 현재 런을 저장한다.
- 애플리케이션 강제 종료/정상 종료 콜백에서도 가능한 범위 내에서 저장한다.
- 게임 오버와 게임 클리어 시에는 이어하기 저장을 만들지 않는다.
- 상점 또는 정산 결과 상태에서도 유효한 런 데이터를 유지한다.
- 한 번 불러온 뒤 다시 나가더라도 저장 플래그가 소진되지 않으며 반복해서 저장할 수 있다.

### 저장 범위

- 현재 게임 흐름과 스테이지 진행 상태
- 보유 골드와 스테이지 획득/사용 골드
- 플레이어 체력과 전투 상태
- 덱, 실린더, 탄환 강화 단계와 누적 스택
- 인벤토리와 상점 제안 상품 및 구매 여부
- 상점 새로고침 비용
- 적 종류, 배치 타일, 현재/최대 체력, 행동 상태와 디버프
- 보스 및 폭탄 등 전투 오브젝트 상태
- 현재 콤보, 남은 콤보 카운트, 실린더 처치 집계
- 전투 보고서 누적 통계
- 완료된 Duel Clock 누적 카운트
- 동시에 살아 있는 적은 최대 6명이며, 제한 중 발생한 추가 스폰 요청은 자리가 생길 때까지 보류

### 메인 메뉴 이어하기

- 메인 메뉴에서 게임 시작을 눌렀을 때 저장 파일이 있으면 `Panel | Load Game`을 연다.
- `Button | Yes`: 저장된 런을 이어서 시작한다.
- `Button | No`: 저장 데이터를 사용하지 않고 새 게임을 시작한다.
- ESC: 불러오기 패널을 닫고 메인 메뉴로 돌아간다.
- 게임 시작 전환 시 `Image_PilDog`도 `Layout | Buttons`와 함께 서서히 사라진다.
- 상점에서 복원되더라도 누적 카운트 UI가 0으로 초기화되지 않고 저장된 값을 표시한다.

주요 코드:

- `Assets/Scripts/Manager/GameStatistics.cs`
- `Assets/Scripts/Manager/StateManager.cs`
- `Assets/Scripts/Manager/GamePauseController.cs`
- `Assets/Scripts/Manager/MainMenuVideoController.cs`
- `Assets/Scripts/Manager/TurnCountText.cs`
- `Assets/Scripts/Manager/ShopManager.cs`

## 11. 상점 구매 완료 표시

- 탄환 상품과 일반 아이템 상품을 구매하면 가격 대신 `구매 완료`를 표시한다.
- 구매 완료 텍스트는 빨간색이다.
- 구매된 슬롯의 버튼은 재구매할 수 없다.
- 새로고침으로 새 상품이 생성되면 구매 상태를 초기화하고 새 상품의 가격과 가격 색상 규칙을 복원한다.
- 저장된 상점 상태를 불러오면 이미 구매한 슬롯은 다시 `구매 완료`로 표시한다.

주요 코드:

- `Assets/Scripts/Manager/ShopManager.cs`

## 12. 검증 및 회귀 테스트

### 자동 검증

```text
dotnet build LOADED.slnx --no-restore
```

최근 코드 검증 결과는 컴파일 오류 0개이다. `OldMovie` 패키지 경고는 작업 기능과 무관한 기존 경고일 수 있다.

### Unity 플레이 모드 확인 목록

1. 빅 베럴 폭탄의 스프라이트, Y 오프셋, 퓨즈 폰트와 스케일을 확인한다.
2. 새 전투 시작 직후 발차기가 가능한지 확인한다.
3. 일시정지에서 조작법을 열고 ESC를 두 번 눌러 조작법과 일시정지가 순서대로 닫히는지 확인한다.
4. 탄피 스택별 추가 사격 횟수와 다중 강화 사운드 중복 여부를 확인한다.
5. 일반 적과 보스의 체력 수치, 피해 연출, 탄환/독 예상 피해를 확인한다.
6. 디버프 툴팁의 줄바꿈, 강조색, 스택 설명을 확인한다.
7. 적 공격 액션 툴팁에 실제 대미지와 사거리가 표시되는지 확인한다.
8. 처치 COUNT에는 콤보 카운트가 줄지 않고 다음 COUNT부터 오른쪽 칸이 감소하는지 확인한다.
9. 직접 처치와 디버프 처치가 콤보에 각각 한 번만 포함되는지 확인한다.
10. 결과 보고서가 순서대로 표시되고 정산 전에는 상점으로 넘어가지 않는지 확인한다.
11. 완벽한 처형 경계값 25%, 75%, 150%에서 각각 메달을 받는지 확인한다.
12. 완벽한 처형 결과 49.99%가 `49%`로 표시되는지 확인한다.
13. 전투와 상점에서 여러 차례 저장/불러오기를 반복해 COUNT, 적 배치, 콤보와 상점 상태가 유지되는지 확인한다.
14. 적이 6명인 상태에서 발생한 추가 스폰이 보류되고, 적 처치로 자리가 생기면 한 명이 스폰되는지 확인한다.
15. 구매한 상점 슬롯이 빨간색 `구매 완료`로 바뀌고 새로고침 후 새 가격으로 복원되는지 확인한다.

## 13. 관련 기존 문서

- `Docs/Dev/0719_ItemShopAndTooltipSystem.md`
- `Docs/Dev/0802_EnemyData_Template_and_ActionPresentation.md`
- `Docs/Dev/0803_CylinderBulletDamagePreview.md`
- `Docs/Dev/0803_Stage1_Boss_BigBarrel.md`
- `Docs/Dev/0804_LoadingTransition_GameStartUI_BulletIcon.md`
- `Docs/Dev/0805_CombatFeedback_ComboGold_Kick_CameraShake.md`
- `Docs/Dev/0808_Overkill_WebGL_UI_Runtime_Fixes.md`

