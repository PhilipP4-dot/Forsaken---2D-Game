using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))]
public class ScytheWielderAI : BaseEnemyAI
{
    // --- State Machine ---
    private enum State { Idle, Chasing, Attacking, Hurt, Dead }
    private State currentState = State.Idle;
    private bool isHostile = false; // Starts friendly

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.0f;
    [SerializeField] private float friendlyWanderRadius = 5f;
    [SerializeField] private float friendlyWanderInterval = 4f;
    private Vector2 wanderTarget;
    private float wanderTimer;

    [Header("Edge Detection")]
    [Tooltip("IMPORTANT: Assign layers considered 'walkable' (e.g., Ground, Wall).")]
    [SerializeField] private LayerMask groundLayer; // <<< CHECK THIS IN INSPECTOR!!!
    [Tooltip("How far down to check for ground from the detection point.")]
    [SerializeField] private float edgeRaycastDistance = 2.0f; // Adjusted based on your fix
    [Tooltip("How far horizontally from the center to check for an edge.")]
    [SerializeField] private float edgeCheckHorizontalOffset = 0.5f;
    [Tooltip("How far vertically *above* the pivot/feet to start the edge check ray.")]
    [SerializeField] private float edgeCheckVerticalOffset = 0.1f; // Adjust based on sprite pivot

    [Header("Targeting (Hostile)")]
    [SerializeField] private float detectionRadius = 8f;
    [SerializeField] private float loseTargetRadius = 10f;
    [Tooltip("Layers for HOSTILE targets (Player, Ally).")]
    [SerializeField] private LayerMask targetLayerMask_Hostile;
    [Tooltip("Layers for ALLIED targets (Enemy).")]
    [SerializeField] private LayerMask targetLayerMask_Allied;
    [SerializeField] private string playerTag = "Player";
    #pragma warning disable 0414 // Suppress unused warning
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string allyTag = "Ally";
    #pragma warning restore 0414
    [SerializeField] private float targetScanInterval = 0.5f;

    [Header("Melee Attack (Hostile)")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 1.8f;
    [SerializeField] private float attackWindup = 0.3f;
    [Tooltip("Optional: Assign child Collider2D hitbox.")]
    [SerializeField] private Collider2D attackHitbox;
    private float nextAttackTime = 0f;
    private bool isAttacking = false;

    [Header("Ally Behaviour")]
    [SerializeField] private float followStopDistance = 2.0f;

    // --- Components & State ---
    private Rigidbody2D rb;
    private Animator animator;
    private EnemyHealth health;
    private Transform currentTarget = null;
    private Transform playerTransform = null;
    private LayerMask currentTargetLayerMask;
    private float targetScanTimer;
    private bool isFacingRight = true;
    private bool isAIActive = true;
    private Vector2 startingPosition;

    // --- Animation Hashes ---
    // *** IMPORTANT: Ensure these parameters exist in your Animator Controller! ***
    private readonly int hashIsMoving = Animator.StringToHash("IsMoving");
    private readonly int hashIsHostile = Animator.StringToHash("IsHostile");
    private readonly int hashAttack = Animator.StringToHash("Attack");
    private readonly int hashHit = Animator.StringToHash("Hit");
    private readonly int hashIsDead = Animator.StringToHash("IsDead");


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();

        if (rb == null) Debug.LogError($"{gameObject.name} missing Rigidbody2D!", this);
        if (animator == null) Debug.LogError($"{gameObject.name} missing Animator!", this);
        if (health == null) Debug.LogError($"{gameObject.name} missing EnemyHealth!", this);
        if (groundLayer == 0) Debug.LogError($"GROUND LAYER NOT SET on {gameObject.name}! Assign Ground/Wall layers in Inspector.", this);

        if (attackHitbox != null) attackHitbox.enabled = false;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
        startingPosition = transform.position;
    }

    void Start()
    {
        FindPlayer();
        SetTargetingMode(false); // Start friendly
        targetScanTimer = Random.Range(0f, targetScanInterval);
        nextAttackTime = Time.time;
        isFacingRight = transform.localScale.x > 0;
        PickNewWanderTarget();
        animator.SetBool(hashIsHostile, isHostile); // Requires IsHostile param in Animator
        currentState = State.Idle;
    }

