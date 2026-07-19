## AI-009: Shop, Reward & Stage System

### Basic Information

* Date: 260718
* Author: Yoon
* AI Tool: Codex
* Model: GPT-5
* Related Feature: Shop / Reward / Currency / Stage Progression

### Problem

탄환 데이터, 적 처치 보상, 골드 UI, 상점 구매와 여러 전투로 구성된 스테이지 진행이 각각 분리되어 있어 하나의 플레이 흐름으로 이어지지 않았다.

상점 후보 추첨, 구매 실패 시 환불, 전투 종료 중 입력 잠금, 보스 전투 이후의 다음 스테이지 전환까지 일관된 상태 관리가 필요했다.

### Why AI Was Used

여러 Manager와 ScriptableObject 사이의 책임을 분리하고 전투 종료·상점·다음 전투 전환 중 발생할 수 있는 상태 경쟁을 빠르게 점검하기 위해 AI를 사용했다.

씬과 에셋의 기존 구조를 보존하면서 필요한 참조, 예외 처리와 Inspector 설정 절차를 함께 정리하는 데 도움을 받았다.

### Main Instructions

적 처치부터 상점과 다음 전투까지 이어지는 시스템을 구현해주세요.

* `BulletData`에 상점 가격, 등급, 상점용 아이콘을 추가해주세요.
* 상점에는 후보 풀에서 서로 다른 탄환 3개를 추첨해 표시해주세요.
* 탄환 등급별 등장 가중치를 Inspector에서 설정할 수 있어야 합니다.
* 골드는 적 처치 보상으로 획득하고 `Text | Current Money`에 `$ {Money}` 형식으로 표시해주세요.
* 구매 시 돈을 먼저 지출하고 덱 추가가 실패하면 환불해주세요.
* 적 처치 보상은 골드, 아이템, 탄환 중 하나를 가중치로 선택할 수 있게 해주세요.
* 하나의 Stage는 여러 `BattleData`로 구성하고 각 전투마다 보드, 전투 타입과 웨이브를 설정할 수 있게 해주세요.
* 일반 전투와 보스 전투 모두 `Battle Clear → Shop → 다음 전투 또는 다음 Stage` 순서로 진행해주세요.
* Battle Clear와 Shop에서는 이동, 장전과 발사를 포함한 플레이어 입력을 잠가주세요.
* 마지막 전투 뒤에도 상점을 이용한 다음 Run Complete로 전환해주세요.
* 플레이어 사망 시 남은 전투를 정리하고 Game Over 상태로 전환해주세요.
* Stage 1 씬에 필요한 Manager와 예시 에셋을 연결하고 적용 방법을 문서화해주세요.

### Output Summary

`StateManager`, `StageData`, `BattleData`, `WaveManager`, `BoardManager`, `ShopManager`, `CurrencyManager`, `RewardManager`로 책임을 분리했다.

```text
StageData[]
└─ StageData
   └─ BattleData[]
      └─ BattleData
         ├─ Board Count / Tile Prefab
         ├─ Battle Type (Normal / Boss)
         └─ EnemyWave[]
            └─ Enemy Prefab / Count
```

`ShopManager`는 남은 등급을 가중치로 먼저 선택한 뒤 해당 등급의 후보 하나를 균등 선택하고, 선택한 탄환은 후보에서 제거해 한 상점 내 중복을 막는다. 기본 상대 가중치는 Common 100, Uncommon 60, Rare 25, Epic 10, Legendary 3이다.

구매 흐름은 `유효성 검사 → TrySpendMoney → TryAddBullet → 구매 버튼 interactable 비활성화` 순서다. 덱 추가 실패 시 가격을 환불한다. 후속 아이템 시스템 요구를 반영해 구매한 버튼 GameObject는 숨기지 않고 화면에 유지한다.

`CurrencyManager`는 골드 오버플로를 방지하고 값이 바뀔 때마다 `MoneyChanged` 이벤트와 `$ {Money}` UI를 갱신한다. `RewardManager`는 적 사망 이벤트에서 전투 완료 처리 전에 보상을 지급한다.

`StateManager`는 Battle, BattleClear, Shop, RunComplete, RunFailed 상태의 패널과 입력 잠금을 관리한다. 전투 종료 시 진행 중인 플레이어 행동이 완전히 끝난 뒤 상태를 바꾸므로 남은 탄환이 다음 전투의 적을 공격하는 경쟁 조건을 막는다.

### Decision

* [ ] 그대로 채택
* [x] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * 적 처치 보상이 전투 완료 이벤트보다 먼저 실행되는지 코드 흐름 확인
  * 상점 추첨이 중복 없이 등급 가중치를 사용하는지 확인
  * 지출 후 덱 추가 실패 시 환불 경로 확인
  * BattleClear와 Shop 상태에서 플레이어 입력 잠금 확인
  * `dotnet build Assembly-CSharp.csproj --no-restore`로 C# 컴파일 확인

* 테스트 결과:

  * 상점 후보는 연결된 슬롯 수와 유효 후보 수 중 작은 값만큼 생성된다.
  * 돈이 가격과 같을 때도 구매할 수 있고, 실패 시 지출 금액이 환불된다.
  * 구매한 탄환은 런타임 덱에 별도 보유 탄환으로 추가된다.
  * 현재 전체 C# 컴파일 결과는 경고 0개, 오류 0개다.
  * 실제 전투 5개 진행, 보스 교체와 Run Complete까지의 Play Mode 검증은 수동 항목으로 남겼다.

* 발견한 문제:

  * 초기 구현은 구매 성공 후 상점 버튼 GameObject를 비활성화했다.
  * 이후 UI 요구사항에 맞춰 버튼은 유지하고 `interactable = false`만 적용하도록 수정했다.

### Human Modifications

사용자가 구매한 상품이 상점에서 사라지지 않아야 한다는 후속 요구를 전달했다. Codex가 기존 비활성화 로직을 `Button.interactable` 변경으로 수정했다.

현재 Stage 1의 마지막 전투는 별도 보스 프리팹이 없어 Test Enemy를 사용한 흐름 검증용 구성이다. 실제 보스 데이터는 사용자가 교체해야 한다.

### Final Result

적 처치 보상으로 획득한 골드를 상점에서 사용해 탄환을 구매하고, 여러 전투와 보스 전투를 거쳐 다음 Stage 또는 Run Complete로 진행하는 흐름이 연결되었다.

상점 추첨·환불·입력 잠금·실패 상태를 방어하며, 구매한 버튼은 사라지지 않고 비활성 상태로 남는다.

### Lessons Learned

진행 상태가 여러 Manager에 걸쳐 있을 때는 패널 전환보다 먼저 행동 종료와 입력 잠금을 확정해야 다음 전투와의 상태 경쟁을 막을 수 있다.

상점의 “판매 완료”는 GameObject 활성 상태와 Button 상호작용 상태를 구분해 명시해야 UI 요구가 정확하게 구현된다.
