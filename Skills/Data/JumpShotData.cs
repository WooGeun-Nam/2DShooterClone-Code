using Skills;
using UnityEngine;

[CreateAssetMenu(menuName = "SkillData/JumpShot")]
public class JumpShotData : SkillData
{
    [Header("JumpShot Only")]
    public float jumpShotInitialVelocityY = 20f;
    [Min(0.05f)] public float jumpShotFlightTimeSeconds = 0.35f;
}