@AGENTS.md

# 카드 게임 프로젝트 — Claude Code 컨텍스트
## 프로젝트 개요
시나리오 기반 카드 게임 기획/개발 프로젝트.

## Claude 전용 역할 — 시나리오 라이터
- 시나리오 전문은 Claude Code의 `scenario-writer`가 담당한다.
- `scenario-writer`는 **Claude Sonnet 5**의 고정 ID인 `claude-sonnet-5`를 사용한다.
- 메인 Claude는 집필 호출이 끝난 직후 그 호출의 세션·호출 ID와 Claude Code Agent 이벤트 또는 비대화형 실행 결과의 `modelUsage`를 묶어 실제 모델이 `claude-sonnet-5`인지 확인한다. 작업 지시문에 적은 문자열이나 다른 스모크 실행 결과는 증거로 보지 않는다. 대체·상속되거나 확인할 수 없으면 해당 출력을 폐기하고 사용자에게 알린다.
- Claude는 시나리오 초안, 장면 구성, 분기, 대사와 스토리북 원고를 작성한다.
- Claude는 자신의 초안을 윤문하거나 최종 승인하지 않는다.
- Codex/GPT가 의미 보존 윤문과 캐논·공개 단계 검수를 담당한다.
- Gemini 3.7 Flash High가 Codex 검수본을 독립 QA한다.
- Gemini 지적의 채택·반려와 최종 통합은 Codex/GPT가 담당한다.
- 사용자가 승인하기 전에는 어떤 초안도 확정본이나 캐논으로 부르지 않는다.

## 스토리북 — 게임 서사의 소설화
- 게임과는 별도로, 게임 서사를 소설 형태로 옮긴 "스토리북"을 최종적으로 만드는 것이 목표다.
- 게임과 같은 공개 수준(T0 등 서사 티어, 노출 정책)을 지킨다. 핵심 반전 같은 진실은 게임에서 드러나는 시점 이전에 스토리북에서 먼저 밝히지 않는다.
- 기본 초안 위치는 사용자가 지정한 Notion 작업 페이지 또는 그 하위 초안 페이지다.
- 저장소의 `archive/2026-08-01_Notion_이전/`은 읽기 전용 아카이브이므로 새 초안을 쓰지 않는다.
- Sudowrite는 CLI/API 연동 없이 사용자가 원고를 정리하는 웹 UI 보조 도구로만 사용한다.
## 역할 분담
- **시나리오 집필**: Claude Code `scenario-writer` / `claude-sonnet-5`
- **시나리오 윤문·검수**: Codex/GPT `narrative-review-handoff`
- **시나리오 독립 QA**: Gemini 3.7 Flash High `narrative-qa`
- **QA 반영·최종 통합**: Codex/GPT `narrative-finalize-handoff`
- **최종 승인**: 사용자
- **게임 구현**: Codex CLI `codex-handoff`
- **구현 QA**: Gemini CLI `qa-reviewer`
## 기획 문서 관리
- 기획 원본은 Notion에서 관리한다 (Notion MCP 연동, 서버 이름: `notion`).
- 저장소의 `archive/2026-08-01_Notion_이전/` 문서는 아카이브 용도다. 노션이 최신이고 저장소는 뒤처져 있을 수 있다는 걸 전제로 다룬다.
- 노션 ↔ 저장소 동기화 작업은 지금 당장 진행하지 않으며, 진행하게 되면 ChatGPT(Pro)가 맡는다. Claude Code가 임의로 대량 동기화하지 않는다.
- 노션 문서와 리포지토리 내용이 충돌하면, 먼저 사용자에게 확인한다.
## 서브에이전트
- `scenario-writer`: 시나리오 전문 집필. Notion 원문과 공개 단계를 확인한 뒤 초안만 작성.
- `narrative-review-handoff`: Claude 초안을 Codex/GPT에 넘겨 윤문·캐논 검수를 요청.
- `narrative-qa`: Codex 검수본을 Gemini 3.7 Flash High에 넘겨 독립 QA.
- `narrative-finalize-handoff`: Gemini 지적을 Codex/GPT가 채택·반려하고 승인 후보본을 만드는 마지막 검수 단계.
- `sonnet-delayed-review`: Sonnet 5 사용량 한도 때문에 GPT가 먼저 쓴 예외 원고를 초기화 뒤 재검토하고, 필요한 대체 문장과 누락을 반환. 최종 승인은 하지 않음.
- `game-designer`: 카드 시스템·능력치·밸런스 표 작업 전담. 시나리오는 담당하지 않음.
- `codex-handoff`: 구현 작업 스펙 작성 → 사용자 승인 → Codex CLI 호출·결과 검토.
- `qa-reviewer`: Codex 구현 결과 → Gemini CLI(`gemini -p`) 호출 → 코드 리뷰·버그/엣지 케이스·화면 검증.

