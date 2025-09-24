using UnityEngine;

namespace Skills
{
    public enum SkillKind { MultiShot, JumpShot, TimeFreezeArrow, LightningArrow}

    /// <summary>
    /// 스킬 파라미터/메타데이터를 담는 ScriptableObject.
    /// </summary>
    // [CHG] 제네릭 슬롯(floatA~F, intA~C) 전면 삭제 + 명시적 필드로 교체
    [CreateAssetMenu(menuName = "SkillData/SkillDataBase")]
    public class SkillData : ScriptableObject
    {
        [Header("Identity")]
        public string id = "skill.multishot";
        public SkillKind kind = SkillKind.MultiShot;
        public Sprite icon;

        [Header("Casting")]
        public float cooldownSeconds = 3f;
        public float castTimeSeconds = 0.1f;          // 애니 이벤트가 없을 때만 폴백으로 사용
        public bool canMoveWhileCasting = true;
        public string animationTrigger = "";          // 예: "Fire", "JumpShot"

        [Header("Common Projectile Params")]
        public float damage = 20f;
        public float minimumRangeMeters = 8f;         // 조준 최소 거리
        public bool flipWithFacing = true;            // 좌우 반전 적용 여부
        
        [Header("Optional Overrides")]
        [Tooltip("이 스킬만 사용할 발사체 프리팹(비우면 Shooter의 기본 프리팹 사용)")]
        public GameObject projectilePrefabOverride;
    }
}