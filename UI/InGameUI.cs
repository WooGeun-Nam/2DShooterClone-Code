using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 전투 HUD: 플레이어/적 HP바와 중앙 타이머를 표시.
/// - Health.onHPChanged(currentHp, maxHp) 이벤트를 구독해 실시간 반영
/// - StartTimer(durationSeconds)으로 타이머 시작(초 표시)
/// - 타이머 종료 시 onTimerEnded 이벤트 호출
/// </summary>
[DisallowMultipleComponent]
public sealed class InGameUI : MonoBehaviour
{
    [Header("References - Health")]
    [Tooltip("플레이어의 Health")]
    public Health playerHealth;
    [Tooltip("적의 Health")]
    public Health botHealth;

    [Header("UI - HP 슬라이더")]
    [Tooltip("플레이어 HP 슬라이더")]
    public Slider playerHPBar;
    [Tooltip("적 HP 슬라이더")]
    public Slider botHPBar;

    [Header("UI - HP 텍스트 (Slider 내부 TMP)")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI botHPText;

    [Header("UI - Center Timer")]
    [Tooltip("중앙 타이머 TMP 텍스트")]
    public TextMeshProUGUI centerTimerText;

    [Header("Timer Settings")]
    [Tooltip("씬 진입 시 자동으로 타이머 시작할지 여부")]
    public bool autoStartTimer = true;
    [Tooltip("자동 시작 시 타이머 길이(초)")]
    public float autoStartDuration = 90f;

    [Header("Events")]
    public UnityEvent onTimerEnded;

    // 내부 상태
    private float _remainingTimeSeconds = 0f;
    private bool _isTimerRunning = false;

    void Awake()
    {
        // 레퍼런스 자동 연결 보조
        if (!playerHealth)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) playerHealth = player.GetComponent<Health>();
        }
        if (!botHealth)
        {
            var bot = GameObject.FindGameObjectWithTag("Bot");
            if (bot) botHealth = bot.GetComponent<Health>();
        }

        // HP 라벨 자동 탐색(비어 있을 때만)
        if (!playerHPText && playerHPBar)
            playerHPText = playerHPBar.GetComponentInChildren<TextMeshProUGUI>(true);
        if (!botHPText && botHPBar)
            botHPText = botHPBar.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    void OnEnable()
    {
        // Health 이벤트 구독
        if (playerHealth) playerHealth.onHPChanged.AddListener(OnPlayerHPChanged);
        if (botHealth)  botHealth.onHPChanged.AddListener(OnBotHPChanged);
    }

    void OnDisable()
    {
        if (playerHealth) playerHealth.onHPChanged.RemoveListener(OnPlayerHPChanged);
        if (botHealth)  botHealth.onHPChanged.RemoveListener(OnBotHPChanged);
    }

    void Start()
    {
        // 초기 값 동기화
        if (playerHealth) OnPlayerHPChanged(playerHealth.CurrentHP, playerHealth.MaxHP);
        if (botHealth)  OnBotHPChanged(botHealth.CurrentHP, botHealth.MaxHP);

        if (autoStartTimer) StartTimer(autoStartDuration);
        else UpdateTimerText(_remainingTimeSeconds);
    }

    void Update()
    {
        if (!_isTimerRunning) return;

        _remainingTimeSeconds -= Time.deltaTime;
        if (_remainingTimeSeconds <= 0f)
        {
            _remainingTimeSeconds = 0f;
            _isTimerRunning = false;
            UpdateTimerText(_remainingTimeSeconds);
            onTimerEnded?.Invoke();
            return;
        }
        UpdateTimerText(_remainingTimeSeconds);
    }
    
    // 타이머 시작(초)
    public void StartTimer(float durationSeconds)
    {
        _remainingTimeSeconds = Mathf.Max(0f, durationSeconds);
        _isTimerRunning = true;
        UpdateTimerText(_remainingTimeSeconds);
    }

    // 현재 남은 시간을 유지한 채로 재개
    public void ResumeTimer()
    {
        if (_remainingTimeSeconds > 0f) _isTimerRunning = true;
    }

    // 타이머 일시정지
    public void PauseTimer() => _isTimerRunning = false;

    // 타이머 강제 종료(0으로 설정)
    public void StopTimer()
    {
        _remainingTimeSeconds = 0f;
        _isTimerRunning = false;
        UpdateTimerText(_remainingTimeSeconds);
    }

    // 이벤트 수신 처리
    private void OnPlayerHPChanged(float currentHp, float maxHp)
    {
        if (playerHPBar)
        {
            playerHPBar.maxValue = Mathf.Max(1f, maxHp);
            playerHPBar.value = Mathf.Clamp(currentHp, 0f, playerHPBar.maxValue);
        }
        SetHPLabel(playerHPText, currentHp, maxHp);
    }

    private void OnBotHPChanged(float currentHp, float maxHp)
    {
        if (botHPBar)
        {
            botHPBar.maxValue = Mathf.Max(1f, maxHp);
            botHPBar.value = Mathf.Clamp(currentHp, 0f, botHPBar.maxValue);
        }
        SetHPLabel(botHPText, currentHp, maxHp);
    }

    private void UpdateTimerText(float totalSecondsRemaining)
    {
        string display = $"{totalSecondsRemaining:00}";

        if (centerTimerText) centerTimerText.text = display;
    }

    private void SetHPLabel(TextMeshProUGUI label, float currentHp, float maxHp)
    {
        if (!label) return;
        label.text = Mathf.CeilToInt(currentHp).ToString();
    }
}
