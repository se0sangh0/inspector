# Unity 실행 검증 — 2026-09-13

이전 문서의 "이 머신에 Unity 가 없어 미실행"은 **과거 관찰**이다.
사용자가 외장 볼륨의 에디터 경로를 알려준 뒤 실제로 실행해 검증했다.

## 1. 환경

| 항목 | 값 |
|---|---|
| 에디터 | `/Volumes/KIOXIA/01_Game/02.Editor/6000.3.9f1/Unity.app` |
| 실행 파일 | 위 경로의 `Contents/MacOS/Unity` |
| 버전 | **6000.3.9f1** (`Application.unityVersion` 으로 실행 중 확인) |
| 원본 기준 | 저장소 `HEAD = 6d10138`, 미추적 `art/review/2026-09-13-reaper-animation/` 포함 |
| `ProjectVersion.txt` | `6000.3.9f1 (7a9955a4f2fa)` — 사본도 동일. **6000.4.8f1 로 올리지 않았다** |
| 검증 사본 | `/Volumes/KIOXIA/01_Game/01.Project/inspector-unity-validation-2026-09-13` |

사본은 `Assets` · `Packages` · `ProjectSettings` · `UserSettings` 만 복사했다.
`Library` · `Temp` · `Logs` 는 복사하지 않았다.
**`.meta` 2181개가 원본과 같은 개수로 보존됐다.**

기존 `01.Project` 의 `FPS_sample` · `2405011` · `Shooting` 은 열지도 바꾸지도 않았다.

## 2. MCP 연결 — **연결하지 못했다**

지시대로 설정 파일만 보고 단정하지 않고 **실제 활성 도구 목록을 조회**했다.

| 확인 | 결과 |
|---|---|
| 내 활성 MCP 서버 | `notion` 하나뿐. Unity MCP 없음 |
| 도구 검색(`unity editor mcp play mode console scene`) | Unity 관련 도구 0건 |
| 저장소 `.mcp.json` | `{"mcpServers": {}}` — 비어 있음 |
| `Packages/manifest.json` | MCP 의존성 없음 |
| 다른 프로젝트(FPS_sample·2405011·Shooting) | 재사용할 Unity MCP 설치본 없음 |

**설치하지 않은 이유:** MCP for Unity(CoplayDev)는 ① Unity 에디터 패키지와
② Claude Code MCP 클라이언트 등록의 **두 부분**이 모두 필요하다.
②는 이 세션의 MCP 클라이언트 구성을 바꾸는 일이며, 새 서버는 세션 시작 시점에
연결되므로 **지금 세션 안에서 살아 있는 연결을 만들 수 없다.**
전역 설정을 바꾸지 말라는 지시도 있어 클라이언트 등록을 시도하지 않았다.

**대신 배치 모드로 동등한 검증을 수행했다.** MCP 로만 가능한 것(에디터 UI 조작,
실시간 콘솔 스트리밍)은 아래 §7 에 미확인으로 남겼다.

## 3. 컴파일 — **성공**

```
Unity -batchmode -quit -nographics -accept-apiupdate -projectPath <사본> -logFile ...
→ "Exiting batchmode successfully now!"
```

| 항목 | 결과 |
|---|---|
| 라이선스 | `Successfully resolved entitlement details` |
| 에셋 임포트 | 완료 (첫 임포트라 Library 신규 생성) |
| **컴파일 오류** | **0개** |
| 컴파일 경고 | **46개 — 전부 `CS0618` 하나** |

경고는 전부 `TMP_Text.enableWordWrapping is obsolete` 다.
`MagicStoneShopPanel`·`EventPanel`·`NodeSystem`·`BattleCardView` 등 12개 이상
파일에 걸쳐 있고 **전부 기존 소스**다. 이번 검증용 스크립트에서 생긴 것은 없다.
목록: [`unity-validation/compile_warnings.txt`](unity-validation/compile_warnings.txt)

## 4. 자동 테스트 — **실행할 테스트가 없다**

`Packages/manifest.json` 에 `com.unity.test-framework: 1.6.0` 이 있지만
**`.asmdef` 도 테스트 파일도 하나도 없다.** Test Runner 로 실행할 대상이 없어
EditMode·PlayMode 테스트를 **실행하지 않았다.** 결과 XML 도 없다.

## 5. 거두는 자 — 슬롯·클립·매핑 실측

배치 모드에서 `AnimatorController` 와 `AnimationClip` 을 직접 읽었다.
전문: [`unity-validation/validation_report.md`](unity-validation/validation_report.md)

### 컨트롤러 구조 (실측)

파라미터: `Attack1` `Attack2` `Attack3` `Attack4` — 전부 Trigger.

