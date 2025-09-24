using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover2D : MonoBehaviour
{
    [Header("Move")]
    [Tooltip("최대 수평 속도")]
    public float maxSpeed = 6f;
    [Tooltip("가속도(입력 있을 때)")]
    public float accel = 22f;
    [Tooltip("감속도(입력 없을 때)")]
    public float decel = 26f;

    [Header("Facing")]
    [Tooltip("SpriteRenderer를 좌우 뒤집어 바라보는 방향을 표현")]
    public bool flipSprite = true;
    [Tooltip("우향이 기본인지(스프라이트가 오른쪽을 보고 그려졌다면 true)")]
    public bool defaultFaceRight = true;

    private Rigidbody2D rigidBody;
    private SpriteRenderer spriteRenderer;
    private float _inputX; // -1~1
    
    // 외부 Read 메소드
    public float InputX => _inputX;
    public bool IsMoving => Mathf.Abs(rigidBody.linearVelocity.x) > 0.01f;
    
    void Awake()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }
    
    // -1(좌), 0(정지), 1(우). 아날로그 값도 허용(-1~1).
    // 모바일 버튼/가상 스틱에서 호출
    public void SetInput(float x)
    {
        _inputX = Mathf.Clamp(x, -1f, 1f);
    }

    // 즉시 수평 입력을 0으로.
    public void Stop() => _inputX = 0f;

    void FixedUpdate()
    {
        // 목표 수평 속도
        float targetVx = _inputX * maxSpeed;

        // 현재 수평 속도와의 차이
        float vx = rigidBody.linearVelocity.x;
        float diff = targetVx - vx;

        // 가/감속 선택
        float a = (Mathf.Abs(targetVx) > 0.01f) ? accel : decel;

        // 이번 물리 프레임에서 변경 가능한 최대치
        float maxDelta = a * Time.fixedDeltaTime;

        // 수평 속도 보정
        float newVx = vx + Mathf.Clamp(diff, -maxDelta, maxDelta);

        // y속도는 그대로 보존, 중력/점프에 전적으로 맡김
        rigidBody.linearVelocity = new Vector2(newVx, rigidBody.linearVelocity.y);

        // 스프라이트 바라보는 방향
        if (flipSprite && spriteRenderer)
        {
            // 입력 우선, 없으면 실제 이동 방향으로
            float dir = Mathf.Abs(_inputX) > 0.01f ? _inputX : Mathf.Sign(newVx);
            if (Mathf.Abs(dir) > 0.01f)
            {
                bool faceRight = dir > 0f;
                spriteRenderer.flipX = defaultFaceRight ? !faceRight : faceRight;
            }
        }
    }
    
    public void ForceFace(float direction)
    {
        if (!flipSprite || spriteRenderer == null) return;
        if (Mathf.Abs(direction) < 0.001f) return;

        bool faceRight = direction > 0f;
        spriteRenderer.flipX = defaultFaceRight ? !faceRight : faceRight;
    }
}