using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreezeArrowEffect : ProjectileObserver
{
    [Header("Freeze Settings")]
    [Tooltip("정지(동결) 지속 시간(초)")]
    public float freezeDurationSeconds = 1.0f;

    [Tooltip("정지 중 Rigidbody2D를 Kinematic으로 전환할지 여부")]
    public bool forceKinematicWhileFrozen = true;

    [Tooltip("정지 동안 Animator 재생 속도를 0으로 멈출지 여부")]
    public bool stopAnimatorWhileFrozen = true;
    
    [Header("Ice Pillar (Ground Hit)")]
    public GameObject icePillarPrefab;
    public float icePillarLifetimeSeconds = 2.5f;
    public Vector2 icePillarScale = new Vector2(1.0f, 2.0f);

    // 솟아오르는 연출
    [Tooltip("얼음기둥이 솟아오르는 데 걸리는 시간(초)")]
    public float icePillarRiseDurationSeconds = 0.25f;

    [Tooltip("성장 중 충돌 떨림 방지를 위해 성장 완료 후 Collider를 켤지 여부")]
    public bool enableColliderAfterRise = true;

    [Tooltip("지면 법선에 맞춰 회전할지 여부(대부분은 세워두기 위해 false 권장)")]
    public bool alignPillarToGroundNormal = false;
    
    [Tooltip("지면 접점에서 초기로 얼마나 띄울지(파고듦 방지). 0에 가까울수록 더 아래에서 시작")]
    [Range(0f, 0.2f)]
    public float icePillarBaseLiftMeters = 0.02f;

    [Tooltip("성장 시 피벗 보정 계수 (피벗이 가운데면 0.5, 하단이면 0.0 권장)")]
    [Range(0f, 1f)]
    public float icePillarPivotCompensation = 0.5f;
    
    [Header("Freeze Overlay (Optional)")]
    public GameObject freezeOverlayPrefab;        // 얼음 프리팹(스프라이트)
    public Vector3 overlayLocalOffset = new Vector3(0f, 0f, 0f); // 캐릭터 중심
    
    public override void OnProjectileHitEnemy(Collider2D enemyCollider)
    {
        if (enemyCollider == null) return;

        // 피격 대상의 CharacterControllerBaseFSM
        var controller = enemyCollider.GetComponentInParent<CharacterControllerBaseFSM>();
        if (controller == null) return;

        controller.FreezeFor(
            seconds: Mathf.Max(0.05f, freezeDurationSeconds),
            lockMovementWhileFrozen: true,
            stopAnimatorWhileFrozen: stopAnimatorWhileFrozen,
            forceKinematicWhileFrozen: forceKinematicWhileFrozen
        );
        
        if (freezeOverlayPrefab != null)
        {
            var anchor = controller.transform;

            // 중복 방지: 기존 오버레이 있으면 제거
            var existing = anchor.Find("__FreezeOverlay");
            if (existing != null) Destroy(existing.gameObject);

            // 자식으로 생성 → 캐릭터 플립/위치 자동 추종
            var overlay = Instantiate(freezeOverlayPrefab, anchor);
            overlay.name = "__FreezeOverlay";
            overlay.transform.localPosition = overlayLocalOffset;
            overlay.SetActive(true);

            // 프로젝타일은 곧 풀 반환되므로, '대상 컨트롤러'에서 코루틴을 돌림
            controller.StartCoroutine(Co_ShowAndFadeOverlay(overlay, Mathf.Max(0.05f, freezeDurationSeconds)));
        }
    }
    
    // 지면에 박혔을 때 코루틴
    public override void OnProjectileStuckToGround(Vector2 hitPoint, Vector2 hitNormal)
    {
        if (icePillarPrefab == null) return;

        // 스폰 위치 : 접점에서 아주 살짝 띄워 파고듦 방지
        Vector3 baseWorldPosition = (Vector3)hitPoint + (Vector3)(hitNormal.normalized * icePillarBaseLiftMeters);
        
        Quaternion spawnRotation = Quaternion.identity;
        if (alignPillarToGroundNormal)
        {
            float angleDegrees = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg - 90f;
            spawnRotation = Quaternion.Euler(0f, 0f, angleDegrees);
        }

        // 생성(초기 스케일은 가로만 적용, 세로는 아주 얇게 시작)
        GameObject icePillarObject = Instantiate(icePillarPrefab, baseWorldPosition, spawnRotation);
        Vector3 targetScale = new Vector3(
            Mathf.Max(0.01f, icePillarScale.x),
            Mathf.Max(0.01f, icePillarScale.y),
            1f
        );
        float initialScaleY = 0.01f; // 거의 0에서 시작
        icePillarObject.transform.localScale = new Vector3(targetScale.x, initialScaleY, 1f);

        // Collider는 성장 끝나고 켜기
        Collider2D icePillarCollider2D = icePillarObject.GetComponent<Collider2D>();
        if (enableColliderAfterRise && icePillarCollider2D != null)
        {
            icePillarCollider2D.enabled = false;
        }

        // 성장 코루틴 시작(피벗이 '가운데'인 전형적 스프라이트 기준)
        StartCoroutine(Co_RiseIcePillar(
            icePillarObject.transform,
            hitNormal.normalized,
            targetScale,
            initialScaleY,
            icePillarCollider2D
        ));

        // 수명 후 제거
        Destroy(icePillarObject, Mathf.Max(0.1f, icePillarLifetimeSeconds));
    }

    // 성장 코루틴 추가
    private System.Collections.IEnumerator Co_RiseIcePillar(
        Transform pillarTransform,
        Vector2 groundNormal,
        Vector3 targetScale,
        float initialScaleY,
        Collider2D pillarCollider2D)
    {
        // 생성 시 사운드 재생
        SoundManager.Instance.PlaySFX("SFX_IcePillar");
        
        float elapsed = 0f;
        Vector3 startScale = new Vector3(targetScale.x, initialScaleY, 1f);

        // 피벗이 가운데면: scale.y가 커질수록 ‘높이/2’ 만큼 위로 올려서 밑둥을 땅에 고정
        // world 기준 보정이므로 groundNormal 방향으로 0.5 * (deltaScaleY) 만큼 위치 이동
        while (elapsed < icePillarRiseDurationSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, icePillarRiseDurationSeconds));

            // 부드러운 가속/감속(이징)
            float eased = t * t * (3f - 2f * t); // SmoothStep

            float currentScaleY = Mathf.Lerp(initialScaleY, targetScale.y, eased);
            float deltaScaleY = currentScaleY - pillarTransform.localScale.y;

            // 스케일 적용
            pillarTransform.localScale = new Vector3(targetScale.x, currentScaleY, 1f);

            // 위치 보정: 가운데 피벗 보정치 = (deltaScaleY * 0.5) * 법선
            pillarTransform.position += (Vector3)(groundNormal * (deltaScaleY * icePillarPivotCompensation));

            yield return null;
        }

        // 최종 정렬(정확한 타겟 적용)
        float finalDeltaScaleY = targetScale.y - pillarTransform.localScale.y;
        pillarTransform.localScale = targetScale;
        pillarTransform.position += (Vector3)(groundNormal * (finalDeltaScaleY * icePillarPivotCompensation));

        // 성장 완료 후 Collider 켜기
        if (pillarCollider2D != null && enableColliderAfterRise)
        {
            pillarCollider2D.enabled = true;
        }
    }
    
    private static IEnumerator Co_ShowAndFadeOverlay(GameObject overlay, float freezeSec)
    {
        if (overlay == null) yield break;

        // TransformFadeOut은 Awake에서 0.5초 페이드 후 Destroy 함. (설정 불가) :contentReference[oaicite:3]{index=3}
        const float fadeLen = 0.5f;

        float wait = Mathf.Max(0f, freezeSec - fadeLen);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        if (overlay != null)
        {
            // 여기서 붙이면 이제부터 0.5초 페이드 시작
            overlay.AddComponent<TransformFadeOut>();
            // TransformFadeOut이 끝나면 오브젝트가 자동 Destroy됨
        }
    }
}
