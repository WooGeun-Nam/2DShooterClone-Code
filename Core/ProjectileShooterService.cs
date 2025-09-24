using System.Collections;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 공통 발사 서비스(Idle/스킬에서 모두 호출).
/// - ObjectPool<GameObject> 사용(풀링)
/// - 목표 좌표 스냅샷 → 중력 포물선(비행시간 고정)으로 발사
/// - 충돌 시 Health.ApplyDamage() 후 즉시 풀로 반환
/// - 비행 중 Rigidbody2D 속도를 따라 화살 머리 회전
/// - 발사 순간에만 SoundManager SFX 재생
/// - 스킬별로 '프리팹 오버라이드' 하여 발사 가능
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectileShooterService : MonoBehaviour
{
    [Header("필수")]
    [Tooltip("발사 위치(총구)")]
    public Transform firePoint;

    [Tooltip("풀링할 투사체 프리팹 (Rigidbody2D + Collider2D 권장)")]
    public GameObject projectilePrefab;

    [Min(1)] public int poolInitialSize = 12;

    [Header("포물선(도달 시간 기반)")]
    [Tooltip("목표까지 도달하도록 맞출 비행 시간(초)")]
    [Min(0.1f)] public float defaultFlightTime = 0.8f;

    [Tooltip("투사체에 적용할 GravityScale(0 이하면 프리팹 설정 유지)")]
    public float overrideGravityScale = 0f;

    [Header("전투/충돌")]
    [Min(0f)] public float defaultDamage = 10f;
    [Tooltip("맞출 대상 레이어(예: Enemy)")]
    public LayerMask hitMask;

    [Tooltip("미스 시 자동 회수 시간(초)")]
    [Min(0.1f)] public float projectileLifeTime = 2.5f;

    [Header("화살 방향")]
    [Tooltip("비행 중에도 속도 벡터를 따라 지속 회전")]
    public bool continuousOrientToVelocity = true;

    [Tooltip("이 속도 이상일 때만 방향을 갱신(저속시 흔들림 방지)")]
    [Min(0f)] public float orientMinSpeed = 0.05f;

    [Tooltip("회전 보간 속도(높을수록 즉각 회전)")]
    [Min(0f)] public float orientLerpSpeed = 12f;

    [Tooltip("스프라이트가 +X가 전방이면 0, 위/다른 기준이면 각도 보정")]
    public float headingOffsetDeg = 0f;

    [Header("Audio (SoundManager)")]
    [Tooltip("발사 시 재생할 SFX 이름(확장자 제외). 비워두면 재생 안 함")]
    public string shootSfxName = "SFX_ArrowShot";
    
    [Header("지면 충돌/스틱")]
    [Tooltip("지면으로 인식할 레이어(예: Ground: TilemapCollider2D)")]
    public LayerMask groundMask;

    [Tooltip("지면에 '박히기' 상태 유지 시간(초)")]
    [Min(0.05f)] public float groundStickLifetime = 1.0f;

    [Tooltip("프레임 사이 터널링 방지용 선분 Raycast 사용")]
    public bool useSegmentRaycast = true;

    [Tooltip("선분 Raycast 충돌 여유(스킨) 거리")]
    [Min(0f)] public float segmentRaycastSkin = 0.02f;

    // 기본 풀(기본 프리팹용)
    private ObjectPool<GameObject> _pool;

    // [ADD] 프리팹별 풀 캐시(오버라이드 프리팹 대응)
    private Dictionary<GameObject, ObjectPool<GameObject>> _pools;

    void Awake()
    {
        if (!firePoint) firePoint = transform;

        Transform poolParent = null;

        // 기본 프리팹 풀
        _pool = new ObjectPool<GameObject>(projectilePrefab, poolInitialSize, poolParent);

        // 프리팹별 풀 캐시 초기화
        _pools = new Dictionary<GameObject, ObjectPool<GameObject>>();
        if (projectilePrefab != null) _pools[projectilePrefab] = _pool;
    }

    
    // ===========================
    // 발사 API
    // ===========================
    
    // 목표 트랜스폼을 스냅샷해서 발사 (기본 프리팹)
    public void FireAtTransform(Transform target, float? flightTime = null, float? damage = null)
    {
        if (!target) return;
        FireCore((Vector2)target.position, null, flightTime, damage);
    }

    // 목표 좌표를 스냅샷해서 발사 (기본 프리팹)
    public void FireAtPosition(Vector2 targetPosition, float? flightTime = null, float? damage = null)
    {
        FireCore(targetPosition, null, flightTime, damage);
    }
    
    // 프리팹 지정 발사 (Override)
    // 특정 프리팹으로 목표 트랜스폼을 향해 발사
    public void FireAtTransform(GameObject prefabOverride, Transform target, float? flightTime = null, float? damage = null)
    {
        if (!target) return;
        FireCore((Vector2)target.position, prefabOverride, flightTime, damage);
    }

    // 특정 프리팹으로 목표 좌표를 향해 발사
    public void FireAtPosition(GameObject prefabOverride, Vector2 targetPosition, float? flightTime = null, float? damage = null)
    {
        FireCore(targetPosition, prefabOverride, flightTime, damage);
    }

    // ===========================
    // 내부 코어
    // ===========================

    // [프리팹별 풀 얻기(없으면 생성)
    private ObjectPool<GameObject> GetPoolFor(GameObject prefab)
    {
        if (prefab == null) return _pool;                      // null이면 기본 프리팹용 풀
        if (prefab == projectilePrefab) return _pool;          // 기본 프리팹이면 기본 풀

        if (_pools.TryGetValue(prefab, out var pool))          // 이미 있으면 재사용
            return pool;

        // 새 프리팹이면 새 풀 생성(초기 용량은 동일 정책 사용)
        pool = new ObjectPool<GameObject>(prefab, poolInitialSize, null);
        _pools[prefab] = pool;
        return pool;
    }

    // 실제 발사 로직(프리팹 선택 포함)
    private void FireCore(Vector2 targetPosition, GameObject prefabOverride, float? flightTime, float? damage)
    {
        GameObject prefabToUse = prefabOverride != null ? prefabOverride : projectilePrefab;
        if (!prefabToUse || !firePoint) return;

        // 발사 순간 SFX (단 한 번)
        if (!string.IsNullOrEmpty(shootSfxName))
            SoundManager.Instance?.PlaySFX(shootSfxName);

        // 프리팹에 맞는 풀에서 꺼내 초기화
        var pool = GetPoolFor(prefabToUse);
        GameObject projectileObject = pool.Get();
        projectileObject.transform.SetPositionAndRotation(firePoint.position, Quaternion.identity);

        // 부모의 flip/scale 상속 방지
        projectileObject.transform.SetParent(null, true);
        projectileObject.transform.localScale = Vector3.one;

        // Rigidbody2D 확보 및 중력 세팅
        if (!projectileObject.TryGetComponent<Rigidbody2D>(out var projectileRigidbody))
            projectileRigidbody = projectileObject.AddComponent<Rigidbody2D>();

        if (overrideGravityScale > 0f) projectileRigidbody.gravityScale = overrideGravityScale;
        if (projectileRigidbody.gravityScale <= 0f) projectileRigidbody.gravityScale = 1f;

        projectileRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        projectileRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 초기 속도 계산 (비행 시간 고정)
        Vector2 startPosition = firePoint.position;
        float gravityMagnitude = Mathf.Abs(Physics2D.gravity.y * projectileRigidbody.gravityScale);
        float flightTimeSeconds = Mathf.Max(0.1f, flightTime ?? defaultFlightTime);

        float initialVelocityX = (targetPosition.x - startPosition.x) / flightTimeSeconds;
        float initialVelocityY = (targetPosition.y - startPosition.y + 0.5f * gravityMagnitude * flightTimeSeconds * flightTimeSeconds) / flightTimeSeconds;
        Vector2 initialVelocity = new Vector2(initialVelocityX, initialVelocityY);

        projectileRigidbody.linearVelocity = initialVelocity;

        // 런타임 컴포넌트 구성(없으면 자동 부착)
        var runtime = projectileObject.GetComponent<ProjectileRuntime>();
        if (!runtime) runtime = projectileObject.AddComponent<ProjectileRuntime>();
        runtime.Configure(
            pool: pool,                                 // ← 선택된 풀 전달
            damage: damage ?? defaultDamage,
            mask: hitMask,
            life: projectileLifeTime,
            orient: continuousOrientToVelocity,
            minSpeed: orientMinSpeed,
            lerpSpeed: orientLerpSpeed,
            headingOffsetDeg: headingOffsetDeg,
            ownerRoot: transform.root,
            ownerTag: gameObject.tag,
            groundMask: groundMask,
            groundStickLifetimeSeconds: groundStickLifetime,
            enableSegmentRaycast: useSegmentRaycast,
            segmentRaycastSkinDistance: segmentRaycastSkin
        );

        // 스폰 직후에도 초기 속도 방향을 바라보게 1회 정렬
        runtime.AlignToCurrentVelocityImmediate();
    }

    // ===== 내부 런타임 컴포넌트 =====
    [DisallowMultipleComponent]
    private sealed class ProjectileRuntime : MonoBehaviour
    {
        private ObjectPool<GameObject> _pool;
        private float _damage;
        private LayerMask _mask;
        private float _life;
        private bool _armed;

        private Transform _ownerRoot;
        private string _ownerTag;
        
        // 회전 관련
        private bool _orient;
        private float _minSpeed;
        private float _lerpSpeed;
        private float _headingOffsetDeg;

        private Rigidbody2D _rigidbody2D;
        
        private LayerMask _groundMask;
        private float _groundStickLifetimeSeconds;
        private bool _enableSegmentRaycast;
        private float _segmentRaycastSkinDistance;

        private Collider2D _collider2D;
        private Vector2 _previousPosition;
        private bool _isStuckToGround;
        
        private ProjectileObserver[] _observers;

        public void Configure(
            ObjectPool<GameObject> pool,
            float damage,
            LayerMask mask,
            float life,
            bool orient,
            float minSpeed,
            float lerpSpeed,
            float headingOffsetDeg,
            Transform ownerRoot,
            string ownerTag,
            LayerMask groundMask,
            float groundStickLifetimeSeconds,
            bool enableSegmentRaycast,
            float segmentRaycastSkinDistance
        )
        {
            _pool = pool;
            _damage = Mathf.Max(0f, damage);
            _mask = mask;
            _life = Mathf.Max(0.1f, life);
            _orient = orient;
            _minSpeed = Mathf.Max(0f, minSpeed);
            _lerpSpeed = Mathf.Max(0f, lerpSpeed);
            _headingOffsetDeg = headingOffsetDeg;
            _observers = GetComponentsInChildren<ProjectileObserver>(includeInactive: true);

            _ownerRoot = ownerRoot;
            _ownerTag = ownerTag;

            if (!_rigidbody2D) _rigidbody2D = GetComponent<Rigidbody2D>();
            if (!_collider2D) _collider2D = GetComponent<Collider2D>();

            _groundMask = groundMask;
            _groundStickLifetimeSeconds = Mathf.Max(0.05f, groundStickLifetimeSeconds);
            _enableSegmentRaycast = enableSegmentRaycast;
            _segmentRaycastSkinDistance = Mathf.Max(0f, segmentRaycastSkinDistance);

            _armed = true;
            _isStuckToGround = false;

            // 재사용 대비: 물리/콜라이더 초기화
            if (_rigidbody2D != null)
            {
                _rigidbody2D.simulated = true;
                _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            }
            if (_collider2D != null) _collider2D.enabled = true;

            _previousPosition = transform.position;

            StopAllCoroutines();
            StartCoroutine(Co_AutoReturn(_life));
        }

        // '지면에 박히기' 전환 루틴
        private void StickToSurface(Vector2 hitPoint, Vector2 hitNormal)
        {
            if (_isStuckToGround) return;

            // 위치/회전 고정: 화살 머리가 노멀 반대 방향 전환
            Vector2 adjustedPoint = hitPoint - hitNormal.normalized * 0.01f; // 살짝 파고들지 않게 오프셋
            transform.position = adjustedPoint;

            float angleDegrees = Mathf.Atan2(-hitNormal.y, -hitNormal.x) * Mathf.Rad2Deg + _headingOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

            // 물리/충돌 비활성화
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                _rigidbody2D.simulated = false;
            }
            if (_collider2D != null) _collider2D.enabled = false;

            _armed = false;               // 이후 적에게 피해 주지 않음
            _isStuckToGround = true;

            if (_observers != null) {
                foreach (var ob in _observers)
                    ob.OnProjectileStuckToGround(hitPoint, hitNormal);
            }
            
            // 1초(설정값) 뒤 반환
            StopAllCoroutines();
            StartCoroutine(Co_AutoReturn(_groundStickLifetimeSeconds));
        }
        
        public void AlignToCurrentVelocityImmediate()
        {
            if (!_orient || _rigidbody2D == null) return;
            Vector2 currentVelocity = _rigidbody2D.linearVelocity;
            if (currentVelocity.sqrMagnitude < _minSpeed * _minSpeed) return;

            float angleDegrees = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg + _headingOffsetDeg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);
        }
        
        private void Update()
        {
            // 프레임 사이 터널링 보정
            // 이전 : 현재 선분 Raycast로 Ground 선행 검출
            if (_enableSegmentRaycast && !_isStuckToGround && _rigidbody2D != null)
            {
                Vector2 currentPosition = transform.position;
                Vector2 segment = currentPosition - _previousPosition;
                float distance = segment.magnitude;

                if (distance > 0f)
                {
                    Vector2 direction = segment / distance;
                    RaycastHit2D hit = Physics2D.Raycast(
                        _previousPosition,
                        direction,
                        distance + _segmentRaycastSkinDistance,
                        _groundMask
                    );
                    if (hit.collider != null)
                    {
                        StickToSurface(hit.point, hit.normal);
                        _previousPosition = transform.position;
                        return;
                    }
                }

                _previousPosition = currentPosition;
            }

            // 비행 중에도 지속적으로 진행 방향을 보게 회전
            if (_orient && _rigidbody2D != null)
            {
                Vector2 currentVelocity = _rigidbody2D.linearVelocity;
                if (currentVelocity.sqrMagnitude >= _minSpeed * _minSpeed)
                {
                    float angle = Mathf.Atan2(currentVelocity.y, currentVelocity.x) * Mathf.Rad2Deg + _headingOffsetDeg;
                    var targetRotation = Quaternion.Euler(0f, 0f, angle);
                    if (_lerpSpeed > 0f)
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _lerpSpeed * Time.deltaTime);
                    else
                        transform.rotation = targetRotation;
                }
            }
        }

        // 생명주기가 끝나면 자동으로 풀 반환
        private IEnumerator Co_AutoReturn(float lifetimeSeconds)
        {
            yield return new WaitForSeconds(lifetimeSeconds);
            ReturnToPool();
        }

        // 타격 메소드
        private void TryHit(Collider2D other)
        {
            if (_ownerRoot != null && other.transform.root == _ownerRoot) return;
            if (!string.IsNullOrEmpty(_ownerTag) && other.CompareTag(_ownerTag)) return;
            
            if (!_armed) return;
            if (_mask.value != 0 && (((1 << other.gameObject.layer) & _mask) == 0)) return;

            var healthComponent = other.GetComponent<Health>();
            if (healthComponent != null && _damage > 0f)
            {
                healthComponent.ApplyDamage(_damage); // HP 감소 → InGameUI가 onHPChanged로 슬라이더/텍스트 갱신
            }

            Vector2 contactPoint = other.ClosestPoint(transform.position);
            
            _armed = false;
            
            if (_observers != null) {
                foreach (var ob in _observers)
                {
                    ob.OnProjectileDealtDamage(_damage, contactPoint);
                    ob.OnProjectileHitEnemy(other);
                }
            }
            
            ReturnToPool(); // 데미지 적용 직후 즉시 풀 반환
        }

        // 풀 반환 메소드
        private void ReturnToPool()
        {
            if (_rigidbody2D != null)
            {
                _rigidbody2D.linearVelocity = Vector2.zero;
                _rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                _rigidbody2D.simulated = true;
            }
            if (_collider2D != null) _collider2D.enabled = true;

            if (_pool != null) _pool.Return(gameObject);
            else gameObject.SetActive(false);
        }

        // Stay,Enter (대상과 겹치거나 부딪혔을 때 타격 메소드 호출
        private void OnTriggerStay2D(Collider2D other) => TryHit(other);
        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider == null) return;

            // Ground 레이어 라면 지면에 박히는 연출
            if (_groundMask.value != 0 && (((1 << collision.collider.gameObject.layer) & _groundMask) != 0))
            {
                // 접점 기준
                ContactPoint2D contact = collision.GetContact(0);
                StickToSurface(contact.point, contact.normal);
                return;
            }

            // 지면이 아니라면 타격 처리
            TryHit(collision.collider);
        }

        // 종료될 때 모든 코루틴 정리, 안전 제거
        private void OnDisable()
        {
            StopAllCoroutines();
            _armed = false;
        }
    }
}
