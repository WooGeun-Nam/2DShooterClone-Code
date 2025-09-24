using System.Collections.Generic;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class LightningStrikeHitbox : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("이 낙뢰 한 번에 적용할 데미지")]
    public float damage = 20f;

    [Tooltip("대상 필터(플레이어/적 등 맞아야 할 레이어만 체크)")]
    public LayerMask targetLayers = ~0;

    [Header("Lifetime")]
    [Tooltip("낙뢰 VFX가 유지되는 시간(초). 시간 후 자동 파괴")]
    public float lifetimeSeconds = 0.5f;

    [Header("Damage Text (Optional)")]
    public GameObject damageTextPrefab;
    public float damageTextLifetimeSeconds = 0.8f;
    public Vector2 damageTextRandomOffset = new Vector2(0.05f, 0.10f);
    
    private readonly HashSet<Health> alreadyHitThisStrike = new HashSet<Health>();
    private Collider2D triggerCollider2D;
    
    private void Reset()
    {
        // 기본 콜라이더, 리지드바디 설정
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.simulated = true;
    }

    private void Awake()
    {
        triggerCollider2D = GetComponent<Collider2D>();
        if (triggerCollider2D != null) triggerCollider2D.isTrigger = true;

        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            rigidbody2D.gravityScale = 0f;
            rigidbody2D.simulated = true;
        }
    }
    
    private void OnEnable()
    {
        alreadyHitThisStrike.Clear();

        if (lifetimeSeconds > 0f)
            Destroy(gameObject, lifetimeSeconds);
    }
    
    // 스폰 프레임에 이미 겹쳐있는 경우 Enter가 오지 않을 수 있으므로 Stay에서도 1회만 처리
    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyDamage(other, isFromInitialOverlap: true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyDamage(other, isFromInitialOverlap: false);
    }

    // 대상에게 데미지를 주는 메소드
    private void TryApplyDamage(Collider2D other, bool isFromInitialOverlap)
    {
        if (((1 << other.gameObject.layer) & targetLayers) == 0)
            return;

        Health health = other.GetComponentInParent<Health>();
        if (health == null || health.IsDead)
            return;

        if (alreadyHitThisStrike.Contains(health))
            return; // 한 번의 낙뢰에서 중복 타격 방지

        float hpBefore = health.CurrentHP;
        health.ApplyDamage(damage);
        float damageApplied = Mathf.Clamp(hpBefore - health.CurrentHP, 0f, damage);
        
        if (damageApplied > 0f)
        {
            alreadyHitThisStrike.Add(health);

            if (damageTextPrefab != null)
            {
                Vector2 closestPoint = other.ClosestPoint(transform.position);
                Vector2 randomOffset = new Vector2(
                    Random.Range(-damageTextRandomOffset.x, damageTextRandomOffset.x),
                    damageTextRandomOffset.y
                );
                Vector3 textWorldPosition = (Vector3)(closestPoint + randomOffset);
                ShowDamageText(textWorldPosition, damageApplied);
            }
        }
    }
    
    // 낙뢰 전용 데미지 표기
    private void ShowDamageText(Vector3 worldPosition, float amount)
    {
        GameObject instance = Instantiate(damageTextPrefab);

        // UGUI(TextMeshProUGUI)가 붙어있으면 → Canvas 밑에 붙이고 스크린 좌표 배치
        TextMeshProUGUI ugui = instance.GetComponentInChildren<TextMeshProUGUI>(true);
        if (ugui != null)
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[LightningStrikeHitbox] No Canvas found in scene!");
                Destroy(instance);
                return;
            }

            instance.transform.SetParent(canvas.transform, false);

            Vector3 screenPosition = (Camera.main != null)
                ? Camera.main.WorldToScreenPoint(worldPosition)
                : worldPosition;

            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            rectTransform.position = screenPosition;

            ugui.text = Mathf.RoundToInt(amount).ToString();
        }

        if (damageTextLifetimeSeconds > 0f && instance.GetComponent<TransformFadeOut>() == null)
            Destroy(instance, damageTextLifetimeSeconds);
    }
}
