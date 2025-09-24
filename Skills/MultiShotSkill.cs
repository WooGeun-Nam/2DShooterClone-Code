using UnityEngine;

namespace Skills.Impl
{
    // 멀티샷(동시에 여러 갈래 발사).
    public class MultiShotSkill : Skills.ISkill
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
            if (skillRuntime == null || skillRuntime.projectileShooter == null || skillRuntime.data == null) return;

            MultiShotData multiShotData = skillRuntime.data as MultiShotData;
            if (multiShotData == null)
            {
                Debug.LogWarning("[MultiShotSkill] MultiShotData가 필요합니다. SkillData 에셋을 MultiShotData로 교체하세요.");
                return;
            }

            int projectileCount = Mathf.Max(2, multiShotData.multishotProjectileCount);
            float spreadDegrees = Mathf.Max(0f, multiShotData.multishotSpreadDegrees);
            
            // 겹침 방지
            if (projectileCount == 2 && spreadDegrees < 0.01f)
                spreadDegrees = 5f;

            Transform characterTransform = skillRuntime.characterController.transform;
            Vector3 shooterWorldPosition = characterTransform.position;

            // 기준 목표 지점/방향 계산
            Vector3 baseTargetWorldPosition;
            Vector2 baseDirectionNormalized;

            if (skillRuntime.opponentTransform != null)
            {
                baseTargetWorldPosition = skillRuntime.opponentTransform.position;
                Vector2 rawDirection = (baseTargetWorldPosition - shooterWorldPosition);
                rawDirection.y = Mathf.Max(rawDirection.y, 0.25f); // 살짝 위 보정(가시성)
                baseDirectionNormalized = rawDirection.normalized;
            }
            else
            {
                bool isFacingRight = characterTransform.localScale.x > 0f;
                baseDirectionNormalized = isFacingRight ? Vector2.right : Vector2.left;
                baseTargetWorldPosition = shooterWorldPosition + (Vector3)(baseDirectionNormalized * skillRuntime.data.minimumRangeMeters);
            }

            float distanceToBaseTarget = Vector2.Distance(shooterWorldPosition, baseTargetWorldPosition);
            float aimDistanceMeters = Mathf.Max(skillRuntime.data.minimumRangeMeters, distanceToBaseTarget);

            float[] angleOffsetsDegrees = (projectileCount == 2)
                ? new float[] { -spreadDegrees * 0.5f, +spreadDegrees * 0.5f }
                : new float[] { -spreadDegrees, 0f, +spreadDegrees };

            foreach (float angleOffset in angleOffsetsDegrees)
            {
                Vector2 shotDirectionNormalized =
                    (Quaternion.Euler(0f, 0f, angleOffset) * (Vector3)baseDirectionNormalized).normalized;

                Vector3 aimWorldPosition = shooterWorldPosition + (Vector3)(shotDirectionNormalized * aimDistanceMeters);
                skillRuntime.projectileShooter.FireAtPosition((Vector2)aimWorldPosition);
            }
        }

        public void EndCast(SkillRuntime skillRuntime) { }
    }
}
