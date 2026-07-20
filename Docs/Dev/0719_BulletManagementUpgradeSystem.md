## AI-011: Bullet Management & Upgrade System

> 260720 후속 변경: 레벨별 데이터에 크리티컬 확률과 조건부 이벤트가 추가됐다. 상세 규칙은 `0718_Combat_BulletEffects.md`를 따른다.

### Basic Information

* Date: 260719
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Bullet / Deck / Shop / Tooltip / Custom Inspector

### Problem

Next Chip 이미지는 덱이 변경될 때 간헐적으로 `None` 상태가 되었고, draw deck의 마지막 탄환을 장전하면 graveyard 재활용이 다음 장전까지 지연되어 이미지가 사라지는 구간도 있었다. Bullet Tooltip에는 등급 정보가 없었다.

또한 보유 탄환은 모두 같은 `BulletData` 참조만 사용해 개별 강화 레벨을 보존할 수 없었으며, 상점에서 보유 탄환을 확인·선택·제거·강화하는 관리 흐름과 레벨별 능력치·비용 설정 UI가 필요했다.

### Why AI Was Used

draw deck, 장전실과 graveyard를 오가는 동일 탄환의 개별 상태를 유지하려면 기존 덱 컬렉션 타입과 발사 흐름 전체를 함께 변경해야 했다. 참조 누락 없이 영향 범위를 추적하고 기존 ScriptableObject 에셋 호환성을 유지하기 위해 AI를 사용했다.

사용자가 미리 만든 관리 패널과 버튼 프리팹 구조를 분석해 이름 기반 자동 연결과 Canvas 프리팹 직렬화 연결을 동시에 구성했다.

### Main Instructions

탄환 표시 오류와 탄환 관리·강화 시스템을 구현해주세요.

* 가끔 `Image | Next Chip`의 Sprite가 `None`으로 표시되는 원인을 찾아 수정해주세요.
* draw deck에 탄환이 1개만 남으면 graveyard를 미리 합쳐 셔플해 Next Chip이 비지 않게 해주세요.
* `Panel | Bullet Tooltip`에 `Text | Bullet Grade`도 표시해주세요.
* `Panel | Shop > Button | Manage Bullet`을 누르면 `Panel | Manage Bullets`을 활성화해주세요.
* 관리 화면을 열면 `Layout | Shop Items`를 숨기고 `Layout | Bullet Manage`를 표시해주세요.
* `Button | Close`를 누르면 관리 패널을 닫고 `Layout | Shop Items`를 다시 표시해주세요.
* 보유 탄환을 `Layout | n`에 행당 최대 5개씩 `Button _ My Bullet` 프리팹으로 표시해주세요.
* 탄환 목록은 Transform 저장 순서와 관계없이 `Layout | 1`, `2`, `3` 순서로 생성해주세요.
* 각 버튼의 `Image | Bullet Sprite`에 해당 탄환 아이콘을 표시하고, 클릭하면 `Layout | Bullet Manage`의 정보 UI를 채워주세요.
* `Button | Remove`를 누르면 선택 탄환을 비용을 지불하고 덱에서 제거해주세요.
* 탄환은 개별적으로 최대 3레벨까지 강화할 수 있게 해주세요.
* 이름은 기본 이름, `(+1)`, `(+2)`, `(+3)` 형식으로 표시해주세요.
* 레벨별 대미지, 사거리, 크리티컬 확률, 크리티컬 배율, 반동, 설명, 효과, 조건부 이벤트, 관통 확률과 연출 데이터를 다르게 설정할 수 있게 해주세요.
* 탄환 이름은 등급 색상을 사용하고 레벨 접미사는 RichText로 +1~+3 각각 다른 색상 변수를 사용해주세요.
* `Button | Upgrade`로 비용을 지불하고 강화할 수 있게 해주세요.
* 제거 비용과 강화 비용도 현재 레벨별로 `BulletData`에서 설정할 수 있게 해주세요.
* `BulletData`를 편리하게 편집할 수 있는 커스텀 Inspector를 만들어주세요.
* 작업 내용을 기존 `Docs/Dev/0715_*.md` 형식과 프롬프트를 포함해 문서화해주세요.

