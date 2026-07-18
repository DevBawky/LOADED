# Shop, Enemy Reward, Stage/State System

## 구현 결과

상점용 탄환 메타데이터, 적 처치 보상, 화폐 UI, 탄환 구매, 스테이지 데이터와 전투 흐름을 하나의 런타임 흐름으로 연결했다.

```text
StageData[]
└─ StageData
   └─ BattleData[]
      └─ BattleData
         ├─ Board Count / Tile Prefab
         ├─ Battle Type (Normal / Boss)
         └─ EnemyWave[]
            └─ Enemy Prefab / Count

Normal Battle → Battle Clear → Shop → Next Battle
Boss Battle   → Battle Clear → Shop → Next Stage
Final Battle  → Battle Clear → Shop → Run Complete
```

역할은 다음과 같이 분리되어 있다.

| 구성 요소 | 역할 |
| --- | --- |
| `StateManager` | 스테이지/전투 인덱스, 패널, 입력 잠금과 전체 진행 상태 관리 |
| `StageData` | 한 스테이지를 구성하는 `BattleData` 에셋 배열 관리 |
| `BattleData` | 전투 하나의 보드, 타입과 웨이브 설정 관리 |
| `WaveManager` | 현재 전투에 전달된 웨이브 생성과 적 턴 실행 |
| `BoardManager` | 전투 데이터에 맞춰 보드를 재생성 |
| `ShopManager` | 후보 탄환 중 3개를 등급 가중치로 추첨하고 구매 처리 |
| `CurrencyManager` | 현재 골드, 지출과 `$ {Money}` UI 갱신 |
| `RewardManager` | `EnemyData`의 드랍 판정 결과를 골드/아이템/탄환으로 지급 |

## 수정·추가 파일

- `Assets/Scripts/Bullet/BulletData.cs`
- `Assets/Scripts/Enemy/EnemyData.cs`
- `Assets/Scripts/Manager/BoardManager.cs`
- `Assets/Scripts/Manager/CurrencyManager.cs`
- `Assets/Scripts/Manager/DeckManager.cs`
- `Assets/Scripts/Manager/RewardManager.cs`
- `Assets/Scripts/Manager/ShopManager.cs`
- `Assets/Scripts/Manager/BattleData.cs`
- `Assets/Scripts/Manager/StageData.cs`
- `Assets/Scripts/Manager/StateManager.cs`
- `Assets/Scripts/Manager/WaveManager.cs`
- `Assets/Scripts/Player/PlayerCylinderUI.cs`
- `Assets/Scripts/Player/PlayerMove.cs`
- `Assets/Scripts/Manager/Battle SO/Stage 1 Battle 01~05.asset`
- `Assets/Scripts/Manager/Stage SO/Stage 1.asset`
- `Assets/Scenes/Stage 1.unity`

## BulletData 상점 필드

각 `BulletData`에 다음 필드를 추가했다.

| Inspector 필드 | 의미 |
| --- | --- |
| `Bullet Icon` | 상점의 `Image | Sprite`에 표시하는 탄환 아이콘 |
| `Cylinder Icon` | 플레이어 실린더 HUD에 원본 색상 그대로 표시하는 아이콘 |
| `Price` | 상점 가격. 음수는 사용할 수 없음 |
| `Grade` | 상점 등장 가중치에 사용하는 등급 |

기존 직렬화 필드 `sprite`는 `[FormerlySerializedAs("sprite")]`가 적용된 `bulletIcon`으로 이름을 이전했다. 기존에 Sprite가 연결된 에셋은 Unity 재임포트 시 참조가 유지된다.

상점은 `Bullet Icon`, 플레이어 실린더 HUD는 `Cylinder Icon`만 사용한다. Cylinder Icon에는 Bullet Icon이나 Primary Color 대체 규칙이 없으므로 각 탄환에 직접 지정해야 한다. 실린더 슬롯 Image 색상은 항상 `(1, 1, 1, 1)`이며 Sprite 원본 색상을 그대로 표시한다. Cylinder Icon이 없으면 해당 슬롯 Image만 숨긴다. 현재 8개 기본 BulletData에는 `Assets/Sprites/UI/Bullet.png`에서 분할한 Bullet Icon과 Cylinder Icon이 모두 연결되어 있다. 상점의 Bullet Icon만 미할당된 경우에는 해당 탄환의 `Primary Line Color` 블록을 임시 표시한다.

등급 Enum 값은 저장 데이터 호환성을 위해 숫자를 고정한다.

