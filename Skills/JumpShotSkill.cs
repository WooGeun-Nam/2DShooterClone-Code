using UnityEngine;

namespace Skills.Impl
{
    /// <summary>
    /// 점프샷:
    /// - 스킬 시작 시 충분한 상승 속도를 보장합니다.
    /// </summary>
    public class JumpShotSkill : Skills.ISkill
    {
        public bool IsUsable(SkillRuntime skillRuntime, float currentTime)
        {
            if (skillRuntime == null) return false;

            // 쿨다운
            if (currentTime < skillRuntime.nextReadyTime)
                return false;

            // 빙결 상태 차단
            if (skillRuntime.characterController.isMovementFrozen)
                return false;

            // 애니메이터의 IsGround 파라미터 체크
            if (skillRuntime.animator != null)
            {
                bool isGround = skillRuntime.animator.GetBool("IsGrounded");
                if (!isGround)
                    return false; // 공중이면 점프샷 금지
            }

            return true;
        }

        
        public void BeginCast(SkillRuntime skillRuntime)
        {
            // 점프 시작 시 Sound
            SoundManager.Instance.PlaySFX("SFX_Jump");
            
            if (skillRuntime == null || skillRuntime.rigidbody2D == null || skillRuntime.data == null) return;

            // [ADD] 점프가 시작되는 프레임에 '착지 전까지 수평 이동 락' 활성화
            if (skillRuntime != null && skillRuntime.characterController != null)
                skillRuntime.characterController.EnableHorizontalMoveLockUntilGrounded();
            
            JumpShotData jumpShotData = skillRuntime.data as JumpShotData;
            if (jumpShotData == null)
            {
                Debug.LogWarning("[JumpShotSkill] JumpShotData가 필요합니다. SkillData 에셋을 JumpShotData로 교체하세요.");
                return;
            }

            Vector2 currentLinearVelocity = skillRuntime.rigidbody2D.linearVelocity;
            float requiredJumpVelocityY = jumpShotData.jumpShotInitialVelocityY;
            currentLinearVelocity.y = Mathf.Max(currentLinearVelocity.y, requiredJumpVelocityY);
            skillRuntime.rigidbody2D.linearVelocity = currentLinearVelocity;
        }
        
        public void OnImpact(SkillRuntime skillRuntime)
        {
            if (skillRuntime == null || skillRuntime.projectileShooter == null || skillRuntime.data == null) return;

            JumpShotData jumpShotData = skillRuntime.data as JumpShotData;
            if (jumpShotData == null)
            {
                Debug.LogWarning("[JumpShotSkill] JumpShotData가 필요합니다. SkillData 에셋을 JumpShotData로 교체하세요.");
                return;
            }

            float flightTimeSeconds = Mathf.Max(0.05f, jumpShotData.jumpShotFlightTimeSeconds);

            // 1) 상대가 있을 경우: 상대 트랜스폼으로 발사
            if (skillRuntime.opponentTransform != null)
            {
                skillRuntime.projectileShooter.FireAtTransform(
                    skillRuntime.opponentTransform,
                    flightTime: flightTimeSeconds
                );
                return;
            }

            // 2) 상대가 없으면: 캐릭터 바라보는 방향 기준 최소 사거리 전방으로 발사(폴백)
            Transform characterTransform = skillRuntime.characterController.transform;
            Vector3 shooterWorldPosition = characterTransform.position;

            bool isFacingRight = characterTransform.localScale.x > 0f;
            Vector2 forwardDirection = isFacingRight ? Vector2.right : Vector2.left;

            float minimumForwardDistanceMeters = Mathf.Max(1f, skillRuntime.data.minimumRangeMeters);
            Vector3 aimWorldPosition = shooterWorldPosition + (Vector3)(forwardDirection * minimumForwardDistanceMeters);

            skillRuntime.projectileShooter.FireAtPosition(
                (Vector2)aimWorldPosition,
                flightTime: flightTimeSeconds
            );
        }

        public void EndCast(SkillRuntime skillRuntime) { }
    }
}