### Output Summary

`BulletInstance`를 추가해 한 발의 `BulletData`, 현재 레벨과 획득 순서를 보관한다. `DeckManager`의 draw deck, loaded bullets와 graveyard는 모두 같은 `BulletInstance` 참조를 이동시키므로 장전·발사·재셔플 이후에도 개별 강화 레벨이 유지된다.

`BulletData`에는 기본 레벨과 +1~+3의 `BulletLevelData`를 추가했다. 각 레벨에서 다음 항목을 독립 설정할 수 있다.

* 설명, 대미지, 사거리, 크리티컬 확률과 크리티컬 배율
* 대상 지정 효과 배열, 조건부 이벤트와 관통 단계별 확률
* 탄환 선 Material, 기본색과 보조색
* 턴 비소비 여부와 반동
* 현재 레벨의 제거 비용과 다음 레벨 강화 비용

이름은 등급 색상을 TMP Text 전체 색상으로 적용하고, `(+1)`~`(+3)` 접미사는 레벨별 색상 변수를 사용하는 RichText 태그로 표시한다. 260720 등급 개편 후 기본 등급 색상은 Normal, Rare, Ace, Legendary별로 제공하며 탄환마다 커스텀 색상으로 덮어쓸 수 있다. 기존 Uncommon과 Rare 에셋은 모두 새 Rare로 통합했다.

`BulletManagementUI`는 Canvas 루트에서 Manager와 UI를 찾고 다음 흐름을 제공한다.

1. Manage Bullet 버튼으로 관리 패널을 연다.
2. Shop Items 레이아웃을 숨기고 Bullet Manage 레이아웃을 표시한다.
3. 보유 탄환을 획득 순서대로 정렬하고 레이아웃 이름의 숫자를 기준으로 1~3행에 행당 5개씩 생성한다.
4. Close 버튼으로 관리 패널을 닫고 Shop Items 레이아웃을 복원한다.
5. 탄환 버튼을 클릭하면 아이콘, 이름, 등급, 설명과 현재 레벨 능력치를 표시한다.
6. 제거 또는 강화 버튼은 현재 골드와 비용을 비교해 `interactable`을 갱신한다.
7. 결제 후 덱 작업이 실패하면 비용을 환불한다.

Next Chip 문제에는 두 가지 원인이 있었다. 첫째, 표시 갱신이 `Layout | Inventory`에 붙은 툴팁 컴포넌트의 활성 상태에 의존해 Inventory 상위 패널이 꺼지면 덱 변경 이벤트를 놓칠 수 있었다. 툴팁 컨트롤러를 Canvas 루트로 옮기고, `Image | Next Chip`에 `NextBulletUI`를 추가해 이벤트·OnEnable·Start·LateUpdate에서 실제 Sprite와 다음 탄환을 동기화했다. Bullet Icon이 없으면 Cylinder Icon을 대체 이미지로 사용한다.

둘째, 기존 `DeckManager`는 draw deck이 완전히 비어 있는 다음 장전 시도에서만 graveyard를 재활용했다. 이제 장전 전후에 draw deck이 1개 이하인지 검사하고, graveyard에 탄환이 있으면 즉시 합쳐 셔플한다. 따라서 마지막 탄환을 장전한 직후에도 다음 탄환 미리보기가 유지된다. graveyard와 draw deck이 모두 비어 있고 나머지 탄환이 전부 장전된 상태는 실제 다음 탄환이 없으므로 빈 상태를 유지한다.

`BulletDataEditor`는 기본 정보, 등급·레벨 색상, 기본 능력치와 +1~+3 능력치를 Foldout으로 구분하고 ID와 아이콘 누락 경고 및 비용 의미를 Inspector에 표시한다.

### Decision

