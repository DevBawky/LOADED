# 이벤트 제작 가이드

이 문서는 새로운 `EventDefinition`을 만들고 Event 씬의 이벤트 풀에 등록하는 방법을 설명한다. 일반 선택지, 조건부 선택지, 탄환 선택, 확률 판정, 실패할수록 확률이 증가하는 반복 이벤트까지 현재 구현된 기능을 기준으로 작성되었다.

## 1. 빠른 제작 순서

1. Project 창에서 이벤트 에셋을 보관할 폴더를 연다.
   권장 위치는 `Assets/Scripts/Event/Events`다.
2. 빈 공간을 우클릭하고 `Create > LOADED > Event Definition`을 선택한다.
3. 에셋 이름을 원하는 이름으로 변경한다. 예: `Event_BrokenSlotMachine`.
4. `Event Id`, 표시 이름, 이미지, 대사, 선택지와 가중치를 설정한다.
5. Unity 상단 메뉴에서 `Tools > LOADED > Refresh Event Definition Pool`을 실행한다.
6. NodeMap에서 Event 노드로 진입해 동작을 확인한다.

> 에셋 파일 이름은 자유롭게 변경할 수 있다. 단, `Assets/Scripts/Event/EventDefinition.cs` 스크립트 파일 이름은 변경하지 않는다. MonoBehaviour/ScriptableObject 스크립트 파일 이름과 클래스 이름이 달라지면 Unity가 `Missing (Mono Script)`로 인식할 수 있다.

## 2. 이벤트 풀 등록

Event 씬은 `EventSceneManagers` 프리팹에 직렬화된 Event Pool과 `Resources/Events`의 에셋을 합쳐 사용한다. `Resources/Events` 런타임 탐색은 누락을 막는 안전망이지만, 신규 이벤트 구현의 완료 조건은 저장 위치와 관계없이 직렬화 풀까지 갱신하는 것이다. 새 이벤트를 만들거나 이동·삭제한 뒤에는 다음 메뉴를 실행한다.

```text
Tools > LOADED > Refresh Event Definition Pool
```

이 메뉴는 프로젝트 전체에서 모든 `EventDefinition`을 검색하므로 에셋이 반드시 `Resources` 폴더에 있을 필요는 없다. 실행 후 `EventSceneManagers` 프리팹의 Event Pool에 모든 이벤트가 null과 중복 없이 한 번씩 들어갔는지 확인한다.

`Tools > LOADED > Build Dedicated Event Scene`은 Event 씬을 처음 구성하거나 전체 구성을 다시 만들 때 사용하는 메뉴다. 수동으로 수정한 Anchor와 레이아웃이 있다면 단순 이벤트 추가에는 이 메뉴를 사용하지 말고 `Refresh Event Definition Pool`만 사용한다.

## 3. 기본 정보 설정

### Identity

| 필드 | 설명 |
| --- | --- |
| `Event Id` | 세이브 데이터에 기록되는 고유 ID. 예: `broken-slot-machine` |
| `Display Name` | Event 씬 제목에 표시되는 이름 |
| `Artwork` | 화면 왼쪽에 표시되는 이벤트 이미지 |

`Event Id`는 다른 이벤트와 중복되지 않아야 하며, 출시 후에는 변경하지 않는 것이 좋다. 진행 중인 이벤트를 저장할 때 이 ID를 사용하기 때문이다. `Event Id`가 비어 있으면 에셋 이름을 대신 사용한다.

### Dialogue

| 필드 | 설명 |
| --- | --- |
| `Dialogue` | 이벤트에 처음 진입했을 때 표시되는 본문 |
| `Choices` | 플레이어가 선택할 수 있는 선택지 목록 |

선택지는 최대 3개까지만 화면에 표시된다. 배열에 4개 이상을 넣어도 네 번째 이후 선택지는 표시되지 않는다.

선택지 문장은 다음 형식을 권장한다.

```text
[행동 이름] 행동에 대한 설명
```

예시:

```text
[동전을 넣는다] 10 골드를 지불하고 레버를 당긴다.
[총공에게 맡긴다] 탄환 하나를 무료로 강화한다.
[떠난다] 기계를 뒤로하고 길을 나선다.
```

대괄호 안의 행동 이름과 `강화`, `제거`, `무료`, 비용, 아이템·탄환 이름은 Event UI에서 강조 색상으로 표시된다.