| 상태 | 클립 길이 | 실제 스프라이트 키 |
|---|---|---|
| Idle | 0.517s @60fps | `Scarecrow_Idle_0,2,3,4,5,6` (6키) |
| Attack1 | 0.517s | `Scarecrow_Attack_0~3` |
| Attack2 | 0.517s | `Scarecrow_Attack2_0~3` |
| Attack3 | 0.517s | `Scarecrow_Attack3_0,2,3,10` |
| Attack4 | 0.517s | `Scarecrow_Attack4_0~3` |

전이는 `Idle → AttackN` (조건 `If AttackN`, exitTime 없음),
`AttackN → Idle` (조건 없음, **exitTime 0.52**). 즉 **공격 종료 후 Idle 복귀가
컨트롤러에 이미 배선돼 있다.**

### 3↔4 교차는 실재하며 클립 내용으로 확인됐다

| JSON 키 | 값 | 실제 재생 스프라이트 |
|---|---|---|
| `attack1Anim` | Attack1 | `Scarecrow_Attack_*` |
| `attack2Anim` | Attack2 | `Scarecrow_Attack2_*` |
| **`attack3Anim`** | **Attack4** | **`Scarecrow_Attack4_*`** |
| **`attack4Anim`** | **Attack3** | **`Scarecrow_Attack3_*`** |

**이름만 보고 고치지 않았다.** 텍스처 크기가 단서를 준다:
`Scarecrow_Attack.png`(498×501)와 `Scarecrow_Attack4.png`(498×501)가 한 쌍,
`Scarecrow_Attack2.png`·`Scarecrow_Attack3.png`(409×610)가 다른 쌍이다.
**교차가 의도적일 가능성을 배제할 수 없다.** 판단에는 클립 4종의 실제 그림을
사람이 봐야 한다.

관찰 1건: `Attack3` 클립만 스프라이트 인덱스가 `0, 2, 3, 10` 으로 불규칙하다.
나머지는 `0~3` 연속이다. 의도인지 슬라이싱 잔재인지 **미확인**이다.

### 스킬 → 트리거 경로 (코드 실측)

`BattleCardSprites.cs:124-128` 의 `TriggerAttackImmediate` 는
`skillIndex` 부터 **0까지 내려가며** 첫 사용 가능한 트리거를 쏜다.
근접은 `PlayMeleeAttackSequence(0, ...)` 로 **인덱스 0 고정**이다.

| # | 스킬 | isRanged | 실제 트리거 | 재생 클립 |
|---|---|:--:|---|---|
| 0 | 휘두르기 (전열 2명 30) | 없음 = 근접 | Attack1 | `Scarecrow_Attack_*` |
| 1 | 까마귀 부름 (소환) | true | Attack2 | `Scarecrow_Attack2_*` |
| 2 | 수확 (전체 50) | true | Attack4 | `Scarecrow_Attack4_*` |
| 3 | 순간이동 (배치 역순) | true | Attack3 | `Scarecrow_Attack3_*` |

**효과 발생 시점과 모션 재생은 분리돼 있다.** `BattleManager.EnemyAction.cs:90`
이 `OnSkillCast` 로 모션을 요청하고, 효과는 같은 흐름의 별도 단계에서 적용된다.
**Play Mode 로 타이밍을 눈으로 확인하지는 않았다** (§7).

## 6. Attacker · Crow · Scarecrow · Wolf — 엔진 내 실측

| 자산 | 클립 | 스프라이트 키 | **null 참조** | 텍스처 |
|---|---|---:|:--:|---|
| Attacker | Idle, Attack, Attack2 | 10 | **0** | 677×369 ×3 |
| Crow | Idle | 4 | **0** | 1062×235 |
| Scarecrow | Idle, Attack1~4 | 22 | **0** | 409×610 ×3, 498×501 ×2 |
| Wolf | Idle, Attack, Attack2 | 12 | **0** | 481×519 ×3 |

**누락 참조 0개.** 네 자산 모두 스프라이트가 온전히 연결돼 있다.

### 씬·프리팹 의존성 (GUID 기준)

씬 7개, 프리팹 20개의 의존성을 `AssetDatabase.GetDependencies` 로 조사했다.
`Resources/Animators` 자산 58개가 참조되는데 **참조원은 `AnimationTest.unity`
하나뿐**이다. 게임플레이 씬은 이들을 씬 참조로 갖지 않고, 런타임에
`Resources.Load(animatorPath)` 로 불러온다(`DefaultSetting.cs:266-297`).

**정상 게임 흐름에서의 출현은 확인하지 않았다** — 테스트 씬 의존성 조사와
실제 전투 진입은 다른 검증이다.

## 7. wave-a 12시트 — 엔진 임포트·슬라이싱·전환 실측