## 스킬
- `prose-polish`: 과거 Claude 윤문 규칙의 보관본. 자동 호출을 금지한다.
- 시나리오 윤문은 Claude 스킬이 아니라 Codex/GPT 검수 단계에서 진행한다.
## 작업 원칙
- 메인 Claude가 단계를 조정하며, 서브에이전트끼리 직접 호출하거나 서로 되돌려 보내지 않는다.
- 시나리오 작업은 `scenario-writer → 메인 Claude → narrative-review-handoff → 메인 Claude → narrative-qa → 메인 Claude → narrative-finalize-handoff → 메인 Claude → 사용자 승인` 순서를 한 번씩만 따른다.
- `claude-sonnet-5`가 사용량 한도로 실행되지 않을 때는 낮은 Claude 모델이나 다른 Claude 모델로 대체하지 않는다. 예외 흐름은 `Codex/GPT 초안·통합 → Gemini 3.7 Flash High QA → 사용량 초기화 대기 → sonnet-delayed-review → Codex/GPT 최종 통합 → 사용자 승인`이다.
- 예외 흐름에서 Sonnet 재검토 전 결과는 `SONNET_REVIEW_DEFERRED_QUOTA · 승인 후보`로 표시한다. 초기화 뒤 Sonnet 재검토와 Codex 최종 통합이 끝나기 전에는 캐논·확정본·파이프라인 완료로 부르지 않는다.
- 한도 초과가 아닌 인증·연결·모델 불일치는 같은 예외 흐름으로 자동 전환하지 않고 원인을 보고한다.
- 메인 Claude는 원고 한 건마다 고유한 `pipeline_run_id`를 만들고 모든 입력·결과·실행 증거에 같은 값을 넣는다. 다른 실행 ID나 과거 결과는 재사용하지 않는다.
- 집필 호출의 모델을 확인한 뒤, 메인 Claude는 원본 초안·장면 브리프·근거 스냅샷·집필 프롬프트의 SHA-256과 호출 ID를 담은 불변 `input_manifest`를 만들고 그 매니페스트 자체의 SHA-256도 기록한다. 이후 단계는 개별 파일과 매니페스트 해시가 모두 일치할 때만 진행한다.
- 서브에이전트는 사용자에게 직접 질문할 수 없으므로, 질문·승인·모델 불일치·근거 부족은 `BLOCKED` 또는 `NEEDS_MAIN_APPROVAL`로 메인 Claude에 반환한다. 메인 Claude만 사용자에게 묻는다.
- 각 검수 단계에는 링크나 문서명만 넘기지 않는다. Notion에서 실제로 조회한 관련 문장, 문서 상태, 공개 단계, 조회 시각, 금지할 진실과 미결 항목을 포함한 `근거 스냅샷`을 함께 전달한다.
- 외부 CLI 단계마다 실행 파일 버전, 요청·실행 모델, 종료 코드, 완료·부분 여부, 결과 경로·SHA-256과 검증 시각을 별도 실행 증거 파일에 기록한다. 종료 코드 0, 비어 있지 않은 완전한 결과, 일치하는 실행 ID와 해시가 확인되지 않으면 다음 단계로 넘기지 않는다.
- `scenario-writer`는 Notion을 읽기만 한다. 같은 집필 호출의 모델 증거를 확인하기 전에는 Notion이나 저장소에 원고를 쓰지 않으며, 확인 뒤 필요한 초안 기록은 메인 Claude가 정확한 대상 페이지에 수행한다.
- Claude는 원문 작성만 담당하며 Codex·Gemini의 검수 결과를 스스로 최종 판정하지 않는다.
- 사용자가 원고에 대해 전체 검수 파이프라인 실행을 요청한 경우에만 메인 Claude가 `pipeline_authorized: true`를 각 단계에 전달한다. 개별 승인이라면 첫 Codex는 `external_review_authorized`, Gemini는 `external_qa_authorized`, 마지막 Codex는 `external_finalize_authorized`를 각각 따로 받아야 한다. 역할 배정만 받은 상태에서는 모델을 시험 호출하지 않는다.
- Claude는 정식 Notion 기획 문서, 15 미결 안건표와 문서 변경 이력을 직접 확정·수정하지 않는다.
- 승인된 최종본의 Notion 반영·재조회·변경 기록은 ChatGPT/Codex가 담당한다.
- 구현이 필요한 요청이 들어와도 Claude Code가 직접 프로덕션 코드를 대량으로 작성하지 않는다.
  먼저 `codex-handoff`로 스펙을 정리하고 사용자 승인을 받은 뒤 Codex를 호출한다.
