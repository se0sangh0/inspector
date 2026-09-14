# Unity 검증 보고 2026-09-13 18:45:34
Unity 6000.3.9f1
projectPath /Volumes/KIOXIA/01_Game/01.Project/inspector-unity-validation-2026-09-13

## 컨트롤러: Animators/Enemies/Scarecrow/Scarecrow
  에셋 경로: Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow.controller
  파라미터: Attack1(Trigger), Attack2(Trigger), Attack3(Trigger), Attack4(Trigger)
  레이어 Base Layer
    상태 Idle  클립=Idle  길이=0.517s  프레임률=60
      스프라이트 6키: Scarecrow_Idle_0 | Scarecrow_Idle_2 | Scarecrow_Idle_3 | Scarecrow_Idle_4 | Scarecrow_Idle_5 | Scarecrow_Idle_6
      전이 → Attack1  조건[If Attack1]  exitTime=없음
      전이 → Attack2  조건[If Attack2]  exitTime=없음
      전이 → Attack3  조건[If Attack3]  exitTime=없음
      전이 → Attack4  조건[If Attack4]  exitTime=없음
    상태 Attack1  클립=Attack1  길이=0.517s  프레임률=60
      스프라이트 4키: Scarecrow_Attack_0 | Scarecrow_Attack_1 | Scarecrow_Attack_2 | Scarecrow_Attack_3
      전이 → Idle  조건[]  exitTime=0.52
    상태 Attack2  클립=Attack2  길이=0.517s  프레임률=60
      스프라이트 4키: Scarecrow_Attack2_0 | Scarecrow_Attack2_1 | Scarecrow_Attack2_2 | Scarecrow_Attack2_3
      전이 → Idle  조건[]  exitTime=0.52
    상태 Attack3  클립=Attack3  길이=0.517s  프레임률=60
      스프라이트 4키: Scarecrow_Attack3_0 | Scarecrow_Attack3_2 | Scarecrow_Attack3_3 | Scarecrow_Attack3_10
      전이 → Idle  조건[]  exitTime=0.52
    상태 Attack4  클립=Attack4  길이=0.517s  프레임률=60
      스프라이트 4키: Scarecrow_Attack4_0 | Scarecrow_Attack4_1 | Scarecrow_Attack4_2 | Scarecrow_Attack4_3
      전이 → Idle  조건[]  exitTime=0.52

## enemies.json 매핑 실측
  enemy_reaper_boss  animator=Animators/Enemies/Scarecrow/Scarecrow
    attack1Anim = Attack1  → 클립 존재  스프라이트[Scarecrow_Attack_0,Scarecrow_Attack_1,Scarecrow_Attack_2,Scarecrow_Attack_3]
    attack2Anim = Attack2  → 클립 존재  스프라이트[Scarecrow_Attack2_0,Scarecrow_Attack2_1,Scarecrow_Attack2_2,Scarecrow_Attack2_3]
    attack3Anim = Attack4  → 클립 존재  스프라이트[Scarecrow_Attack4_0,Scarecrow_Attack4_1,Scarecrow_Attack4_2,Scarecrow_Attack4_3]
    attack4Anim = Attack3  → 클립 존재  스프라이트[Scarecrow_Attack3_0,Scarecrow_Attack3_2,Scarecrow_Attack3_3,Scarecrow_Attack3_10]
  enemy_raider_01  animator=Animators/Enemies/Wolf/Wolf
  enemy_crow_01  animator=Animators/Enemies/Crow/Crow
  enemy_goblin_01  animator=Animators/Enemies/Goblin/Goblin

## Attacker / Crow / Scarecrow / Wolf 자산 상태
  Animators/Fellows/Attacker/Attacker
    클립 3개: Idle, Attack, Attack2
    스프라이트 키 10개, null 참조 0개
      Assets/Resources/Animators/Fellows/Attacker/Attacker_Attack_1.png  677x369  스프라이트 2개
      Assets/Resources/Animators/Fellows/Attacker/Attacker_Attack2.png  677x369  스프라이트 3개
      Assets/Resources/Animators/Fellows/Attacker/Attacker_Idle.png  677x369  스프라이트 4개
  Animators/Enemies/Crow/Crow
    클립 1개: Idle
    스프라이트 키 4개, null 참조 0개
      Assets/Resources/Animators/Enemies/Crow/Crow_Idle.png  1062x235  스프라이트 4개
  Animators/Enemies/Scarecrow/Scarecrow
    클립 5개: Idle, Attack1, Attack2, Attack3, Attack4
    스프라이트 키 22개, null 참조 0개
      Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack.png  498x501  스프라이트 4개
      Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack2.png  409x610  스프라이트 5개
      Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack3.png  409x610  스프라이트 4개
      Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack4.png  498x501  스프라이트 4개
      Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Idle.png  409x610  스프라이트 6개
  Animators/Enemies/Wolf/Wolf
    클립 3개: Idle, Attack, Attack2
    스프라이트 키 12개, null 참조 0개
      Assets/Resources/Animators/Enemies/Wolf/Wolf_Attack.png  481x519  스프라이트 4개
      Assets/Resources/Animators/Enemies/Wolf/Wolf_Attack2.png  481x519  스프라이트 4개
      Assets/Resources/Animators/Enemies/Wolf/Wolf_Idle.png  481x519  스프라이트 4개

## wave-a 산출물의 프로젝트 내 참조
  Assets 안의 wave-a 경로 자산: 0개
  → Assets 밖(art/ 폴더)에 있으므로 Unity 에셋 데이터베이스에 없다면 0이 정상이다.
  씬 7개, 프리팹 20개 검사
  씬·프리팹이 의존하는 Animators 자산 58개
    Assets/Resources/Animators/Enemies/Crow/Crow.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Crow/Crow_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Crow/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Attack2.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Goblin.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Goblin_Attack.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Goblin_Attack2.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Goblin_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Goblin/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Attack1.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Attack2.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Attack3.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Attack4.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack2.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack3.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Attack4.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Scarecrow/Scarecrow_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Attack2.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Wolf.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Wolf_Attack.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Wolf_Attack2.png  ← AnimationTest.unity
    Assets/Resources/Animators/Enemies/Wolf/Wolf_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Attack2.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Attacker.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Attacker_Attack_1.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Attacker_Attack2.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Attacker_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Attacker/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/Attack2.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/Caster.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/Caster_Attack2.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/Caster_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/CasterSprite.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Caster/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Defender/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Defender/Defender.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Defender/Defender_Attack.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Defender/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Defender/Tanker_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Offender/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Offender/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Offender/Offender.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Offender/Offender_Attack_2.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Offender/Offender_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Priest/Attack.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Priest/Idle.anim  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Priest/Priest.controller  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Priest/Priest_Attack_3.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Priest/Priest_Idle.png  ← AnimationTest.unity
    Assets/Resources/Animators/Fellows/Priest/Priest_Idle_5.png  ← AnimationTest.unity
