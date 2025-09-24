using System.Collections;
using UnityEngine;

/// <summary>
/// SpriteRenderer의 알파(투명도) 값을 부드럽게 페이드 아웃시키는 클래스
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TransformFadeOut : MonoBehaviour
{
    private SpriteRenderer spriteRenderer; // 대상 SpriteRenderer 컴포넌트
    [Tooltip("페이드 아웃에 걸리는 시간 (초)")]
    private float lerpTime = 0.5f;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        // 오브젝트 활성화 시 바로 페이드 아웃 시작
        StartCoroutine(AlphaLerp(1, 0));
    }

    /// <summary>
    /// 스프라이트의 알파 값을 시작 값에서 끝 값으로 부드럽게 보간합니다.
    /// </summary>
    /// <param name="start">시작 알파 값</param>
    /// <param name="end">끝 알파 값</param>
    private IEnumerator AlphaLerp(float start, float end)
    {
        float currentTime = 0.0f;
        float percent = 0.0f;

        while (percent < 1)
        {
            currentTime += Time.deltaTime; // 경과 시간 업데이트
            percent = currentTime / lerpTime; // 진행률 계산

            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(start, end, percent); // 알파 값 보간
            spriteRenderer.color = color;

            yield return null; // 다음 프레임까지 대기
        }
        // 최종적으로 끝 값으로 설정하여 정확성을 보장
        Color finalColor = spriteRenderer.color;
        finalColor.a = end;
        spriteRenderer.color = finalColor;

        // 페이드 아웃 완료 후 오브젝트 파괴 (필요에 따라 오브젝트 풀에 반환 로직 추가 가능)
        Destroy(gameObject);
    }
}