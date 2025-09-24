using UnityEngine;

public class SeamlessCloudScrollerSprite : MonoBehaviour
{
    [System.Serializable]
    public struct BackgroundCloudTheme
    {
        public string themeName;
        public Sprite backgroundSprite;      // BackgroundImage용
        public Sprite cloudSprite;           // 구름 스크롤용
        public float cloudSpeedUnitsPerSecond; // 구름 속도
    }

    [Header("Theme Settings")]
    [SerializeField] private BackgroundCloudTheme[] themes;

    [Tooltip("Awake에서 랜덤 선택 여부")]
    [SerializeField] private bool chooseRandomOnAwake = true;

    [Tooltip("랜덤이 아닐 때 사용할 인덱스")]
    [SerializeField] private int selectedThemeIndex = 0;

    [Header("Scene References")]
    [Tooltip("BackGround 오브젝트 (세그먼트 부모로 지정)")]
    public GameObject parentObject;

    [Tooltip("BackgroundImage 오브젝트 (SpriteRenderer 필요)")]
    public GameObject backgroundImageObject;

    [Tooltip("구름 세그먼트 프리팹 (SpriteRenderer 포함, 스케일은 건드리지 않음)")]
    public GameObject segmentPrefab;

    [Tooltip("비워두면 Camera.main 사용")]
    public Camera targetCamera;

    [Header("Wrapping")]
    [SerializeField] private float wrapPadding = 0f;

    // 내부 상태
    private Transform segmentA;
    private Transform segmentB;
    private float segmentWidth;
    private float halfWidth;
    private float cameraDistanceZ;
    private float moveSpeedUnitsPerSecond;
    private bool isInitialized;

    private void Awake()
    {
        if (themes == null || themes.Length == 0 || parentObject == null ||
            backgroundImageObject == null || segmentPrefab == null)
        {
            Debug.LogError("[SeamlessCloudScrollerSprite] 세팅 누락!");
            return;
        }
        if (targetCamera == null) targetCamera = Camera.main;

        int index = chooseRandomOnAwake
            ? Random.Range(0, themes.Length)
            : Mathf.Clamp(selectedThemeIndex, 0, themes.Length - 1);

        ApplyTheme(themes[index]);
    }

    private void ApplyTheme(BackgroundCloudTheme theme)
    {
        // 배경 적용
        var backgroundRenderer = backgroundImageObject.GetComponent<SpriteRenderer>();
        if (backgroundRenderer != null)
        {
            backgroundRenderer.sprite = theme.backgroundSprite;
        }

        // 기존 세그먼트 제거
        CleanupSegments();

        // 새 세그먼트 생성 (프리팹 그대로 사용, 스케일은 변경)
        GameObject segA = Instantiate(segmentPrefab, parentObject.transform);
        GameObject segB = Instantiate(segmentPrefab, parentObject.transform);

        // Sprite 교체
        var rendA = segA.GetComponent<SpriteRenderer>();
        var rendB = segB.GetComponent<SpriteRenderer>();
        if (rendA != null) rendA.sprite = theme.cloudSprite;
        if (rendB != null) rendB.sprite = theme.cloudSprite;

        // 월드 폭 계산 (스케일이 적용된 상태의 bounds 사용)
        segmentWidth = rendA.bounds.size.x;
        halfWidth = segmentWidth * 0.5f;

        // 배치
        Vector3 basePosition = transform.position;
        segA.transform.position = basePosition;
        segB.transform.position = basePosition + Vector3.right * segmentWidth;

        // 참조 저장
        segmentA = segA.transform;
        segmentB = segB.transform;

        // 속도 적용
        moveSpeedUnitsPerSecond = theme.cloudSpeedUnitsPerSecond;
        cameraDistanceZ = Mathf.Abs(transform.position.z - targetCamera.transform.position.z);

        isInitialized = true;
        enabled = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        Vector3 movement = Vector3.left * moveSpeedUnitsPerSecond * Time.deltaTime;
        segmentA.position += movement;
        segmentB.position += movement;

        float leftEdgeX = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, cameraDistanceZ)).x;

        WrapIfOutOfScreen(segmentA, leftEdgeX, segmentB);
        WrapIfOutOfScreen(segmentB, leftEdgeX, segmentA);
    }

    private void WrapIfOutOfScreen(Transform segment, float leftEdgeX, Transform other)
    {
        float segmentRight = segment.position.x + halfWidth;

        if (segmentRight < leftEdgeX - wrapPadding)
        {
            float otherRight = other.position.x + halfWidth;
            float newX = otherRight + halfWidth;
            segment.position = new Vector3(newX, segment.position.y, segment.position.z);
        }
    }

    private void CleanupSegments()
    {
        if (segmentA != null) DestroyImmediate(segmentA.gameObject);
        if (segmentB != null) DestroyImmediate(segmentB.gameObject);
        segmentA = null;
        segmentB = null;
    }
}
