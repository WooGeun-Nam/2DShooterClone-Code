using UnityEngine;

[RequireComponent(typeof(Animator))]
[DisallowMultipleComponent]
public class AnimatorSpeedSync : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private Rigidbody2D rigidBody;     // 없으면 자동 할당 시도
    [SerializeField] private string speedParam = "Speed";

    [Header("Tuning")]
    [SerializeField] private float multiplier = 1f;   // 속도→애니값 배율
    [SerializeField] private float smooth = 0f;       // 0이면 즉시, >0이면 부드럽게(Lerp)

    private Animator _anim;
    private float _lastX;
    private float _speedSmoothed;

    void Awake()
    {
        _anim = GetComponent<Animator>();
        if (rigidBody == null) rigidBody = GetComponent<Rigidbody2D>();
        _lastX = transform.position.x;
    }

    void Update()
    {
        // 소스 속도 계산: Rigidbody2D가 있으면 그것, 없으면 프레임 위치 변화로 대체
        float velocity = rigidBody ? rigidBody.linearVelocity.x : (transform.position.x - _lastX) / Mathf.Max(Time.deltaTime, 1e-6f);
        _lastX = transform.position.x;

        float target = Mathf.Abs(velocity) * multiplier;

        // 스무딩
        _speedSmoothed = smooth > 0f
            ? Mathf.Lerp(_speedSmoothed, target, 1f - Mathf.Exp(-smooth * Time.deltaTime))
            : target;

        _anim.SetFloat(speedParam, _speedSmoothed);
    }
}