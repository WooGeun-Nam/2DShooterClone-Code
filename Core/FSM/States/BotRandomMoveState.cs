// --------------------------------------------------------------------------------------
// BotRandomMoveState
// - 봇 전용 이동 상태 (좌/우 랜덤 + '거리' 랜덤)
// - 목표 거리 달성 시 BotIdleAttackState(랜덤 대기/공격)로 전환
// - 레일 끝(Clamp) 인지: 의도한 진행 방향으로 '전진이 멈춤'이 잠시 지속되면
//   → 경계에 닿은 것으로 간주하고 즉시 Idle로 전환
// - 실제 가속/감속/속도 계산은 PlayerMover2D가 담당 (수평 전용)
// --------------------------------------------------------------------------------------
using UnityEngine;

public sealed class BotRandomMoveState : CharacterControllerBaseFSM.ICharacterState
{
    private readonly CharacterControllerBaseFSM owner;
    private readonly PlayerMover2D mover;
    private readonly float moveInputMagnitude;     // 입력 크기
    private readonly bool autoFlipOnRailEdge;      // (보조) 스턱 시 자동 반전
    private readonly Vector2 moveDistanceRange;    // 이번 턴 이동할 '거리' 범위
    private readonly Vector2 idleDurationRange;    // 다음 Idle(대기/공격) 시간 범위

    // 내부 상태
    private float currentMoveInput;                // -mag 또는 +mag
    private float startX;                          // 이동 시작 X
    private float targetDistance;                  // 목표 이동 거리(절대값)

    // 진행/경계 감지
    private float lastX;                           // 직전 프레임 위치
    private float noProgressTimer;                 // 진행 정지 누적 시간

    // 튜닝 상수
    private const float MinProgressEpsilon  = 0.005f; // 이만큼도 못 움직이면 '전진 없음'으로 간주
    private const float BoundaryHoldTime    = 0.12f;  // 전진 없음이 이 시간 지속되면 '경계 히트'
    private const float StuckSpeedThreshold = 0.02f;  // (보조) 속도 기반 스턱 판정
    private const float StuckTimeToFlip     = 0.20f;  // (보조) 자동 반전 대기 시간

    private float stuckTimer; // 속도 기반 스턱 타이머

    public BotRandomMoveState(
        CharacterControllerBaseFSM owner,
        PlayerMover2D mover,
        float moveInputMagnitude,
        bool autoFlipOnRailEdge,
        Vector2 moveDistanceRange,
        Vector2 idleDurationRange)
    {
        this.owner = owner;
        this.mover = mover;
        this.moveInputMagnitude = Mathf.Abs(moveInputMagnitude);
        this.autoFlipOnRailEdge = autoFlipOnRailEdge;
        this.moveDistanceRange  = moveDistanceRange;
        this.idleDurationRange  = idleDurationRange;
    }

    public void OnEnter()
    {
        // 방향 랜덤 + 최소 입력 크기 보장
        float mag = Mathf.Max(0.1f, moveInputMagnitude);
        float dir = (Random.value < 0.5f ? -1f : 1f);
        currentMoveInput = dir * mag;

        // 거리 랜덤 결정
        startX = owner.transform.position.x;
        targetDistance = Random.Range(moveDistanceRange.x, moveDistanceRange.y);

        // 진행/경계 감지 초기화
        lastX = startX;
        noProgressTimer = 0f;

        // 스턱 타이머 초기화
        stuckTimer = 0f;

        // 혹시 남아있을지도 모를 공격 트리거 정리
        owner.ResetAttackTrigger();
    }

    public void OnUpdate()
    {
        float nowX = owner.transform.position.x;
        float movedFromStart = Mathf.Abs(nowX - startX);

        // 목표 거리 이동 시 Idle(공격) 상태로 전환
        if (movedFromStart >= targetDistance)
        {
            GoIdleWithRandomDelay();
            return;
        }

        // 레일 경계(Clamp) 간접 감지
        //    - 현재 프레임 이동량(Δx): nowX - lastX
        //    - 기존 방향(sign): Mathf.Sign(currentMoveInput)
        float delta = nowX - lastX;
        float intendedSign = Mathf.Sign(currentMoveInput);

        // 기존 방향으로의 실제 전진량
        float signedProgress = delta * intendedSign;

        if (signedProgress <= MinProgressEpsilon)
        {
            // 진행이 거의 없으면 누적
            noProgressTimer += Time.deltaTime;

            // 일정 시간 지속되면 경계에 닿았다고 판단 후 Idle 전환
            if (noProgressTimer >= BoundaryHoldTime)
            {
                GoIdleWithRandomDelay();
                return;
            }
        }
        else
        {
            // 정상 전진 중이면 타이머 리셋
            noProgressTimer = 0f;
        }

        // 속도 기반 스턱 탈출: 완전 끼임 케이스에 대비
        if (autoFlipOnRailEdge && Mathf.Abs(currentMoveInput) > 0.01f)
        {
            float absVx = Mathf.Abs(owner.rigidBody2D.linearVelocity.x);
            if (absVx < StuckSpeedThreshold)
            {
                stuckTimer += Time.deltaTime;
                if (stuckTimer >= StuckTimeToFlip)
                {
                    // 반전해도 '경계 끝' 로직이 우선 적용
                    currentMoveInput = -currentMoveInput;
                    currentMoveInput = Mathf.Sign(currentMoveInput)
                                     * Mathf.Max(0.5f, Mathf.Abs(currentMoveInput));
                    stuckTimer = 0f;

                    // 반전한 방향으로의 진행 감지 리셋
                    lastX = nowX;
                    noProgressTimer = 0f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        // 마지막 위치 갱신
        lastX = nowX;
    }

    public void OnFixedUpdate()
    {
        // 착지 전까지 이동 금지
        if (owner.lockHorizontalMoveUntilGrounded && !owner.IsGrounded)
        {
            if (mover != null) mover.SetInput(0f);
            return;
        }

        // 접지 시 이동 금지 해제
        if (owner.lockHorizontalMoveUntilGrounded && owner.IsGrounded)
            owner.lockHorizontalMoveUntilGrounded = false;

        // 랜덤 이동 입력 적용
        if (mover != null)
            mover.SetInput(currentMoveInput);
    }

    public void OnExit()
    {
        if (mover != null)
            mover.SetInput(0f);
    }

    // ─────────────────────────────────────────────────────────────────────────────

    private void GoIdleWithRandomDelay()
    {
        float idleDur = Random.Range(idleDurationRange.x, idleDurationRange.y); // 예: 1~2초
        owner.ChangeState(new BotIdleAttackState(owner, idleDur));
    }
}
