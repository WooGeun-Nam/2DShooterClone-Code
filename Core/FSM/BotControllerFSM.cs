// --------------------------------------------------------------------------------------
// BotControllerFSM
// - CharacterControllerBaseFSM 상속
// - 상태 바인딩: BotRandomMoveState(거리 기반 랜덤 이동) ↔ BotIdleAttackState(랜덤 대기/공격)
// - 실제 수평 이동은 PlayerMover2D가 담당 (수직 미간섭)
// --------------------------------------------------------------------------------------
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMover2D))]
public sealed class BotControllerFSM : CharacterControllerBaseFSM
{
    [Header("References")]
    [Tooltip("수평 이동 전용 컴포넌트 (필수)")]
    public PlayerMover2D mover;

    [Header("랜덤 이동 (거리 기반)")]
    [Tooltip("한 번에 이동할 '거리' 범위 (유니티 단위)")]
    public Vector2 moveDistanceRange = new Vector2(1.5f, 3.0f);

    [Tooltip("주입할 수평 입력 크기(보통 1.0 이상)")]
    public float moveInputMagnitude = 1.0f;

    [Tooltip("경계/걸림에서 일정 시간 속도가 거의 0이면 자동으로 방향 반전")]
    public bool autoFlipOnRailEdge = true;

    [Header("랜덤 Idle(공격) 파라미터")]
    [Tooltip("Idle(공격) 상태에서 머무는 대기 시간 범위(초)")]
    public Vector2 idleDurationRange = new Vector2(1.0f, 2.0f);

    // 내부 상태 캐시
    private ICharacterState cachedMoveState;

    protected override void Awake()
    {
        base.Awake();
        if (!mover) mover = GetComponent<PlayerMover2D>();
    }

    // 랜덤 이동부터 시작
    protected override ICharacterState CreateInitialState() => CreateMoveState();

    // BotIdleAttackState에서 호출 가능하도록 public override
    public override ICharacterState CreateMoveState()
    {
        if (cachedMoveState == null)
        {
            cachedMoveState = new BotRandomMoveState(
                owner: this,
                mover: mover,
                moveInputMagnitude: moveInputMagnitude,
                autoFlipOnRailEdge: autoFlipOnRailEdge,
                moveDistanceRange: moveDistanceRange,
                idleDurationRange: idleDurationRange
            );
        }
        return cachedMoveState;
    }
}
