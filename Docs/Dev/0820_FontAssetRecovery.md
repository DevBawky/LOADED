# 폰트 에셋 복구 기록

## 장애 원인

Unity 배치 실행 중 `Assets/` 내용이 비정상적으로 사라진 뒤 Git 추적 파일만
우선 복원되었다. `Assets/Package/`의 커스텀 폰트와 TMP 기본 에셋은 로컬에서
관리되는 ignored 에셋이므로 Git 이력에 없었고, 씬과 프리팹에는 GUID 참조만
남은 상태가 되었다.

이번 UI/씬 직렬화 변경은 `m_fontAsset` 값을 수정하지 않았다. 폰트 표시가
사라진 직접 원인은 참조 변경이 아니라 참조 대상 에셋과 `.meta`의 부재였다.

## 복구 내용

- 이전 `loaded-node-map-validation` 검증 복사본의 `Assets/Package/` 761개
  파일과 `Assets/Package.meta`를 원래 GUID 그대로 복원했다.
- `Fonts_Ko`, `Bold_Ko SDF`, `Galmuri9 SDF`, `LiberationSans SDF`와 원본
  TTF, 아틀라스, 머티리얼 및 TMP 의존성을 함께 복구했다.
- 씬과 프리팹의 `m_fontAsset`이 참조하는 14개 GUID가 모두 정확히 하나의
  `.meta` 파일로 해석되는지 검사한다.

## 재발 방지

- Unity 배치 실행이나 Editor builder 실행 전후에 ignored/untracked 로컬 에셋
  루트의 존재 여부, 파일 수, 대표 GUID를 비교한다.
- `git restore`는 로컬 패키지와 폰트를 복구하지 못하므로 전체 복구 수단으로
  사용하지 않는다.
- `Assets/` 또는 로컬 패키지 루트가 예상과 다르게 사라지면 즉시 Unity 실행과
  후속 builder를 중단하고, `.meta`가 보존된 백업부터 확보한다.
- 컴파일과 EditMode 테스트 외에 실제 직렬화 GUID 해석 검사를 별도로 수행한다.

## 적용 및 확인 방법

1. Unity를 열어 `Assets/Package/Fonts_Ko.asset`, `Bold_Ko SDF.asset`,
   `Galmuri9 SDF.asset`이 정상 import되는지 확인한다.
2. MainMenu, Battle, Shop, Event, Treasure 씬에서 한글 및 숫자 텍스트가 원래
   폰트와 머티리얼로 표시되는지 확인한다.
3. Console에서 TMP 폰트, 머티리얼 또는 Missing Reference 오류가 없는지 확인한다.
4. 폰트 에셋을 이동하거나 다시 생성하지 않는다. 경로 이동이 필요해도 Unity
   Editor 안에서 수행해 기존 `.meta` GUID를 유지한다.