## 4. 등장 확률과 조건부 가중치

### 기본 가중치

`Base Weight`는 이벤트의 기본 추첨 가중치다. 실제 확률은 선택 가능한 모든 이벤트의 최종 가중치 합을 기준으로 계산된다.

- `0`: 등장하지 않음
- 값이 클수록 다른 이벤트보다 자주 등장
- `Once Per Run`: 한 번 완료한 뒤 같은 런에서 다시 등장하지 않음

### Weight Rules

가중치 규칙은 위에서 아래 순서로 적용된다.

| Statistic | 의미 |
| --- | --- |
| `Elite Clears` | 완료한 엘리트 전투 수 |
| `Shop Visits` | 완료한 상점 노드 수 |
| `Event Clears` | 완료한 이벤트 노드 수 |
| `Money` | 현재 보유 골드 |
| `Owned Bullets` | 현재 보유 탄환 수 |
| `Current Health Percent` | 현재 체력 비율, 0~100 |
| `Cumulative Battle Turns` | 지금까지 전투에서 누적된 턴 수 |

`Comparison`으로 조건을 설정하고 `Operation`으로 가중치를 변경한다.

| Operation | 동작 |
| --- | --- |
| `Add` | 현재 가중치에 `Value`를 더함. 음수로 확률 감소 가능 |
| `Multiply` | 현재 가중치에 `Value`를 곱함. `0~1`로 확률 감소 가능 |

예를 들어 상점에 한 번도 방문하지 않았을 때 가중치를 20 올리려면 다음과 같이 설정한다.

```text
Statistic: Shop Visits
Comparison: Equal
Threshold: 0
Operation: Add
Value: 20
```

엘리트를 2번 이상 클리어했을 때 가중치를 2배로 만들려면 다음과 같이 설정한다.

```text
Statistic: Elite Clears
Comparison: Greater Than Or Equal
Threshold: 2
Operation: Multiply
Value: 2
```

## 5. 선택지 조건

각 Choice의 `Requirements`는 선택지 활성 여부를 결정한다. 조건을 만족하지 못하면 버튼이 비활성화되고 `Unavailable Reason`이 빨간색으로 표시된다.

| Requirement | 의미 | Amount 사용 여부 |
| --- | --- | --- |
| `None` | 항상 허용 | 사용 안 함 |
| `Money At Least` | 지정한 골드 이상 보유 | 사용 |
| `Removable Bullet Exists` | 최소 보유량을 지키면서 제거할 탄환 존재 | 사용 안 함 |
| `Upgradable Bullet Exists` | 강화 가능한 탄환 존재 | 사용 안 함 |
| `Bullet Space Exists` | 탄환 보유 공간 존재 | 사용 안 함 |
| `Item Space Exists` | 인벤토리 빈칸 존재 | 사용 안 함 |

효과 실행 전에 실제 골드, 탄환·아이템 공간과 보상 데이터도 자동 검사한다. 따라서 단순한 이벤트는 Requirement를 생략해도 안전하지만, 플레이어에게 구체적인 실패 사유를 보여주려면 Requirement와 `Unavailable Reason`을 직접 작성하는 것이 좋다.

예시:

```text
Type: Money At Least
Amount: 20
Unavailable Reason: 20 골드가 필요합니다.
```

## 6. 이벤트 효과

| Effect Type | 동작 | 필수 설정 |
| --- | --- | --- |
| `Gain Money` | 골드 획득 | `Amount` |
| `Lose Money` | 골드 지불 | `Amount` |
| `Heal` | 체력 회복 | `Amount` |
| `Lose Health` | 체력 감소 | `Amount` |
| `Add Bullet` | 탄환 획득 | `Bullet` |
| `Remove Chosen Bullet` | 플레이어가 고른 탄환 제거 | 없음 |
| `Upgrade Chosen Bullet` | 플레이어가 고른 탄환 강화 | 없음 |
| `Add Item` | 아이템 획득 | `Item` |

`Remove Chosen Bullet`과 `Upgrade Chosen Bullet`에서는 `Bullet` 칸을 비워 둔다. 선택지를 누르면 탄환 관리 패널이 열리고 플레이어가 대상 탄환을 직접 고른다.

