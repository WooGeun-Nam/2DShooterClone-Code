using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public sealed class LightningArrowEffect : ProjectileObserver
{
    [Header("Strike Settings")]
    [Tooltip("낙뢰 발생 횟수")]
    public int strikeCount = 2;

    [Tooltip("낙뢰 간 간격(초)")]
    public float strikeIntervalSeconds = 1.0f;

    [Tooltip("낙뢰 VFX 프리팹")]
    public GameObject lightningStrikePrefab;

    [Tooltip("각 낙뢰 VFX의 유지 시간(초)")]
    public float strikeVfxDurationSeconds = 0.5f;
    
    [Header("Damage")]
    [Tooltip("낙뢰 1회당 피해량")]
    public float damagePerStrike = 50f;

    [Tooltip("낙뢰 중심(bottom-center) 기준 범위 반경(미터)")]
    public float damageRadiusMeters = 1f;

    [Tooltip("스폰 지역 오프셋(지면 간섭 방지)")]
    public Vector2 spawnOffset = new Vector2(0f, 0.02f);

    [Tooltip("모든 낙뢰가 끝난 뒤 투사체 제거")]
    public bool destroyProjectileAfterAllStrikes = true;
    
    [Header("Pivot / Alignment")]
    [Tooltip("프리팹의 스프라이트 Pivot이 Bottom-Center가 아닐 때 강제로 bottom-center 정렬을 맞춤")]
    public bool forceBottomCenterAlignment = true;
    
    // 미세 정렬 오프셋
    [Header("Fine Alignment")]
    [Tooltip("bottom-center 정렬 후 추가로 내릴(음수) 또는 올릴(양수) 오프셋")]
    public float alignmentExtraYOffset = -1.5f;
    
    private bool hasStartedStrikes;
    private Coroutine strikeRoutine;
    
    private void OnEnable()
    {
        hasStartedStrikes = false;
        strikeRoutine = null;
    }

    private void OnDisable()
    {
        if (strikeRoutine != null)
        {
            StopCoroutine(strikeRoutine);
            strikeRoutine = null;
        }
        hasStartedStrikes = false;
    }
    
    // 투사체가 적에 타격 했을 시
    public override void OnProjectileHitEnemy(Collider2D enemy)
    {
        if (hasStartedStrikes) return;
        hasStartedStrikes = true;

        Vector2 anchorPosition = enemy.ClosestPoint(transform.position);

        // 지면 찾기 (밑으로 레이캐스트)
        RaycastHit2D groundHit = Physics2D.Raycast(anchorPosition, Vector2.down, 5f, LayerMask.GetMask("Ground"));
        if (groundHit.collider != null)
        {
            anchorPosition = groundHit.point;
        }

        strikeRoutine = StartCoroutine(Co_StrikeRepeatedly(anchorPosition));
    }
    
    // 투사체가 지면에 박혔을 때
    public override void OnProjectileStuckToGround(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (hasStartedStrikes) return;
        hasStartedStrikes = true;

        strikeRoutine = StartCoroutine(Co_StrikeRepeatedly(hitPoint));
    }
    
    // 낙뢰 반복 생성 코루틴
    private IEnumerator Co_StrikeRepeatedly(Vector2 basePoint)
    {
        Vector3 anchorWorld = (Vector3)basePoint + (Vector3)spawnOffset;

        int totalStrikeCount = Mathf.Max(1, strikeCount);
        for (int index = 0; index < totalStrikeCount; index++)
        {
            // 낙뢰 VFX 스폰
            if (lightningStrikePrefab != null)
            {
                GameObject vfxObject = Instantiate(lightningStrikePrefab, anchorWorld, Quaternion.identity);
                SoundManager.Instance.PlaySFX("SFX_Lightning");
                
                // bottom-center 정렬 보정
                if (forceBottomCenterAlignment)
                {
                    SpriteRenderer spriteRenderer = vfxObject.GetComponentInChildren<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        float halfHeight = spriteRenderer.bounds.extents.y;
                        vfxObject.transform.position = anchorWorld + new Vector3(0f, halfHeight + alignmentExtraYOffset, 0f);
                    }
                    else
                    {
                        vfxObject.transform.position = anchorWorld + new Vector3(0f, alignmentExtraYOffset, 0f);
                    }
                }
                else
                {
                    vfxObject.transform.position = anchorWorld + new Vector3(0f, alignmentExtraYOffset, 0f);
                }

                // 필요 시, 한 번의 낙뢰 데미지를 스킬 파라미터에 맞춰 전달하고 싶다면:
                LightningStrikeHitbox hitbox = vfxObject.GetComponent<LightningStrikeHitbox>();
                if (hitbox != null)
                {
                    hitbox.damage = damagePerStrike; // LightningArrowEffect의 설정을 그대로 사용
                }

                if (strikeVfxDurationSeconds > 0f && vfxObject.GetComponent<TransformFadeOut>() == null)
                {
                    Destroy(vfxObject, strikeVfxDurationSeconds);
                }
            }

            bool isLastStrike = (index >= totalStrikeCount - 1);
            if (!isLastStrike && strikeIntervalSeconds > 0f)
            {
                yield return new WaitForSeconds(strikeIntervalSeconds);
            }
        }

        strikeRoutine = null;
        if (destroyProjectileAfterAllStrikes)
        {
            Destroy(gameObject);
        }
    }
}
