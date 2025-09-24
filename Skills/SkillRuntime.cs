using UnityEngine;

namespace Skills
{
    /// <summary>
    /// 스킬 실행 시 필요한 모든 참조/상태를 담는 런타임 컨텍스트.
    /// </summary>
    public class SkillRuntime
    {
        // 고정 참조
        public readonly CharacterControllerBaseFSM characterController;
        public readonly ProjectileShooterService projectileShooter;
        public readonly Animator animator;
        public readonly Rigidbody2D rigidbody2D;
        public readonly PlayerMover2D playerMover;
        public Transform opponentTransform
        {
            get { return characterController != null ? characterController.opponent : null; }
        }

        // 데이터/상태
        public readonly SkillData data;
        public float castStartTime;
        public float nextReadyTime;

        public SkillRuntime(CharacterControllerBaseFSM owner, SkillData data)
        {
            this.characterController = owner;
            this.data = data;

            this.projectileShooter = owner ? owner.shooter : null;
            this.animator         = owner ? owner.animator : null;
            this.rigidbody2D      = owner ? owner.rigidBody2D : null;
            this.playerMover      = owner ? owner.GetComponent<PlayerMover2D>() : null;
        }
    }
}