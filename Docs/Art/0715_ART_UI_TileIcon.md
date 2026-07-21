# AI-002: Western Skill Tile Icons

## Basic Information

- Date: 260715
- Author: Yoon
- AI Tool: ChatGPT Image Generation
- Model: GPT Image
- Related Feature: UI / Skill Tile Icons

## Problem

Shogun Showdown 형식의 전술 보드 게임에서 사용할 스킬 타일 아이콘이 필요했다.

게임의 핵심 행동인 이동, 회전, 대기, 장전, 사격을 플레이어가 아이콘만 보고도 빠르게 구분할 수 있어야 했다. 또한 전국시대 분위기가 아닌 프로젝트의 서부 세계관과 어울리는 통일된 픽셀 아트 스타일이 필요했다.

## Why AI Was Used

5개의 스킬 타일 아이콘 초안을 빠르게 제작하고, 각 행동을 직관적으로 표현할 수 있는 시각 요소를 확인하기 위해 AI를 사용했다.

공통 프레임과 색상, 픽셀 밀도는 유지하면서 각 기능에 맞는 오브젝트와 상징을 빠르게 비교하는 것을 목표로 했다.

## Common Style Guide

- 서부 테마의 전술 보드 게임용 스킬 타일
- 정사각형 게임 UI 아이콘
- 픽셀 아트 스타일
- 두꺼운 목재 프레임
- 황동 또는 금속 재질의 모서리 장식
- 어두운 내부 배경
- 갈색, 황토색, 금색 중심의 색상 구성
- 작은 크기에서도 식별 가능한 단순한 실루엣
- 높은 명암 대비
- 중앙 정렬 구도
- 텍스트 없음
- 인물 얼굴 없음
- 복잡한 배경 없음

---

## 1. Move Tile Icon

### Main Instructions

X축 좌우 이동을 의미하는 서부 테마의 스킬 타일 아이콘을 생성한다.

- 굵은 양방향 수평 화살표 사용
- 카우보이 부츠와 박차 사용
- 이동감을 표현하는 먼지 효과 추가
- 작은 크기에서도 좌우 이동 기능이 명확하게 보여야 함

### Prompt Used

```text
Create a square pixel art skill tile icon for a western themed tactical board game.

Use a thick wooden frame with brass corner plates and a dark inner background. Keep the frame, pixel density, lighting, and color palette consistent with the rest of the western skill tile set.

The icon represents movement on the X axis only. Place a large, bold, golden double headed horizontal arrow across the upper center.

Place a pair of brown cowboy boots with a clearly visible metal spur below the arrow. Add small dust clouds near the boots to communicate movement.

The composition must be centered, simple, high contrast, and readable at a small in game UI size.

Use a strong silhouette, clean pixel art, warm brown and gold colors, no text, no character face, and no busy background.
```

### Output Summary

굵은 금색 양방향 화살표와 카우보이 부츠를 중심으로 구성된 이동 아이콘이 생성되었다.

초기 결과보다 부츠와 박차의 형태를 명확하게 하고, 다른 아이콘과 동일한 목재 및 황동 프레임을 사용하도록 이동 아이콘을 다시 생성했다.

---

## 2. Rotate Tile Icon

### Main Instructions

캐릭터가 현재 위치에서 방향을 전환하는 행동을 의미하는 스킬 타일 아이콘을 생성한다.

- 회전을 의미하는 원형 화살표 사용
- 서부 분위기를 나타내는 카우보이 모자 사용
- 회전 방향과 동작이 한눈에 보여야 함

### Prompt Used

```text
Create a square pixel art skill tile icon for a western themed tactical board game.

Use a thick wooden frame with brass corner plates and a dark inner background. Keep the frame, pixel density, lighting, and color palette consistent with the rest of the western skill tile set.

The icon represents rotating or turning direction while remaining in the same position.

Place a large golden circular arrow around the center. Put a brown cowboy hat with a sheriff badge inside the circular arrow.

Add subtle curved motion lines and small dust effects to communicate rotation.

The composition must be centered, simple, high contrast, and readable at a small in game UI size.

Use a strong silhouette, clean pixel art, warm brown and gold colors, no text, no character face, and no busy background.
```

### Output Summary

금색 원형 화살표 안에 카우보이 모자가 배치된 회전 아이콘이 생성되었다.

원형 화살표와 회전 궤적을 통해 현재 위치에서 방향을 전환하는 기능을 표현했다.

---

## 3. Wait Tile Icon

### Main Instructions

현재 턴에 행동하지 않고 시간을 보내는 대기 행동을 의미하는 스킬 타일 아이콘을 생성한다.

- 시간의 흐름을 나타내는 모래시계 사용
- 정적인 분위기와 서부 테마를 함께 표현
- 다른 행동 아이콘보다 차분한 구도로 구성

### Prompt Used

```text
Create a square pixel art skill tile icon for a western themed tactical board game.

Use a thick wooden frame with brass corner plates and a dark inner background. Keep the frame, pixel density, lighting, and color palette consistent with the rest of the western skill tile set.

The icon represents waiting and taking no immediate action.

Place a large glowing western style hourglass in the center. Show golden sand falling inside the hourglass.

Add a resting cowboy hat and a small tumbleweed near the bottom to reinforce the western theme and the feeling of time passing.

The composition must be centered, calm, simple, high contrast, and readable at a small in game UI size.

Use a strong silhouette, clean pixel art, warm brown and gold colors, no text, no character face, and no busy background.
```

