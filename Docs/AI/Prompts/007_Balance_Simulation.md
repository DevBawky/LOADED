# P007 Balance Simulation

## Metadata

- Status: Initial
- Related AI Usage ID: 미사용

## Initial Prompt

```text
LOADED의 탄환 덱과 탄창 콤보 밸런스를 분석하는 시뮬레이션 도구를 설계한다.

기본 조건:
- 덱 크기, 탄창 크기, 탄환 종류별 장수 설정
- 셔플 덱과 버린 더미 재셔플 규칙
- 고정 시드 지원
- 최소 100,000회 반복
- 탄환 효과를 단순 모델로 교체 가능
- CSV 또는 JSON 출력

지표:
- 탄환별 등장 빈도와 순서 조합 빈도
- 평균 탄창 길이, 평균 및 최대 발사 피해
- 무효 탄환 비율과 특정 콤보 발생 확률
- 덱 크기별 변화
- 탄환 제거와 복제의 영향

시뮬레이션 로직과 Unity 런타임 코드를 분리하고, 확률 계산과 피해 계산을 분리한다. 결과는 재현 가능해야 하며 그래프 라이브러리를 필수 의존성으로 추가하지 않는다. 먼저 데이터 모델, 실행 흐름, 출력 형식, 검증 방법을 제안하라.
```

## Usage Notes

- Date:
- Tool and Model:
- Output Summary:
- Validation:
