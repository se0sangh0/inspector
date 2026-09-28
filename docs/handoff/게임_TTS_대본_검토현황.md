# 게임 TTS 대본 검토 현황

- 2026-09-27 현재: 정적 문안 87개 중 Claude 수정·원문 대조 검토 69개 완료, 18개 미완료.
- Gemini 3.8 문단별 오타 검사 69개 완료. 이 중 띄어쓰기 지적 1건은 국립국어원 근거 대조로 원문을 유지했다.
- Codex 의미 확인 68개 완료. `evt_unfamiliar_log_choice_0`은 같은 사건 본문의 시간 맥락을 추가해 Claude가 읽기 지시를 다시 확인해야 한다.
- Algenib WAV 46개 제작·Unity 연결, 41개 미제작. 완성 WAV도 전체 발음 청취 검수는 미완료다.
- Claude Opus 5.5·추론 최대 사용. 이전 세션 초기화 시각은 지났지만, 2026-09-28 현재 Claude Code 조직 구독 접근 차단과 웹 무료 계정의 Opus 사용 제한으로 중단됐다.
- AI Studio 웹 TTS도 사용량 한도에 도달했다. 초기화 시각은 화면에 나오지 않았다.
- 아래는 낭독 대본이다. 게임 화면 원문과 번역 키는 별도로 유지한다.
- 영어 연출은 대본과 분리해서 AI Studio Style 입력란에 넣는다.

## 의미 확인이 끝난 낭독 대본

### opening_01

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
이전 탐사 결과
이전 탐사대와 연락이 두절되었다.
해당 임무는 실패로 종결한다.
```

연출: Calm, flat official-report tone. Read the heading as a title and pause briefly. Deliver both lines evenly, and let the last line settle without added drama.

<details><summary>수정 전 원문</summary>

```text
이전 탐사 결과
이전 탐사대와의 연락이 두절되었다.
해당 임무는 실패로 종결한다.
```

</details>

### opening_02

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
후속 투입 통보
후속 투입 절차가 승인되었다.
교전 수칙을 숙지한 뒤 즉시 현장으로 이동하라.
```

연출: Composed, clipped briefing delivery. Read the heading as a title, then pause briefly. Keep an even pace and flat authority with understated tension. No dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
후속 투입 통보
후속 투입 절차가 승인되었다.
교전 수칙 숙지 후 즉시 현장으로 이동하라.
```

</details>

### combat_guide

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
전투 안내.
첫째, 손패에서 카드를 클릭해 공용 스택을 쌓으세요.
둘째, '턴 종료'로 이번 턴을 확정합니다.
셋째, 쌓인 스택에 맞춰 동료가 자동으로 행동합니다.
```

연출: Calm, steady bureau-manual delivery with no dramatic coloring. Brief pause after the title and between the three steps; lightly set off the quoted label in step two.

<details><summary>수정 전 원문</summary>

```text
전투 안내
① 손패의 카드를 클릭해 공용 스택을 쌓으세요.
② [턴 종료]로 이번 턴을 확정합니다.
③ 동료가 쌓인 스택에 맞춰 자동으로 행동합니다.
```

</details>

### route_R02-A

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
발자국이 남은 숲길
낮은 수풀 사이로 좁은 길이 이어진다.
잘린 덩굴과 뒤엉킨 발자국이 보인다.
```

연출: Calm, low field-report delivery with restrained unease. Short pause after the title line. Read the last line as one even phrase with no internal pause or added stress. Do not break after '덩굴과' or after '뒤엉킨', so the line's word grouping stays as open as on the page. No dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
발자국이 남은 숲길
낮은 수풀 사이로 좁은 길이 이어진다.
잘린 덩굴과 뒤엉킨 발자국이 보인다.
```

</details>

### route_R02-A_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
발자국이 남은 숲길
낮은 수풀 사이로 좁은 길이 이어진다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, level bureau-report tone with restrained tension. Read the title plainly and pause. Keep the path line even, give the short movement fragment a brief beat, and state the final warning as a measured caution without extra alarm.

<details><summary>수정 전 원문</summary>

```text
발자국이 남은 숲길
낮은 수풀 사이로 좁은 길이 이어진다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_R02-B

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
천막의 흔적
나무 사이에 덧댄 천이 보인다.
낮은 목소리와 금속 마찰음이 들린다.
```

연출: Measured, understated field-report delivery. Short pause after the title. Read the last line evenly and a little lower, without dramatizing.

<details><summary>수정 전 원문</summary>

```text
천막의 흔적
나무 사이에 덧댄 천이 보인다.
낮은 목소리와 금속 마찰음이 들린다.
```

</details>

### route_R02-B_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
천막의 흔적
나무 사이에 덧댄 천이 보인다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, clipped field-report tone with restrained unease. Short pause after the title. Read the final line plainly: state the dangerous movement ahead as written, without adding doubt, and keep the hedge only on the consequence—serious harm upon entering is a possibility, not a certainty.

<details><summary>수정 전 원문</summary>

```text
천막의 흔적
나무 사이에 덧댄 천이 보인다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_R02-C

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
젖은 숲 가장자리
물먹은 나무뿌리가 길을 좁힌다.
수풀 안쪽에서 짧은 움직임이 반복된다.
```

연출: Low, even field-report delivery. Read the title plainly, with a brief pause after it and between lines. Keep the last line quiet and matter-of-fact; do not dramatize the movement.

<details><summary>수정 전 원문</summary>

```text
젖은 숲 가장자리
물먹은 나무뿌리가 길을 좁힌다.
수풀 안쪽에서 짧은 움직임이 반복된다.
```

</details>

### route_R02-C_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
젖은 숲 가장자리
물먹은 나무뿌리가 길을 좁힌다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Low, even field-report delivery. Brief pause after the title and between lines. Read the warning plainly, without dramatizing.

<details><summary>수정 전 원문</summary>

```text
젖은 숲 가장자리
물먹은 나무뿌리가 길을 좁힌다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_R03-A

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
수레가 선 길
협곡 그늘에 짐수레와 천막이 모여 있다.
여러 사람의 시선이 길목에 머문다.
```

연출: Calm, measured field-report tone. Pause briefly after the title. Read the final line quietly and evenly, without adding menace.

<details><summary>수정 전 원문</summary>

```text
수레가 선 길
협곡 그늘에 짐수레와 천막이 모여 있다.
여러 사람의 시선이 길목에 머문다.
```

</details>

### route_R03-A_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
수레가 선 길
협곡 그늘에 짐수레와 천막이 모여 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, measured bureau-report delivery. Brief pause after the title. Read the second line evenly; give the warning clipped and flat, without dramatizing.

<details><summary>수정 전 원문</summary>

```text
수레가 선 길
협곡 그늘에 짐수레와 천막이 모여 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_R03-B

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
흰 천 표식
바위틈마다 바랜 천 조각이 묶여 있다.
약 냄새와 낮은 기도 소리가 섞여 온다.
```

연출: Measured, restrained report tone. Pause briefly after the title. Read the last line a little softer and slower, without dramatizing or imitating the prayer.

<details><summary>수정 전 원문</summary>

```text
흰 천 표식
바위 틈마다 바랜 천 조각이 묶여 있다.
약 냄새와 낮은 기도 소리가 섞여 온다.
```

</details>

### route_R03-B_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
흰 천 표식
바위틈마다 바랜 천 조각이 묶여 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, measured field-report delivery with restrained unease. Read the title as a label, then pause briefly. Keep the cloth line quiet and even. State the warning plainly and clearly, without dramatizing or softening it.

<details><summary>수정 전 원문</summary>

```text
흰 천 표식
바위 틈마다 바랜 천 조각이 묶여 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_R03-C

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
갈라진 비탈
갈라진 비탈 아래에 임시 천막이 이어진다.
금속을 손질하는 소리가 비탈 아래에서 들린다.
```

연출: Measured, understated bureau-report tone. Brief pause after the title and between lines. Read the last line evenly, without dramatizing the sound.

<details><summary>수정 전 원문</summary>

```text
갈라진 비탈
갈라진 비탈 아래에 임시 천막이 이어진다.
금속을 손질하는 소리가 비탈 아래에서 들린다.
```

</details>

### route_R03-C_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
갈라진 비탈
갈라진 비탈 아래에 임시 천막이 이어진다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Even, restrained field-report tone. Brief pause after the title; when the same words open the next line, don't stress them. Deliver the warning clearly and steadily, without dramatizing.

<details><summary>수정 전 원문</summary>

```text
갈라진 비탈
갈라진 비탈 아래에 임시 천막이 이어진다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_FARM-A

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
왼편 밭가
마른 줄기로 빽빽한 밭 가장자리가 왼쪽으로 길게 휘어 있다.
헤집어진 흙 위로 발자국이 이어지고, 앞쪽 줄기들이 바스락거린다.
```