    void FixedUpdate()
    {
        if (!isAIActive || currentState == State.Dead) { if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }
        if (currentState == State.Hurt) { if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }
        if (isAttacking) { if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }

        if (isHostile) { UpdateTargetScan(); UpdateHostileState(); ExecuteHostileState(); }
        else { UpdateFriendlyState(); ExecuteFriendlyState(); }
        UpdateAnimation();
    }

    // --- State Updates ---
    void UpdateHostileState()
    {
        if (currentState == State.Hurt || isAttacking) return;
        if (currentTarget == null) { if (currentState != State.Idle) { currentState = State.Idle; Debug.Log($"{gameObject.name} (Hostile) lost target, going Idle."); } return; }
        float distanceToTargetSqr = (currentTarget.position - transform.position).sqrMagnitude;
        if (distanceToTargetSqr > loseTargetRadius * loseTargetRadius) { currentTarget = null; currentState = State.Idle; Debug.Log($"{gameObject.name} (Hostile) target out of range, going Idle."); return; }
        if (distanceToTargetSqr <= attackRange * attackRange) { if (currentState != State.Attacking) { currentState = State.Attacking; Debug.Log($"{gameObject.name} (Hostile) entering Attack state."); } }
        else { if (currentState != State.Chasing) { currentState = State.Chasing; Debug.Log($"{gameObject.name} (Hostile) entering Chasing state."); } }
    }
    void UpdateFriendlyState()
    {
        if (currentState == State.Hurt) return;
        if (Vector2.Distance(transform.position, wanderTarget) < 0.5f) { if (currentState != State.Idle) { currentState = State.Idle; wanderTimer = friendlyWanderInterval + Random.Range(-1f, 1f); Debug.Log($"{gameObject.name} (Friendly) reached wander target, idling."); } wanderTimer -= Time.fixedDeltaTime; if (wanderTimer <= 0) { PickNewWanderTarget(); currentState = State.Chasing; Debug.Log($"{gameObject.name} (Friendly) finished idling, wandering to new target."); } }
        else { if (currentState != State.Chasing) { currentState = State.Chasing; Debug.Log($"{gameObject.name} (Friendly) moving to wander target."); } }
    }

    // --- State Execution ---
    void ExecuteHostileState() { switch (currentState) { case State.Idle: HostileIdle(); break; case State.Chasing: ChaseTarget(currentTarget); break; case State.Attacking: AttackTarget(); break; } }
    void ExecuteFriendlyState() { switch (currentState) { case State.Idle: FriendlyIdle(); break; case State.Chasing: ChaseTarget(null, wanderTarget); break; } }

