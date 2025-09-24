using UnityEngine;

namespace Skills
{
    /// <summary>
    /// 공용 스킬 캐스팅 상태: Begin → (Impact) → End.
    /// 애니 이벤트(NotifyAttackImpact) 우선, 없으면 castTime 타임아웃으로 임팩트 처리.
    /// </summary>
    public sealed class SkillCastingState : CharacterControllerBaseFSM.ICharacterState
    {
        private readonly CharacterControllerBaseFSM controller;
        private readonly ISkill skillImplementation;
        private readonly SkillRuntime skillRuntime;

        private bool hasImpacted;

        private bool preferredFacingRight;
        
        public SkillCastingState(CharacterControllerBaseFSM controller, SkillRuntime runtime, ISkill impl)
        {
            this.controller = controller;
            this.skillImplementation = impl;
            this.skillRuntime = runtime;
        }

        // OnEnter: 스킬 시작 즉시 트리거 + 캐스팅종류 등록
        public void OnEnter()
        {
            hasImpacted = false;

            // 스킬 시작 표식(자동공격 등 차단) + 종류 등록
            controller.BeginSkillCasting(skillRuntime.data.kind);

            // 충돌 트리거들 정리 후, 스킬 트리거 즉시 ON
            if (skillRuntime.animator && !string.IsNullOrEmpty(skillRuntime.data.animationTrigger))
            {
                skillRuntime.animator.ResetTrigger("Fire");       // 기본공격 트리거 제거
                skillRuntime.animator.ResetTrigger("JumpShot");   // 혹시 남아있을 수 있는 이전 트리거 제거
                skillRuntime.animator.ResetTrigger(skillRuntime.data.animationTrigger);
                skillRuntime.animator.SetTrigger(skillRuntime.data.animationTrigger);
            }
            
            // 이동 잠금 옵션
            if (!skillRuntime.data.canMoveWhileCasting && skillRuntime.playerMover != null)
                skillRuntime.playerMover.SetInput(0f);

            // 이벤트 기반만 사용
            skillImplementation.BeginCast(skillRuntime);
            
            if (skillRuntime.characterController != null && skillRuntime.characterController.opponent != null)
            {
                float dx = skillRuntime.characterController.opponent.position.x
                           - skillRuntime.characterController.transform.position.x;
                preferredFacingRight = dx >= 0f; // 여기서는 임계값 상관없음(= 정확 비교 아님)
                var mover = skillRuntime.characterController.GetComponent<PlayerMover2D>();
                if (mover != null) mover.ForceFace(dx);
            }
        }

        public void OnUpdate()
        {
            var mover = skillRuntime.characterController.GetComponent<PlayerMover2D>();
            if (mover != null) mover.ForceFace(preferredFacingRight ? 1f : -1f);
            
            // 빙결 상태면 애니메이션 끝 이벤트를 못 받으므로 즉시 인터럽트 종료
            if (controller != null && controller.isMovementFrozen)
            {
                SafeFinish(interrupted: true);
                return;
            }
            
            // 프레임(임팩트 이벤트)만 허용
            if (!hasImpacted && controller.ConsumeImpactFlag())
            {
                hasImpacted = true;
                skillImplementation.OnImpact(skillRuntime);
            }

            // 클립 마지막 프레임 이벤트로만 종료
            if (controller.ConsumeSkillEndFlag())
            {
                skillImplementation.EndCast(skillRuntime);
                skillRuntime.nextReadyTime = Time.time + Mathf.Max(0f, skillRuntime.data.cooldownSeconds);

                controller.EndSkillCasting();
                controller.ForceToLocomotion();
                controller.ChangeState(controller.CreateMoveState());
            }
        }

        public void OnExit()
        {
            controller.EndSkillCasting();

            if (skillRuntime.animator && !string.IsNullOrEmpty(skillRuntime.data.animationTrigger))
                skillRuntime.animator.ResetTrigger(skillRuntime.data.animationTrigger);
        }


        public void OnFixedUpdate()
        {
            if (!skillRuntime.data.canMoveWhileCasting && skillRuntime.playerMover != null)
                skillRuntime.playerMover.SetInput(0f); // 캐스팅 중엔 계속 0 유지
        }
        
        private void SafeFinish(bool interrupted)
        {
            // 스킬 구현 종료 콜백
            skillImplementation.EndCast(skillRuntime);
            
            // 빙결에 맞아 끊겼을 때도 쿨다운 적용
            skillRuntime.nextReadyTime = Time.time + Mathf.Max(0f, skillRuntime.data.cooldownSeconds);

            // 캐스팅 종료 표시 & 상태 복귀
            controller.EndSkillCasting();
            controller.ForceToLocomotion();
            controller.ChangeState(controller.CreateMoveState());
        }
    }
}