| Enum | 값 |
| --- | ---: |
| `Common` | 0 |
| `Uncommon` | 1 |
| `Rare` | 2 |
| `Epic` | 3 |
| `Legendary` | 4 |

기본 탄환 데이터는 다음과 같이 정리했다.

| 탄환 | ID | 가격 | 등급 |
| --- | --- | ---: | --- |
| Normal | `bullet_normal` | 5 | Common |
| Pierce | `bullet_pierce` | 8 | Uncommon |
| Stun | `bullet_stun` | 10 | Uncommon |
| Venom | `bullet_venom` | 10 | Uncommon |
| Weakness | `bullet_weakness` | 12 | Uncommon |
| Ghost | `bullet_ghost` | 14 | Rare |
| Lifesteal | `bullet_lifesteal` | 15 | Rare |
| Power | `bullet_power` | 20 | Epic |

기존에 중복되어 있던 `bulletId = 0`과 Ghost/Power/Weakness의 잘못된 표시 이름도 함께 수정했다. 가격과 등급은 초기 밸런스값이므로 각 `BulletData` Inspector에서 자유롭게 변경할 수 있다.

## 적 처치 드랍

`EnemyData > Defeat Drops`는 다음 두 필드로 구성된다.

- `Drop Chance`: 적 한 마리를 처치했을 때 드랍 표를 실행할 확률(0~100%)
- `Drop Items`: 확률 판정에 성공했을 때 선택할 보상 목록

한 적의 사망 이벤트마다 `Drop Chance`를 한 번 판정하고, 성공하면 유효한 `Drop Items` 중 정확히 하나를 `Selection Weight` 비율로 선택한다. 가중치가 0인 항목과 필요한 데이터가 비어 있는 항목은 후보에서 제외된다.

`EnemyDropType`은 다음 보상을 지원한다.

| 타입 | `Amount` 의미 | 필요한 참조 |
| --- | --- | --- |
| `Gold` | 지급 골드 | 없음 |
| `InventoryItem` | 지급을 시도할 복사 수 | `Item Data` |
| `Bullet` | 덱에 추가할 복사 수 | `Bullet Data` |

골드 지급은 `EnemyController.Defeated`를 받는 `WaveManager`에서 전투 완료 처리보다 먼저 실행한다. 강제 `Destroy`, 씬 종료 또는 웨이브 생성 롤백은 사망 이벤트를 발생시키지 않으므로 보상이 잘못 중복 지급되지 않는다.

`Stage 1`의 `Test Enemy` 기본 드랍은 다음과 같다.

```text
Drop Chance       : 100
Drop Type         : Gold
Amount            : 5
Selection Weight  : 1
```

## 골드와 Money UI

`CurrencyManager`는 `Starting Money`, 런타임 `Current Money`, `Text | Current Money` 참조를 관리한다.

- 골드 획득: `AddMoney(int amount)`
- 구매 지출: `TrySpendMoney(int amount)`
- 현재 값: `CurrentMoney`
- 변경 이벤트: `MoneyChanged(int currentMoney)`

값이 변할 때마다 `Panel | Floating > Panel | Money > Text | Current Money`를 다음 형식으로 갱신한다.

```text
$ 0
$ 5
$ 25
```

더하기는 `int.MaxValue`에서 포화되어 오버플로하지 않는다. 가격과 보유 골드가 정확히 같아도 구매할 수 있도록 구매 조건은 `CurrentMoney >= Price`다.

## 상점 추첨과 구매

`Stage 1 > @_StateManager > ShopManager`에는 다음 값이 연결되어 있다.

- `Bullet Pool`: Normal, Pierce, Stun, Venom, Power, Weakness, Ghost, Lifesteal 총 8종
- `Slots`: `Layout | Shop Items` 안의 전용 `Button | Bullet Item` 3개
- `Currency Manager`: 같은 GameObject의 `CurrencyManager`
- `Deck Manager`: Scene의 `@_DeckManager`

상점 슬롯은 3개이며 현재 연결된 후보 풀 8종 중 중복 없이 3개를 순차 추첨한다. 후보 풀 크기는 Inspector에서 자유롭게 변경할 수 있다.

기본 등급 가중치는 다음과 같다.

| 등급 | Appearance Weight |
| --- | ---: |
| Common | 100 |
| Uncommon | 60 |
| Rare | 25 |
| Epic | 10 |
| Legendary | 3 |

