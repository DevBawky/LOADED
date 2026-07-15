## AI-001: BoardManager

### Basic Information

* Date: 260715
* Author: Yoon
* AI Tool: Codex
* Model: 5.6 Sol - Medium
* Related Feature: Level Design

### Problem

스테이지의 Tile 개수가 변경될 때마다 직접 위치를 조정해야 했으며, Tile 개수에 따라 스테이지의 중심이 한쪽으로 치우치는 문제가 있었다.

`BoardCount`와 `BoardDistance` 값만 변경해도 Tile이 중앙을 기준으로 좌우 대칭으로 자동 배치되는 구조가 필요했다.

### Why AI Was Used

Tile 개수가 홀수이거나 짝수인 경우를 모두 만족하는 중앙 정렬 공식을 계산하고 예외 처리를 빠르게 구현하기 위해 AI를 사용했다.

또한 기존 Inspector 참조를 유지하면서 필드 역할을 변경하고, null 참조와 중복 생성 등의 예외 상황까지 함께 처리하기 위해 도움을 받았다.

### Main Instructions

변수 값에 따라 중앙에서 좌우 대칭으로 스테이지를 생성하는 `BoardManager`를 구현해주세요.

* `int BoardCount`만큼 Tile을 생성합니다.
* 각 Tile은 `float BoardDistance` 간격으로 X축에 일렬 배치합니다.
* 홀수 개일 때는 가운데 Tile이 부모 오브젝트의 로컬 좌표 `(0, 0, 0)`에 위치해야 합니다.
* 짝수 개일 때는 가운데 빈 공간을 기준으로 전체 Tile이 좌우 대칭으로 배치되어야 합니다.
* Tile Prefab과 Tile이 생성될 부모 Transform은 Inspector에서 할당합니다.
* 생성된 Tile은 해당 부모 Transform의 자식으로 정리합니다.
* Tile의 위치와 회전은 부모 오브젝트의 로컬 좌표를 기준으로 설정합니다.
* 기존 `SpawnOrigin` 필드가 있다면 `TileParent` 역할로 변경하고, 기존 Inspector 참조가 유지되도록 처리합니다.
* 런타임 자동 탐색은 사용하지 않습니다.
* 중복 생성과 null 참조를 방지해주세요.
* `BoardCount`에 잘못된 음수 값이 들어오는 경우도 방어해주세요.
* 기존 프로젝트의 폴더 구조와 코딩 스타일을 먼저 확인하고 따릅니다.

작업 후 수정된 스크립트, Tile 배치 방식, Unity Inspector 적용 방법을 간단히 설명하고 컴파일 오류 여부를 확인해주세요.

### Output Summary

기존 `BoardManager`의 Tile 생성 방식을 수정하여, 설정된 `BoardCount`만큼 Tile이 중앙을 기준으로 좌우 대칭 배치되도록 구현했다.

Tile은 부모 Transform의 로컬 좌표를 기준으로 X축에 배치된다. 홀수 개일 때는 중앙 Tile이 로컬 좌표 `(0, 0, 0)`에 위치하며, 짝수 개일 때는 중앙의 빈 공간을 기준으로 양쪽 Tile이 대칭을 이루도록 계산된다.

기존 `SpawnOrigin` 필드는 `TileParent` 역할로 변경되었으며, 기존 Inspector 참조가 유지되도록 처리되었다.

추가로 Tile Prefab과 Tile Parent에 대한 null 검사, 중복 생성 방지, 음수 `BoardCount` 입력 방어가 적용되었다.

### Decision

* [ ] 그대로 채택
* [x] 수정 후 채택
* [ ] 일부만 채택
* [ ] 폐기

### Validation

* 검증 방법:

  * 중앙 정렬 공식에 `BoardCount` 5와 4를 대입하여 예상 X 좌표 확인
  * `Instantiate(tilePrefab, tileParent)` 호출과 로컬 위치·회전 설정 코드 확인
  * null 참조, 음수 `BoardCount`, 중복 생성 방어 코드 확인
  * `dotnet build LOADED.slnx --no-restore`로 C# 컴파일 확인
  * Unity Play Mode에서의 실제 배치와 Console 확인은 수동 검증 항목으로 남김

