using UnityEngine;
using System.Collections;

// Ensures necessary components are attached to the GameObject
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))] // Assuming you have an EnemyHealth script
public class SkeletonMaceAI : BaseEnemyAI // Inherit from your base AI class
{
    // --- State Machine ---
    private enum State { Idle, Chasing, Attacking, Hurt, Dead }
    private State currentState = State.Idle;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;

    [Header("Edge Detection")]
    [Tooltip("Assign layers considered 'walkable' (e.g., Ground, Platform).")]
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("How far down to check for ground from the origin point.")]
    [SerializeField] private float edgeRaycastDistance = 1.5f; // Needs to be long enough to hit ground from origin height
    [Tooltip("Horizontal offset from pivot (feet) for edge check.")]
    [SerializeField] private float edgeCheckHorizontalOffset = 0.5f; // Adjust based on skeleton width
    [Tooltip("Vertical offset from pivot (feet) for edge check start. INCREASE THIS IN INSPECTOR if pivot is at feet! Try 0.8 or 1.0.")]
    [SerializeField] private float edgeCheckVerticalOffset = 0.8f; // <<< INCREASED DEFAULT - TUNE IN INSPECTOR!

    [Header("Targeting")]
    [SerializeField] private float detectionRadius = 12f;
    [SerializeField] private float loseTargetRadius = 16f;
    [Tooltip("Layers for HOSTILE targets (e.g., Player, Ally).")]
    [SerializeField] private LayerMask targetLayerMask_Hostile;
    [Tooltip("Layers for ALLIED targets (e.g., Enemy). Used if skeleton becomes an ally.")]
    [SerializeField] private LayerMask targetLayerMask_Allied;
    [SerializeField] private float targetScanInterval = 0.4f;

    [Header("Mace Attack")]
    [SerializeField] private float attackRange = 1.5f; // Maybe increase slightly?
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackCooldown = 3.0f;
    [Tooltip("Optional: Effect to apply on hit (e.g., slow, stun). Assign EffectSO here.")]
    [SerializeField] private EffectSO attackEffect;
    [Tooltip("Optional: Assign child Collider2D hitbox for the mace swing.")]
    [SerializeField] private Collider2D attackHitbox;
    [SerializeField] private float attackWindup = 0.6f;
    private float nextAttackTime = 0f;
    private bool isAttacking = false;

    // --- Components & State ---
    private Rigidbody2D rb;
    private Animator animator;
    private EnemyHealth health;
    private Transform currentTarget = null;
    private LayerMask currentTargetLayerMask;
    private float targetScanTimer;
    private bool isFacingRight = true;
    private bool isAIActive = true;

    // --- Animation Hashes ---
    private readonly int hashIsMoving = Animator.StringToHash("IsMoving");
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
        if (groundLayer == 0) Debug.LogError($"GROUND LAYER NOT SET on {gameObject.name}! Assign Ground/Platform layers.", this);
        if (targetLayerMask_Hostile == 0) Debug.LogError($"TARGET LAYER MASK HOSTILE NOT SET on {gameObject.name}! Assign Player/Ally layers.", this);

        if (attackHitbox != null) attackHitbox.enabled = false;

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 1f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    void Start()
    {
        isAIActive = true;
        currentTargetLayerMask = targetLayerMask_Hostile;
        targetScanTimer = Random.Range(0f, targetScanInterval);
        nextAttackTime = Time.time;
        isFacingRight = transform.localScale.x > 0;
        currentState = State.Idle;
        FindNewTarget();
    }

    void FixedUpdate()
    {
        if (!isAIActive || currentState == State.Dead)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        if (currentState == State.Hurt)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        if (isAttacking)
        {
             if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        UpdateTargetScan();
        UpdateState(); // <<< Check logs from this function

        if (currentState != State.Hurt && !isAttacking)
        {
            ExecuteState();
        }

        UpdateAnimation();
    }

    // --- State Logic ---

    void UpdateTargetScan()
    {
        targetScanTimer -= Time.fixedDeltaTime;
        if (targetScanTimer <= 0f)
        {
            FindNewTarget();
            targetScanTimer = targetScanInterval;
        }
    }

    void FindNewTarget()
    {
        Transform potentialTarget = FindClosestTarget(currentTargetLayerMask);
        if (potentialTarget != null && currentTarget != potentialTarget)
        {
            currentTarget = potentialTarget;
        }
        else if (potentialTarget == null && currentTarget != null)
        {
            currentTarget = null;
        }
    }

    // Finds the closest target within the detection radius on the specified layer
    Transform FindClosestTarget(LayerMask layer)
    {
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, layer);
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;
        foreach (Collider2D col in potentialTargets)
        {
            if (col.gameObject == this.gameObject || !IsTargetAlive(col.transform)) continue;
            float distanceSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestTarget = col.transform;
            }
        }
        // --- FIX: Added missing return statement ---
        return closestTarget;
    }

    // Checks if a potential target is valid (active and has health component indicating it's alive)
    bool IsTargetAlive(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;
        PlayerHealth pHealth = target.GetComponent<PlayerHealth>();
        if (pHealth != null && pHealth.isDead) return false;
        EnemyHealth eHealth = target.GetComponent<EnemyHealth>();
        if (eHealth != null && !(eHealth.CurrentState == EnemyHealth.State.Alive_Enemy || eHealth.CurrentState == EnemyHealth.State.Alive_Ally)) return false;
        // --- FIX: Added missing return statement ---
        return true; // If none of the above conditions were met, the target is alive
    }


    void UpdateState()
    {
        if (currentState == State.Hurt || currentState == State.Dead) return;

        if (currentTarget == null)
        {
            if (currentState != State.Idle)
            {
                 // Debug.Log($"[{Time.frameCount}] {gameObject.name}: No target, switching to Idle.");
                 currentState = State.Idle;
            }
            return;
        }

        float distanceToTargetSqr = (currentTarget.position - transform.position).sqrMagnitude;
        // DEBUG: Log distance and range check
        // Debug.Log($"[{Time.frameCount}] {gameObject.name}: DistSqr={distanceToTargetSqr:F2}, AttackRangeSqr={attackRange * attackRange:F2}");

        if (distanceToTargetSqr > loseTargetRadius * loseTargetRadius)
        {
            // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Target too far ({Mathf.Sqrt(distanceToTargetSqr):F1}m > {loseTargetRadius}m), losing target and going Idle.");
            currentTarget = null;
            currentState = State.Idle;
            return;
        }

        bool canAttack = !isAttacking && Time.time >= nextAttackTime;
        // DEBUG: Log attack readiness
        // Debug.Log($"[{Time.frameCount}] {gameObject.name}: CanAttack Check: isAttacking={isAttacking}, Time={Time.time:F2}, nextAttackTime={nextAttackTime:F2} => canAttack={canAttack}");


        if (distanceToTargetSqr <= attackRange * attackRange) // In Attack Range
        {
            // Debug.Log($"[{Time.frameCount}] {gameObject.name}: In attack range.");
            if (canAttack)
            {
                if (currentState != State.Attacking)
                {
                    Debug.Log($"[{Time.frameCount}] {gameObject.name}: Can attack and not already attacking. Switching to Attacking state.");
                    currentState = State.Attacking;
                }
                 else {
                     // Debug.Log($"[{Time.frameCount}] {gameObject.name}: In range, can attack, already in Attacking state (waiting for ExecuteState).");
                 }
            }
            else // In range, but can't attack (cooldown/animating)
            {
                 // Debug.Log($"[{Time.frameCount}] {gameObject.name}: In range, but cannot attack yet (Cooldown/Animating). CurrentState={currentState}");
                if (currentState != State.Attacking && currentState != State.Idle)
                {
                    // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Switching to Idle while waiting for attack cooldown/animation.");
                    currentState = State.Idle; // Stop moving
                }
            }
        }
        else // Outside Attack Range
        {
             // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Outside attack range.");
            if (currentState != State.Chasing)
            {
                 // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Switching to Chasing state.");
                 currentState = State.Chasing;
            }
        }
    }

    void ExecuteState()
    {
        // DEBUG: Log which state is executing
        // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Executing State: {currentState}");
        switch (currentState)
        {
            case State.Idle:      Idle();        break;
            case State.Chasing:   ChaseTarget(); break;
            case State.Attacking: AttackTarget();break; // <<< Check logs from AttackTarget
        }
    }

    // --- Actions ---

    void Idle()
    {
         if (rb.bodyType == RigidbodyType2D.Dynamic && Mathf.Abs(rb.linearVelocity.x) > 0.01f)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }
    }

    void ChaseTarget()
    {
        if (currentTarget == null || rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Idle();
            return;
        }

        float horizontalInput = Mathf.Sign(currentTarget.position.x - rb.position.x);
        bool isGroundAhead = IsNearEdge(horizontalInput); // <<< Check the Debug.DrawRay for this in Scene view

         // DEBUG: Log edge detection result
         // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Chasing. Moving direction: {horizontalInput}. Ground Ahead: {isGroundAhead}");


        if (!isGroundAhead)
        {
            // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Edge detected while chasing, stopping horizontal movement.");
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop horizontal movement at edge
            return; // Don't apply movement this frame
        }

        // Apply movement only if ground is ahead
        float targetVelocityX = horizontalInput * moveSpeed;
        rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);
        FaceDirection(horizontalInput);
    }

    // Checks if there is ground ahead. Ensure offsets are tuned in Inspector!
    bool IsNearEdge(float moveDirection)
    {
        if (Mathf.Approximately(moveDirection, 0f)) return true; // Not moving horizontally, no edge check needed

        // Calculate origin based on pivot (feet) + offsets
        // Ensure edgeCheckVerticalOffset is large enough in Inspector if pivot is at feet
        Vector2 rayOrigin = (Vector2)transform.position
                            + (Vector2.right * moveDirection * edgeCheckHorizontalOffset)
                            + (Vector2.up * edgeCheckVerticalOffset); // Vertical offset from feet

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, edgeRaycastDistance, groundLayer);

        // Visualize the ray in Scene view (Color indicates hit)
        Debug.DrawRay(rayOrigin, Vector2.down * edgeRaycastDistance, (hit.collider != null) ? Color.green : Color.red, 0.1f);

        return hit.collider != null; // True if ground is hit
    }

    void AttackTarget()
    {
        // DEBUG: Log entry and check
        Debug.Log($"[{Time.frameCount}] {gameObject.name}: Entering AttackTarget. Can Attack: {!isAttacking && Time.time >= nextAttackTime}");
        if (currentTarget == null || rb == null || isAttacking || Time.time < nextAttackTime)
        {
            if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop before attack
        FaceTarget(currentTarget.position);

        isAttacking = true;
        // DEBUG: Log trigger
        Debug.Log($"[{Time.frameCount}] {gameObject.name}: Triggering Attack Animation.");
        animator.SetTrigger(hashAttack); // Trigger the mace swing animation
        nextAttackTime = Time.time + attackCooldown;

        if (attackHitbox != null) StartCoroutine(EnableHitboxDuringAttack());
        StartCoroutine(AttackAnimationEnd()); // Reset isAttacking flag after cooldown duration
    }

    // --- Damage & Effects Handling ---

    public void AnimationTrigger_DamageTarget()
    {
        // DEBUG: Log event call
        Debug.Log($"[{Time.frameCount}] {gameObject.name}: AnimationTrigger_DamageTarget called.");
        if (currentTarget == null || !isAIActive || currentState == State.Dead || !isAttacking) return;
        if (Vector2.Distance(transform.position, currentTarget.position) <= attackRange * 1.1f)
        { ApplyDamageAndEffects(currentTarget.gameObject); }
        else { Debug.Log($"[{Time.frameCount}] {gameObject.name}: Target moved out of range during animation event check."); }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (attackHitbox != null && collision.gameObject == attackHitbox.gameObject && isAttacking)
        {
            Debug.Log($"[{Time.frameCount}] {gameObject.name}: Hitbox Trigger with {collision.gameObject.name} on layer {LayerMask.LayerToName(collision.gameObject.layer)}.");
            if (collision.transform.IsChildOf(transform) || collision.gameObject == gameObject) return;
            if (((1 << collision.gameObject.layer) & currentTargetLayerMask) != 0)
            {
                // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Hitbox collided with valid target layer.");
                if(IsTargetAlive(collision.transform))
                {
                    // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Target {collision.gameObject.name} is alive. Applying damage via hitbox.");
                    ApplyDamageAndEffects(collision.gameObject);
                }
                // else { Debug.Log($"[{Time.frameCount}] {gameObject.name}: Target {collision.gameObject.name} is not alive."); }
            }
             // else { Debug.Log($"[{Time.frameCount}] {gameObject.name}: Hitbox collided with object on incorrect layer: {LayerMask.LayerToName(collision.gameObject.layer)}."); }
        }
    }

    void ApplyDamageAndEffects(GameObject targetObject)
    {
        // DEBUG: Log function entry
        Debug.Log($"[{Time.frameCount}] {gameObject.name}: ApplyDamageAndEffects called on target: {targetObject.name}");
        PlayerHealth playerHealth = targetObject.GetComponent<PlayerHealth>();
        EnemyHealth enemyHealth = targetObject.GetComponent<EnemyHealth>();
        float actualDamage = attackDamage;
        if (playerHealth != null)
        {
            Debug.Log($"[{Time.frameCount}] {gameObject.name}: Found PlayerHealth on {targetObject.name}. Calling TakeDamage({actualDamage}).");
             playerHealth.TakeDamage(actualDamage); // Make sure PlayerHealth.TakeDamage also has a log!
        }
        else if (enemyHealth != null)
        {
             // Debug.Log($"[{Time.frameCount}] {gameObject.name}: Found EnemyHealth on {targetObject.name}. Calling TakeDamage({actualDamage}).");
             enemyHealth.TakeDamage(actualDamage);
        }
        else
        {
             Debug.LogWarning($"[{Time.frameCount}] {gameObject.name}: No PlayerHealth or EnemyHealth found on target {targetObject.name}!");
        }
        if (attackEffect != null)
        {
             // Debug.Log($"[{Time.frameCount}] Applying effect '{attackEffect.name}' to {targetObject.name}");
             attackEffect.ApplyOnHit(this.gameObject, targetObject, actualDamage);
        }
    }

    // --- Coroutines for Attack Timing ---
    IEnumerator EnableHitboxDuringAttack()
    {
         if (attackHitbox == null) yield break;
        yield return new WaitForSeconds(attackWindup);
        if(isAttacking && currentState == State.Attacking) // Check state again
        {
            // Debug.Log($"[{Time.frameCount}] Enabling attack hitbox");
            attackHitbox.enabled = true;
        }
        yield return new WaitForSeconds(0.3f); // Adjust hitbox active duration based on animation
        if(attackHitbox != null)
        {
            attackHitbox.enabled = false;
            // Debug.Log($"[{Time.frameCount}] Disabling attack hitbox");
        }
        yield break; // Added missing yield break
    }
    IEnumerator AttackAnimationEnd()
    {
        float waitTime = Mathf.Max(0.1f, attackCooldown * 0.9f); // Wait most of the cooldown
        yield return new WaitForSeconds(waitTime);
        if (isAttacking) // Check if still attacking (might have been interrupted)
        {
            isAttacking = false;
            // Debug.Log($"[{Time.frameCount}] {gameObject.name} Attack cooldown period ended, isAttacking = false.");
        }
        yield break; // Added missing yield break
    }

    // --- Animation & Flipping ---
    void UpdateAnimation()
    {
        if (animator == null) return;
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isAttacking && currentState == State.Chasing;
        animator.SetBool(hashIsMoving, isMoving);
    }
    void FaceTarget(Vector2 targetPosition)
    {
        float directionX = targetPosition.x - transform.position.x;
        FaceDirection(directionX);
    }
    void FaceDirection(float horizontalDirection)
    {
         if (Mathf.Abs(horizontalDirection) > 0.01f)
        {
            bool shouldFaceRight = horizontalDirection > 0;
            if (shouldFaceRight != isFacingRight) Flip();
        }
    }
    void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
    }

    // --- Public Methods (Called by EnemyHealth or Base Class) ---
    public void TriggerHitAnimation()
    {
         if (!isAIActive || currentState == State.Dead || currentState == State.Hurt) return;
        currentState = State.Hurt;
        animator.SetTrigger(hashHit); // Play hit animation
        // Debug.Log($"[{Time.frameCount}] {gameObject.name} triggered Hit animation.");

        // Interrupt attack
        StopCoroutine(nameof(EnableHitboxDuringAttack));
        StopCoroutine(nameof(AttackAnimationEnd));
        if (attackHitbox != null) attackHitbox.enabled = false;
        isAttacking = false;

        StopCoroutine(nameof(HurtRecoveryCoroutine)); // Prevent multiple recovery routines
        StartCoroutine(nameof(HurtRecoveryCoroutine));
    }
    IEnumerator HurtRecoveryCoroutine()
    {
        yield return new WaitForSeconds(0.5f); // Adjust recovery time based on hit animation
        if(currentState == State.Hurt && currentState != State.Dead) // Check state hasn't changed
        {
            currentState = State.Idle; // Recover to Idle
            // Debug.Log($"[{Time.frameCount}] {gameObject.name} recovered from Hurt state.");
        }
         yield break; // Added missing yield break
    }
    public void TriggerDeath()
    {
        if (currentState == State.Dead) return;
        // Debug.Log($"[{Time.frameCount}] {gameObject.name} triggered Death.");
        currentState = State.Dead;
        isAIActive = false;
        isAttacking = false;
        if (attackHitbox != null) attackHitbox.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static; // Make static on death
            StartCoroutine(DisableColliderAfterDelay(0.1f)); // Disable collider shortly after
        }
        if (animator != null)
        {
            animator.SetBool(hashIsMoving, false);
            animator.SetBool(hashIsDead, true); // Trigger death animation
        }
        StopAllCoroutines(); // Stop AI routines
        Destroy(gameObject, 5f); // Destroy after animation plays (adjust time)
    }
    IEnumerator DisableColliderAfterDelay(float delay)
    {
         yield return new WaitForSeconds(delay);
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
         yield break; // Added missing yield break
    }

    // --- BaseEnemyAI Overrides ---
    public override void SetActiveAI(bool isActive)
    {
        if (isAIActive == isActive || currentState == State.Dead) return;
        isAIActive = isActive;
        // Debug.Log($"[{Time.frameCount}] {gameObject.name} AI Active set to: {isActive}");
        if (!isActive)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if(animator != null) animator.SetBool(hashIsMoving, false);
            StopAllCoroutines();
            if(currentState != State.Dead) currentState = State.Idle;
            isAttacking = false;
            if(attackHitbox != null) attackHitbox.enabled = false;
        }
        else
        {
            if(animator != null) animator.enabled = true;
            FindNewTarget();
            if(currentState != State.Dead) currentState = State.Idle;
        }
    }
    public override void SetTargetingMode(bool targetEnemies)
    {
        currentTargetLayerMask = targetEnemies ? targetLayerMask_Allied : targetLayerMask_Hostile;
        // string layerNames = ""; // Optional logging
        // for(int i=0; i<32; i++) { if(((1 << i) & currentTargetLayerMask) != 0) { layerNames += LayerMask.LayerToName(i) + " "; } }
        // Debug.Log($"[{Time.frameCount}] {gameObject.name} targeting mode set. Targets Enemies: {targetEnemies}. Active Mask Layers: {layerNames}");
        currentTarget = null;
        FindNewTarget();
    }

    // --- Gizmos ---
    // Draws gizmos relative to the actual pivot point (transform.position)
    void OnDrawGizmosSelected()
    {
        // --- Edge Detection Ray ---
        // Uses the actual calculation origin, adjust offsets in Inspector!
        if(rb != null)
        {
            // Determine direction based on isFacingRight for consistency
            float direction = isFacingRight ? 1f : -1f;
            // Calculate origin using the same logic as IsNearEdge
            Vector2 origin = (Vector2)transform.position
                             + (Vector2.right * direction * edgeCheckHorizontalOffset)
                             + (Vector2.up * edgeCheckVerticalOffset); // Vertical offset from feet

            // Simulate raycast for gizmo color (use try-catch as Physics2D calls might error outside play mode)
            bool groundAhead = false;
            try { groundAhead = Physics2D.Raycast(origin, Vector2.down, edgeRaycastDistance, groundLayer); } catch {}

            Gizmos.color = groundAhead ? Color.green : Color.red; // Green if ground detected, Red if not
            Gizmos.DrawRay(origin, Vector2.down * edgeRaycastDistance);
        }


        // --- Other Gizmos ---
        // Optionally use the visual offset method from previous response if you want spheres centered on sprite
        // Vector3 visualGizmoCenter = transform.position + Vector3.up * 1.0f; // Example offset
        Vector3 gizmoCenter = transform.position; // Draw relative to actual pivot

        // Targeting Radii
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(gizmoCenter, detectionRadius);
        Gizmos.color = Color.gray; Gizmos.DrawWireSphere(gizmoCenter, loseTargetRadius);
        // Attack Range
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(gizmoCenter, attackRange);
        // Line to Target
        if(currentTarget != null) { Gizmos.color = Color.cyan; Gizmos.DrawLine(gizmoCenter, currentTarget.position); }
    }
}
