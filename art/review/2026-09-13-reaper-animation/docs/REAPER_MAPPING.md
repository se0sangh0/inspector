# 거두는 자 — 엔진 슬롯과 새 아트 대응표

- 작성일: 2026-09-13
- 성격: **읽기 전용 조사 결과와 권장안.** 데이터·코드·클립을 바꾸지 않았다
- 확인 방법: `enemies.json` · `enemy_skills.json` · `Scarecrow.controller` ·
  `BattleManager.EnemyAction.cs` · `BattleCardSprites.cs` 정적 읽기

## 1. 지금 엔진에 있는 것

`enemy_reaper_boss` 는 **이미 게임 엔티티로 존재한다.** HP 700, 공격력 30,
`tier: Boss`, 영혼석 20 드롭. 아트만 플레이스홀더다.

- `animatorPath`: `Animators/Enemies/Scarecrow/Scarecrow`
- `spritePath`: `Characters/test_enemy_goblin` — **폴백도 테스트용이다**
  (고블린·약탈자·까마귀도 같은 값을 쓴다)
- `visualScale`: 1.25 · `flipSprite`: true

컨트롤러 상태는 `Idle`, `Attack1`, `Attack2`, `Attack3`, `Attack4` 다섯이다.

## 2. 스킬 → 애니메이션 슬롯 (현재 배선)

`enemies.json` 의 애니메이션 지정은 **3번과 4번이 교차돼 있다.**

```
attack1Anim = Attack1
attack2Anim = Attack2
attack3Anim = Attack4   ← 교차
attack4Anim = Attack3   ← 교차
```

**이름만 보고 바로잡지 말 것.** 클립 내용이 이름과 다를 수 있어 의도적 교차일
가능성이 있다. 클립 프레임을 실제로 재생해 확인하기 전에는 손대지 않는다.

호출 경로는 다음과 같다.

```
BattleManager.EnemyAction.cs:85   skillIndex = Array.IndexOf(enemy.skillIds, skill.id)
                    :90   OnSkillCast(effectType, skillIndex, skill.isRanged)
BattleCardView.cs:385-389         → BattleCardSprites.PlayAttack(cat, skillIndex)
BattleCardSprites.cs:108-118      Melee  → PlayMeleeAttackSequence(0, ...)  ← 인덱스 0 고정
                                  그 외  → TriggerAttackImmediate(skillIndex)
```

`skillIds` 순서와 각 스킬의 근접 여부를 합치면 이렇게 된다.

| # | 스킬 ID | 표시명 | isRanged | 실제 재생 슬롯 | 근거 |
|---|---|---|---|:--:|---|
| 0 | `enemy_skill_reaper_swing` | 휘두르기 | 없음(=근접) | **Attack1** | 근접은 인덱스 0 고정 |
| 1 | `enemy_skill_reaper_summon` | 까마귀 부름 | true | **Attack2** | `attack2Anim` |
| 2 | `enemy_skill_reaper_harvest` | 수확 | true | **Attack4** | `attack3Anim=Attack4` |
| 3 | `enemy_skill_reaper_teleport` | 순간이동 | true | **Attack3** | `attack4Anim=Attack3` |

스킬 내용:

- **휘두르기** — 전열 1·2번에 각 30 데미지. 광역, **유일한 근접기**
- **까마귀 부름** — `enemy_crow` 소환, 쿨다운 3턴, 원거리
- **수확** — HP 50% 이하에서 1회 강제, 아군 전체 50 데미지, 원거리
- **순간이동** — 파티 배치 순서를 역순으로 뒤집음, 원거리

## 3. 새 아트 4종과의 대응 — 권장안

지시서의 4종은 **아트 제작 목록**이고, 엔진의 5종은 **기존 슬롯 목록**이다.
서로 다른 층위이므로 "5종 대 4종 충돌"이 아니다. 슬롯에 맞춰 배치하면 된다.

