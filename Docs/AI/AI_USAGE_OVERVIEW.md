# LOADED AI Usage Overview

## Purpose

LOADED 개발에서 AI는 기획을 대신 결정하는 도구가 아니라, 설계 검토, 구현 보조, 테스트 도출, 분석 도구 제작, 반복 작업 자동화에 사용한다.

## AI Usage Principles

1. 핵심 게임 규칙과 최종 의사결정은 개발자가 수행한다.
2. AI 출력은 코드 리뷰와 테스트 후 반영한다.
3. AI가 생성한 수치와 밸런스를 정답으로 간주하지 않는다.
4. 주요 프롬프트와 수정 지시를 기록한다.
5. 채택하지 않은 결과와 실패 사례도 기록한다.
6. 외부 에셋과 생성형 에셋의 출처를 기록한다.
7. 개인정보, API 키, 비공개 인증 정보를 입력하지 않는다.

## Tools

| 도구 | 모델 또는 버전 | 용도 | 사용 범위 | 비고 |
|---|---|---|---|---|
| Codex | 기록 필요 | Unity 코드 구현 및 리팩터링 | 게임 시스템 | |
| ChatGPT | 기록 필요 | 설계 검토, QA, 문서화 | 기획 및 검증 | |
| 기타 도구 | 기록 필요 | 아트 또는 사운드 | 에셋 | |

## Main AI Use Cases

1. 핵심 전투 구조 설계
2. 턴 처리 순서 검토
3. 탄환 덱 구현
4. 탄환 효과 구조 설계
5. 적 행동 시스템 구현
6. 보스 패턴 설계 검토
7. 밸런스 시뮬레이션 도구 제작
8. 테스트 케이스 및 엣지 케이스 도출
9. Web 빌드 오류 분석

## Validation Methods

- 컴파일 확인
- 코드 리뷰
- EditMode 테스트
- PlayMode 테스트
- 고정 시드 재현 테스트
- 수동 플레이 테스트
- PC 및 모바일 웹 테스트
- 시뮬레이션 결과와 실제 플레이 결과 비교

## Human Decisions

아래 항목은 최종적으로 개발자가 결정한다.

- 게임 핵심 규칙
- 탄환 효과
- 적 구성
- 보스 구조
- 밸런스 수치
- AI 출력 채택 여부
- 에셋 최종 사용 여부

## Related Documents

- [AI Usage Log](AI_USAGE_LOG.md)
- [Prompt Index](PROMPT_INDEX.md)
- [AI Validation](AI_VALIDATION.md)
- [AI Asset Log](AI_ASSET_LOG.md)