가중치는 절대 확률이 아니라 현재 남아 있는 등급 사이의 상대 비율이다. 먼저 등급을 가중치로 선택하고, 해당 등급 후보 중 하나를 균등 선택한다. 따라서 같은 등급 탄환 수가 많아져도 그 등급 자체의 등장 가중치가 곱절로 증가하지 않는다. 예를 들어 Common 100과 Epic 10만 남았다면 등급 추첨 비율은 100:10이다. 선택된 탄환은 후보에서 제거하므로 같은 상점에 동일 탄환이 두 번 표시되지 않는다. 가중치 0은 해당 등급을 후보에서 제외한다.

슬롯 표시 형식은 다음과 같다.

- `Image | Sprite`: `BulletData.BulletIcon`
- `Text | Cost`: `$5`, `$10`처럼 공백 없는 `${Cost}`

구매 성공 순서는 다음과 같다.

1. 슬롯과 탄환 데이터가 유효한지 확인한다.
2. `CurrencyManager.TrySpendMoney(Price)`로 돈을 지출한다.
3. `DeckManager.TryAddBullet(BulletData)`로 런타임 덱에 탄환을 추가한다.
4. 구매한 `Button`의 `Interactable`을 끄고 GameObject를 비활성화해 재구매를 막는다.

덱 추가가 실패하면 이미 지출한 가격을 즉시 환불한다. 획득 탄환은 `startingBullets`가 아니라 런타임 `deck`의 끝에 추가된다. 현재 덱은 마지막 요소가 Top인 LIFO 구조이므로 다음 장전에서 구매 탄환이 먼저 등장한다. 동일한 `BulletData`의 중복 구매와 중복 보유는 허용된다.

## StageData 구조

전투 하나는 독립된 `BattleData` ScriptableObject이고, `StageData`는 실행 순서대로 배치한 `BattleData` 참조 배열이다.

Project 창에서 `Create > Loaded > Battle`로 `BattleData`를 만들 수 있다.

`BattleData` 필드:

- `Battle Id`: 저장/조회에 사용할 전투 고유 ID
- `Display Name`: 전투 표시 이름
- `Battle Type`: `Normal` 또는 `Boss`
- `Board Count`: 이번 전투에서 생성할 타일 수
- `Tile Prefab`: 이번 전투에서 사용할 `BoardTile` 프리팹
- `Spawn Term`: 같은 전투 안에서 다음 웨이브가 등장하기까지 필요한 플레이어 턴 수
- `Waves`: 이번 전투의 웨이브 배열

Project 창에서 `Create > Loaded > Stage`로 `StageData`를 만들 수 있다.

`StageData` 필드:

- `Stage Id`: 저장/조회에 사용할 고유 ID
- `Display Name`: 화면 표시용 이름
- `Battles`: 해당 스테이지에서 실행할 `BattleData` 에셋 참조 배열

각 `EnemyWave`는 여러 `EnemyWaveEntry`를 가지며, 각 Entry에서 `Enemy Prefab`과 `Count`를 설정한다. 즉 한 전투 안에 여러 웨이브와 여러 종류의 적을 함께 구성할 수 있다.

`Stage 1.asset`은 다음 5개 독립 BattleData 에셋을 순서대로 참조한다.

- `Stage 1 Battle 01~04.asset`: `Normal`, 각 Test Enemy 2마리
- `Stage 1 Battle 05.asset`: `Boss`, Test Enemy 3마리
- 모든 전투: Board Count 7, 기존 Tile Prefab

현재 프로젝트에는 별도의 보스 EnemyData/프리팹이 없으므로 `Stage 1 Battle 05.asset`의 Test Enemy 3마리는 흐름 검증용 임시 구성이다. 실제 보스 프리팹을 만든 뒤 해당 BattleData의 `Waves > Enemies > Enemy Prefab`을 교체해야 한다.

## StateManager 흐름

`StateManager`가 다음 패널과 버튼을 직접 제어한다.

| 상태 | MainGame | Stage Clear | Shop | 플레이어 입력 |
| --- | --- | --- | --- | --- |
| Battle | On | Off | Off | 허용 |
| BattleClear | Off | On | Off | 잠금 |
| Shop | Off | Off | On | 잠금 |
| RunComplete | Off | On | Off | 잠금 |
| RunFailed | Off | On | Off | 잠금 |

모든 전투는 현재 플레이어 발사/이동과 적 턴 연출이 완전히 끝날 때까지 입력을 먼저 잠근 뒤 `Panel | Stage Clear`를 활성화한다. 따라서 연속 발사의 남은 탄환이 전환 직후 생성된 다음 전투 적을 공격하는 상태 경쟁이 발생하지 않는다. `Button | Go To Maintenance`를 누르면 `Panel | Shop`이 열리고 새 상품 3개가 생성된다. `Button | Go To Battle`을 눌러야 다음 BattleData의 Board와 Wave가 적용된다.