`Add Item`에는 `Item` 참조가 필요하다. `Add Bullet`의 `Bullet`을 비우면 상점과 같은 등급 가중치로 무작위 탄환을 지급한다. 선택지 문구가 특정 탄환 이름을 명시하지 않는 보상은 이 방식을 사용하고, 특정 이름을 명시한 경우에만 `Bullet` 참조를 연결한다. 고정 보상은 버튼 호버 시 해당 보상 툴팁이 표시된다.

체력, 골드, 덱과 인벤토리 변경 사항은 선택 즉시 Event UI에 반영되고 런 세이브에도 저장된다.

## 7. 일반 선택지 만들기

확률과 반복을 사용하지 않는 기존 이벤트는 Choice의 `Effects`만 설정하면 된다.

### 20 골드를 지불하고 체력 25 회복

```text
Button Text: [치료받는다] 20 골드를 내고 체력을 25 회복한다.
Outcome Text: 의사는 상처를 능숙하게 봉합했다.

Requirements
- Money At Least / Amount 20 / "20 골드가 필요합니다."

Effects
- Lose Money / Amount 20
- Heal / Amount 25

Use Success Chance: false
Continue After Success: false
```

### 탄환 무료 강화

```text
Button Text: [손질을 맡긴다] 탄환 하나를 무료로 강화한다.

Requirements
- Upgradable Bullet Exists / "강화 가능한 탄환이 없습니다."

Effects
- Upgrade Chosen Bullet
```

무료 강화 버튼에 호버하거나 탄환 관리 패널에서 대상을 고를 때 다음 등급의 설명 툴팁이 표시된다.

## 8. 반복 선택지 구조

Choice에는 세 종류의 효과 배열이 있다.

| 배열 | 실행 시점 |
| --- | --- |
| `Attempt Effects` | 성공 확률을 굴리기 전에 항상 실행 |
| `Effects` | 성공했을 때 실행 |
| `Failure Effects` | 실패했을 때 실행 |

골드나 체력을 시도 비용으로 소모하려면 `Attempt Effects`에 넣는다. 성공 보상은 `Effects`, 실패 페널티는 `Failure Effects`에 넣는다.

### 반복 및 확률 필드

| 필드 | 설명 |
| --- | --- |
| `Use Success Chance` | 성공/실패 확률 판정 활성화 |
| `Base Success Chance Percent` | 첫 시도의 성공 확률 |
| `Success Chance Increase On Failure Percent` | 실패할 때마다 다음 성공 확률에 더할 값 |
| `Failure Outcome Text` | 실패했을 때 표시할 대사 |
| `Continue After Success` | 성공 후에도 선택지 화면 유지 |
| `Continue After Failure` | 실패 후에도 선택지 화면 유지 |
| `Maximum Selections` | 해당 선택지의 최대 선택 횟수. `0`은 무제한 |
| `Selection Limit Reason` | 최대 횟수 도달 시 표시할 사유 |

반복 도중 선택 횟수, 실패 횟수, 현재 확률과 결과 대사는 저장된다. 이벤트 도중 게임을 종료하고 다시 불러와도 같은 상태에서 이어진다.

반복 이벤트에는 자원 부족이나 최대 횟수 도달 시 빠져나갈 수 있도록 `[떠난다]` 같은 종료 선택지를 하나 넣는 것을 권장한다.

### 텍스트 변수

Button Text, Outcome Text, Failure Outcome Text에서 다음 변수를 사용할 수 있다.

| 변수 | 표시 값 |
| --- | --- |
| `{chance}` | 현재 성공 확률 |
| `{attempt}` | 다음 시도 번호 |
| `{selections}` | 완료한 선택 횟수 |
| `{failures}` | 현재까지 실패 횟수 |

예시:

```text
Button Text:
[레버를 당긴다] 체력 5를 잃는다. 성공 확률 {chance}%

Failure Outcome Text:
기계가 멈췄다. {failures}번 실패했다. 다음 성공 확률은 {chance}%다.
```

## 9. 실패할수록 좋은 보상을 주는 이벤트 예제

첫 번째 Choice를 다음과 같이 설정한다.

```text
Button Text: [상자를 찾는다] 체력 5를 잃고 수색한다. 성공 확률 {chance}%
Outcome Text: 마침내 상자를 찾아냈다.
Failure Outcome Text: 아무것도 찾지 못했다. 다음 성공 확률은 {chance}%다.

Attempt Effects
- Lose Health / Amount 5 / Amount Per Previous Selection 1

Use Success Chance: true
Base Success Chance Percent: 20
Success Chance Increase On Failure Percent: 15
Continue After Success: false
Continue After Failure: true
Maximum Selections: 5
```

