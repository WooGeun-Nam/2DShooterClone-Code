# 2DShooterClone – 코드 구조

> Unity 기반 2D 슈터 액션 게임 **2DShooterClone**의 코드 구조 정리.  
> FSM, 스킬, 시스템, UI 단위로 나누어 관리됩니다.

---

## Core

- `FSM/CharacterControllerBaseFSM` — 캐릭터 FSM 관리 부모 클래스
- `FSM/PlayerControllerFSM` — 플레이어 FSM 제어
- `FSM/BotControllerFSM` — 봇 FSM 및 자동 동작 제어
- `FSM/States/BotIdleAttackState` — 봇 자동 공격·스킬 상태
- `FSM/States/BotRandomMoveState` — 봇 자동 이동 상태
- `FSM/States/IdleAttackState` — 플레이어 대기·공격 상태
- `FSM/States/PlayerMoveState` — 플레이어 이동 상태
- `FSM/States/SkillCastingState` — 스킬 사용 상태
- `AnimatorSpeedSync` — 이동속도 기반 애니메이션 처리
- `GameManager` — 게임 시작·종료 제어
- `Health` — 캐릭터 체력 관리
- `MobileHoldButton` — 모바일 좌/우 입력 버튼 처리
- `ObjectPool` — 오브젝트 풀 관리 (투사체)
- `PlayerMover2D` — 캐릭터 이동 제어
- `ProjectileObserver` — 투사체 타격 확인
- `ProjectileShooterService` — 투사체 발사 서비스
- `TilemapRailClamp` — 캐릭터가 타일맵 밖으로 벗어나지 않게 제어

---

## Skills

- `Data/SkillData` — 범용 스킬 데이터 (SO)
- `Data/MultiShotData` — 멀티샷 전용 데이터 (SkillData 상속)
- `Data/JumpShotData` — 점프샷 전용 데이터 (SkillData 상속)
- `ISkill` — 스킬 실행 공통 인터페이스
- `SkillRuntime` — 스킬 참조 및 상태 보관
- `SkillSelector` — 스킬 선택 및 쿨타임 관리
- `MultiShotSkill` — 멀티샷 구현
- `JumpShotSkill` — 점프샷 구현
- `TimeFreezeArrowSkill` — 동결 화살 구현
- `FreezeArrowEffect` — 동결 화살 효과 (빙결/벽 생성)
- `LightningArrowSkill` — 낙뢰 화살 구현
- `LightningArrowEffect` — 낙뢰 화살 효과 (VFX 생성)
- `LightningStrikeHitbox` — 낙뢰 VFX 데미지 판정

---

## System

- `SoundManager` — 게임 사운드 제어 (싱글톤)

---

## UI

- `HitFxAndDamageTextObserver` — 피격 효과 및 데미지 텍스트 처리
- `InGameUI` — 체력, 타이머 UI 제어
- `SeamlessCloudScrollerSprite` — 랜덤 배경 및 구름 스크롤
- `SkillBarUI` — 스킬 버튼 UI 생성 헬퍼
- `SkillButtonUI` — 스킬 버튼 세팅 및 기능 할당
- `TransformFadeOut` — FadeOut 후 오브젝트 파괴
