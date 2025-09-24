// --------------------------------------------------------------------------------------
// CharacterControllerBaseFSM
// - Player/Bot이 공통으로 쓰는 컨트롤러 베이스
// - 책임: 상대 Lazy-Resolve, 정지 판정, 애니 이벤트(Impact) 수신, 트리거/Locomotion 정리,
//         상태 전환 유틸 제공 (실제 상태는 외부 상태별 스크립트로 분리)
// --------------------------------------------------------------------------------------
using System.Collections;
using Skills;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class CharacterControllerBaseFSM : MonoBehaviour
{
    // ===== 공용 필드(인스펙터 호환 유지) =====
    [Header("필수 참조")]
    public ProjectileShooterService shooter;
    public Transform opponent;
    public string opponentTag = ""; // 공격 대상의 태그

    [Header("정지-자동사격 규칙")]
    [Tooltip("|vx| <= 이하면 '정지'로 간주 (정지 유지 시 자동 재사격)")]
    public float stopThreshold = 0.08f;

    [Header("애니메이션")]
    public Animator animator;          // 공격 재생용 Animator
    public string attackTrigger = "Fire";    // 공격 트리거
    [Tooltip("Attack 상태 판정에 사용할 태그/이름")]
    public bool useStateTag = true;
    public string attackStateNameOrTag = "Attack";
    public int attackLayerIndex = 0;

    [Header("Animator Names (강제 복귀용)")]
    public string locomotionStateName = "Locomotion";
    public int locomotionLayerIndex = 0;
    
    private TilemapRailClamp railClamp;
    protected bool isCastingSkill = false;
    protected SkillKind? currentCastingKind = null; // MultiShot, JumpShot 등
    
    // ====== Freeze(시간 정지) 유틸 ======
    private Coroutine freezeCoroutine;
    private float freezeRemainingSeconds;
    public bool isMovementFrozen;
    
    public bool IsGrounded => railClamp != null && railClamp.IsGrounded;

    // '착지 전까지 수평 이동 락' 플래그와 활성 메서드
    public bool lockHorizontalMoveUntilGrounded; // 기본값 false

    public void EnableHorizontalMoveLockUntilGrounded()
    {
        lockHorizontalMoveUntilGrounded = true;
    }
    
    public void BeginSkillCasting(SkillKind kind)
    {
        isCastingSkill = true;
        currentCastingKind = kind;
    }
    public void EndSkillCasting()
    {
        isCastingSkill = false;
        currentCastingKind = null;
    }
    public bool IsCastingSkill() => isCastingSkill;
    
    private bool skillEndFlag = false;
    public void NotifySkillEnd() { skillEndFlag = true; }
    public bool ConsumeSkillEndFlag()
    {
        if (!skillEndFlag) return false;
        skillEndFlag = false;
        return true;
    }

    public void OnAnimationSkillEndEvent()
    {
        if (!isCastingSkill) return;          // 기본공격일 땐 무시
        NotifySkillEnd();
    }
    
    public void OnHighAttackChargeEvent(float chargeSeconds)
    {
        if (!isCastingSkill) return;          // 기본공격일 땐 무시
        if (currentCastingKind != SkillKind.MultiShot) return; // 멀티샷일 때만 정지
        SoundManager.Instance.PlaySFX("SFX_Pulling_Arrow");
        CallChargeStart(chargeSeconds);
    }
    
    // ===== 내부 공용 자원 =====
    [System.NonSerialized] public Rigidbody2D rigidBody2D;

    // 상태 전환용 인터페이스(외부 파일의 상태들이 구현)
    public interface ICharacterState
    {
        void OnEnter();
        void OnUpdate();
        void OnFixedUpdate();
        void OnExit();
    }

    // 현재 상태 + 애니 Impact 플래그
    protected ICharacterState currentState;
    protected bool animationImpactFlag;

    // ===== 생명주기 =====
    protected virtual void Awake()
    {
        rigidBody2D = GetComponent<Rigidbody2D>();
        if (!animator) animator = GetComponentInChildren<Animator>();
        
        railClamp = GetComponent<TilemapRailClamp>();
    }

    protected virtual void Start()
    {
        ChangeState(CreateInitialState());
    }

    protected virtual void Update()
    {
        // 빙결상태면 Update 발생하지 않음
        if (isMovementFrozen)
        {
            if (isCastingSkill) // 스킬 사용중이라면 캔슬
                currentState?.OnUpdate();
            return;
        }
        
        // 상대 Lazy-Resolve
        if (!opponent && !string.IsNullOrEmpty(opponentTag))
        {
            var found = GameObject.FindWithTag(opponentTag);
            if (found) opponent = found.transform;
        }
        
        // Animator에 Ground 상태 반영
        if (animator && railClamp != null)
            animator.SetBool("IsGrounded", railClamp.IsGrounded);
        
        currentState?.OnUpdate();
    }

    protected virtual void FixedUpdate()
    {
        // 빙결상태에서는 물리갱신(State)도 중단
        if (isMovementFrozen) return;     
        
        // 공중에 있는 동안에는 수평 입력을 강제로 0으로
        if (lockHorizontalMoveUntilGrounded && railClamp != null && !railClamp.IsGrounded)
        {
            var playerMover = GetComponent<PlayerMover2D>();
            if (playerMover != null)
                playerMover.SetInput(0f);
        }

        // 착지 확인되면 락 해제
        if (lockHorizontalMoveUntilGrounded && railClamp != null && railClamp.IsGrounded)
            lockHorizontalMoveUntilGrounded = false;
        
        currentState?.OnFixedUpdate();
    }

    // 애니메이션 이벤트(예: Attack 9프레임)에서 호출
    public void NotifyAttackImpact() => animationImpactFlag = true;

    // ===== 파생 클래스가 구현해야 할 팩토리 메서드 =====
    protected abstract ICharacterState CreateInitialState();
    public abstract ICharacterState CreateMoveState(); // IdleAttackState가 호출할 수 있도록 public

    // ===== 상태 전환/유틸 =====
    public void ChangeState(ICharacterState next)
    {
        if (ReferenceEquals(next, currentState)) return;
        currentState?.OnExit();
        currentState = next;
        currentState?.OnEnter();
    }

    public bool IsStopped()
    {
        return Mathf.Abs(rigidBody2D.linearVelocity.x) <= stopThreshold;
    }

    public bool IsInAttack()
    {
        if (!animator) return false;
        var info = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
        return (useStateTag && info.IsTag(attackStateNameOrTag))
               || (!useStateTag && info.IsName(attackStateNameOrTag));
    }

    public void ResetAttackTrigger()
    {
        if (animator && !string.IsNullOrEmpty(attackTrigger))
            animator.ResetTrigger(attackTrigger);
    }

    public void TriggerAttackOnce()
    {
        if (animator && !string.IsNullOrEmpty(attackTrigger))
        {
            animator.ResetTrigger(attackTrigger);
            animator.SetTrigger(attackTrigger);
        }
    }

    public void ForceToLocomotion()
    {
        if (!animator) return;

        if (!string.IsNullOrEmpty(attackTrigger))
            animator.ResetTrigger(attackTrigger);

        if (!string.IsNullOrEmpty(locomotionStateName))
            animator.CrossFadeInFixedTime(locomotionStateName, 0.05f, locomotionLayerIndex, 0f);
    }
    
    // 애니메이션 임팩트 플래그를 소비(읽고 즉시 false로 초기화)
    public bool ConsumeImpactFlag()
    {
        if (animationImpactFlag)
        {
            animationImpactFlag = false;
            return true;
        }
        return false;
    }
    
    public void CallChargeStart(float sec)
    {
        if (animator) StartCoroutine(PauseAnimatorFor(animator, sec));
    }
    private IEnumerator PauseAnimatorFor(Animator anim, float sec)
    {
        float prev = anim.speed;
        anim.speed = 0f;
        yield return new WaitForSeconds(sec);
        anim.speed = prev;
    }
    
    public bool IsFacingRight()
    {
        // scale.x 플립을 기준으로 방향 판정
        return transform.localScale.x > 0f;
    }

    public Vector2 GetForwardDirection()
    {
        return IsFacingRight() ? Vector2.right : Vector2.left;
    }
    
    // FreezeArrow 관련 메서드
    public void FreezeFor(
        float seconds,
        bool lockMovementWhileFrozen = true,
        bool stopAnimatorWhileFrozen = true,
        bool forceKinematicWhileFrozen = true)
    {
        seconds = Mathf.Max(0.05f, seconds);

        // 이미 동결 중이면 남은 시간을 연장
        if (freezeCoroutine != null)
        {
            freezeRemainingSeconds = Mathf.Max(freezeRemainingSeconds, seconds);
            // 이동 잠금 플래그는 OR로 유지
            isMovementFrozen = isMovementFrozen || lockMovementWhileFrozen;
            return;
        }

        freezeCoroutine = StartCoroutine(Co_FreezeFor(seconds, lockMovementWhileFrozen, stopAnimatorWhileFrozen, forceKinematicWhileFrozen));
    }
    
    // 빙결상태 코루틴
    private IEnumerator Co_FreezeFor(
        float seconds,
        bool lockMovementWhileFrozen,
        bool stopAnimatorWhileFrozen,
        bool forceKinematicWhileFrozen)
    {
        freezeRemainingSeconds = seconds;

        // 이동 잠금
        isMovementFrozen = lockMovementWhileFrozen;

        // Animator 정지
        var animatorList = new System.Collections.Generic.List<Animator>();
        var animatorPrevSpeeds = new System.Collections.Generic.List<float>();
        if (stopAnimatorWhileFrozen)
        {
            GetComponentsInChildren(true, animatorList);
            for (int i = 0; i < animatorList.Count; i++)
            {
                Animator targetAnimator = animatorList[i];
                if (!targetAnimator) continue;
                animatorPrevSpeeds.Add(targetAnimator.speed);
                targetAnimator.speed = 0f;
            }
        }

        // Rigidbody2D 고정
        var rigidbodyList = new System.Collections.Generic.List<Rigidbody2D>();
        var prevBodyTypes = new System.Collections.Generic.List<RigidbodyType2D>();
        if (forceKinematicWhileFrozen)
        {
            GetComponentsInChildren(true, rigidbodyList);
            for (int i = 0; i < rigidbodyList.Count; i++)
            {
                Rigidbody2D targetRigidbody2D = rigidbodyList[i];
                if (!targetRigidbody2D) continue;
                prevBodyTypes.Add(targetRigidbody2D.bodyType);
                targetRigidbody2D.linearVelocity = Vector2.zero;
                targetRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }
        }

        // 카운트다운
        while (freezeRemainingSeconds > 0f)
        {
            freezeRemainingSeconds -= Time.deltaTime;
            yield return null;
        }

        // 복구
        if (stopAnimatorWhileFrozen)
        {
            for (int i = 0; i < animatorList.Count; i++)
            {
                Animator targetAnimator = animatorList[i];
                if (!targetAnimator) continue;
                float previous = (i < animatorPrevSpeeds.Count) ? animatorPrevSpeeds[i] : 1f;
                targetAnimator.speed = previous;
            }
        }
        if (forceKinematicWhileFrozen)
        {
            for (int i = 0; i < rigidbodyList.Count; i++)
            {
                Rigidbody2D targetRigidbody2D = rigidbodyList[i];
                if (!targetRigidbody2D) continue;
                RigidbodyType2D previousType = (i < prevBodyTypes.Count) ? prevBodyTypes[i] : RigidbodyType2D.Dynamic;
                targetRigidbody2D.bodyType = previousType;
            }
        }

        if (this is BotControllerFSM)
        {
            ForceToLocomotion();                 // 애니 트리거/레이어를 안전 상태로
            ChangeState(CreateMoveState());      // 랜덤 이동 상태로 재진입(자동 행동 재개)
        }

        isMovementFrozen = false;
        freezeCoroutine = null;
    }
}