`Amount Per Previous Selection`이 `1`이므로 첫 시도는 체력 5, 두 번째는 6, 세 번째는 7을 잃는다.

성공 보상을 실패 횟수에 따라 변경하려면 `Effects`에 여러 `Add Item` 효과를 추가하고 각 효과의 `Use Selection Range`를 활성화한다.

| 보상 | Minimum Previous Selections | Maximum Previous Selections |
| --- | ---: | ---: |
| 일반 아이템 | 0 | 0 |
| 고급 아이템 | 1 | 1 |
| 희귀 아이템 | 2 | -1 |

`Maximum Previous Selections`의 `-1`은 상한이 없다는 의미다. 현재 선택 횟수 구간에 해당하는 효과만 실행되고, 해당 보상만 호버 툴팁에 표시된다.

두 번째 Choice에는 효과 없는 종료 선택지를 추가한다.

```text
Button Text: [수색을 포기한다] 더 이상의 위험을 감수하지 않는다.
Outcome Text: 당신은 폐허를 뒤로하고 길을 나섰다.
Continue After Success: false
```

## 10. 성공 후에도 계속 선택하는 이벤트 예제

확률 없이 체력을 골드로 반복 교환하는 선택지는 다음과 같이 만들 수 있다.

```text
Button Text: [대가를 치른다] 체력을 잃고 골드를 얻는다. 현재 {attempt}번째 거래

Attempt Effects
- Lose Health / Amount 3 / Amount Per Previous Selection 1

Effects
- Gain Money / Amount 10 / Amount Per Previous Selection 5

Use Success Chance: false
Continue After Success: true
Maximum Selections: 4
Selection Limit Reason: 상인은 더 이상 거래하지 않습니다.
```

첫 거래는 체력 3을 잃고 10 골드, 두 번째 거래는 체력 4를 잃고 15 골드를 받는다. 별도의 `[떠난다]` 선택지를 함께 제공한다.

## 11. 효과 적용 구간

각 Event Effect에는 반복 횟수에 따른 추가 설정이 있다.

| 필드 | 설명 |
| --- | --- |
| `Amount Per Previous Selection` | 이전 선택 횟수마다 Amount에 누적해서 더함 |
| `Use Selection Range` | 선택 횟수 구간에 해당할 때만 효과 실행 |
| `Minimum Previous Selections` | 효과가 활성화되는 최소 이전 선택 횟수 |
| `Maximum Previous Selections` | 최대 이전 선택 횟수. `-1`은 제한 없음 |

첫 선택의 `Previous Selections` 값은 `0`이다. 효과 구간은 최소·최대 값을 모두 포함한다.

## 12. 다중 선택, 중간 선택지, 후속 장소

- `Bullet Selection Count`, `Item Selection Count`, `Relic Selection Count`는 필요한 대상을 모두 고른 뒤 한 번의 확인으로 효과를 적용한다. 탄환은 `Require Distinct Bullet Types`, `Require Same Bullet Grade`, 등급·ID 제한을 함께 설정할 수 있다.
- `Special Action = Random Bullet Offer`는 비용과 몰수를 먼저 한 번만 적용하고, 최대 3개의 무작위 탄환 중 하나를 고르는 중간 화면을 연다. 제안 목록은 런 세이브에 저장되어 재접속해도 다시 추첨되지 않는다.
- `Special Action = Slot Machine`은 3개의 릴 결과를 이벤트 설명 아래에 표시한다. 탄환은 등급 테두리 없이 `Cylinder Icon`만, 아이템은 `Icon`을 사용한다. 두 릴 일치는 비용의 3배 골드, 세 릴 일치는 그림 보상과 잭팟탄(+3)을 지급한다.
- `Special Action = Bullet Quiz`는 보유 탄환 하나의 등급 테두리를 단서로 최대 3개의 답을 제시한다.
- `Add Pending Status Effect`는 즉시 적용하지 않고 다음 전투 시작 시 플레이어에게 한 번 적용한다.
- Event Definition의 `Normal Battle Chance Percent`, `Elite Battle Chance Percent`, `Shop Chance Percent`는 이벤트 종료 후 곧바로 해당 장소로 이어질 확률이다. 합계에서 남는 확률은 NodeMap 귀환이며, 합계가 100을 넘지 않도록 작성한다.

