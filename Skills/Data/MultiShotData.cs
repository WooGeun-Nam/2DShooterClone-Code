using Skills;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillData/MultiShot")]
public class MultiShotData : SkillData
{
    [Header("MultiShot Only")]
    public int multishotProjectileCount = 2;
    public float multishotSpreadDegrees = 6f;
    public float multishotChargeSeconds = 1.0f;
}