연출: Calm, restrained report tone. Short pause after the title and at the comma; keep the final clause low and unhurried.

<details><summary>수정 전 원문</summary>

```text
왼편 밭가
마른 줄기 빽빽한 밭 가장자리가 왼쪽으로 길게 휘어 있다.
헤집어진 흙 위로 발자국이 이어지고, 앞쪽 줄기들이 바스락거린다.
```

</details>

### route_FARM-A_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
왼편 밭가
마른 줄기로 빽빽한 밭 가장자리가 왼쪽으로 길게 휘어 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, restrained field-report tone. Brief pause after the title. Read the warning plainly and evenly, without dramatizing.

<details><summary>수정 전 원문</summary>

```text
왼편 밭가
마른 줄기 빽빽한 밭 가장자리가 왼쪽으로 길게 휘어 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_FARM-B

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
마른 고랑 사잇길
갈라진 고랑이 곧게 뻗어 있고, 양옆으로는 시든 줄기가 빽빽하다.
고랑 흙이 갓 헤집어져 있고, 앞쪽 어딘가에서 마른 줄기가 사각거린다.
```

연출: Read the title plainly, then pause briefly. Use a calm, even report tone for the terrain. Slow slightly on the last line and take a short breath at the comma. Keep the rustle understated, without implying its source.

<details><summary>수정 전 원문</summary>

```text
마른 고랑 사잇길
갈라진 고랑이 곧게 뻗어 있고, 양옆으로 시든 줄기가 빽빽하다.
고랑 흙이 갓 헤집어져 있고, 앞쪽 어딘가에서 마른 줄기가 사각거린다.
```

</details>

### route_FARM-B_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
마른 고랑 사잇길
갈라진 고랑이 곧게 뻗어 있고, 양옆에는 시든 줄기가 빽빽하다.
전방에 위험한 움직임이 있다. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, restrained field-report delivery. Pause briefly after the title and before the warning. Read the warning plainly and evenly, without dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
마른 고랑 사잇길
갈라진 고랑이 곧게 뻗어 있고, 양옆으로 시든 줄기가 빽빽하다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### route_FARM-C

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
오른편 밭고랑
낮은 둑을 넘어 오른쪽으로, 마른 줄기가 빽빽이 서 있다.
줄기가 서걱이고, 헤집어진 흙에 발자국이 찍혀 있다.
```

연출: Calm, low field-report tone. Brief pause after the title and between lines. Keep the last line even and understated. Do not suggest what caused the rustling or whose footprints they are.

<details><summary>수정 전 원문</summary>

```text
오른편 밭고랑
낮은 둑을 넘어 오른쪽, 마른 줄기가 빽빽이 서 있다.
줄기가 서걱이고, 헤집어진 흙에 발자국이 찍혀 있다.
```

</details>

### route_FARM-C_warning

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
오른편 밭고랑
낮은 둑을 넘어 오른쪽, 마른 줄기가 빽빽이 서 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

연출: Calm, measured field-report tone. Pause briefly after the title. Read the warning evenly, without dramatizing.

<details><summary>수정 전 원문</summary>

```text
오른편 밭고랑
낮은 둑을 넘어 오른쪽, 마른 줄기가 빽빽이 서 있다.
전방에 위험한 움직임. 들어서면 심각한 피해를 입을 수 있다.
```

</details>

### observation_OBS_TORN_BASKET

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
찢어진 바구니
전투가 끝난 자리에서 찢어진 바구니가 발견된다. 안에 담겨 있던 평범한 약초와 열매, 땔감이 쏟아져 있다.
```

연출: Calm, low field-report delivery. Brief pause after the title. Read the ordinary items plainly and let the understatement carry the unease.

<details><summary>수정 전 원문</summary>

```text
찢어진 바구니
전투가 끝난 자리에서 찢어진 바구니가 발견된다. 안쪽에서는 평범한 약초와 열매, 땔감이 쏟아져 있다.
```

</details>

### observation_OBS_FOREST_BARRICADE

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
숲을 향한 목책
전투가 끝난 자리에서, 숲을 향해 세워진 목책이 확인된다. 목책 위쪽 끝에는 흰 천 조각이 묶여 있다.
```

연출: Calm, measured field-report tone. Brief pause after the title and at the comma. Deliver the white-cloth line plainly, without implying what it means.

<details><summary>수정 전 원문</summary>

```text
숲을 향한 목책
전투가 끝난 자리에서 숲을 향해 세워진 목책이 확인된다. 목책 위쪽 끝에는 흰 천 조각이 묶여 있다.
```

</details>

### evt_cold_camp_intro

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
식어 있는 야영지
협곡 입구에 버려진 야영지가 있다. 불 꺼진 화덕의 재 밑이 아직 따뜻하고, 말리다 만 약초 다발과 흙을 털지 않은 도구가 흩어져 있다.
```

