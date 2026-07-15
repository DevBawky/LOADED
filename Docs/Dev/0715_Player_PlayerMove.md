## AI-002: PlayerMove

### Basic Information

* Date: 260715
* Author: Yoon
* AI Tool: Codex
* Model: 5.6 Sol - Medium
* Related Feature: Player Control

### Problem

플레이어가 `BoardManager`로 생성된 스테이지 위에서 Tile 단위로 이동하고, 방향 전환과 대기 행동을 수행할 수 있는 조작 기능이 필요했다.

키보드와 마우스 입력뿐만 아니라 `Canvas > Panel | Behaviour Tile > Layout | Tiles`에 있는 Move, Rotate, Wait 버튼으로도 같은 행동을 실행할 수 있어야 했다.

### Why AI Was Used

키보드·마우스 입력과 UI 버튼이 동일한 행동 메소드를 사용하도록 구조를 정리하고, `BoardManager`의 Tile 간격과 맵 범위를 이용해 한 칸 이동을 구현하기 위해 AI를 사용했다.

또한 맵 경계 처리, 명시적인 참조 연결, 행동 완료 시 턴을 전달할 수 있는 확장 지점까지 함께 구현하기 위해 도움을 받았다.

### Main Instructions

플레이어의 이동, 회전, 대기 기능을 구현해주세요.

* 플레이어는 `BoardManager`가 생성한 맵 위에서 한 칸씩 이동합니다.
* A 키를 누르면 왼쪽으로 한 칸 이동합니다.
* D 키를 누르면 오른쪽으로 한 칸 이동합니다.
* 맵의 첫 번째 또는 마지막 Tile을 벗어나지 않도록 처리합니다.
* 마우스 휠 버튼을 클릭하면 캐릭터 루트 오브젝트의 `Scale.x`에 `-1`을 곱해 방향을 전환합니다.
* S 키를 누르면 아무 행동 없이 한 턴을 넘깁니다.
* `Canvas > Panel | Behaviour Tile > Layout | Tiles`에 있는 Move, Rotate, Wait 버튼을 각 행동 메소드에 연결합니다.
* Move 버튼은 캐릭터가 현재 바라보는 방향으로 한 칸 이동합니다.
* 키보드·마우스 입력과 UI 버튼은 동일한 공개 메소드를 사용해야 합니다.
* 런타임 자동 탐색을 사용하지 않고 필요한 참조는 Inspector와 Scene에서 직접 연결합니다.
* 이동, 회전, 대기가 완료되면 턴 완료 상태를 외부 시스템에 전달할 수 있도록 구성합니다.
* 기존 프로젝트의 폴더 구조와 코딩 스타일을 먼저 확인하고 따릅니다.

작업 후 수정된 스크립트, 입력 방식, UI 버튼 연결, 턴 처리 방식과 컴파일 오류 여부를 간단히 설명해주세요.

### Output Summary

`PlayerMove`에 왼쪽 이동, 오른쪽 이동, 바라보는 방향으로 이동, 회전, 대기 메소드를 구현했다.

새 Input System의 `Keyboard.current`와 `Mouse.current`를 사용해 A, D, S 키와 마우스 가운데 버튼 입력을 처리했다. 한 프레임에 여러 행동이 동시에 처리되지 않도록 키보드 행동 이후에는 입력 처리를 종료한다.

`BoardManager`에는 현재 플레이어의 위치를 기준으로 인접 Tile의 위치를 계산하는 `TryGetAdjacentTilePosition`을 추가했다. Tile Parent의 로컬 X축과 `BoardDistance`를 기준으로 목표 Tile을 계산하며, 맵 범위를 벗어나는 이동은 거부한다.

SampleScene의 Player 인스턴스에 `BoardManager` 참조를 직접 할당하고 Move, Rotate, Wait 버튼의 `OnClick`을 각각 `MoveForward`, `Rotate`, `Wait`에 연결했다. 런타임 자동 탐색은 사용하지 않았다.

행동이 완료되면 `TurnCount`를 증가시키고 `TurnCompleted` 이벤트를 호출하도록 구성했다.

### Decision

