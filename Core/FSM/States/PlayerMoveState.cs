// --------------------------------------------------------------------------------------
// PlayerMoveState
// - 플레이어 전용 이동 상태 (입력 기반)
// - 책임: 수평 속도로 '정지 여부'만 판단하고, 정지 시 공용 IdleAttackState로 전환
// - 이동 로직 자체는 PlayerMover2D가 담당하므로, 본 상태는 트리거/전환만 관리
// --------------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerMoveState : CharacterControllerBaseFSM.ICharacterState
{
    private readonly CharacterControllerBaseFSM owner;

    private readonly Skills.SkillSelector skillSelector;

    public PlayerMoveState(CharacterControllerBaseFSM owner)
    {
        this.owner = owner;

        // SkillSelector 주입
        skillSelector = owner.GetComponent<Skills.SkillSelector>();
        if (skillSelector == null)
            Debug.LogWarning("[PlayerMoveState] SkillSelector가 없습니다. 스킬 입력은 무시됩니다.");
    }

    public void OnEnter()
    {
        // 공격 트리거가 남아있지 않도록 안전 초기화
        owner.ResetAttackTrigger();
    }

    public void OnUpdate()
    {
        // 정지 시 IdleAttack 상태로 전환
        if (owner.IsStopped())
        {
            owner.ChangeState(new IdleAttackState(owner));
            return;
        }
    }

    public void OnFixedUpdate() { }

    public void OnExit() { }
}