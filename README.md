> [!WARNING]
> 이 저장소는 심사 목적으로 공개되어 있습니다. <br>
> 오픈소스 소프트웨어가 아니며, 코드와 에셋의 복제,수정,재배포,상업적 이용 및 다른 프로젝트에서의 사용을 금지합니다. <br>
> Copyright © 2026 Bawky Studio. All rights reserved.

# LOADED

상점에서 탄환을 선택하고 강화해 덱을 구성한 뒤,<br> 전투에서 무작위로 장전되는 탄환의 조합과 발사 타이밍으로 적의 예고 공격을 돌파하는 <br> __`턴제 불릿 빌딩 로그라이크`__ 입니다.

## 플레이

- #### [LOADED WebGL 빌드](https://devbawky.github.io/LOADED/)
- #### [1분 플레이 영상](https://www.youtube.com/watch?v=UC7fXD5tF34)
- #### [저장소](https://github.com/DevBawky/LOADED)
- 플랫폼: WebGL(**`PC 권장`**), 키보드 및 마우스

## 핵심 플레이

- 이동, 방향 전환, 대기, 장전, 발사 중 하나를 선택하면 턴이 진행됩니다.
- 보유한 탄환은 무작위로 실린더에 장전되며, 탄환 효과와 발사 순서의 조합이 전투 결과를 결정합니다.
- 전투 보상과 상점을 통해 탄환을 구매하고 강화해 덱을 발전시킵니다.
- 적의 다음 행동과 예상 피해를 확인하고 장전과 발사 사이의 위험을 판단합니다.

## 조작법

| 입력 | 동작 |
| --- | --- |
| `A` / `D` | 왼쪽 / 오른쪽 이동 |
| `W` 또는 마우스 가운데 버튼 | 방향 전환 |
| `S` | 대기 |
| `R` | 탄환 장전 |
| `Space` 또는 마우스 왼쪽 버튼 | 장전된 탄환 발사 |

게임 화면의 액션 버튼으로도 조작할 수 있습니다.

## 실행 방법

- Unity: `6000.3.21f1`
- 시작 씬: `Assets/Scenes/MainMenu.unity`
- 빌드 씬: `MainMenu` → `Stage 1` → `Ending`

1. Unity Hub에서 저장소 루트를 Unity `6000.3.21f1`로 엽니다.
2. 아래 서드파티 에셋 중 코드 및 렌더링 의존성인 `Damage Numbers Pro`와 `Old Movie - Old Film Screen Effect`를 임포트합니다.
3. 원본과 동일한 사운드 구성이 필요하면 아래 오디오 에셋 3종을 추가로 임포트합니다.
4. `Assets/Scenes/MainMenu.unity`를 열고 Play Mode를 실행합니다.

`main` 브랜치의 `WebBuild/` 변경 사항은 GitHub Actions를 통해 GitHub Pages에 배포됩니다.

## 서드파티 에셋

Unity Asset Store 에셋의 원본 파일은 재배포할 수 없어 공개 저장소에서 제외했습니다. 웹 데모와 플레이 영상은 별도의 에셋 설치 없이 확인할 수 있으며, Unity 프로젝트를 직접 실행하려면 각 에셋을 정식으로 내려받아 임포트해야 합니다.

| 에셋 | 용도 | 소스 재현 시 필요 여부 |
| --- | --- | --- |
| [Damage Numbers Pro](https://assetstore.unity.com/packages/2d/gui/damage-numbers-pro-186447) | 대미지 텍스트 팝업 | 필수 |
| [Old Movie - Old Film Screen Effect](https://assetstore.unity.com/packages/vfx/shaders/fullscreen-camera-effects/old-movie-old-film-screen-effect-270021) | 필름 노이즈 및 화면 효과 | 필수 |
| [Free Deadly Kombat](https://assetstore.unity.com/packages/audio/sound-fx/free-deadly-kombat-228835) | 피격 및 근접 효과음 | 원본 사운드 재현 시 필요 |
| [WEAPON & GUN SOUND EFFECTS](https://assetstore.unity.com/packages/audio/sound-fx/weapon-gun-sound-effects-225044) | 총격 및 장전 효과음 | 원본 사운드 재현 시 필요 |
| [Gun Sounds Pack Vol 1](https://assetstore.unity.com/packages/audio/sound-fx/weapons/gun-sounds-pack-vol-1-289021) | 총격, 실린더 및 폭발 효과음 | 원본 사운드 재현 시 필요 |

해당 에셋은 각각의 제공자 라이선스와 Unity Asset Store EULA를 따릅니다.

## 저장소 구조

- `Assets/Scripts/`: 게임플레이 및 UI 코드
- `Assets/Scenes/`: 메인 메뉴, 전투, 엔딩 씬
- `Assets/Tests/`: Unity 테스트
- `Assets/StreamingAssets/`: 튜토리얼 및 연출 영상
- `Docs/Art/`: 아트 및 UI 기록
- `Docs/Dev/`: 날짜별 구현·수정 기록
- `Docs/Submission/`: 출품 및 AI 활용 문서
- `Tools/BalanceSimulation/`: 밸런스 분석 도구
- `WebBuild/`: 배포용 WebGL 빌드

## 문서

- [탄환 덱 생명주기](Docs/BulletDeckLifecycle.md)
- [첫 실행 가이드 구현 기록](Docs/Dev/0809_FirstRunGuide.md)
- [WebGL 저장 및 로딩 구현 기록](Docs/Dev/0809_WebGL_Save_And_Loading.md)
- [AI 활용 기술 문서](Docs/Submission/AI_USAGE_TECHNICAL_DOCUMENT.md)
- [개발 및 커밋 규칙](CONTRIBUTING.md)

## 기술 환경

- Unity `6000.3.21f1`
- Universal Render Pipeline 2D
- Unity Input System
- WebGL / GitHub Pages

## 라이선스

이 프로젝트는 평가 목적으로만 사용할 수 있습니다. 자세한 내용은 [LICENSE](LICENSE)를 확인하세요. 포함되거나 참조된 서드파티 패키지와 에셋에는 각각의 라이선스가 적용됩니다.