### Output Summary

중앙의 모래시계와 하단의 카우보이 모자, 회전초를 이용한 대기 아이콘이 생성되었다.

모래가 떨어지는 모습을 통해 한 턴을 소비하거나 시간을 보내는 기능을 표현했다.

---

## 4. Reload Tile Icon

### Main Instructions

리볼버에 탄약을 다시 장전하는 행동을 의미하는 스킬 타일 아이콘을 생성한다.

- 리볼버 실린더를 핵심 오브젝트로 사용
- 탄환과 회전 화살표를 함께 배치
- 회전 아이콘과 혼동되지 않도록 탄약 요소를 강조

### Prompt Used

```text
Create a square pixel art skill tile icon for a western themed tactical board game.

Use a thick wooden frame with brass corner plates and a dark inner background. Keep the frame, pixel density, lighting, and color palette consistent with the rest of the western skill tile set.

The icon represents reloading a revolver.

Place a large revolver cylinder in the center with clearly visible empty chambers. Surround it with a golden curved reload arrow.

Add several brass revolver cartridges and a western ammunition belt near the bottom.

Make the ammunition and cylinder more visually important than the arrow so the icon cannot be confused with the rotate action.

The composition must be centered, simple, high contrast, and readable at a small in game UI size.

Use a strong silhouette, clean pixel art, warm brown, gold, and steel colors, no text, no character face, and no busy background.
```

### Output Summary

리볼버 실린더와 탄환, 탄띠, 회전 화살표로 구성된 장전 아이콘이 생성되었다.

실린더와 탄약을 크게 배치하여 단순한 회전 기능이 아니라 장전 행동임을 구분했다.

---

## 5. Fire Tile Icon

### Main Instructions

리볼버를 발사하는 행동을 의미하는 스킬 타일 아이콘을 생성한다.

- 리볼버와 총구 화염을 핵심 요소로 사용
- 연기, 불꽃, 탄환으로 발사 순간을 강조
- 다른 아이콘보다 강한 동작감과 밝은 효과 사용

### Prompt Used

```text
Create a square pixel art skill tile icon for a western themed tactical board game.

Use a thick wooden frame with brass corner plates and a dark inner background. Keep the frame, pixel density, lighting, and color palette consistent with the rest of the western skill tile set.

The icon represents firing a revolver.

Place a large western revolver in the center, aimed horizontally. Show a bright, explosive muzzle flash coming from the barrel.

Add a small flying bullet, smoke, sparks, and subtle recoil motion lines to clearly communicate that the gun has just fired.

The composition must be centered, dynamic, high contrast, and readable at a small in game UI size.

Use a strong silhouette, clean pixel art, warm brown, gold, orange, and steel colors, no text, no character face, and no busy background.
```

### Output Summary

발사 중인 리볼버와 큰 총구 화염, 탄환, 연기를 중심으로 한 사격 아이콘이 생성되었다.

밝은 총구 화염과 강한 명암 대비를 사용해 다른 행동보다 공격적인 기능임을 강조했다.

---

## Decision

- [ ] 그대로 채택
- [x] 수정 후 채택
- [ ] 일부만 채택
- [ ] 폐기

## Validation

- 검증 방법:
  - 5개의 아이콘을 동일한 크기로 축소하여 식별성 확인
  - 각 아이콘만 보고 행동을 구분할 수 있는지 확인
  - 프레임, 색상, 픽셀 밀도와 명암이 통일되어 있는지 확인
  - 실제 게임 UI에 배치했을 때 주변 요소와 겹치지 않는지 확인

- 테스트 결과:
  - 이동, 회전, 대기, 장전, 사격의 기능이 서로 다른 핵심 오브젝트로 구분되었다.
  - 목재 프레임과 황동 모서리 장식을 공통으로 사용하여 시리즈의 통일감을 확보했다.
  - 어두운 배경과 밝은 핵심 심벌의 대비로 작은 크기에서도 주요 기능을 확인할 수 있었다.

- 발견한 문제:
  - 최초 이동 아이콘은 다른 아이콘과 프레임 및 구도 차이가 있어 다시 생성했다.
  - 장전과 회전 아이콘은 모두 원형 화살표를 포함하므로, 장전 아이콘에서는 실린더와 탄약을 더 크게 표현할 필요가 있었다.
  - 대기 아이콘은 다른 아이콘보다 세부 오브젝트가 많아 실제 UI 적용 시 단순화가 필요할 수 있다.

## Human Modifications

5개의 아이콘이 하나의 세트처럼 보이도록 목재 프레임, 황동 모서리, 어두운 배경, 금색 심벌을 공통 규칙으로 지정했다.

최초 이동 아이콘은 다른 아이콘과 스타일을 맞추고 기능의 가독성을 높이기 위해 다시 생성했다.

실제 프로젝트 적용 과정에서는 각 이미지를 동일한 해상도로 조정하고, 외곽 여백과 프레임 크기를 통일할 예정이다.

## Final Result

<img src="/Assets/Sprites/UI/UI_TileIcon.png">

이동, 회전, 대기, 장전, 사격으로 구성된 서부 테마의 스킬 타일 아이콘 5종을 제작했다.

각 아이콘은 서로 다른 행동을 직관적으로 표현하면서도 공통된 픽셀 아트 스타일과 프레임 디자인을 유지하도록 구성되었다.