연출: Calm, measured field-report tone with restrained unease. Read the first line as a title, then pause briefly. Deliver the sentence beginning with '불 꺼진' as one even observation. Keep '아직 따뜻하고' understated with no dramatic swell, and take only a light breath at the comma after it. Read '약초 다발과 흙을 털지 않은 도구' without a break, giving these traces the same weight as the warm ash.

<details><summary>수정 전 원문</summary>

```text
식어 있는 야영지
협곡 입구에 버려진 야영지가 있다. 꺼진 화덕의 재 아래쪽이 아직 따뜻하고, 말리다 만 약초 다발과 흙을 털지 않은 도구가 흩어져 있다.
```

</details>

### evt_cold_camp_choice_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
휴식하기
```

연출: Read plainly as a menu choice: calm, even, and brief, with no added emotion.

<details><summary>수정 전 원문</summary>

```text
휴식하기
```

</details>

### evt_cold_camp_choice_0_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
일행은 화덕에 새 불을 지피고 잠시 쉰 뒤, 다시 나아간다.
```

연출: Calm, restrained narration of a brief rest. Slight pause at the comma, then an even, steady finish.

<details><summary>수정 전 원문</summary>

```text
일행이 화덕에 새 불을 지피고 짧게 쉰 뒤 진행한다.
```

</details>

### evt_cold_camp_choice_1

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
흩어져서 살펴보기
```

연출: Read as a brief choice label in a calm, even, restrained tone. Leave a slight natural pause after the first word and add no dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
흩어져서 살펴보기
```

</details>

### evt_cold_camp_choice_1_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
일행이 흩어져 물건을 조사하던 중, 짐 뒤에 있던 괴생물체를 쫓아낸다. 회수한 물건에서 영혼석을 확보한다.
```

연출: Calm, measured report tone. Brief pause at the comma. Keep the creature mention restrained, with only a faint chill; neither dramatize nor dismiss it. Read the soul stone line plainly, without triumph.

<details><summary>수정 전 원문</summary>

```text
일행이 흩어져 물건을 조사하던 중 짐 뒤의 괴생물체를 쫓아낸다. 회수한 물건에서 영혼석을 확보한다.
```

</details>

### evt_cold_camp_choice_2

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
지나치기
```

연출: Read as a brief choice label. Keep the delivery level, calm, and restrained, with no added emphasis or emotion.

<details><summary>수정 전 원문</summary>

```text
지나치기
```

</details>

### evt_cold_camp_choice_2_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
일행은 기록에 없는 휴식처를 그대로 지나친다.
```

연출: Understated, even report tone at a measured pace. Slight pause after the object phrase. End flat, implying neither danger nor relief.

<details><summary>수정 전 원문</summary>

```text
일행은 기록에 없는 휴식처를 그대로 지나친다.
```

</details>

### evt_field_altar_intro

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
임시 상납소
피난 안내도의 화살표가 전부 이곳을 가리키고 있었다. 접수구 위에는 이렇게 적혀 있었다. “상납은 질서다.”
```

연출: Read the title as a brief heading, then pause. Calm, restrained report tone. Leave a small beat before the quoted line and read it flat, without added emphasis.

<details><summary>수정 전 원문</summary>

```text
임시 상납소
피난 안내도의 화살표가 전부 이곳을 가리키고 있었다. 접수구 위에 적힌 글씨 — “상납은 질서다.”
```

</details>

### evt_field_altar_choice_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
영혼석을 바친다
```

연출: Read as a short, deliberate choice line. Keep it even and restrained, with no fear, relief, or hint of the outcome.

<details><summary>수정 전 원문</summary>

```text
영혼석을 바친다
```

</details>

### evt_field_altar_choice_0_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
“이상하리만큼 마음이 편해졌다. 그게 제일 이상했다.”
```

연출: Quiet, even delivery. First sentence soft and at ease. Short pause. Second sentence lower and flatter, letting the strangeness land.

<details><summary>수정 전 원문</summary>

```text
“이상하리만큼 마음이 편해졌다. 그게 제일 이상했다.”
```

</details>

### evt_field_altar_choice_1

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
접수구를 뒤진다
```

연출: Calm, restrained delivery; read as a brief, deliberate choice without dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
접수구를 뒤진다
```

