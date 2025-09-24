// ==========================
// ProjectileObserver.cs
// - 프로젝타일 이벤트를 받는 '컴포넌트' 베이스 클래스
// - 필요한 메서드만 override 해서 사용
// ==========================
using UnityEngine;

public abstract class ProjectileObserver : MonoBehaviour
{
    // 투사체가 적을 맞췄을 때 호출
    public virtual void OnProjectileHitEnemy(Collider2D enemyCollider) { }
    
    // 투사체가 Ground에 '박혔을' 때 호출 (히트 지점/노멀 제공)
    public virtual void OnProjectileStuckToGround(Vector2 hitPoint, Vector2 hitNormal) { }

    // 피해량/정확한 히트 지점을 통지
    public virtual void OnProjectileDealtDamage(float damageAmount, Vector2 hitPoint) { }
}