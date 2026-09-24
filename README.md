# INSPECTOR · 괴이탐사국

카드로 공용 역할 스택을 만들고 영혼석 동료의 자동 행동을 유도하는
다크 호러 로그라이크 덱빌딩 오토배틀러입니다.

[![Deploy GitHub Pages](https://github.com/se0sangh0/inspector/actions/workflows/deploy-pages.yml/badge.svg)](https://github.com/se0sangh0/inspector/actions/workflows/deploy-pages.yml)

- 게임명: 한국어 `괴이탐사국` · 중국어 번체 `怪異探查局` · 영어 `INSPECTOR`
- 랜딩 페이지: [https://davi-dev.uk/](https://davi-dev.uk/)
- 저장소: [https://github.com/se0sangh0/inspector](https://github.com/se0sangh0/inspector)

## 게임 소식

- **Steam 출시 예정**입니다. 스토어 페이지는 **2026년 12월~2027년 2월**에 개설할 예정입니다.
  이 기간은 게임 출시일이 아닌 스토어 페이지 개설 계획입니다.
- **대구게임인재육성사업 예비 창업팀**으로 선정됐습니다.
- **2026 대만 이노테크 전시회 동상 및 특별상**을 수상했습니다.
- **2026 태국 발명전시회 특별상**을 수상했습니다.
  대만 이노테크 전시회에서 함께 진행된 행사입니다.

## 이야기의 바탕

한국·일본·중국·대만 등지의 민간 설화에서 모티프를 얻었습니다.
각 지역에 전해 내려오는 이야기를 괴이탐사국의 세계에서 새롭게 풀어냅니다.

> 현재 기획의 기준은 **Notion**입니다. 이 저장소의 과거 기획 문서는 2026-08-01 이전 기록을 보존하는 아카이브이며, 최신 기획으로 사용하지 않습니다.

## 처음 확인할 곳

1. [16. 프로토타입 개발 사양서](https://app.notion.com/p/3b96aef5baac81209a2dcf56163e0ea5) — 구현 범위·작업 순서·합격 기준
2. [괴이탐사국 팀 기획서](https://app.notion.com/p/3af6aef5baac800298b6c2154b9b2699) — 최신 기획 진입점
3. [기획서 안내 · 문서 지도](https://app.notion.com/p/3af6aef5baac818e95d2c2a39782a267) — 역할별 읽는 순서
4. [15. 미결 안건표](https://app.notion.com/p/3af6aef5baac8185866ff29253c2e69f) — 지금 결정할 항목
5. [HANDOFF.md](HANDOFF.md) — 현재 구현 상태와 기획 차이
6. [AGENTS.md](AGENTS.md) — 팀 공통 작업·검증 규칙
7. Unity Hub — Unity **6000.3.9f1**로 이 폴더 열기

## 정보가 관리되는 위치

| 정보 | 기준 위치 |
|---|---|
| 최신 게임 기획·미결 상태 | Notion `기획 문서`와 `15. 미결 안건표` |
| 전체 기획 문서 목록 | Notion [기획 문서 데이터베이스](https://app.notion.com/p/48e78f58eca144708c66425d28b88b10) |
| 용어·디자인·생성 이력 | Notion [용어 사전](https://app.notion.com/p/229e9a1f30df495d996a0eb52abc1525) · [디자인 가이드](https://app.notion.com/p/e749c2ad0b9a417f91ec85ce1641c73a) · [AI 디자인 생성 기록](https://app.notion.com/p/5c53f3ac5a5d47359d6c8e8c419254a1) |
| 실제 구현 | `Assets/`, `Packages/`, `ProjectSettings/`와 직접 확인한 테스트 결과 |
| 현재 구현 인수인계 | `HANDOFF.md` |
| 과거 기획·디자인 기록 | `archive/2026-08-01_Notion_이전/` |
| 비런타임 컨셉 아트 | `art/` |
| 제출·행정 자료 | 로컬 전용 `private/` · Git 추적 제외 |

## 저장소 구조

- `Assets/` — Unity 코드·씬·프리팹·게임 에셋
- `Packages/`, `ProjectSettings/` — Unity 패키지와 프로젝트 설정
- `운영/` — Git, 회의, 코드 작성 등 저장소 협업 규칙
- `회의록/`, `주간_체크리스트/` — 팀 운영 기록
- `docs/` — 구현 인수인계와 병합 기록
- `site/` — 다국어 랜딩 페이지 시안과 GitHub Pages 배포 소스
- `art/` — 브랜드·그래픽 제작 원본·컨셉 아트·박람회 원본
- `archive/` — 읽기 전용 과거 기획·디자인·인수인계 기록
- `private/` — 발표자료·지원사업 서류·폰트·비공개 참고자료 · Git 추적 제외

## 협업 원칙

- 기획을 바꾸기 전에 Notion 원문과 미결 상태를 확인합니다.
- 코드·씬·프리팹 변경은 현재 체크아웃과 직접 검증 결과로 판단합니다.
- 구조나 파일 이름을 바꾸면 README, [로컬 프로젝트 허브](00_프로젝트_허브.md), 관련 링크를 함께 점검합니다.
- 상세 규칙은 [협업 운영 규칙](운영/00_협업_운영_규칙.md)과 [Unity C# 코드 작성 규칙](운영/01_코드_작성_규칙.md)을 따릅니다.
- 사람과 자동화 작업자는 공통으로 [AGENTS.md](AGENTS.md)의 기준 문서·변경 안전·검증 원칙을 따릅니다.

## GitHub Pages

- 배포 원본: `site/preview/`
- 배포 워크플로: [`.github/workflows/deploy-pages.yml`](.github/workflows/deploy-pages.yml)
- 자동 배포: `main`의 `site/preview/**` 또는 배포 워크플로가 바뀔 때
- 수동 배포: GitHub Actions의 `Run workflow`
- 언어: 한 페이지에서 한국어·영어·중국어 번체 전환

현재 공개 페이지는 개발 중 콘셉트 비주얼을 사용하는 시안입니다. 영상과 일부
전시 정보는 정식 리소스가 준비될 때 교체합니다.

## Git 추적 범위

- 추적: `Assets/`, `Packages/`, `ProjectSettings/`, `art/` 원본, `archive/`, 팀 공통 문서와 규칙
- 비추적: `private/`, `art/_exports/`, Unity·IDE 생성 파일과 개인 도구 상태