| 엔진 슬롯 | 쓰는 스킬 | 권장 아트 | 적합도 근거 |
|---|---|---|---|
| `Idle` | 대기 | **idle_float** | 직접 대응 |
| `Attack1` | 휘두르기 (근접) | **attack_two_hand** | 전열 2명 동시 타격 = 크게 쓸어내는 양손 궤적과 맞는다 |
| `Attack2` | 까마귀 부름 (소환) | **gesture_point** | 빈손을 들어 부르는 동작이 소환과 맞는다 |
| `Attack4` | 수확 (전체 50) | **attack_one_hand** | 남은 공격 모션. 다만 전체 대상 원거리라 한손 근접 궤적과는 덜 맞는다 |
| `Attack3` | 순간이동 (배치 역순) | **미배정** | 대응할 새 아트가 없다 |

### 재사용 가능한 슬롯 / 추가가 필요한 슬롯

- **바로 채워지는 슬롯 3개**: `Idle`, `Attack1`, `Attack2`
- **덜 맞지만 채울 수 있는 슬롯 1개**: `Attack4`(수확).
  `attack_one_hand` 를 넣으면 동작은 나오지만 "전체 대상"이 읽히지 않는다.
  전체를 쓸어내는 큰 수확 모션을 별도로 만드는 편이 낫다
- **추가 제작이 필요한 슬롯 1개**: `Attack3`(순간이동).
  4종 목록에 대응물이 없다

즉 **4종만으로는 슬롯 하나가 빈다.** 선택지는 두 가지다.

1. `Attack3`(순간이동)용 모션을 5번째로 추가 제작한다
2. `gesture_point` 를 `Attack2` 와 `Attack3` 에 함께 쓴다 —
   소환과 순간이동이 같은 모션으로 보인다는 대가가 있다

**어느 쪽도 이 문서에서 확정하지 않는다.** 기존 스킬이나 슬롯을 지우지 않는다.

## 4. 새 아트 제작 시 반드시 지킬 엔진 제약

**`flipSprite: true` 다.** `EnemyDef.cs:16` 주석에 따르면 적의 기본 페이싱은
**좌향**이고, 이 플래그는 반대로 그려진 아트를 보정한다.
네 적(고블린·약탈자·거두는 자·까마귀)이 모두 `true` 다.

- 새 아트를 어느 방향으로 그릴지 **먼저 정해야 한다**
- 정면으로 그리면 좌우 반전 시 **낫이 반대 손으로 보인다**
- 지시서의 "캐릭터 전체를 좌우 반전하지 마"와 엔진의 반전이 충돌할 수 있다
- `visualScale: 1.25` 라 보스는 확대돼 표시된다. 디테일 손실을 감안한다

## 5. 확인하지 않은 것

- **[갱신] Unity 6000.3.9f1 배치 모드로 컨트롤러·클립을 실측했다.**
  결과는 [`UNITY_VALIDATION.md`](UNITY_VALIDATION.md) §5. 위 표의 매핑이
  **실제 스프라이트 키로 확인됐다** (`attack3Anim=Attack4` → `Scarecrow_Attack4_*`).
- 다만 **실제 게임 화면에서 네 적을 본 적은 없다.** Play Mode 미실행이다
- `Attack3`·`Attack4` 클립의 **그림**은 여전히 보지 못했다.
  3·4 교차가 의도인지는 사람이 네 클립을 봐야 판단할 수 있다
- wave-a 미연결 판단의 범위: `Assets/` 안에서 문자열 `graphics-remake` ·
  `animation-8f` · `384x512` 검색 0건, 그리고 `fellow.json`·`enemies.json` 의
  `animatorPath`·`spritePath` 값이 전부 구형 `Animators/` 와
  `Characters/test_enemy_goblin` 을 가리킨다는 두 가지다.
  **`.meta` GUID 역참조와 씬·프리팹 내부 참조는 조사하지 않았다.**
