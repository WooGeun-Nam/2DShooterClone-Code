using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class Health : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private float maxHP = 500f;
    [SerializeField] private bool useIFrames = true;       // 피격 직후 잠깐 무적
    [SerializeField] private float iFrameDuration = 0.15f; // 무적 시간

    [Header("Events")]
    public UnityEvent<float, float> onHPChanged; // (current, max)
    public UnityEvent onHurt;
    public UnityEvent onDie;

    public float MaxHP => maxHP;
    public float CurrentHP { get; private set; }
    public bool IsDead => CurrentHP <= 0f;

    private float _iFrameUntil;

    void Awake()
    {
        CurrentHP = Mathf.Max(1f, maxHP);
        BroadcastHP();
    }

    // 대미지 적용. 음수/0이면 무시.
    public void ApplyDamage(float damage)
    {
        if (IsDead || damage <= 0f) return;
        if (useIFrames && Time.time < _iFrameUntil) return;

        CurrentHP = Mathf.Max(0f, CurrentHP - damage);
        onHurt?.Invoke();
        BroadcastHP();

        if (useIFrames && iFrameDuration > 0f)
            _iFrameUntil = Time.time + iFrameDuration;

        if (CurrentHP <= 0f)
            onDie?.Invoke();
    }

    // 체력 변경 이벤트
    private void BroadcastHP() => onHPChanged?.Invoke(CurrentHP, maxHP);
}