</details>

### evt_field_altar_choice_1_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
“앞사람의 상납분이다. 장부에는 이미 ‘수령 완료’라고 적혀 있었다.”
```

연출: Level, restrained delivery. Brief pause after the first sentence. Read the ledger entry flat and matter-of-fact, and let it land quietly.

<details><summary>수정 전 원문</summary>

```text
“앞사람의 상납분이다. 장부에는 이미 ‘수령 완료’라고 적혀 있었다.”
```

</details>

### evt_field_altar_choice_1_result_1

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
“접수구 안쪽에서, 손이 아닌 것이 손목을 잡았다.”
```

연출: Low, even, report-like delivery. The line is set in quotation marks; do not perform it as a character or suggest whose words they are. Take a brief beat at the comma, then read the rest flat and unhurried. No gasp and no dramatic stress on the 'not a hand' part, but keep the negation clearly articulated.

<details><summary>수정 전 원문</summary>

```text
“접수구 안쪽에서, 손이 아닌 것이 손목을 잡았다.”
```

</details>

### evt_field_altar_choice_2

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
무시한다
```

연출: Brief and level; a calm, decisive choice with no added emotion.

<details><summary>수정 전 원문</summary>

```text
무시한다
```

</details>

### evt_field_altar_choice_2_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
변화 없음.
```

연출: Even, matter-of-fact delivery, like reading a log entry. Add no relief or suspense.

<details><summary>수정 전 원문</summary>

```text
변화 없음.
```

</details>

### evt_supply_cache_intro

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
봉인된 보급 상자
탐사국 보급 상자. 로고가 낯익은데, 볼수록 눈이 미끄러진다. 잠금 장치는 이쪽을 향해 달려 있다.
```

연출: Calm, restrained field-report tone. Brief pause after the title. Keep the sentence about the logo understated. Take a small beat before the final sentence, then deliver it flat, with no dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
봉인된 보급 상자
탐사국 보급 상자. 로고가 낯익은데, 볼수록 눈이 미끄러진다. 잠금 장치는 이쪽을 향해 달려 있다.
```

</details>

### evt_supply_cache_choice_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
규정 해제 코드를 시도한다
```

연출: Even, deliberate delivery; read as a single procedural action, with no dramatic pause or added tension.

<details><summary>수정 전 원문</summary>

```text
규정 해제 코드를 시도한다
```

</details>

### evt_supply_cache_choice_0_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
“코드가 맞았다. 당신이 이 코드를 어떻게 알고 있었는지는, 넘어가자.”
```

연출: Calm, dry, matter-of-fact. Short beat at the comma; deliver '넘어가자' understated, neither accusing nor joking.

<details><summary>수정 전 원문</summary>

```text
“코드가 맞았다. 당신이 이 코드를 어떻게 알고 있었는지는, 넘어가자.”
```

</details>

### evt_supply_cache_choice_0_result_1

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
“‘권한 없음.’ 상자가 조금 더 단단해진 것 같다.”
```

연출: Say the inner single-quoted phrase flat and clipped, in the same voice as the rest, without implying who or what it comes from. Brief pause. Then read the remainder quietly and evenly, keeping the hedged uncertainty of the ending, with no dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
“‘권한 없음.’ 상자가 조금 더 단단해진 것 같다.”
```

</details>

### evt_supply_cache_choice_1

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
강제로 연다
```

연출: Short, firm, neutral read as a choice label. Restrained, with no dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
강제로 연다
```

</details>

### evt_supply_cache_choice_1_result_0

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
“경첩이 아니라, 이빨이 부러지는 소리가 났다.”
```

연출: Low and measured. Take a brief beat at the comma, then say the rest plainly, without extra emphasis.

<details><summary>수정 전 원문</summary>

```text
“경첩이 아니라 이빨이 부러지는 소리가 났다.”
```

</details>

### evt_supply_cache_choice_2

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
놓아둔다
```

연출: Quiet, level, and restrained. Read it as a brief decision with a falling intonation and no added emphasis.

<details><summary>수정 전 원문</summary>

```text
놓아둔다
```

</details>

### evt_crow_watcher_intro

상태: 문단 검토 완료 · 음성 제작·Unity 연결, 청취 미검증

```text
지켜보는 까마귀
까마귀 한 마리가 앉아 있다. 날개 소리를 들은 기억이 없다. 처음부터 거기 있었던 것처럼.
```