12시트를 **검증 사본에만** 복사해 임포트했다. 원 저장소의 연결이나 승인 상태는
바꾸지 않았다. 전문: [`unity-validation/wavea_report.md`](unity-validation/wavea_report.md)

### 임포트·슬라이싱 — 12/12 정상

- 12시트 모두 1536×1024, Multiple 모드, 4×2 = **8슬라이스**
- 셀 원점이 12시트 모두 동일: `0,512 | 384,512 | 768,512 | 1152,512 | 0,0 | …`
  (Unity 텍스처는 좌하단 원점이라 윗줄이 y=512)
- 피벗 `(0.50, 0.02)` — **전 프레임 동일**. 기존 파이프라인의 발 기준선 y=500 을
  정규화한 값 `(512-500)/512 = 0.0234`
- Point 필터·압축 없음으로 설정해 픽셀 흐림을 배제

### 공격 종료 → 대기 시작 경계 (불투명 영역 실측)

각 캐릭터의 공격 마지막 프레임과 대기 첫 프레임의 불투명 경계를 견줬다.

| 캐릭터 | 폭 Δ | 높이 Δ | 좌 Δ | 우 Δ | 상 Δ | **하 Δ** |
|---|---:|---:|---:|---:|---:|---:|
| **offender** | **+50** | **+56** | −11 | **+39** | +56 | **0** |
| caster | +53 | +34 | −22 | +31 | +34 | **0** |
| goblin | +30 | +26 | −16 | +14 | +26 | **0** |
| raider | +3 | +38 | −2 | +1 | +38 | **0** |
| priest | +10 | −2 | −1 | +9 | −2 | **0** |
| defender | −5 | −9 | +2 | −3 | −9 | **0** |

**하단 Δ 가 6종 모두 0px 다.** 발 기준선 정렬이 공격→대기 경계에서 정확히
유지된다. 캐릭터가 위아래로 튀지 않는다는 뜻이다.

**offender 의 우측 Δ+39px 는 이전 정적 측정값과 정확히 일치한다.**
엔진이 같은 값을 독립적으로 재현했다. 다만 이는 **자세 차이**이지 위치 이동이
아니다. offender 와 caster 가 경계 변화가 가장 크고, defender 가 가장 작다.

**"실제로 튀는지"는 아직 답이 아니다.** wave-a 에는 컨트롤러도 전이 설정도
없다. 튀는 정도는 전이의 블렌드 시간에 좌우되며, 그 설정이 아직 존재하지 않는다.
기존 Scarecrow 컨트롤러의 `exitTime 0.52` 는 **구형 자산의 설정**이지
wave-a 의 설정이 아니다.

## 8. 이번 Unity 검증에서 확인하지 못한 것

- **Play Mode 진입·종료** — 배치 모드로만 검증했다. Play Mode 콘솔과 진입 후
  참조 상태는 확인하지 않았다
- **에디터 UI 에서의 실제 재생** — MCP 미연결이라 클립을 눈으로 재생하지 못했다.
  스프라이트 키 목록과 길이는 읽었지만 **그림을 본 것은 아니다**
- **Scarecrow Attack1~4 클립의 실제 그림** — 3↔4 교차가 의도인지 판단하려면
  사람이 네 클립을 봐야 한다
- **정상 게임 흐름에서의 4종 출현** — 전투 진입·스킬 발동을 실행하지 않았다
- **효과 발생 시점과 모션의 동기** — 코드 경로는 읽었으나 실행 관찰은 없다
- **자동 테스트** — 실행할 테스트가 존재하지 않는다
- **`Attack3` 클립의 불규칙 인덱스**(0,2,3,10) 원인

## 9. 발견한 게임 소스 결함

**이번 검증에서 새로 발견한 소스 결함은 없다.** 컴파일 오류 0, 누락 참조 0,
컨트롤러 배선 정상이다.

`CS0618` 경고 46건은 결함이 아니라 **구형 TMP API 사용**이다.
최소 수정은 `enableWordWrapping = x` → `textWrappingMode = x ? TextWrappingModes.Normal : TextWrappingModes.NoWrap` 이다.
**이번에 원 저장소를 고치지 않았다.**

## 10. 변경 범위 최종 확인

| 대상 | 상태 |
|---|---|
| 원 저장소 `Assets` · `Packages` · `ProjectSettings` · 게임 코드 · 원본 이미지 | **무변경** |
| 원 저장소 변경 | `art/review/2026-09-13-reaper-animation/` 안의 문서·증거 파일뿐 |
| 검증 사본 | 임시 에디터 스크립트 2개(`Assets/Editor/_Validation/`)와 wave-a 시트 12장(`Assets/_WaveAValidation/`) 추가. **사본에만 존재한다** |
| Git | commit·push 하지 않았다 |
| 버전 | 업그레이드하지 않았다 |
| 새 아트 | 생성하지 않았다 |