* [ ] 그대로 채택
* [x] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * 새 런타임 스크립트와 Editor 스크립트를 Unity 6000.3.15f1 참조로 컴파일
  * Canvas 프리팹의 컴포넌트 소유 GameObject와 Script GUID 확인
  * 보유 탄환이 draw deck, loaded bullets와 graveyard에서 중복 없이 수집되는지 코드 확인
  * 제거·강화 결제 실패와 덱 작업 실패 환불 경로 확인
  * 레벨별 능력치가 발사, 관통, 라인, 반동과 UI에서 `BulletInstance`를 통해 조회되는지 확인
  * draw deck 2개와 graveyard N개 상태에서 장전 후 남은 1개가 graveyard와 합쳐지는지 코드 흐름 확인
  * 탄환 행이 이름의 숫자 순서로 정렬되고 0~4번 탄환이 `Layout | 1`에 생성되는지 확인
  * Open/Close에서 Shop Items, Manage Bullets와 Bullet Manage 활성 상태가 서로 반대로 전환되는지 확인

* 테스트 결과:

  * 런타임 코드와 커스텀 Inspector 컴파일 결과는 경고 0개, 오류 0개였다.
  * Canvas 프리팹에는 `NextBulletUI`, `BulletManagementUI`와 Canvas 루트의 `InventoryTooltipUI`가 연결되어 있다.
  * 관리 패널은 초기 비활성 상태이며 Manage Bullet 버튼 클릭 시 런타임 목록을 생성하도록 구현되어 있다.
  * 행 탐색은 sibling index가 아닌 `Layout | n`의 숫자를 사용하므로 첫 탄환은 항상 `Layout | 1`에 생성된다.
  * Close 버튼은 생성한 목록을 정리하고 관리 패널을 닫은 뒤 Shop Items를 다시 활성화한다.
  * draw deck이 장전 전후 1개 이하가 되면 graveyard가 즉시 재활용되며, 같은 상태 변경 이벤트로 Next Chip이 갱신된다.
  * 실제 버튼 배치, 비용 지출, 강화 전후 발사 대미지와 Next Chip 복구는 Unity Play Mode 수동 검증 항목으로 남겼다.

* 발견한 문제:

  * Unity가 생성한 `Assembly-CSharp.csproj`가 새 파일을 즉시 포함하지 않아 최초 외부 빌드에서는 `BulletInstance` 타입을 찾지 못했다.
  * 검증 시 새 스크립트 항목을 프로젝트 파일에 임시로 포함해 컴파일한 뒤 생성 파일 변경을 원상복구했다.
  * Canvas YAML 연결 검토 중 Inventory RectTransform과 툴팁 컴포넌트의 소유 GameObject를 각각 다시 대조해 올바른 대상에 수정했다.

### Human Modifications

사용자가 `Canvas`와 `Button _ My Bullet` 프리팹의 시각적 레이아웃을 미리 제작했다. Codex는 기존 오브젝트 이름, 앵커와 LayoutGroup 설정을 변경하지 않고 동작 컴포넌트와 프리팹 참조만 연결했다.

각 탄환의 실제 +1~+3 밸런스 수치, 등급 색상과 레벨 접미사 색상은 새 커스텀 Inspector에서 사용자가 설정해야 한다. 새 필드의 기본 제거 비용은 5, 강화 비용은 10이다.

### Final Result

Next Chip 이미지는 특정 패널 활성 상태와 무관하게 다음 탄환과 계속 동기화된다. draw deck이 1개 이하가 되면 graveyard를 미리 셔플하므로 실제로 재활용 가능한 탄환이 있는 동안 이미지가 비지 않는다. Bullet Tooltip에는 이름·등급·레벨별 설명과 두 아이콘이 표시된다.

상점의 관리 패널에서 보유 탄환을 행당 최대 5개씩 확인하고 하나를 선택해 비용을 지불하여 제거하거나 +3까지 강화할 수 있다. 강화 레벨은 탄환 개별 상태로 유지되며 실제 발사 능력치와 UI에 모두 반영된다.

### Lessons Learned

런타임에 같은 ScriptableObject를 여러 장 보유할 수 있는 시스템에서는 공유 에셋 자체에 레벨을 저장하면 모든 복사본이 함께 변경된다. 불변 설정은 `BulletData`, 개별 진행 상태는 `BulletInstance`로 분리해야 한다.

여러 패널의 정보를 갱신하는 UI 컴포넌트는 비활성화될 수 있는 하위 패널보다 Canvas 같은 안정적인 생명주기의 루트에 배치하는 것이 안전하다.