연출: Read the title as a brief heading, then pause. Quiet, level, restrained delivery. Short pause before the final sentence; let it trail off without emphasis.

<details><summary>수정 전 원문</summary>

```text
지켜보는 까마귀
까마귀 한 마리가 앉아 있다. 날개 소리를 들은 기억이 없다. 처음부터 거기 있었던 것처럼.
```

</details>

### evt_crow_watcher_choice_0

상태: 문단 검토 완료 · 음성 미제작

```text
먹이를 준다
```

연출: Short choice label. Calm, level delivery with no added emotion, pause, or emphasis.

<details><summary>수정 전 원문</summary>

```text
먹이를 준다
```

</details>

### evt_crow_watcher_choice_0_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“까마귀는 먹이를 먹지 않았다. 다만 세는 것을 멈추었다.”
```

연출: Low, even, restrained. Leave a short pause after the first sentence. Say '다만' flatly, without contrastive stress, and let the final word fall quietly.

<details><summary>수정 전 원문</summary>

```text
“까마귀는 먹이를 먹지 않았다. 다만 세는 것을 멈추었다.”
```

</details>

### evt_crow_watcher_choice_1

상태: 문단 검토 완료 · 음성 미제작

```text
쫓아낸다
```

연출: Short, firm, level delivery as a player choice. No added emotion and no trailing tone.

<details><summary>수정 전 원문</summary>

```text
쫓아낸다
```

</details>

### evt_crow_watcher_choice_1_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“그것은 웃는 것처럼 울었다.”
```

연출: Low, measured, restrained. Pause slightly before the final verb, then let it land flat. Do not act out laughing or crying.

<details><summary>수정 전 원문</summary>

```text
“그것은 웃는 것처럼 울었다.”
```

</details>

### evt_crow_watcher_choice_2

상태: 문단 검토 완료 · 음성 미제작

```text
마주 본다
```

연출: Quiet, even delivery; short and deliberate, no added emphasis.

<details><summary>수정 전 원문</summary>

```text
마주 본다
```

</details>

### evt_crow_watcher_choice_2_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“까마귀 눈에 낫이 비쳤다. 이 방에는 낫이 없다.”
```

연출: Quiet, level, restrained. Take a short beat between the sentences. Read the second as a plain statement with no added emphasis, and let the flatness carry the unease.

<details><summary>수정 전 원문</summary>

```text
“까마귀의 눈에 낫이 비쳤다. 이 방에는 낫이 없다.”
```

</details>

### evt_crow_watcher_choice_2_result_1

상태: 문단 검토 완료 · 음성 미제작

```text
“먼저 눈을 돌린 쪽은 당신이었다.”
```

연출: Calm, restrained delivery at an even pace, with no dramatic pause or emphasis. End on a low, level tone.

<details><summary>수정 전 원문</summary>

```text
“먼저 눈을 돌린 쪽은 당신이었다.”
```

</details>

### evt_scratch_marks_intro

상태: 문단 검토 완료 · 음성 미제작

```text
벽에 새겨진 눈금
복도 막다른 벽에 손톱으로 긁은 눈금이 빽빽하다. 마흔일곱 개. 마지막 몇 줄은 피가 섞여 있다. 손바닥을 대보자, 홈의 간격이 네 손가락과 정확히 일치한다.
```

연출: Low, measured, clinical. Keep it restrained, with no dramatic swell. Pause briefly after the title. Read '마흔일곱 개' flat, like a count. At the comma after '대보자', make a short continuing pause, not a full stop, then read the last clause evenly. Say '네 손가락' as written [ne], not [ni], and leave it unstressed so both the 'four' and 'your' readings stay open.

<details><summary>수정 전 원문</summary>

```text
벽에 새겨진 눈금
복도 막다른 벽에 손톱으로 긁은 눈금이 빽빽하다. 마흔일곱 개. 마지막 몇 줄은 피가 섞여 있다. 손바닥을 대보자 홈의 간격이 네 손가락과 정확히 일치한다.
```

</details>

### evt_scratch_marks_choice_0

상태: 문단 검토 완료 · 음성 미제작

```text
마지막 눈금을 긋는다
```

연출: Calm and deliberate, at an even pace. Read it as a plain action choice with restrained tension and no added emphasis.

<details><summary>수정 전 원문</summary>

```text
마지막 눈금을 긋는다
```

</details>

### evt_scratch_marks_choice_0_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“마흔여덟째를 새긴다. 손끝에 전해지는 감촉이 낯설지 않다.”
```

연출: Quiet, even, and restrained. Short pause after the first sentence; let the last clause land softly, without dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
“마흔여덟째를 새긴다. 손끝에 전해지는 감촉이 낯설지 않다.”
```

</details>

### evt_scratch_marks_choice_0_result_1

상태: 문단 검토 완료 · 음성 미제작

```text
마흔여덟째를 긋는다. 동료들이 한 걸음 물러선다.
```

연출: Low, even, restrained narration. Say the number clearly and plainly. Take a short beat after the first sentence, then read the second quietly and flatly, without added emphasis.

<details><summary>수정 전 원문</summary>

```text
“마흔여덟째를 긋는다. 동료들이 한 걸음 물러선다.”
```

</details>

### evt_scratch_marks_choice_1

상태: 문단 검토 완료 · 음성 미제작

```text
흔적을 통째로 긁어 지운다
```

연출: Calm, restrained, decisive; read as a plain player choice at a steady pace.

<details><summary>수정 전 원문</summary>

```text
흔적을 통째로 긁어 지운다
```

</details>

### evt_scratch_marks_choice_1_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“숫자가 사라진다. 모두가 안심한 표정이다. 나만 왠지 손끝이 허전하다.”
```

연출: Quiet first-person read. Even, matter-of-fact delivery on the first two sentences. Take a brief beat, then read the last sentence slightly softer, with a faint, unexplained sense of emptiness, as if something were missing. Don't overplay it.

<details><summary>수정 전 원문</summary>

```text
“숫자가 사라진다. 모두가 안심한 표정이다. 나만 왠지 손끝이 허전하다.”
```

</details>

### evt_scratch_marks_choice_2

상태: 문단 검토 완료 · 음성 미제작

```text
지나친다
```

연출: Calm, even, brief delivery of a choice label. No added emotion or emphasis.

<details><summary>수정 전 원문</summary>

```text
지나친다
```

</details>

### evt_deploy_order_intro

상태: 문단 검토 완료 · 음성 미제작

```text
격리 해제 승인서
바닥에 흩어진 서류 더미. 탐사국 발신 「격리 해제 승인서」다. 피험자 번호, 체액 채취 동의, 재배치 가능 일자… 모든 항목에 도장이 찍혀 있다. 서명란에는 내 이름이 있다. 기억나지 않는다.
```

연출: Restrained, level first-person read. Say the title plainly, then take a short beat. Read the form fields evenly, like a checklist, with a brief pause at the ellipsis. Slow slightly on the last two sentences and keep the final line quiet and flat, with no overt fear.

<details><summary>수정 전 원문</summary>

```text
격리 해제 승인서
바닥에 흩어진 서류 더미. 탐사국 발신 「격리 해제 승인서」다. 피험자 번호, 체액 채취 동의, 재배치 가능 일자… 모든 항목에 도장이 찍혀 있다. 서명란에는 내 이름이 있다. 기억나지 않는다.
```

</details>

### evt_deploy_order_choice_0

상태: 문단 검토 완료 · 음성 미제작

```text
서류를 챙긴다
```

연출: Calm, matter-of-fact delivery; brief and decisive, without dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
서류를 챙긴다
```

</details>

### evt_deploy_order_choice_0_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
주머니에 접어 넣는다. 문구 하나가 눈에 박힌다. ‘피험자는 절차에 동의하였으며, 기억 소거는 표준 규정에 따른 것임.’
```

연출: Low, restrained narration in short beats, with a brief pause after each sentence. Read the quoted line a touch slower in a detached bureaucratic monotone and end it flat, without dramatic emphasis.

<details><summary>수정 전 원문</summary>

```text
“주머니에 접어 넣는다. 문구 하나가 눈에 박힌다. ‘피험자는 절차에 동의하였으며, 기억 소거는 표준 규정에 따른 것임.’”
```

</details>

### evt_deploy_order_choice_1

상태: 문단 검토 완료 · 음성 미제작

```text
찢어 버린다
```

연출: Brief, decisive delivery as a player choice; steady and restrained, no added drama.

<details><summary>수정 전 원문</summary>

```text
찢어 버린다
```

</details>

### evt_deploy_order_choice_1_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“찢는다. 또 찢는다. 종이 조각이 흩어진다. 동료가 묻는다. ‘뭐였어?’ 대답하지 않는다. 목구멍까지 올라온 말을 삼킨다.”
```

연출: Low, clipped delivery with a short beat between lines. Read the colleague's question plainly, pause briefly after it, and keep the final line restrained rather than dramatic.

<details><summary>수정 전 원문</summary>

```text
“찢는다. 또 찢는다. 종이 조각이 흩어진다. 동료가 묻는다. ‘뭐였어?’ 대답하지 않는다. 목구멍까지 올라온 말을 삼킨다.”
```

</details>

### evt_unfamiliar_log_intro

상태: 문단 검토 완료 · 음성 미제작

```text
두 개의 진입 기록
벽면에 부착된 구형 탐사 일지. 이 구역의 오늘 자 진입 기록이 두 건이다. 한 건은 06:12, 내 서명. 다른 한 건은 03:47, 역시 내 서명. 새벽의 나는 혼자 와 있었다.
```

연출: Title as a separate, neutral beat. Low, even first-person read, like checking a logbook; restrained, no overt fear. Keep both entries in the same rhythm, then pause briefly and read the last line flat and quiet. Read the times as 'yeoseot-si sibi-bun' and 'se-si sasipchil-bun'.

<details><summary>수정 전 원문</summary>

```text
두 개의 진입 기록
벽면에 부착된 구형 탐사 일지. 오늘자, 이 구역 진입 기록이 두 건이다. 한 건은 06:12, 내 서명. 다른 한 건은 03:47, 역시 내 서명. 새벽의 나는 혼자 와 있었다.
```

</details>

### evt_unfamiliar_log_choice_0_result_0

상태: 문단 검토 완료 · 음성 미제작

```text
“종이를 뜯어 주머니에 넣는다. 손이 떨린다. 내 서명은 분명한데, 내 기억은 아니다.”
```

연출: Hushed, controlled first-person monologue. Take a brief beat after each sentence. Let a faint tremor into the second sentence without overplaying it. Read the last sentence level, pause briefly at the comma, and stress no single word so its meaning stays open. End it as a plain statement; do not trail off or let the ending rise.

<details><summary>수정 전 원문</summary>

```text
“종이를 뜯어 주머니에 넣는다. 손이 떨린다. 내 서명은 분명한데, 내 기억은 아니다.”
```

</details>

### evt_unfamiliar_log_choice_1

상태: 문단 검토 완료 · 음성 미제작

```text
기록을 정독한다
```

연출: Calm, deliberate delivery at a steady pace; understated, with a quiet falling ending.

<details><summary>수정 전 원문</summary>

```text
기록을 정독한다
```

</details>

## 맥락 보완 재검토 대기

### evt_unfamiliar_log_choice_0

원문과 현재 낭독 초안: `03:47 기록을 뜯어낸다`

선택지 단독 요청에서 시간 읽기가 미확정으로 표시됐다. 같은 사건 본문에는 새벽 진입 기록임이 명확하고, 그 본문의 Claude 연출에는 03:47 읽기가 이미 있다. 사용자에게 세계관 결정을 다시 요청할 사안으로 처리하지 않고, 다음 Claude 호출에 본문 맥락을 함께 제공해 보류 지시를 해소한다. 보류 문구를 TTS 연출 입력에 넣지 않는다.

## 아직 완료하지 못한 문단

- `evt_unfamiliar_log_choice_1_result_0`
- `evt_forgotten_altar_intro`
- `evt_forgotten_altar_choice_0`
- `evt_forgotten_altar_choice_0_result_0`
- `evt_forgotten_altar_choice_1_result_0`
- `evt_forgotten_altar_choice_2_result_0`
- `evt_hand_in_the_rift_intro`
- `evt_hand_in_the_rift_choice_0`
- `evt_hand_in_the_rift_choice_0_result_0`
- `evt_hand_in_the_rift_choice_0_result_1`
- `evt_hand_in_the_rift_choice_1_result_0`
- `fixed_20c307a800`
- `fixed_1068afeef1`
- `fixed_eac2ac956e`
- `fixed_9cc0efd378`
- `fixed_caa001c01d`
- `fixed_e6763c6eb0`
- `fixed_cad1170358`

## 검수 근거

- 개별 Claude 응답·실제 모델·해시: `_workspace/2026-09-27-game-tts/claude-revision/`
- Gemini 원본 응답·실제 모델: `_workspace/2026-09-27-game-tts/gemini-script-qa/`
- 의미 확인: `_workspace/2026-09-27-game-tts/codex-integration-review.json`
- “마주 본다” 유지 근거: [국립국어원 온라인가나다](https://korean.go.kr/front/onlineQna/onlineQnaView.do?mn_id=98&pageIndex=1&qna_seq=327051)
