using UnityEngine;
using TMPro;

/// <summary>
/// 프로젝타일 명중 시, 히트 이펙트와 데미지 텍스트를 스폰합니다.
/// - ProjectileObserver의 세 훅을 사용:
///   1) OnProjectileDealtDamage(damage, hitPoint) → 데미지 텍스트
///   2) OnProjectileHitEnemy(collider)            → 히트 이펙트(적)
///   3) OnProjectileStuckToGround(point, normal)  → 히트 이펙트(지면·노멀 정렬 옵션)
/// </summary>
public sealed class HitFxAndDamageTextObserver : ProjectileObserver
{
    [Header("Hit Effect")]
    public GameObject hitEffectPrefab;           // 파티클/스프라이트 FX 프리팹
    public float effectLifetimeSeconds = 0.6f;   // 이펙트 생명주기
    public bool alignToHitNormal = false;
    public Vector2 positionOffset;

    [Header("Damage Text (Optional)")]
    public GameObject damageTextPrefab;          // TextMeshPro가 붙은 프리팹
    public Color normalColor = Color.white;
    
    // 데미지 텍스트 출력 메소드
    public override void OnProjectileDealtDamage(float damageAmount, Vector2 hitPoint)
    {
        if (!damageTextPrefab) return;

        var go = Instantiate(damageTextPrefab);

        // UGUI(TextMeshProUGUI)인지 체크
        var ugui = go.GetComponentInChildren<TextMeshProUGUI>();
        if (ugui != null)
        {
            // 씬에 있는 Canvas 찾기(이름/태그는 프로젝트에 맞게)
            var canvas = FindAnyObjectByType<Canvas>(); // Screen Space - Overlay 권장
            if (canvas != null)
            {
                go.transform.SetParent(canvas.transform, worldPositionStays: false);
                Vector3 screen = Camera.main.WorldToScreenPoint(hitPoint);
                var rect = go.GetComponent<RectTransform>();
                rect.position = screen; // 스크린 좌표 배치
                ugui.text = Mathf.RoundToInt(damageAmount).ToString();
                ugui.color = normalColor;
            }
            else
            {
                Debug.LogWarning("No Canvas found for DamageText (UGUI).");
                Destroy(go);
                return;
            }
        }

        // TransformFadeOut 있으면 그대로, 없으면 타이머 정리
        var fade = go.GetComponent<TransformFadeOut>();
        if (!fade && effectLifetimeSeconds > 0f) Destroy(go, effectLifetimeSeconds);
    }

    public override void OnProjectileHitEnemy(Collider2D enemyCollider)
    {
        if (!hitEffectPrefab) return;

        Vector3 pos = enemyCollider ? (Vector3)enemyCollider.bounds.center : transform.position;
        pos += (Vector3)positionOffset;

        var fx = Instantiate(hitEffectPrefab, pos, Quaternion.identity);

        SoundManager.Instance.PlaySFX("SFX_Hit");

        var fade = fx.GetComponent<TransformFadeOut>();
        if (!fade && effectLifetimeSeconds > 0f) Destroy(fx, effectLifetimeSeconds);
    }

    public override void OnProjectileStuckToGround(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (!hitEffectPrefab) return;

        Quaternion rot = alignToHitNormal
            ? Quaternion.Euler(0f, 0f, Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg)
            : Quaternion.identity;

        var fx = Instantiate(hitEffectPrefab, hitPoint + positionOffset, rot);

        var fade = fx.GetComponent<TransformFadeOut>();
        if (!fade && effectLifetimeSeconds > 0f) Destroy(fx, effectLifetimeSeconds);
    }
}
