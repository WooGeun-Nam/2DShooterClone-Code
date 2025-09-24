using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// GameManager
/// - Ready(카운트다운) → Battle(타이머 진행) → End(승/패/무) 전체 흐름 제어
/// - 시작 시 BGM 재생(옵션)
/// - 시작 시 랜덤 맵 세팅
/// - 카운트다운 동안 플레이어/적 컨트롤 비활성화 + 전체 버튼 비활성화
/// - 애니메이터: 카운트다운 동안 Idle 고정 + speed=0 → 종료 시 speed=1
/// - InGameUI와 연동해 전투 타이머 표시/종료 이벤트 처리
/// - 플레이어/적 HP 0 즉시 종료
/// </summary>
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    [Header("Controllers")]
    public PlayerControllerFSM playerController;
    public BotControllerFSM enemyController;

    [Header("Health")]
    public Health playerHealth;
    public Health enemyHealth;
    
    [Header("CharacterMap")]
    public TilemapRailClamp playerMap;
    public TilemapRailClamp botMap;

    [Header("UI")]
    public InGameUI inGameUI;

    [Header("Audio")]
    public string bgmName;

    [Header("Timers")]
    [Tooltip("게임 시작 전 카운트다운(초)")]
    public float readyCountdownSeconds = 3f;
    [Tooltip("배틀 진행 시간(초)")]
    public float battleDurationSeconds = 90f;

    [Header("Animator Startup State")]
    [Tooltip("카운트다운 동안 고정할 상태명. 서브 스테이트머신이면 'Locomotion/Idle'처럼 경로 포함")]
    public string idleStateName = "Idle";
    public int animatorLayerIndex = 0;

    [Header("Events")]
    public UnityEvent onGameStarted;
    public UnityEvent onGameEnded;
    
    [Header("UI Input")]
    public EventSystem eventSystem;
    
    [Header("Game End")]
    public GameObject panelGameEnd;
    public TextMeshProUGUI textEnd;

    // 캐시
    [Header("Character Animators (Optional)")]
    public Animator playerAnimator;
    public Animator botAnimator;
    
    // 맵 랜덤생성 관련 변수
    [Header("Map Generator")]
    public Transform gridParent;    // 생성할 부모(예: Grid 오브젝트)
    public GameObject[] mapPrefabs; // 2가지 맵 프리팹을 넣어둠

    // 내부 상태
    private bool _isBattleRunning;
    private bool _isBattleEnded;

    private void Start()
    {
        GenerateRandomMap();
        
        StartGame();
    }

    public void OnClickRestart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    private void GenerateRandomMap()
    {
        if (mapPrefabs.Length == 0) return;

        // 0 ~ mapPrefabs.Length-1 중에서 랜덤 선택
        int randomIndex = Random.Range(0, mapPrefabs.Length);

        // 선택한 맵 프리팹 생성
        GameObject map = Instantiate(mapPrefabs[randomIndex], Vector3.zero, Quaternion.identity, gridParent);
        Tilemap newTileMap = map.GetComponent<Tilemap>();
        
        // Player, Bot에 신규 맵 정보 주입
        playerMap.tilemap = newTileMap;
        botMap.tilemap = newTileMap;
    }
    
    public void StartGame()
    {
        if (panelGameEnd) panelGameEnd.SetActive(false);
        
        _isBattleEnded = false;
        _isBattleRunning = false;

        // HP가 연결되어 있지 않으면 자동으로 찾기(옵션)
        if (!playerHealth && playerController)
            playerHealth = playerController.GetComponentInChildren<Health>();
        if (!enemyHealth && enemyController)
            enemyHealth = enemyController.GetComponentInChildren<Health>();

        onGameStarted?.Invoke();
        StartCoroutine(Co_RunGameLoop());
    }

    private IEnumerator Co_RunGameLoop()
    {
        // 컨트롤 비활성화(키보드/컨트롤러 입력 차단)
        SetControllersActive(false);

        CacheEventSystem();
        if (eventSystem) eventSystem.enabled = false;

        // 애니메이터 캐싱 및 카운트다운 시작 직전 고정
        CacheCharacterAnimators();

        // Player, Bot 애니메이터 세팅
        if (playerAnimator)
        {
            TryPlayAnimatorState(playerAnimator, idleStateName, animatorLayerIndex, 0f);
            playerAnimator.speed = 0f;
            if (playerAnimator.HasParameterOfType("isGrounded", AnimatorControllerParameterType.Bool))
                playerAnimator.SetBool("isGrounded", true);
        }
        if (botAnimator)
        {
            TryPlayAnimatorState(botAnimator, idleStateName, animatorLayerIndex, 0f);
            botAnimator.speed = 0f;
            if (botAnimator.HasParameterOfType("isGrounded", AnimatorControllerParameterType.Bool))
                botAnimator.SetBool("isGrounded", true);
        }

        // BGM 재생
        if (!string.IsNullOrEmpty(bgmName))
            SoundManager.Instance?.PlayBGM(bgmName);

        // 카운트다운 표시
        TextMeshProUGUI centerLabel = inGameUI ? inGameUI.centerTimerText : null;
        float remainingCountdown = readyCountdownSeconds;
        while (remainingCountdown > 0f)
        {
            if (centerLabel)
                centerLabel.text = Mathf.CeilToInt(remainingCountdown).ToString();
            yield return null;
            remainingCountdown -= Time.deltaTime;
        }

        // 카운트다운 종료 직후: 애니메이터 재개
        if (playerAnimator) playerAnimator.speed = 1f;
        if (botAnimator)  botAnimator.speed  = 1f;

        // "GO!" 표시
        if (centerLabel) centerLabel.text = "GO!";

        if (eventSystem) eventSystem.enabled = true;

        // 컨트롤 활성화
        SetControllersActive(true);

        // InGameUI 타이머 시작
        if (inGameUI) inGameUI.StartTimer(battleDurationSeconds);

        // "GO!" 잠깐 유지
        yield return new WaitForSeconds(0.4f);

        _isBattleRunning = true;

        // 배틀 루프(타임업 혹은 조기 종료까지)
        float elapsed = 0f;
        while (_isBattleRunning && !_isBattleEnded)
        {
            // 체력 체크(HP 0 즉시 종료)
            if (playerHealth && playerHealth.CurrentHP <= 0f)
            {
                EndGame("LOSE");
                break;
            }
            if (enemyHealth && enemyHealth.CurrentHP <= 0f)
            {
                EndGame("WIN");
                break;
            }

            elapsed += Time.deltaTime;
            if (elapsed >= battleDurationSeconds)
            {
                // 타임업 시 잔여 HP 비교
                string result = DecideResultByRemainingHp();
                EndGame(result);
                break;
            }

            yield return null;
        }
    }

    private string DecideResultByRemainingHp()
    {
        float player = (playerHealth ? playerHealth.CurrentHP : 0f);
        float enemy  = (enemyHealth  ? enemyHealth.CurrentHP  : 0f);
        if (Mathf.Approximately(player, enemy)) return "DRAW";
        return (player > enemy) ? "WIN" : "LOSE";
    }

    private void EndGame(string resultText)
    {
        // 게임 종료 확인
        if (_isBattleEnded) return;
        _isBattleEnded = true;
        _isBattleRunning = false;

        // 종료 패널 활성화
        if (panelGameEnd) panelGameEnd.SetActive(true);

        if (playerAnimator) playerAnimator.speed = 1f;
        if (botAnimator)  botAnimator.speed  = 1f;

        // 컨트롤러 비활성화 (이동 제한)
        SetControllersActive(false);

        if (inGameUI) inGameUI.StopTimer();

        textEnd.text = resultText;

        onGameEnded?.Invoke();
    }

    private void SetControllersActive(bool enabled)
    {
        if (playerController) playerController.enabled = enabled;
        if (enemyController)  enemyController.enabled  = enabled;
    }

    // Animator 캐싱: 비어 있으면 Controller에서 획득
    private void CacheCharacterAnimators()
    {
        if (!playerAnimator && playerController)
            playerAnimator = playerController.GetComponentInChildren<Animator>();
        if (!botAnimator && enemyController)
            botAnimator = enemyController.GetComponentInChildren<Animator>();
    }

    // 상태 존재 여부 확인 후 안전하게 재생
    private static bool TryPlayAnimatorState(Animator animator, string stateName, int layerIndex, float normalizedTime = 0f)
    {
        if (animator == null || string.IsNullOrEmpty(stateName)) return false;

        int stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(layerIndex, stateHash))
        {
            animator.Play(stateHash, layerIndex, normalizedTime);
            return true;
        }

        // "Base Layer." 등 접두 표기 실수 보정
        string trimmed = stateName.Replace("Base Layer.", string.Empty).Replace("BaseLayer.", string.Empty);
        if (trimmed != stateName)
        {
            int trimmedHash = Animator.StringToHash(trimmed);
            if (animator.HasState(layerIndex, trimmedHash))
            {
                animator.Play(trimmedHash, layerIndex, normalizedTime);
                return true;
            }
        }

        Debug.LogWarning($"[GameManager] Animator state not found: '{stateName}' on layer {layerIndex}. Skip Play().");
        return false;
    }
    
    private void CacheEventSystem()
    {
        if (!eventSystem)
            eventSystem = EventSystem.current;
    }
}

// Animator 제어 확장 메소드
internal static class AnimatorExtension
{
    public static bool HasParameterOfType(this Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (!animator) return false;
        foreach (var parameter in animator.parameters)
            if (parameter.type == type && parameter.name == name)
                return true;
        return false;
    }
}