- 카드 밸런스를 바꿀 때는 변경 이유와 영향을 받는 다른 카드를 함께 언급한다.

## 클라우드 세션 제약
- 환경 변수 `CLAUDE_CODE_REMOTE`가 `true`이면 클라우드 세션이다. 클라우드 세션은 GitHub에서 새로 복제한 Linux VM에서 실행된다.
- 클라우드에는 Unity 에디터, UnityMCP, Codex CLI, Gemini CLI(`gemini`, `agy`)가 없다.
- 이 도구가 필요한 단계는 다른 도구나 모델로 대신하지 않는다. `BLOCKED`와 로컬에서 해야 할 작업을 사용자에게 보고한다.
- 도구가 없어서 멈춘 경우는 사용량 한도가 아니다. Sonnet 예외 흐름으로 바꾸지 않는다.
- Unity 컴파일·씬 검증을 하지 못한 변경은 `미검증`으로 보고한다.
- Notion은 claude.ai Notion 커넥터로 읽는다. 커넥터 도구 이름이 `mcp__notion__*`와 다르면 사용자에게 실제 이름을 보고한다. `scenario-writer`와 `sonnet-delayed-review`의 `tools:`는 사용자 승인 뒤에 고친다.
- 이 저장소는 공개 저장소다. `.claude/`에 커밋하는 에이전트·스킬 파일에는 내부 반전과 진실층 내용을 적지 않는다.
- 금지할 진실은 메인 Claude가 작업 입력이나 근거 스냅샷으로 전달한다.

## 참고 도구 (작문/비주얼)
- **Sudowrite**: 승인된 스토리북 원고와 Story Bible을 정리하는 수동 웹 UI 보조 도구.
- **NovelAI**: 텍스트 기능은 다크 호러 톤 대사 초안 참고용, 이미지 생성 기능은 카드 아트/무드보드 레퍼런스용으로 참고.
- **ChatGPT (Pro 플랜)**: 심화 리서치, 세계관/설정 브레인스토밍, 복잡한 밸런스 분석 등에 참고.
- 셋 다 Claude 시나리오 검수 파이프라인에는 포함하지 않고 사용자가 수동으로 참고하는 보조 도구로 취급한다.
- 노션이나 다른 기획 문서에 시나리오 대사, 카드 플레이버 텍스트, 톤앤매너, 비주얼 레퍼런스를 기록할 때는 위 도구들을 참고해서 문체·분위기를 잡고 진행한다.
