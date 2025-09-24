// --------------------------------------------------------------------------------------
// IdleAttackState
// - 정지 상태에서 공격 애니메이션을 재생하고,
//   애니메이션 이벤트(NotifyAttackImpact) 수신 시 '단 한 번' 발사
// - Attack이 끝났고 여전히 정지 상태라면 즉시 재트리거(자동사격 루프)
// - 이동을 시작하면 즉시 Locomotion으로 복귀 후 Move 상태로 전환
// --------------------------------------------------------------------------------------
using UnityEngine;

public sealed class IdleAttackState : CharacterControllerBaseFSM.ICharacterState
{
    private readonly CharacterControllerBaseFSM owner;
    private bool didImpactThisCycle;

    public IdleAttackState(CharacterControllerBaseFSM owner)
    {
        this.owner = owner;
    }

    // 공격 트리거 1회 세팅
    public void OnEnter()
    {
        didImpactThisCycle = false;

        // Idle 진입 시, 적을 바라보도록 시선 보정
        if (owner.opponent != null)
        {
            var mover = owner.GetComponent<PlayerMover2D>();
            if (mover != null)
            {
                float dx = owner.opponent.position.x - owner.transform.position.x;
                mover.ForceFace(dx);
            }
        }

        owner.ResetAttackTrigger();
        owner.TriggerAttackOnce();
    }

    // Idle 상태가 유지되는 동안 매 프레임 적을 향해 보기
    public void OnUpdate()
    {
        if (owner.IsCastingSkill()) // 스킬 중엔 자동공격 완전 중단
            return;
        
        // 이동 시 Move 상태로
        if (!owner.IsStopped())
        {
            owner.ForceToLocomotion();
            owner.ChangeState(owner.CreateMoveState());
            return;
        }

        // Idle 유지 중이면, 상대를 계속 바라보도록 시선 업데이트
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
                Debug.LogWarning("[IdleAttackState] Shooter 또는 Opponent 미지정 - 발사 스킵");
        }

        // Attack 종료 후에도 정지면 즉시 재트리거
        if (!owner.IsInAttack())
        {
            if (owner.IsStopped())
            {
                didImpactThisCycle = false;
                owner.ResetAttackTrigger();
                owner.TriggerAttackOnce();
            }
        }
    }

    public void OnFixedUpdate() { }

    // 종료 시 안전하게 Locomotion(기반 애니메이션)으로 복귀
    public void OnExit()
    {
        owner.ForceToLocomotion();
    }
}
