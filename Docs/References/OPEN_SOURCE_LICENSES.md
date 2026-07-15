# Open Source Licenses

## 작성 지침

`Packages/manifest.json`의 직접 의존성을 기준으로 작성했다. 아래 Unity 공식 패키지 외에 Git URL, 로컬 경로, 서드파티 Registry 패키지를 추가하면 저장소와 라이선스를 개별 확인해 표에 기록한다.

| 패키지 또는 라이브러리 | 버전 | 저장소 | 라이선스 | 사용 목적 |
|---|---|---|---|---|
| Unity 2D packages | manifest 참조 | Unity Registry | Unity Package Terms 확인 필요 | 2D 제작 도구 |
| Universal Render Pipeline | 17.3.0 | Unity Registry | Unity Package Terms 확인 필요 | 2D 렌더링 |
| Input System | 1.19.0 | Unity Registry | Unity Package Terms 확인 필요 | PC 및 모바일 입력 |
| Test Framework | 1.6.0 | Unity Registry | Unity Package Terms 확인 필요 | EditMode 및 PlayMode 테스트 |
| Timeline | 1.8.12 | Unity Registry | Unity Package Terms 확인 필요 | 타임라인 |
| Unity UI | 2.0.0 | Unity Registry | Unity Package Terms 확인 필요 | 사용자 인터페이스 |
| Visual Scripting | 1.9.11 | Unity Registry | Unity Package Terms 확인 필요 | 비주얼 스크립팅 |

## Unity Packages

- 확인 파일: `Packages/manifest.json`
- 확인일: 2026-07-14
- 현재 manifest에는 Git URL 또는 로컬 경로로 선언된 외부 패키지가 없다.
- `com.unity.modules.*`는 Unity Editor 기본 모듈로 구분한다.
- `com.unity.2d.*`, `com.unity.*` 직접 의존성의 실제 사용 여부와 배포 조건은 제출 전 다시 확인한다.

## Verification Template

- Package:
- Version:
- Source:
- License URL:
- Direct or Transitive:
- Used Feature:
- Verified By:
- Verification Date:
