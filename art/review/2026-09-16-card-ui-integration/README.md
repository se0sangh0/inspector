# Wave A 카드 UI·스택 심볼 적용 기록

- 작업일: 2026-09-16
- 상태: Unity 연결 및 Preview Scene 검증 완료
- 근거: 사용자가 지정한 `wave-a/card-ui` 폴더의 이미지 6종
- 목적: 손패 카드 배경과 카드·스택 심볼을 교체한다.
- 범위: 그림 연결, 표시 크기, 숫자 대비, 에셋 검증

## 적용 결과

| 역할 | 카드 배경 | 카드·스택 심볼 |
|---|---|---|
| 공격 · Dealer | Card_Attack_Background_v1.png | Badge_Attack_Sword_v1.png |
| 방어 · Tank | Card_Defense_Background_v1.png | Badge_Defense_Shield_v1.png |
| 지원 · Support | Card_Support_Background_v1.png | Badge_Support_Heart_v1.png |

원본 6장은 `Assets/Resources/RemakeV1/CardUI/`에 그대로 복사했다.
제작 원본과 기존 `Icons/sprite_sheet`는 보존했다.
카드 역할·스택 수치·저장 키와 선택 동작은 바꾸지 않았다.

`StackCardArt`는 역할별 그림을 불러와 캐시한다.
`StackCardController`는 새 그림과 밝은 상아색 숫자를 사용한다.
스택 아이콘은 `GamePlayScene_RightMainArea` 프리팹에 연결했다.
게임 씬은 같은 프리팹을 사용한다.

## 표시 설정

- 배경은 Single Sprite, 최대 1024px, Simple 방식이다.
- 배지는 원본 한 장에서 정사각 Sprite 하나를 가져온다.
- 알파 128 이상인 그림의 긴 변이 영역의 75%를 차지한다.
- 남은 여백은 양쪽에 같은 크기로 둔다.
- 배지는 최대 256px로 임포트한다. 64×64 전용 재제작은 하지 않았다.
- 모두 Point 필터, 무압축, 밉맵 없음, Clamp를 사용한다.
- 카드 크기와 비율은 기존 172.5×280을 유지한다.

## 검증 결과

- Unity 6000.3.9f1에서 스크립트 컴파일을 통과했다.
- 그림 6개와 `.meta`, 원본 PNG 해시 일치를 확인했다.
- 프리팹 카드 슬롯 4개와 실제 씬의 스택 참조 3곳을 확인했다.
- 카드 역할별 양수·음수 설정을 총 24회 확인했다.
- 검사 값은 -5, -1, +1, +5다.
- 버림 예정 표시의 적용·해제와 사용 후 재설정을 확인했다.
- 1280×720·1920×1080에서 그림·글자·투명 여백을 확인했다.

검증은 게임 씬을 읽어 별도 Preview Scene에서 진행했다.
아래 이미지는 실제 카드 컴포넌트를 검증용으로 나란히 놓은 결과다.
전체 전투 화면이나 플레이 모드 캡처는 아니다.
열린 사용자 씬과 세이브는 변경하지 않았다.

[1280×720 미리보기](cards-1280x720.png) · [1920×1080 미리보기](cards-1920x1080.png)

[Unity 검증 보고서](integration-report.json)

## 남은 운영 검증

실제 전투에서 선택 애니메이션·손패 겹침·드로우·턴 흐름을 확인한다.
이번 작업은 새 기획 규칙이나 Notion 변경을 포함하지 않는다.

## 다시 적용하기

Unity 메뉴 `Tools > Graphics Remake > Apply Wave A Card UI`를 실행한다.
원본을 임포트하고 프리팹 연결·검증 보고서·미리보기를 업데이트한다.
기존 에셋과 Sprite 식별자는 재적용 시 유지한다.
도구 소스는 `Assets/Editor/WaveACardUiInstaller.cs`다.

## 변경 이력

- 2026-09-16 · Codex: 카드 배경·심볼 6종 연결과 Unity 검증.
