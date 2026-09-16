# Wave A 동료·적 애니메이션 적용 기록

- 작업일: 2026-09-16
- 상태: Unity 적용 및 에디터 재생 검증 완료
- 목적: 사용자가 지정한 기존 시트로 동료 5종·적 2종의 동작을 교체한다.
- 기준: `art/production/graphics-remake/wave-a`의 캐릭터·적 시트.
- Unity: 6000.3.9f1

## 적용 결과

| 캐릭터 | 교체 클립 | Pixels Per Unit |
|---|---|---:|
| Caster | Idle, Attack, Attack2 | 210.1846 |
| Offender | Idle, Attack | 185.2729 |
| Defender | Idle, Attack | 158.0268 |
| Attacker | Idle, Attack, Attack2 | 134.1224 |
| Priest | Idle, Attack | 179.3088 |
| Goblin | Idle, Attack, Attack2 | 221.1180 |
| Raider | Idle, Attack, Attack2 | 178.7736 |

대기 7개와 공격 11개, 총 18개 클립을 연결했다.
Attack2가 있는 4종에는 Attack과 같은 공격 시트를 넣었다.
Caster·Offender는 v2 시트를 사용했다.
Defender는 Idle v1과 DeployBarrier v2를 사용했다.
Attacker·Priest·Goblin·Raider는 v1 시트를 사용했다.

## 연결 방식

- 동료 PNG: `Assets/Resources/RemakeV1/Characters/<캐릭터>/Idle.png`, `Attack.png`.
- 적 PNG: `Assets/Resources/RemakeV1/Enemies/<캐릭터>/Idle.png`, `Attack.png`.
- 기존 동료·Goblin 클립은 GUID를 유지하고 스프라이트 참조를 교체했다.
- Raider는 Wolf 컨트롤러와 클립을 복제한 전용 폴더를 사용한다.
- `enemies.json`의 약탈자 `animatorPath`를 `Animators/Enemies/Raider/Raider`로 바꿨다.
- Wolf 에셋과 나머지 적 데이터는 변경하지 않았다.

14장의 1536×512 원본 PNG를 그대로 복사했다.
384×512 칸마다 투명 여백을 제외한 영역을 슬라이스했다.
총 56개 스프라이트를 Point 필터·무압축·밉맵 없음으로 임포트했다.
각 캐릭터의 기존 Idle 높이와 발 기준선을 PPU·피벗에 반영했다.
Idle과 Attack은 같은 PPU를 사용한다.

클립 길이·루프 설정·이벤트와 기존 컨트롤러 전환값을 보존했다.
고블린·약탈자의 공격은 전환 중에도 첫 자세가 보이도록 간격을 조정했다.
마지막 키는 마지막 자세를 유지하며 기존 클립 길이를 보존한다.

## 검증 결과

- Unity 스크립트 컴파일과 어셈블리 리로드 완료, C# 오류 0건.
- 14장 모두 제작 원본과 바이트가 같다.
- 56개 슬라이스의 범위와 발 기준선 검사를 통과했다.
- 18개 클립에서 Playables로 각 프레임을 샘플링했다.
- 11개 공격 트리거 모두 네 프레임을 출력하고 Idle로 복귀했다.
- 18개 클립의 길이·이벤트·설정은 교체 전과 같다.
- 기존 15개 클립 GUID와 6개 컨트롤러를 보존했다.
- Raider 컨트롤러의 세 클립 참조와 새 에셋의 `.meta`를 확인했다.
- 적 JSON 변경은 약탈자 애니메이터 경로 한 곳뿐이다.
- `git diff --check`를 통과했다.

검증은 별도 Preview Scene에서 실행했다.
사용자가 열어 둔 씬은 저장하거나 바꾸지 않았다.
실제 전투 플레이 모드에서 카드 겹침·피격 효과·타격 시점은 확인하지 않았다.

[Unity 검증 결과](integration-report.json)에 클립별 결과가 있다.
Idle 항목의 공격 트리거 필드는 적용 대상이 아니므로 기본값이다.

## 다시 적용하는 방법

Unity의 `Tools > Graphics Remake > Apply Wave A Animations`를 실행한다.
도구는 원본 시트를 복사하고 슬라이스·클립·참조를 업데이트한다.
그 뒤 같은 검증을 실행해 이 폴더의 JSON 보고서를 갱신한다.
도구 소스는 `Assets/Editor/WaveAAnimationInstaller.cs`다.
적용 중 오류가 나면 이미 처리한 에셋은 유지되므로 보고서를 확인한다.

## 변경 이력

- 2026-09-16 · Codex: 사용자 요청에 따라 7종의 Idle·모든 Attack 클립 교체.
