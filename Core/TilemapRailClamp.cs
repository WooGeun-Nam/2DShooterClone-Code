using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일맵 "바닥 행"에서 좌우 연속 타일 구간을 찾아 그 범위로 x를 클램프.
/// 발 위치를 Collider2D 기준으로 잡아 오차 최소화
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class TilemapRailClamp : MonoBehaviour
{
    [Header("References")]
    public Tilemap tilemap;

    [Header("Feet Sampling")]
    [Tooltip("Collider2D 하단을 발 위치로 사용할지 여부 (권장 ON)")]
    public bool useColliderFeet = true;
    [Tooltip("useColliderFeet가 꺼져 있을 때만 사용. Transform 기준 발 오프셋")]
    public Vector2 feetOffset = new Vector2(0f, -0.45f);
    [Tooltip("현 행에서 아래로 몇 행까지 내려가며 '채워진 타일'을 찾을지")]
    public int scanDownRows = 2;

    [Header("Clamp")]
    [Tooltip("경계에서 살짝 안쪽으로 여유")]
    public float skin = 0.06f;
    [Tooltip("좌우 스캔 최대 타일 수(성능 보호)")]
    public int scanLimit = 512;

    private Rigidbody2D _rigidBody;
    private Collider2D _collider;
    private int _cachedRowY;
    private bool _rowCached;
    private float _rowMinX, _rowMaxX;
    private float _cellHalf;
    
    public bool IsGrounded { get; private set; }

    void Awake()
    {
        _rigidBody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        if (!tilemap) tilemap = FindAnyObjectByType<Tilemap>();
        _cellHalf = tilemap ? tilemap.cellSize.x * 0.5f : 0.5f;
    }

    void FixedUpdate()
    {
        if (!tilemap) return;

        // 발 위치(월드) 계산
        Vector2 rbPos = _rigidBody.position;
        Vector3 feetWorld;
        if (useColliderFeet && _collider != null)
        {
            // Collider 하단을 기준
            float y = _collider.bounds.min.y + 0.02f;
            feetWorld = new Vector3(rbPos.x, y, 0f);
        }
        else
        {
            feetWorld = (Vector3)rbPos + (Vector3)feetOffset;
        }

        // 현 x에서 '실제 바닥 행(y)'을 찾는다
        Vector3Int cell = tilemap.WorldToCell(feetWorld);
        int rowY = FindGroundRowY(cell);
        if (!_rowCached || rowY != _cachedRowY)
        {
            _cachedRowY = rowY;
            _rowCached = true;
            ComputeRowBounds(_cachedRowY, cell.x);
        }
        else
        {
            // 같은 행이라도 좌우 구간은 x 기준으로 갱신
            ComputeRowBounds(_cachedRowY, cell.x);
        }

        // x 클램프 + 경계로 가는 속도 제거
        float minX = _rowMinX + skin;
        float maxX = _rowMaxX - skin;
        float clampedX = Mathf.Clamp(rbPos.x, minX, maxX);

        Vector2 v = _rigidBody.linearVelocity;
        if (rbPos.x <= minX && v.x < 0f) v.x = 0f;
        if (rbPos.x >= maxX && v.x > 0f) v.x = 0f;

        _rigidBody.position = new Vector2(clampedX, rbPos.y);
        _rigidBody.linearVelocity = v;
        
        // Ground 판정: 현 행(rowY)에서 '발 아래 타일의 윗면'과 접촉
        int groundX = FindNearestTileXOnRow(_cachedRowY, cell.x);
        if (groundX != int.MinValue)
        {
            float cellTopY = tilemap.GetCellCenterWorld(new Vector3Int(groundX, _cachedRowY, 0)).y
                             + tilemap.cellSize.y * 0.5f;

            // 콜라이더 하단과 타일 윗면의 근접 여부 + 하강/정지 중인 상황을 Ground로 판단
            float colliderBottomY = (_collider != null) ? _collider.bounds.min.y : feetWorld.y;
            const float contactEpsilon = 0.03f;

            bool closeToTop = (colliderBottomY - cellTopY) <= contactEpsilon;
            bool fallingOrStill = _rigidBody.linearVelocity.y <= 0.01f;

            IsGrounded = closeToTop && fallingOrStill;
        }
        else
        {
            IsGrounded = false;
        }
    }
    
    // 현 샘플 행(cell.y)에서 아래로 scanDownRows까지 내려가며
    // '채워진 타일이 실제로 있는' 가장 높은 행을 반환.
    // 아무것도 없으면 입력 행을 반환.
    private int FindGroundRowY(Vector3Int startCell)
    {
        int y = startCell.y;
        // 현재 행부터 확인
        if (HasAnyTileOnRow(y)) return y;

        // 아래로 스캔
        for (int d = 1; d <= scanDownRows; d++)
        {
            int yy = y - d;
            if (HasAnyTileOnRow(yy)) return yy;
        }
        return y;
    }

    private bool HasAnyTileOnRow(int rowY)
    {
        // 좌우로 넉넉히 살핀다 (scanLimit 보호)
        int baseX = tilemap.WorldToCell(transform.position).x;
        if (RowHasTileAt(rowY, baseX)) return true;

        for (int i = 1; i <= scanLimit; i++)
        {
            if (RowHasTileAt(rowY, baseX - i)) return true;
            if (RowHasTileAt(rowY, baseX + i)) return true;
            // 너무 멀리까지 안 가도록 적당히 끊어도 됨
            if (i > 64) break; // 안전 가드
        }
        return false;
    }

    private bool RowHasTileAt(int rowY, int x)
    {
        return tilemap.HasTile(new Vector3Int(x, rowY, 0));
    }
    
    // 주어진 행에서 기준 x 근처의 연속 타일 구간 [minX, maxX] 계산(월드 좌표)
    private void ComputeRowBounds(int rowY, int startX)
    {
        int baseX = FindNearestTileXOnRow(rowY, startX);
        if (baseX == int.MinValue)
        {
            // 행에 타일이 하나도 없으면 현재 위치 기준으로 최소 구간
            Vector3 w = tilemap.CellToWorld(new Vector3Int(startX, rowY, 0));
            _rowMinX = w.x - _cellHalf;
            _rowMaxX = w.x + _cellHalf;
            return;
        }

        int minXCell = baseX, maxXCell = baseX;

        // 왼쪽
        for (int i = 1; i <= scanLimit; i++)
        {
            int x = baseX - i;
            if (!tilemap.HasTile(new Vector3Int(x, rowY, 0))) break;
            minXCell = x;
        }
        // 오른쪽
        for (int i = 1; i <= scanLimit; i++)
        {
            int x = baseX + i;
            if (!tilemap.HasTile(new Vector3Int(x, rowY, 0))) break;
            maxXCell = x;
        }

        float minCenter = tilemap.GetCellCenterWorld(new Vector3Int(minXCell, rowY, 0)).x;
        float maxCenter = tilemap.GetCellCenterWorld(new Vector3Int(maxXCell, rowY, 0)).x;
        _rowMinX = minCenter - _cellHalf;
        _rowMaxX = maxCenter + _cellHalf;
    }

    private int FindNearestTileXOnRow(int rowY, int startX)
    {
        if (tilemap.HasTile(new Vector3Int(startX, rowY, 0)))
            return startX;

        for (int i = 1; i <= scanLimit; i++)
        {
            int left = startX - i;
            int right = startX + i;
            if (tilemap.HasTile(new Vector3Int(left, rowY, 0)))  return left;
            if (tilemap.HasTile(new Vector3Int(right, rowY, 0))) return right;
        }
        return int.MinValue;
    }
}