`Battle Type = Boss`도 같은 규칙으로 반드시 상점을 거친다. 현재 Stage 안에 다음 BattleData가 있으면 상점 종료 버튼은 `TO BATTLE`, 다음 StageData로 넘어가면 `NEXT STAGE`, 더 진행할 전투가 없으면 `RUN COMPLETE`로 표시된다. 마지막 Battle도 상점을 모두 이용한 뒤 버튼을 눌러 Run Complete로 전환한다.

플레이어 체력이 0이 되면 `PlayerHealth.Defeated`를 받아 현재 전투를 보상 없이 정리하고 `RunFailed`로 전환한다. Stage Clear 패널을 실패 화면으로 재사용해 `GAME OVER`를 표시하고 내비게이션 버튼과 플레이어 입력을 비활성화한다.

BattleClear와 Shop에서는 `PlayerMove.SetInputLocked(true)`를 사용한다. 키보드 이동/회전/대기뿐 아니라 `PlayerShoot`도 `CanStartAction`을 통과하지 못하므로 전투 밖에서 턴이나 탄환이 소비되지 않는다.

## Stage 1 Scene 연결 상태

`##--MANAGERS--##` 아래에 `@_StateManager`를 추가하고 다음 네 Component를 연결했다.

- `StateManager`
- `ShopManager`
- `CurrencyManager`
- `RewardManager`

`StateManager` 주요 참조:

```text
Stages                    = Stage 1.asset
Wave Manager              = @_WaveManager
Board Manager             = @_BoardManager
Shop Manager              = @_StateManager/ShopManager
Player Move               = Player/PlayerMove
Player Health             = Player/PlayerHealth
Main Game Panel           = Panel | MainGame
Stage Clear Panel         = Panel | Stage Clear
Shop Panel                = Panel | Shop
Go To Maintenance Button  = Button | Go To Maintenance
Go To Battle Button       = Button | Go To Battle
Player Spawn Offset       = (0, -0.7, 0)
```

`RewardManager`에는 Money, DeckManager와 Player의 `PlayerInventory`를 연결했다. `WaveManager`에는 `RewardManager`를 연결했다.

씬의 `Panel | Shop` 초기 활성값은 Off다. 런타임 시작 시 `StateManager`가 첫 Battle 패널 상태를 확정한다. 두 내비게이션 Button의 Inspector `On Click`은 비어 있어도 된다. `StateManager`가 활성화될 때 리스너를 코드로 등록하고 비활성화될 때 제거한다.

## 새 스테이지 적용 방법

1. 전투 수만큼 `Create > Loaded > Battle`을 선택해 BattleData 에셋을 만든다.
2. 각 BattleData의 `Battle Id`, `Board Count`, `Tile Prefab`, `Spawn Term`, `Waves`를 설정한다.
3. 마지막 BattleData만 `Battle Type = Boss`로 지정한다.
4. `Create > Loaded > Stage`를 선택해 StageData를 만든다.
5. `Stage Id`를 기존 Stage와 겹치지 않게 입력한다.
6. `Battles` 크기를 정하고 앞에서 만든 BattleData 에셋을 실행 순서대로 넣는다.
7. `Stage 1` Scene의 `@_StateManager > StateManager > Stages` 배열 뒤에 새 StageData를 순서대로 추가한다.
8. Boss 완료 후에도 Shop이 열리고, Shop의 `NEXT STAGE` 버튼이 새 Stage의 첫 BattleData를 여는지 확인한다.

StageData 배열 중 null이거나 Battle이 없는 항목은 건너뛴다. 각 유효 Stage는 마지막 Battle 하나만 Boss여야 하며 시작 시 이 불변식을 검사한다. 잘못된 Boss 순서, 빈 Wave, 잘못된 Enemy Prefab/Count 또는 Board 수용량보다 많은 적이 있으면 전투를 시작하지 않고 `CONFIGURATION ERROR`를 표시한다. 런타임의 후속 Wave 생성이 예외적으로 실패해도 `BATTLE ERROR`로 종료하여 빈 전투에서 무한 대기하지 않는다.

## 상점 후보와 UI 적용 방법