## 13. 테스트 방법

Event 씬은 현재 런 세이브와 활성 Event 노드가 있어야 정상적으로 초기화된다. Event 씬을 에디터에서 직접 실행하기보다 다음 순서로 테스트한다.

1. 새 게임 또는 이어하기로 NodeMap에 진입한다.
2. Event 노드를 선택한다.
3. 제목, 이미지, 대사와 최대 3개의 선택지가 올바르게 표시되는지 확인한다.
4. 조건 미충족 선택지가 비활성화되고 빨간 사유가 표시되는지 확인한다.
5. 체력·골드·탄환·아이템 효과가 UI에 즉시 반영되는지 확인한다.
6. 탄환 제거·강화 선택지가 한 번의 클릭으로 관리 패널을 여는지 확인한다.
7. 반복 이벤트에서 실패 후 확률과 비용이 증가하는지 확인한다.
8. 반복 도중 종료하고 이어하기 했을 때 선택·실패 횟수가 유지되는지 확인한다.
9. 이벤트 완료 후 NodeMap으로 돌아가고 해당 노드가 완료 처리되는지 확인한다.

## 14. 자주 발생하는 문제

### 새 이벤트가 등장하지 않음

- `Tools > LOADED > Refresh Event Definition Pool`을 실행했는지 확인한다.
- `Base Weight`가 0인지 확인한다.
- Weight Rule 계산 결과가 0 이하가 되는지 확인한다.
- `Once Per Run` 이벤트를 현재 런에서 이미 완료했는지 확인한다.
- 이전에 시작한 다른 이벤트가 `Active Event Id`로 저장되어 있으면 그 이벤트가 먼저 재개된다.

### Event 씬 초기화 오류

- Event 씬을 직접 실행하지 말고 유효한 런 세이브가 있는 NodeMap의 Event 노드로 진입한다.
- Event Pool이 비어 있지 않은지 확인한다.
- `Event Id`가 중복되지 않는지 확인한다.
- 획득 효과의 Bullet 또는 Item 참조가 비어 있지 않은지 확인한다.

### 선택지가 보이지 않음

- Choices 배열의 앞에서부터 3개만 표시된다.
- Choice 원소 자체가 `None`인지 확인한다.
- 최대 선택 횟수에 도달한 선택지는 비활성화된다.

### 탄환 획득 후 다음 씬에서 복원되지 않음

- 이벤트 보상으로 사용하는 `BulletData`가 런 데이터 해석에 사용되는 ShopManager의 Bullet Pool에도 포함되어 있는지 확인한다.
- 아이템도 같은 방식으로 ShopManager의 Item Pool 또는 프로젝트의 아이템 리소스 구성에 포함되어 있어야 한다.

### 반복 이벤트에서 빠져나갈 수 없음

- `Continue After Success` 또는 `Continue After Failure`가 켜진 선택지만 존재하면 이벤트가 계속 유지된다.
- 효과가 없고 Continue 옵션이 꺼진 `[떠난다]` 선택지를 추가한다.

## 15. 최종 체크리스트

- [ ] 고유한 `Event Id`를 입력했다.
- [ ] Display Name, Artwork, Dialogue를 설정했다.
- [ ] Choices가 3개 이하이고 종료 선택지가 존재한다.
- [ ] 비용은 `Attempt Effects` 또는 성공 `Effects` 중 의도한 위치에 넣었다.
- [ ] 특정 탄환 보상만 `Add Bullet`의 데이터 참조를 연결했고, 일반 탄환 보상은 비워 두었다.
- [ ] 다중 선택 개수와 등급·중복 조건을 설정했다.
- [ ] 후속 장소 확률 합계가 100% 이하인지 확인했다.
- [ ] 탄환 제거·강화 효과의 Bullet 칸은 비워 두었다.
- [ ] 반복 이벤트의 성공·실패 후 Continue 설정을 확인했다.
- [ ] 최대 반복 횟수 또는 종료 선택지를 마련했다.
- [ ] Weight Rules의 최종 가중치가 0보다 큰 상황이 존재한다.
- [ ] `Refresh Event Definition Pool`을 실행했다.
- [ ] NodeMap의 Event 노드에서 Play Mode 테스트를 완료했다.