* 테스트 결과:

  * 코드 계산상 `BoardCount`가 5이면 X 좌표는 `-2d, -d, 0, d, 2d`가 된다.
  * 코드 계산상 `BoardCount`가 4이면 X 좌표는 `-1.5d, -0.5d, 0.5d, 1.5d`가 된다.
  * Tile은 `TileParent`를 부모로 생성되고 로컬 X 좌표와 단위 회전값을 사용하도록 구현되어 있다.
  * Tile Prefab 또는 Tile Parent가 null인 경우, `BoardCount`가 음수인 경우, 이미 생성된 경우를 방어하는 코드가 적용되어 있다.
  * C# 빌드 결과는 경고 0개, 오류 0개였다.
  * Unity Play Mode 실행을 통한 실제 장면 배치 및 Console 검증은 수행하지 않았다.

* 발견한 문제:

  * 최초 결과에서는 Tile이 시작 위치에서 오른쪽 방향으로만 생성되어 전체 스테이지의 중심이 한쪽으로 치우쳤다.
  * 기존 결과는 홀수와 짝수에 따른 중앙 정렬 방식이 구분되어 있지 않았다.
  * 생성된 Tile의 부모와 로컬 좌표 기준이 요구사항과 다르게 처리되어 수정이 필요했다.

### Human Modifications

최초 AI 결과에서는 Tile이 `SpawnOrigin`을 시작점으로 오른쪽 방향에만 생성되도록 구현되어 있었다.

사용자가 중앙 기준 좌우 대칭 배치와 `SpawnOrigin`의 부모 역할 변경을 추가로 요청했고, Codex가 후속 작업으로 코드를 수정했다. 실제 코드에서는 시작 위치와 각 Tile의 위치를 다음과 같이 계산한다.

`startOffset = -(BoardCount - 1) * BoardDistance * 0.5f`

`positionX = startOffset + BoardDistance * index`

이는 `(index - (BoardCount - 1) / 2f) * BoardDistance`와 동일한 결과를 내며, 홀수 개일 때는 가운데 Tile이 로컬 좌표 0에 위치하고 짝수 개일 때는 중앙의 빈 공간을 기준으로 좌우 대칭이 된다.

또한 `SpawnOrigin`을 단순 생성 시작 위치가 아닌 `TileParent` 역할로 변경하고, `FormerlySerializedAs`를 사용해 기존 Inspector 참조가 유지되도록 했다. 사용자가 직접 수정한 코드는 없다.

### Final Result

`BoardCount`와 `BoardDistance` 값에 따라 스테이지 Tile이 중앙을 기준으로 동적으로 생성되도록 구현했다.

홀수 개의 Tile은 가운데 Tile을 중심으로 배치되며, 짝수 개의 Tile은 중앙의 빈 공간을 기준으로 좌우 대칭 배치된다.

Tile Prefab과 Tile Parent는 Inspector에서 직접 할당하며, 생성된 Tile은 모두 Tile Parent의 자식으로 정리된다.

잘못된 입력값, 중복 생성, null 참조에 대한 방어가 적용되었으며 C# 빌드에서 경고와 오류가 발생하지 않았다. Unity Play Mode에서의 최종 동작 확인은 별도로 필요하다.

### Lessons Learned

Tile을 단순히 일정한 간격으로 생성하는 것만 요청하면 한쪽 방향으로 배치될 수 있으므로, 생성 기준점과 중앙 정렬 방식을 명확하게 작성해야 한다.

특히 홀수와 짝수의 배치 결과, 월드 좌표와 로컬 좌표 중 어떤 기준을 사용할지, 기존 Inspector 참조를 유지해야 하는지를 프롬프트에 구체적으로 포함하는 것이 중요했다.

다음 프롬프트에서는 코드 구현뿐만 아니라 실제로 확인할 테스트 조건과 기대 결과도 처음부터 함께 작성해야 한다.
