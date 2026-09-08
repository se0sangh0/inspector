# INSPECTOR 공통 심볼 v1

> 상태: 2026-08-31 사용자 선택 방향 · 제작용 SVG 정리 완료 · 웹 파비콘 적용 · Unity 미연결

2026-09-08 확인: `site/preview/favicon.svg`는 Micro SVG와 같은 세 경로 도형을
사용하며, 랜딩 페이지가 해당 파일을 파비콘으로 읽습니다.

`V2-F · 수렴하는 경계문`을 한국어·영어·중국어 번체에 공통으로 쓰는
언어 중립 심볼로 정리한 첫 번째 제작 세트입니다.

## 제작 정보

- 안정 자산 ID: `BRAND-SYMBOL-001`
- Git 기준 경로: `art/brand/symbol-mark/v1/`
- 제작 방식: 사용자 선택 래스터를 기준으로 패스를 직접 재구성한 결정적 SVG
- 기준 PNG SHA-256: `ba8f11a402accd336a5caf4b747be8d7f74dca9d55a638462e7b183b9771827c`
- Master SHA-256: `e96639f48ba4016d8a64072f5d604104c7b86e131cb07f34dc6ac87f65ac24b0`
- Print SHA-256: `537e2223b9d19917a17f77425d444fa6250d79d7688776f08765de1fba28ef7f`
- Micro SHA-256: `2119f46e6daaf1cf0cb1754fa6351933a99abccc751630fc52f78ad5751128ec`

## 제작 파일

| 파일 | 용도 |
|---|---|
| [`svg/Inspector_Symbol_Master_v1.svg`](svg/Inspector_Symbol_Master_v1.svg) | 홈페이지, 노트 표지와 중대형 단색 인쇄 |
| [`svg/Inspector_Symbol_Print_v1.svg`](svg/Inspector_Symbol_Print_v1.svg) | 황동박, 형압과 검정 1도 인쇄 |
| [`svg/Inspector_Symbol_Micro_v1.svg`](svg/Inspector_Symbol_Micro_v1.svg) | 파비콘, 16px UI와 6mm 이하 인쇄 |
| [`source/Inspector_Symbol_SelectedReference_v1.png`](source/Inspector_Symbol_SelectedReference_v1.png) | 사용자가 선택한 V2-F 래스터 기준본 |
| [`proofs/Inspector_Symbol_Comparison_v1.png`](proofs/Inspector_Symbol_Comparison_v1.png) | 기준본과 SVG 3종 비교 |

세 SVG는 `viewBox="0 0 1000 1000"`과 `fill="currentColor"`를 사용합니다.
배경, 글자, 외곽선, 그라디언트와 필터는 넣지 않았습니다.

## 사용 규칙

- 일반 웹과 노트 외부 커버에는 `Master`를 먼저 사용합니다.
- 박·형압·검정 1도 작업에는 꼭짓점을 줄인 `Print`를 사용합니다.
- 파비콘과 6mm 이하 표식에는 틈이 가장 넓은 `Micro`를 사용합니다.
- 속지 워터마크는 SVG 형상을 바꾸지 않고 매체에서 색 농도만 조절합니다.
- 심볼 안에 게임명을 합치지 않습니다. 언어별 게임명은 별도 글자 요소로 둡니다.

## 검증 기록

- 세 SVG 모두 XML 구문 검사를 통과했습니다.
- 세 SVG는 `currentColor`만 사용하며 닫힌 패스 3개로 구성됩니다.
- `Micro`는 16×16에서 세 면과 사이 틈을 구분할 수 있습니다.
- `Print`는 300dpi 기준 6mm인 71×71에서 세 면을 구분할 수 있습니다.
- 실물 박·형압의 최소 틈은 인쇄 업체의 금형·종이·압력 조건으로 다시 교정합니다.

검증 이미지는 [`proofs/`](proofs/)에 보존합니다.