* [x] 그대로 채택
* [ ] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * A, D, S 키와 마우스 가운데 버튼이 각 행동 메소드를 호출하는 코드 확인
  * Move, Rotate, Wait 버튼의 Persistent `OnClick` 연결 확인
  * Player 인스턴스에 `BoardManager` 참조가 할당되었는지 Scene 직렬화 데이터 확인
  * Tile 인덱스 계산과 맵 경계 조건을 정적 검토
  * 행동 완료 시 `TurnCount`와 `TurnCompleted`가 처리되는 코드 확인
  * `dotnet build LOADED.slnx --no-restore`로 C# 컴파일 확인
  * Unity Play Mode에서의 실제 입력과 버튼 동작은 수동 검증 항목으로 남김

* 테스트 결과:

  * A 키는 `MoveLeft`, D 키는 `MoveRight`, S 키는 `Wait`를 호출하도록 구현되어 있다.
  * 마우스 가운데 버튼은 `Rotate`를 호출하고 Player 루트의 `Scale.x`에 `-1`을 곱한다.
  * Move 버튼은 Scale.x의 부호를 기준으로 현재 바라보는 방향의 인접 Tile로 이동하도록 연결되어 있다.
  * Rotate 버튼과 Wait 버튼은 각각 `Rotate`, `Wait` 메소드에 연결되어 있다.
  * 이동할 목표 인덱스가 맵 범위를 벗어나면 위치를 변경하지 않고 턴도 소비하지 않는다.
  * 정상적인 이동, 회전, 대기는 `TurnCount`를 1 증가시키고 `TurnCompleted` 이벤트를 호출한다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode 실행을 통한 실제 조작 및 Console 검증은 수행하지 않았다.

* 발견한 문제:

  * UI에는 Move 버튼이 하나뿐이므로 이동 방향에 대한 해석이 필요했다. 캐릭터의 Scale.x 부호를 바라보는 방향으로 사용해 Move 버튼을 전진 행동으로 구현했다.
  * 현재 프로젝트에는 별도의 턴 매니저가 없어 실제 적 행동이나 턴 순환을 실행할 대상이 없었다. 이번 범위에서는 `TurnCount`와 `TurnCompleted` 이벤트를 제공해 이후 턴 시스템이 연결될 수 있도록 했다.
  * Unity Play Mode에서 Player 위치, 버튼 입력, 맵 경계 동작을 최종 확인해야 한다.

### Human Modifications

사용자가 직접 수정한 코드는 없다.

Move 버튼의 방향이 별도로 지정되지 않아, 캐릭터 루트의 Scale.x가 양수이면 오른쪽, 음수이면 왼쪽을 바라보는 것으로 해석했다. 이에 따라 Move 버튼은 현재 바라보는 방향으로 한 칸 이동하도록 구현했다.

또한 별도의 턴 매니저가 없는 현재 구조를 유지하면서도 이후 시스템과 연결할 수 있도록 `TurnCount`와 `TurnCompleted` 이벤트를 추가했다.

### Final Result

플레이어는 A와 D 키로 `BoardManager`가 생성한 맵 위를 한 칸씩 이동할 수 있으며 맵 경계를 벗어나지 않는다.

마우스 가운데 버튼 또는 Rotate 버튼으로 캐릭터 루트의 Scale.x를 반전할 수 있고, S 키 또는 Wait 버튼으로 행동 없이 한 턴을 넘길 수 있다. Move 버튼은 현재 바라보는 방향으로 한 칸 이동한다.

키보드·마우스 입력과 UI 버튼은 동일한 행동 메소드를 사용하며, 필요한 Scene 참조와 버튼 이벤트는 직접 연결되어 있다.

C# 빌드에서 경고와 오류가 발생하지 않았다. Unity Play Mode에서의 최종 동작 확인은 별도로 필요하다.

### Lessons Learned

UI에 이동 버튼이 하나만 있을 때는 버튼이 절대 방향으로 이동하는지, 캐릭터가 바라보는 방향으로 전진하는지를 프롬프트에 명확히 작성해야 한다.

대기 행동으로 턴을 넘기려면 단순히 아무 동작도 하지 않는 것뿐만 아니라 턴 완료를 어떤 시스템에 전달할지도 함께 정의해야 한다.

다음 프롬프트에서는 이동 성공과 실패 시 턴 소비 여부, 시작 Tile, 이동 애니메이션, 턴 매니저 연결 방식과 같은 세부 조건도 처음부터 포함하는 것이 중요하다.
