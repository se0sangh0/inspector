# Wave B 전투 배경·단서 이미지 적용

- 작업일: 2026-09-16
- 목적: 사용자 지정 배경과 단서를 기존 게임 화면에 연결한다.
- 상태: Unity 연결 및 Preview Scene 검증
- 기준: `art/production/graphics-remake/wave-b/`

## 변경 결과

일반·엘리트 전투는 `Battle_Canyon_Floor4_v1.png`를 사용한다.
이는 2026-09-16 사용자가 선택한 공용 적용 범위다.
보스 배경과 밝기 0.28은 유지했다.
협곡의 밝기는 0.85이며 캐릭터 뒤에 표시한다.

| 단서 | Unity 임포트 | 전투 후 화면 |
|---|---|---|
| 찢어진 바구니 | 완료 | 기존 첫 일반 전투에 연결 |
| 숲을 향한 목책 | 완료 | 기존 둘째 일반 전투에 연결 |
| 돌을 둘러놓은 비탈 | 완료 | 대응 경로가 구현되면 연결 |
| 나뭇가지로 덮은 바위 틈 | 완료 | 대응 경로가 구현되면 연결 |
| 밧줄에 매단 뿔 | 완료 | 대응 경로가 구현되면 연결 |
| 길목의 낮은 장벽 | 완료 | 대응 경로가 구현되면 연결 |

현재 관찰 카탈로그에는 두 단서만 있다.
기존 노출 조건·문구·보상·저장 규칙은 유지했다.
나머지 네 그림을 임의의 전투에 배정하지 않았다.
경로 선택 이미지와 Notion 문서는 이번 변경에 포함하지 않았다.

## 파일과 표시 방식

- 배경: `Assets/Resources/RemakeV1/Backgrounds/`
- 단서: `Assets/Resources/RemakeV1/Clues/`
- 원본 PNG 7장을 그대로 복사했다.
- Single Sprite, Point 필터, 무압축, 밉맵 없음으로 임포트했다.
- 최대 크기는 2048px이며 원본 해상도를 유지한다.
- 단서는 2:3 비율을 유지하고 이미지 전체를 표시한다.
- 팝업은 Canvas의 논리 크기를 기준으로 화면 여백을 계산한다.
- 기존 `ResultImage` 파일명으로 들어오는 호출도 지원한다.

`BattleBackground.cs`와 게임 씬의 일반 배경 경로를 바꿨다.
`FieldObservationCatalog.cs`는 기존 두 단서의 그림 경로만 바꿨다.
`PostBattleObservationPanel.cs`는 새 경로와 화면 배율을 처리한다.
구 에셋·GUID와 이전 카드·캐릭터 작업은 보존했다.

## 검증

[Unity 검증 보고서](integration-report.json)를 확인한다.
검증은 별도 Preview Scene에서 실행한다.
사용자가 열어 둔 씬이나 게임 세이브는 저장하지 않는다.

- Unity 컴파일과 이미지 7종 임포트·원본 해시 일치.
- 실제 게임 씬을 다시 열어 일반·엘리트·보스 배경 확인.
- 기존 단서 두 종류 × 720p·1080p의 화면 경계·본문·제목 검사.
- 빈 이미지 처리와 이전 파일명 호환 확인.
- ‘계속’을 두 번 호출해도 후속 처리는 한 번만 실행.

[협곡 720p](battle-1280x720.png) · [협곡 1080p](battle-1920x1080.png)

[바구니 720p](OBS_TORN_BASKET-1280x720.png) · [바구니 1080p](OBS_TORN_BASKET-1920x1080.png)

[목책 720p](OBS_FOREST_BARRICADE-1280x720.png) · [목책 1080p](OBS_FOREST_BARRICADE-1920x1080.png)

이미지는 실제 배경·단서 컴포넌트의 검증용 렌더링이다.
실제 전투의 캐릭터·VFX 가독성과 승리 후 전체 흐름은 미검증이다.

## 재적용

Unity 메뉴 `Tools > Graphics Remake > Apply Wave B Background and Clues`를 실행한다.
도구는 `Assets/Editor/WaveBEnvironmentInstaller.cs`에 있다.
씬은 배경 경로·밝기 필드만 수정하고 다시 로드해 확인한다.
새 기획이나 단서 발생 조건을 추가하지 않는다.

## 변경 이력

- 2026-09-16 · Codex: 사용자 요청으로 공용 배경·기존 단서 그림 교체.
