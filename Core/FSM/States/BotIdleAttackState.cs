// --------------------------------------------------------------------------------------
// BotIdleAttackState
// - Idle(공격) 상태에서 'idleDurationSeconds' 동안 대기하며 공격 루프 수행
// - 애니메이션 이벤트(NotifyAttackImpact) 수신 시 단 한 번 발사
// - Attack 종료 후 정지 상태면 즉시 재트리거(지속 사격)
// - 설정된 시간이 끝나면 무조건 Move 상태로 복귀
// - Idle 동안에는 항상 상대를 바라보도록 시선만 보정(이동/속도 변경 없음)
// --------------------------------------------------------------------------------------
using UnityEngine;
using Skills;

public sealed class BotIdleAttackState : CharacterControllerBaseFSM.ICharacterState
{
    private readonly CharacterControllerBaseFSM owner;
    private readonly float exitAtTime;
    private bool didImpactThisCycle;

    private SkillSelector skillSelector;
    private float autoCastThrottleSeconds = 0.25f;
    private float nextAutoCastAllowedTime = 0f;
    
    public BotIdleAttackState(CharacterControllerBaseFSM owner, float idleDurationSeconds)
    {
        this.owner = owner;
        this.exitAtTime = Time.time + Mathf.Max(0.2f, idleDurationSeconds);

        // SkillSelector 캐싱 (없으면 경고)
        skillSelector = owner.GetComponent<SkillSelector>();
        if (skillSelector == null)
            Debug.LogWarning("[BotIdleAttackState] SkillSelector가 없어 자동 스킬 시전을 건너뜁니다.");
    }

    public void OnEnter()
    {
        didImpactThisCycle = false;

        // IdleAttack 진입 시 봇은 스킬 자동시전 시도
        if (skillSelector != null && !owner.IsCastingSkill())
        {
            if (Time.time >= nextAutoCastAllowedTime)
            {
                bool didCast = skillSelector.TryUseAnyUsable(); // 쿨타임만 기준(1대1)
                nextAutoCastAllowedTime = Time.time + autoCastThrottleSeconds;

                if (didCast)
                {
                    // 스킬을 시작했으므로 일반공격 트리거는 건너뜀
                    return;
                }
            }
        }

        // 일반공격 1회 트리거
        if (owner.opponent != null)
        {
            var mover = owner.GetComponent<PlayerMover2D>();
            if (mover != null)
            {
                float direction = owner.opponent.position.x - owner.transform.position.x;
                mover.ForceFace(direction);
            }
        }

        owner.ResetAttackTrigger();
        owner.TriggerAttackOnce();
    }

    public void OnUpdate()
    {
        if (owner.IsCastingSkill())
            return;

        // 시간 만료 시 Move 복귀
        if (Time.time >= exitAtTime)
        {
            owner.ForceToLocomotion();
            owner.ChangeState(owner.CreateMoveState());
            return;
        }

        // Idle 유지 중 항상 시선 보정 (적 방향)
        if (owner.opponent != null)
        {
            var mover = owner.GetComponent<PlayerMover2D>();
            if (mover != null)
            {
                float dx = owner.opponent.position.x - owner.transform.position.x;
                mover.ForceFace(dx);
            }
        }

        // 애니메이션 임팩트 1회 발사
        if (!didImpactThisCycle && owner.ConsumeImpactFlag())
        {
            didImpactThisCycle = true;

            if (owner.shooter && owner.opponent)
                owner.shooter.FireAtTransform(owner.opponent);
            else
                Debug.LogWarning("[BotIdleAttackState] Shooter/Opponent 미지정 - 발사 스킵");
        }

        // 공격 종료 후 재트리거 직전에 스킬 자동시전 시도
        if (!owner.IsInAttack() && owner.IsStopped())
        {
            if (skillSelector != null && !owner.IsCastingSkill())
            {
                if (Time.time >= nextAutoCastAllowedTime)
                {
                    bool didCast = skillSelector.TryUseAnyUsable(); // 준비된 첫 스킬 즉시 시전
                    nextAutoCastAllowedTime = Time.time + autoCastThrottleSeconds;

                    if (didCast)
                    {
                        // 스킬 시전 시작했으므로 이번 사이클의 일반공격 재트리거는 생략
                        return;
                    }
                }
            }

            // 스킬을 쓰지 않았다면 기존 자동공격 루프 그대로 재트리거
            didImpactThisCycle = false;
            owner.ResetAttackTrigger();
            owner.TriggerAttackOnce();
        }
    }

    public void OnFixedUpdate() { }

    public void OnExit()
    {
        owner.ForceToLocomotion();
    }
}
