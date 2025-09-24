using UnityEngine;

namespace Skills.Impl
{
    /// <summary>
    /// 낙뢰 화살:
    /// - 낙뢰 타격/표기는 발사체 프리팹에 부착된 LightningArrowEffect가 담당.
    /// - SkillData.projectilePrefabOverride가 지정되어 있으면 그 프리팹으로 발사.
    /// </summary>
    public class LightningArrowSkill : ISkill
    {
        public bool IsUsable(SkillRuntime skillRuntime, float currentTime)
        {
            return currentTime >= skillRuntime.nextReadyTime;
        }

        public void BeginCast(SkillRuntime skillRuntime)
        {
        }

        public void OnImpact(SkillRuntime skillRuntime)
        {
            if (skillRuntime == null || skillRuntime.projectileShooter == null)
                return;

            GameObject projectilePrefabOverride =
                (skillRuntime.data != null) ? skillRuntime.data.projectilePrefabOverride : null;

            // 상대가 있으면 상대 트랜스폼으로 발사 (특수 투사체 우선)
            if (skillRuntime.opponentTransform != null)
            {
                if (projectilePrefabOverride != null)
                {
                    skillRuntime.projectileShooter.FireAtTransform(
                        projectilePrefabOverride,
                        skillRuntime.opponentTransform
                    );
                }
                else
                {
                    // 특수 투사체가 없을 시 경고
                    Debug.LogWarning("[LightningArrowSkill] projectilePrefabOverride is null. Fallback to default projectile.");
                    skillRuntime.projectileShooter.FireAtTransform(skillRuntime.opponentTransform);
                }
                return;
            }

            // 상대가 없으면 최소 사거리 전방 발사
            Transform characterTransform = skillRuntime.characterController.transform;
            Vector3 shooterWorldPosition = characterTransform.position;

            bool isFacingRight = characterTransform.localScale.x > 0f;
            Vector2 forwardDirection = isFacingRight ? Vector2.right : Vector2.left;

            float minimumForwardDistanceMeters = Mathf.Max(
                1f,
                (skillRuntime.data != null) ? skillRuntime.data.minimumRangeMeters : 8f
            );

            Vector3 aimWorldPosition = shooterWorldPosition + (Vector3)(forwardDirection * minimumForwardDistanceMeters);

            if (projectilePrefabOverride != null)
            {
                skillRuntime.projectileShooter.FireAtPosition(projectilePrefabOverride, (Vector2)aimWorldPosition);
            }
            else
            {
                Debug.LogWarning("[LightningArrowSkill] projectilePrefabOverride is null. Fallback to default projectile.");
                skillRuntime.projectileShooter.FireAtPosition((Vector2)aimWorldPosition);
            }
        }

        public void EndCast(SkillRuntime skillRuntime)
        {
        }
    }
}