    // --- State Actions ---
    void HostileIdle() { if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); }
    void FriendlyIdle() { if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); }

    void ChaseTarget(Transform targetTransform = null, Vector2? targetPosition = null)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;
        Vector2 destination; float horizontalInput = 0f;
        if (targetTransform != null) { destination = targetTransform.position; horizontalInput = Mathf.Sign(destination.x - rb.position.x); }
        else if (targetPosition.HasValue) { destination = targetPosition.Value; horizontalInput = Mathf.Sign(destination.x - rb.position.x); }
        else { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }
        if (isHostile && targetTransform != null && Vector2.Distance(rb.position, destination) <= attackRange) { if (currentState != State.Attacking) currentState = State.Attacking; rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }
        if (!isHostile && targetPosition.HasValue && Vector2.Distance(rb.position, destination) < 0.5f) { if (currentState != State.Idle) currentState = State.Idle; rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); return; }

        bool isGroundAhead = IsNearEdge(horizontalInput);
        if (!isGroundAhead) { rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); if (!isHostile) { PickNewWanderTarget(); currentState = State.Chasing; } else { currentState = State.Idle; } return; }

        float targetVelocityX = horizontalInput * moveSpeed;
        rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);
        // Debug.Log($"[ChaseTarget] Applying Velocity: {rb.velocity}"); // Can comment this out if console gets spammy
        FaceDirection(horizontalInput);
    }

    bool IsNearEdge(float moveDirection)
    {
        if (Mathf.Approximately(moveDirection, 0f)) return true;
        Vector2 rayOrigin = (Vector2)transform.position + (Vector2.right * moveDirection * edgeCheckHorizontalOffset) + (Vector2.up * edgeCheckVerticalOffset);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, edgeRaycastDistance, groundLayer); // Using your fixed distance
        string hitName = (hit.collider != null) ? hit.collider.name : "NULL"; string hitLayerName = (hit.collider != null) ? LayerMask.LayerToName(hit.collider.gameObject.layer) : "N/A";
        // Debug.Log($"[EdgeCheck] Checking Dir:{moveDirection:F1} | Origin:{rayOrigin:F2} | Dist:{edgeRaycastDistance} | GroundMask(Value):{groundLayer.value} | Hit:'{hitName}' on Layer:'{hitLayerName}'"); // Can comment out if working
        Debug.DrawRay(rayOrigin, Vector2.down * edgeRaycastDistance, (hit.collider != null) ? Color.green : Color.red, 0.1f);
        return hit.collider != null;
    }

    void AttackTarget()
    {
        if (currentTarget == null || rb == null || isAttacking) return;
        if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        FaceTarget(currentTarget.position);
        if (Time.time >= nextAttackTime) { isAttacking = true; animator.SetTrigger(hashAttack); nextAttackTime = Time.time + attackCooldown; Debug.Log($"{gameObject.name} triggered Attack animation."); if (attackHitbox != null) StartCoroutine(EnableHitboxDuringAttack()); StartCoroutine(AttackAnimationEnd()); }
    }

    public void AnimationTrigger_DamageTarget() { if (!isHostile || currentTarget == null || !isAIActive || currentState == State.Dead || !isAttacking) return; Debug.Log($"{gameObject.name} Attack Animation Event: Attempting Damage."); if (Vector2.Distance(transform.position, currentTarget.position) <= attackRange * 1.1f) { Debug.Log($"Dealing {attackDamage} melee damage to {currentTarget.name}"); PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>(); EnemyHealth enemyHealth = currentTarget.GetComponent<EnemyHealth>(); if (playerHealth != null) playerHealth.TakeDamage(attackDamage); else if (enemyHealth != null && (enemyHealth.CurrentState == EnemyHealth.State.Alive_Ally || enemyHealth.CurrentState == EnemyHealth.State.Alive_Enemy)) enemyHealth.TakeDamage(attackDamage); } else { Debug.Log($"{gameObject.name} Attack Animation Event: Target moved out of range."); } }
    IEnumerator EnableHitboxDuringAttack() { if (attackHitbox == null) yield break; yield return new WaitForSeconds(attackWindup); if(isAttacking) { Debug.Log("Enabling attack hitbox"); attackHitbox.enabled = true; } yield return new WaitForSeconds(0.2f); if(attackHitbox != null) attackHitbox.enabled = false; Debug.Log("Disabling attack hitbox"); }
    void OnTriggerEnter2D(Collider2D collision) { if (attackHitbox != null && collision.gameObject == attackHitbox.gameObject && isAttacking) { Debug.Log($"{gameObject.name} Attack Hitbox detected collision with {collision.gameObject.name}"); if (collision.transform.IsChildOf(transform) || collision.gameObject == gameObject) return; bool validTarget = false; if (isHostile && (collision.CompareTag(playerTag) || collision.CompareTag(allyTag))) { validTarget = true; } if(validTarget) { Debug.Log($"Dealing {attackDamage} melee damage to {collision.gameObject.name} via Hitbox"); PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>(); EnemyHealth enemyHealth = collision.GetComponent<EnemyHealth>(); if (playerHealth != null) playerHealth.TakeDamage(attackDamage); else if (enemyHealth != null && (enemyHealth.CurrentState == EnemyHealth.State.Alive_Ally || enemyHealth.CurrentState == EnemyHealth.State.Alive_Enemy)) enemyHealth.TakeDamage(attackDamage); } } }
    IEnumerator AttackAnimationEnd() { float attackAnimLength = 0.5f; try { AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0); if(stateInfo.IsName("Scythe_Attack")) { attackAnimLength = stateInfo.length; } } catch {} yield return new WaitForSeconds(attackAnimLength); isAttacking = false; if(currentState != State.Dead && currentState != State.Hurt) { currentState = State.Idle; } Debug.Log($"{gameObject.name} Attack animation finished, isAttacking = false."); }
    void PickNewWanderTarget() { wanderTarget = startingPosition + Random.insideUnitCircle * friendlyWanderRadius; Debug.Log($"{gameObject.name} wandering towards {wanderTarget}"); }
    void FindPlayer() { GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag); if (playerObj != null) { playerTransform = playerObj.transform; } else { Debug.LogWarning($"{gameObject.name} could not find Player!", this); } }
    void UpdateTargetScan() { targetScanTimer -= Time.fixedDeltaTime; if (targetScanTimer <= 0f) { FindNewTarget(); targetScanTimer = targetScanInterval; } }
    void FindNewTarget() { if (!isHostile) { currentTarget = null; return; } Transform potentialTarget = FindClosestTarget(currentTargetLayerMask); if (potentialTarget != null && currentTarget != potentialTarget) { Debug.Log($"{gameObject.name} (Hostile) found new target: {potentialTarget.name}"); currentTarget = potentialTarget; } else if (potentialTarget == null && currentTarget != null) { Debug.Log($"{gameObject.name} (Hostile) lost target."); currentTarget = null; } }
    Transform FindClosestTarget(LayerMask layer) { Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, layer); Transform closestTarget = null; float minDistanceSqr = Mathf.Infinity; foreach (Collider2D col in potentialTargets) { if (col.gameObject == this.gameObject || !IsTargetAlive(col.transform)) continue; float distanceSqr = (col.transform.position - transform.position).sqrMagnitude; if (distanceSqr < minDistanceSqr) { minDistanceSqr = distanceSqr; closestTarget = col.transform; } } return closestTarget; }
    bool IsTargetAlive(Transform target) { if (target == null || !target.gameObject.activeInHierarchy) return false; PlayerHealth pHealth = target.GetComponent<PlayerHealth>(); if (pHealth != null && pHealth.isDead) return false; EnemyHealth eHealth = target.GetComponent<EnemyHealth>(); if (eHealth != null && !(eHealth.CurrentState == EnemyHealth.State.Alive_Enemy || eHealth.CurrentState == EnemyHealth.State.Alive_Ally)) return false; return true; }
    void UpdateAnimation() { if (animator == null) return; bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f; animator.SetBool(hashIsMoving, isMoving); }
    void FaceTarget(Vector2 targetPosition) { float directionX = targetPosition.x - transform.position.x; FaceDirection(directionX); }
    void FaceDirection(float horizontalDirection) { if (Mathf.Abs(horizontalDirection) > 0.01f) { bool shouldFaceRight = horizontalDirection > 0; if (shouldFaceRight != isFacingRight) { Flip(); } } }
    void Flip() { isFacingRight = !isFacingRight; Vector3 theScale = transform.localScale; theScale.x *= -1; transform.localScale = theScale; }

    // --- Public Methods ---
    public void TriggerHitAnimation() // Called by EnemyHealth.TakeDamage
    {
        if (!isAIActive || currentState == State.Dead || currentState == State.Hurt) return;
        currentState = State.Hurt; animator.SetTrigger(hashHit); Debug.Log($"{gameObject.name} triggered Hit animation.");
        if (!isHostile)
        {
            isHostile = true;
            animator.SetBool(hashIsHostile, true); // Requires IsHostile param in Animator
            Debug.LogWarning($"{gameObject.name} HAS BECOME HOSTILE!"); FindNewTarget();
            StopCoroutine(nameof(HurtRecoveryCoroutine)); StartCoroutine(nameof(HurtRecoveryCoroutine));
        } else { StopCoroutine(nameof(HurtRecoveryCoroutine)); StartCoroutine(nameof(HurtRecoveryCoroutine)); }
    }

    IEnumerator HurtRecoveryCoroutine()
    {
        yield return new WaitForSeconds(0.5f); // Hurt animation duration
        if(currentState == State.Hurt && currentState != State.Dead) { currentState = State.Idle; Debug.Log($"{gameObject.name} recovered from Hurt state. Returning to Idle."); }
    }

    public void TriggerDeath() // Called by EnemyHealth
    {
        if (currentState == State.Dead) return; Debug.Log($"{gameObject.name} triggered Death."); currentState = State.Dead; isAIActive = false; isAttacking = false;
        if (attackHitbox != null) attackHitbox.enabled = false; if (rb != null) { rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Static; StartCoroutine(DisableColliderAfterDelay(0.1f)); }
        if (animator != null) { animator.SetBool(hashIsMoving, false); animator.SetBool(hashIsDead, true); } // Requires IsDead param in Animator
        StopAllCoroutines(); Destroy(gameObject, 5f);
    }

    IEnumerator DisableColliderAfterDelay(float delay) { yield return new WaitForSeconds(delay); Collider2D col = GetComponent<Collider2D>(); if (col != null) col.enabled = false; }

    public override void SetActiveAI(bool isActive)
    {
        if (isAIActive == isActive || currentState == State.Dead) return; isAIActive = isActive; Debug.Log($"{gameObject.name} AI Active set to: {isActive}");
        if (!isActive) { if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); if(animator != null) animator.SetBool(hashIsMoving, false); StopAllCoroutines(); if(currentState != State.Dead) currentState = State.Idle; }
        else { if(animator != null) animator.enabled = true; if(isHostile) FindNewTarget(); else PickNewWanderTarget(); if(currentState != State.Dead) currentState = State.Idle; }
    }

    public override void SetTargetingMode(bool targetEnemies) // True = target enemies (Ally mode), False = target player/allies (Hostile mode)
    {
        currentTargetLayerMask = targetEnemies ? targetLayerMask_Allied : targetLayerMask_Hostile; bool becomingHostile = !targetEnemies;
        if (isHostile != becomingHostile) { isHostile = becomingHostile; animator.SetBool(hashIsHostile, isHostile); Debug.Log($"{gameObject.name} Hostility set to: {isHostile}"); } // Requires IsHostile param in Animator
        string layerNames = ""; for(int i=0; i<32; i++) { if(((1 << i) & currentTargetLayerMask) != 0) { layerNames += LayerMask.LayerToName(i) + " "; } } Debug.Log($"{gameObject.name} targeting mode set. Targets Enemies: {targetEnemies}. Layers: {layerNames}");
        currentTarget = null; if (playerTransform == null) FindPlayer(); if(isHostile) FindNewTarget(); else { currentState = State.Idle; PickNewWanderTarget(); }
    }

    // --- Gizmos ---
    void OnDrawGizmosSelected()
    {
        // Draw Edge Detection Ray
        if(rb != null) {
            float direction = isFacingRight ? 1f : -1f;
            Vector2 hOffset = Vector2.right * direction * edgeCheckHorizontalOffset;
            Vector2 vOffset = Vector2.up * edgeCheckVerticalOffset; // Use the adjusted offset
            Vector2 origin = (Vector2)transform.position + hOffset + vOffset;
            // Use try-catch for safety in editor if groundLayer isn't set yet
            bool groundAhead = false;
            try { groundAhead = Physics2D.Raycast(origin, Vector2.down, edgeRaycastDistance, groundLayer); } catch {}
            Gizmos.color = groundAhead ? Color.green : Color.red;
            Gizmos.DrawRay(origin, Vector2.down * edgeRaycastDistance);
        }

        // Other Gizmos
        Gizmos.color = isHostile ? Color.red : Color.cyan; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.gray; Gizmos.DrawWireSphere(transform.position, loseTargetRadius);
        if (isHostile) { Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, attackRange); }
        else { Gizmos.color = Color.green; Vector2 center = Application.isPlaying ? startingPosition : (Vector2)transform.position; Gizmos.DrawWireSphere(center, friendlyWanderRadius); if(Application.isPlaying) Gizmos.DrawLine(transform.position, wanderTarget); }
        try { if(health != null && health.CurrentState == EnemyHealth.State.Alive_Ally) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(transform.position, followStopDistance); } } catch {}
        if(currentTarget != null && isHostile) { Gizmos.color = Color.red; Gizmos.DrawLine(transform.position, currentTarget.position); }
    }
}