1. `Assets/Sprites/UI/Bullet.png`를 선택해 `Texture Type = Sprite (2D and UI)`, `Sprite Mode = Multiple`로 설정한다.
2. Sprite Editor에서 `Slice > Grid by Cell Size`를 선택하고 `32 x 32`로 분할한 뒤 Apply한다. 원본 크기는 `256 x 64`이므로 8열 2행으로 분할된다.
3. 각 `BulletData`에서 분할된 Sprite를 `Bullet Icon`과 `Cylinder Icon`에 각각 지정한다. Cylinder Icon은 별도 필수 참조이며 Bullet Icon으로 대체되지 않는다.
4. 각 `BulletData`의 `Price`, `Grade`를 설정한다.
5. `@_StateManager > ShopManager > Bullet Pool`에 상점 후보를 넣는다.
6. 현재 기본 8종을 모두 사용하려면 배열 크기를 8로 설정한다. 후보 수를 늘리거나 줄여도 동작한다.
7. `Grade Weights`에서 등급별 상대 가중치를 설정한다.
8. `Slots`에는 실제 탄환 슬롯 3개만 연결한다.
9. 각 Slot의 `Button`, 자식 `Image | Sprite`, 자식 `Text | Cost`를 연결한다.
10. 레이아웃 안의 `Shop Item`이나 `Button | Remove Bullet`은 탄환 Slot에 연결하지 않는다.

상점 슬롯 수를 늘리려면 UI Button을 추가하고 `Slots` 배열에도 같은 순서로 추가한다. 추첨 수는 유효한 Slot 수와 유효 후보 수 중 작은 값으로 자동 결정된다.

## 적 보상 적용 방법

골드 보상 예시:

```text
EnemyData
└─ Defeat Drops
   ├─ Drop Chance = 50
   └─ Drop Items[0]
      ├─ Drop Type = Gold
      ├─ Amount = 10
      └─ Selection Weight = 1
```

이 설정은 적 처치 시 50% 확률로 골드 10을 지급한다.

여러 보상 후보 예시:

```text
Drop Chance = 100
Drop Items
├─ Gold 5, Weight 80
├─ InventoryItem 1, Weight 15, Item Data 지정
└─ Bullet 1, Weight 5, Bullet Data 지정
```

성공한 한 번의 드랍에서 셋 중 하나만 선택된다. 각각을 독립 확률로 여러 개 동시에 드랍하려면 현재 공용 확률/가중치 표가 아니라 별도의 다중 드랍 규칙이 필요하다.

## 검증 체크리스트

1. 시작 시 MainGame만 활성이고 Shop/Stage Clear가 꺼져 있는지 확인한다.
2. Test Enemy 한 마리를 처치할 때 Money가 정확히 `$ 5` 증가하는지 확인한다.
3. 마지막 적 처치 보상이 Stage Clear 전환 전에 반영되는지 확인한다.
4. 일반 전투 완료 후 Go To Maintenance가 Shop을 여는지 확인한다.
5. 상점에 현재 후보 8종 중 서로 다른 탄환 3개가 표시되는지 확인한다.
6. Cost가 `$5` 형식이고 Money는 `$ 10` 형식인지 확인한다.
7. 돈이 부족하면 돈과 덱이 변하지 않는지 확인한다.
8. 돈이 가격과 같을 때 구매되는지 확인한다.
9. 구매 후 해당 Button GameObject가 비활성화되고 덱 수가 1 증가하는지 확인한다.
10. 구매 탄환이 다음 Reload에서 가장 먼저 장전되는지 확인한다.
11. Shop에서 이동, 회전, 대기, 장전과 발사가 실행되지 않는지 확인한다.
12. Go To Battle 이후 새 Battle의 Board Count와 Tile Prefab이 적용되는지 확인한다.
13. 하나의 Battle에 Wave를 2개 이상 넣었을 때 Spawn Term 후 다음 Wave가 생성되는지 확인한다.
14. Boss 완료 후에도 Stage Clear와 Shop을 거친 뒤 다음 Stage로 이동하는지 확인한다.
15. 마지막 Boss 완료 후 Shop을 이용하고 `RUN COMPLETE` 버튼으로 종료되는지 확인한다.
16. 전투 중 플레이어 체력이 0이 되면 남은 적이 정리되고 GAME OVER가 표시되며 입력과 진행 버튼이 잠기는지 확인한다.
17. 탄환 장전 시 실린더 슬롯이 각 BulletData의 Cylinder Icon을 표시하고 Image 색상이 항상 흰색인지 확인한다.

## 빌드 검증

`dotnet build Assembly-CSharp.csproj --no-restore` 기준으로 경고 0개, 오류 0개를 확인했다.
