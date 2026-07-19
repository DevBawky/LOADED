## AI-010: Item Shop, Inventory & Tooltip System

### Basic Information

* Date: 260719
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Item / Shop / Inventory / Tooltip

### Problem

상점은 탄환만 판매할 수 있었고, 턴을 소비하지 않고 즉시 효과를 실행하는 일반 아이템 구조가 없었다.

구매한 아이템을 제한된 인벤토리에 보관하고, 상점·인벤토리·다음 탄환 UI에서 동일한 데이터 기반 툴팁을 화면 밖으로 잘리지 않게 표시할 시스템이 필요했다.

### Why AI Was Used

상점 추첨, 중복 불가 인벤토리, 즉시 사용 효과와 UI 갱신을 기존 Manager 구조에 안전하게 연결하기 위해 AI를 사용했다.

여러 해상도와 Canvas 모드에서 마우스를 따라가는 툴팁의 실제 모서리를 계산하고 화면 안으로 보정하는 로직도 함께 구현했다.

### Main Instructions

탄환 외의 일반 아이템과 관련 UI를 구현해주세요.

* 상점에서 탄환을 구매해도 버튼이 사라지지 않고 `Interactable`만 `false`가 되게 해주세요.
* Item은 턴을 소비하지 않고 클릭 즉시 효과가 발동해야 합니다.
* 즉시 체력을 20 회복하는 포션과 현재 draw deck을 재셔플하는 아이템을 예시로 만들어주세요.
* `Layout | Shop Items`의 첫 번째와 두 번째 `Button | Shop Item`에 아이템을 무작위로 표시해주세요.
* 아이템에는 등급이 없으므로 모든 고유 아이템의 등장 확률은 같아야 합니다.
* 구매한 아이템은 `Panel | Floating > Layout | Inventory`에 등록하고 최대 3개, 같은 아이템 중첩 불가로 제한해주세요.
* 인벤토리 슬롯이나 상점 아이템을 호버하면 이름, 설명과 아이콘을 가진 Item Tooltip을 표시해주세요.
* 상점 Bullet Item이나 `Panel | MainGame > Next Chip`을 호버하면 Bullet Tooltip을 표시해주세요.
* 툴팁은 마우스를 따라가되 어떤 화면 가장자리에서도 잘리지 않도록 위치를 보정해주세요.
* 구현 결과와 씬 연결 방법을 Markdown 문서로 작성해주세요.

### Output Summary

`ItemData`, `PlayerInventory`, `InventoryUI`를 추가·확장해 최대 3칸의 중복 불가 인벤토리를 구현했다. 아이템 사용 경로에서는 `PlayerMove.CompleteTurn()`을 호출하지 않는다.

예시 아이템은 다음과 같다.

| 에셋 | 표시 이름 | 가격 | 효과 |
| --- | --- | ---: | --- |
| `Assets/Resources/Items/Whiskey.asset` | 회복 포션 | 5 | 체력을 즉시 20 회복한다. 최대 체력이면 소모하지 않는다. |
| `Assets/Resources/Items/PocketWatch.asset` | 덱 재셔플러 | 5 | 현재 draw deck만 즉시 다시 섞는다. |

`ShopManager`는 아이템 후보를 중복 없이 균등 추첨해 두 슬롯에 표시한다. 구매 성공 시 첫 빈 인벤토리 칸에 등록하고 버튼의 `interactable`만 끈다. 인벤토리가 가득 찼거나 같은 아이템을 보유 중이면 구매할 수 없다.

`InventoryTooltipUI`는 인벤토리 슬롯, 상점 아이템, 상점 탄환과 Next Chip을 검사한다. Item Tooltip은 이름·설명·아이콘을, Bullet Tooltip은 이름·설명·탄환 아이콘·실린더 아이콘을 표시한다. 툴팁을 배치한 뒤 월드 모서리를 화면 좌표로 변환해 `Screen Padding` 안쪽으로 이동시킨다.

새 아이템은 `Create > Loaded > Item`으로 만들고 `ShopManager.Item Pool`에 한 번 등록하면 다른 아이템과 동일한 확률로 등장한다.

### Decision

* [ ] 그대로 채택
* [x] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * 최대 3칸과 동일 `ItemData` 중복 방어 코드 확인
  * 성공한 효과만 아이템을 소모하는지 확인
  * 아이템 사용 경로에 턴 완료 호출이 없는지 확인
  * 툴팁의 네 방향 화면 경계 보정 코드 확인
  * `dotnet build Assembly-CSharp.csproj --no-restore`로 C# 컴파일 확인

* 테스트 결과:

  * 회복은 실제 체력이 증가할 수 있을 때만 성공하고 재셔플은 draw deck이 있을 때만 성공한다.
  * 상점 아이템은 등급 가중치 없이 동일 확률로 선택된다.
  * 현재 전체 C# 컴파일 결과는 경고 0개, 오류 0개다.
  * 실제 포인터 위치와 여러 해상도에서의 툴팁 배치는 Play Mode 수동 검증으로 남겼다.

* 발견한 문제:

  * 초기 툴팁 컨트롤러가 인벤토리 레이아웃에 붙어 있어 상위 패널 비활성화 시 Next Chip 갱신 이벤트까지 끊길 수 있었다.
  * 후속 탄환 관리 작업에서 컨트롤러를 항상 활성인 Canvas 루트로 이동하고 Next Chip 표시를 전용 컴포넌트로 분리했다.

### Human Modifications

사용자가 제작한 `Canvas` 프리팹의 오브젝트 이름과 레이아웃을 유지하고, Codex가 이름 기반 자동 탐색과 직렬화 참조를 함께 사용하도록 연결했다.

아이템 아이콘, 최종 설명과 가격 밸런스는 `ItemData` 에셋에서 사용자가 자유롭게 변경할 수 있다.

### Final Result

상점에서 같은 확률로 등장하는 일반 아이템을 구매해 최대 3칸 인벤토리에 중복 없이 보관하고, 턴 소비 없이 즉시 사용할 수 있게 되었다.

아이템과 탄환 툴팁은 데이터의 이름·설명·아이콘을 표시하고 마우스를 따라가면서 화면 밖으로 잘리지 않게 보정된다.

### Lessons Learned

여러 UI 영역을 동시에 감시하는 컨트롤러는 특정 패널의 활성 상태에 생명주기가 묶이지 않도록 항상 활성인 공통 루트에 두어야 한다.

즉시 사용 아이템은 효과 실행 성공 여부와 소비 여부를 하나의 반환값으로 연결하면 최대 체력 같은 실패 상황에서 아이템이 잘못 사라지는 문제를 막을 수 있다.
