// --------------------------------------------------------------------------------------
// PlayerControllerFSM
// - CharacterControllerBaseFSM 상속
// - 상태 바인딩: 초기/이동 상태는 PlayerMoveState, 정지 시 IdleAttackState로 전환
// - 이동 로직 자체는 PlayerMover2D가 담당 (수평 전용)
// --------------------------------------------------------------------------------------
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMover2D))]
public sealed class PlayerControllerFSM : CharacterControllerBaseFSM
{
    [Header("References")]
    [Tooltip("수평 이동 전용 컴포넌트")]
    public PlayerMover2D mover;

    // 내부 상태 캐시
    private ICharacterState cachedMoveState;

    protected override void Awake()
    {
        base.Awake();
        if (!mover) mover = GetComponent<PlayerMover2D>();
    }

    // 최초 진입 상태
    protected override ICharacterState CreateInitialState() => CreateMoveState();

    // IdleAttackState에서 호출 가능하도록 public override
    public override ICharacterState CreateMoveState()
    {
        if (cachedMoveState == null)
            cachedMoveState = new PlayerMoveState(this);
        return cachedMoveState;
    }
}