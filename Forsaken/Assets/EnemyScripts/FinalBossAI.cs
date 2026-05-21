using UnityEngine;
using System.Collections;

// Ensures necessary components are attached to the GameObject
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))]
public class FinalBossAI : BaseEnemyAI // Inherits from a base AI class (assumed)
{
    // --- State Machine ---
    // Defines the possible states the boss can be in
    private enum State { Idle, Chasing, Attacking, Hurt, Dead }
    private State currentState = State.Idle; // Initial state

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f; // Speed at which the boss moves

    [Header("Edge Detection")]
    [Tooltip("IMPORTANT: Assign layers considered 'walkable' (e.g., Ground, Wall). CHECK THIS IN INSPECTOR!")]
    [SerializeField] private LayerMask groundLayer; // Layers the boss considers ground to walk on
    [Tooltip("How far down to check for ground.")]
    [SerializeField] private float edgeRaycastDistance = 2.0f; // Length of the raycast to check for edges
    [Tooltip("Horizontal offset from center for edge check.")]
    [SerializeField] private float edgeCheckHorizontalOffset = 0.6f; // How far sideways the edge check ray starts
    [Tooltip("Vertical offset from pivot for edge check start.")]
    [SerializeField] private float edgeCheckVerticalOffset = 0.1f; // How far up/down the edge check ray starts

    [Header("Targeting")]
    [SerializeField] private float detectionRadius = 15f; // How far the boss can initially detect targets
    [SerializeField] private float loseTargetRadius = 20f; // If the target goes beyond this distance, the boss loses track
    [Tooltip("Layers for HOSTILE targets (Player, Ally). Boss usually uses this.")]
    [SerializeField] private LayerMask targetLayerMask_Hostile; // Layers containing targets the boss normally attacks
    [Tooltip("Layers for ALLIED targets (Enemy). Used if boss becomes an ally.")]
    [SerializeField] private LayerMask targetLayerMask_Allied; // Layers containing targets if the boss switches sides (e.g., charm effect)
    #pragma warning disable 0414 // Suppress unused warning if tags aren't directly used in *this* script but might be by base or other systems
    [SerializeField] private string playerTag = "Player"; // Tag for player objects
    [SerializeField] private string allyTag = "Ally";     // Tag for ally objects
    #pragma warning restore 0414
    [SerializeField] private float targetScanInterval = 0.3f; // How often the boss scans for the closest target

    [Header("Melee Attack")]
    [SerializeField] private float attackRange = 2.0f; // How close the target needs to be for the boss to attack
    [SerializeField] private float attackDamage = 25f; // Base damage of the melee attack
    [SerializeField] private float attackCooldown = 2.5f; // Time between attacks
    [Tooltip("Force applied to player on hit.")]
    [SerializeField] private float knockbackForce = 60f; // <<< ADJUST THIS VALUE (Increased default based on logs)
    [Tooltip("Optional: Effect to apply on hit (e.g., slow, burn). Assign EffectSO here.")]
    [SerializeField] private EffectSO attackEffect; // ScriptableObject defining any status effect applied on hit
    [Tooltip("Optional: Assign child Collider2D hitbox.")]
    [SerializeField] private Collider2D attackHitbox; // Specific collider used for attack detection (can be null)
    [SerializeField] private float attackWindup = 0.5f; // Delay before the hitbox activates/damage is applied in the animation
    private float nextAttackTime = 0f; // Timestamp when the next attack is available
    private bool isAttacking = false; // Flag specifically for tracking if the attack *animation* is playing

    // --- Components & State ---
    private Rigidbody2D rb; // Reference to the Rigidbody2D component for physics
    private Animator animator; // Reference to the Animator component for animations
    private EnemyHealth health; // Reference to the EnemyHealth script for health management
    private Transform currentTarget = null; // The current target the boss is focused on
    private LayerMask currentTargetLayerMask; // The layer mask currently being used for targeting
    private float targetScanTimer; // Timer for the target scanning interval
    private bool isFacingRight = true; // Tracks the boss's facing direction
    private bool isAIActive = true; // Flag to enable/disable the AI logic

    // --- Animation Hashes ---
    // Pre-calculating hash IDs for animator parameters is more efficient than using strings repeatedly
    private readonly int hashIsMoving = Animator.StringToHash("IsMoving");
    private readonly int hashAttack = Animator.StringToHash("Attack");
    private readonly int hashHit = Animator.StringToHash("Hit");
    private readonly int hashIsDead = Animator.StringToHash("IsDead");

    // Called when the script instance is being loaded
    void Awake()
    {
        // Get references to required components
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();

        // --- Error Checking ---
        if (rb == null) Debug.LogError($"{gameObject.name} missing Rigidbody2D!", this);
        if (animator == null) Debug.LogError($"{gameObject.name} missing Animator!", this);
        if (health == null) Debug.LogError($"{gameObject.name} missing EnemyHealth!", this);

        // Check if ground layer is assigned in the inspector, log error if not
        if (groundLayer == 0)
        {
            Debug.LogError($"GROUND LAYER NOT SET on {gameObject.name}! Assign Ground/Wall layers in Inspector.", this);
        }
        else // Log the names of the layers included in the mask for easier debugging
        {
            string groundLayerNames = "";
            for (int i = 0; i < 32; i++) { if (((1 << i) & groundLayer) != 0) { groundLayerNames += LayerMask.LayerToName(i) + " "; } }
            Debug.Log($"{gameObject.name} Awake: Ground Layer Mask includes: {groundLayerNames}(Value: {groundLayer.value})");
        }
        // Check if hostile target layer mask is assigned
        if (targetLayerMask_Hostile == 0) Debug.LogError($"TARGET LAYER MASK HOSTILE NOT SET on {gameObject.name}! Assign Player/Ally layers in Inspector.", this);

        // Disable the attack hitbox initially if it exists
        if (attackHitbox != null) attackHitbox.enabled = false;

        // Configure Rigidbody settings
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic; // Make sure it's affected by physics
            rb.gravityScale = 1f; // Standard gravity
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent the boss from tilting/rotating
        }
    }

    // Called before the first frame update
    void Start()
    {
        isAIActive = true; // Ensure AI starts active
        currentTargetLayerMask = targetLayerMask_Hostile; // Default to attacking hostile targets
        targetScanTimer = Random.Range(0f, targetScanInterval); // Randomize initial scan time to stagger scans across multiple enemies
        nextAttackTime = Time.time; // Allow attacking immediately if conditions met
        isFacingRight = transform.localScale.x > 0; // Determine initial facing direction based on scale
        currentState = State.Idle; // Start in Idle state
        FindNewTarget(); // Perform initial target scan
    }

    // Called every fixed framerate frame, suitable for physics calculations
    void FixedUpdate()
    {
        // --- Early Exits ---
        // If AI is inactive or dead, stop horizontal movement and do nothing else
        if (!isAIActive || currentState == State.Dead)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Use velocity for kinematic/dynamic
            return;
        }
        // If hurt, stop horizontal movement and let the hurt animation/recovery play out
        if (currentState == State.Hurt)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }
        // If the attack animation is playing, stop horizontal movement
        if (isAttacking)
        {
             if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
        }

        // --- Core AI Logic ---
        UpdateTargetScan();   // Check if it's time to scan for targets
        UpdateState();        // Determine the correct state based on conditions (target distance, cooldowns)

        // Only execute state actions if not hurt and not in the middle of an attack animation
        if (currentState != State.Hurt && !isAttacking)
        {
            ExecuteState(); // Perform actions based on the current state (Idle, Chase, Attack)
        }

        UpdateAnimation();    // Update animator parameters based on movement/state
    }

    // --- State Logic ---

    // Manages the timer for scanning for new targets
    void UpdateTargetScan()
    {
        targetScanTimer -= Time.fixedDeltaTime;
        if (targetScanTimer <= 0f)
        {
            FindNewTarget(); // Time to scan
            targetScanTimer = targetScanInterval; // Reset timer
        }
    }

    // Scans for the closest valid target within the detection radius
    void FindNewTarget()
    {
        Transform potentialTarget = FindClosestTarget(currentTargetLayerMask);

        // Check if a new, different target was found
        if (potentialTarget != null && currentTarget != potentialTarget)
        {
            Debug.Log($"{gameObject.name} found new target: {potentialTarget.name}");
            currentTarget = potentialTarget;
        }
        // Check if a target was previously held but is now gone
        else if (potentialTarget == null && currentTarget != null)
        {
            Debug.Log($"{gameObject.name} lost target (scan found nothing).");
            currentTarget = null;
        }
        // If potentialTarget is the same as currentTarget, or both are null, do nothing.
    }

    // Helper function to find the closest target within a given layer mask
    Transform FindClosestTarget(LayerMask layer)
    {
        // Find all colliders within the detection radius on the specified layers
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, layer);
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity; // Use squared distance for efficiency (avoids sqrt)

        foreach (Collider2D col in potentialTargets)
        {
            // Ignore self and dead/inactive targets
            if (col.gameObject == this.gameObject || !IsTargetAlive(col.transform)) continue;

            float distanceSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestTarget = col.transform;
            }
        }
        return closestTarget;
    }

    // Checks if a potential target is valid (active and has health component indicating it's alive)
    bool IsTargetAlive(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false; // Basic checks

        // Check PlayerHealth component
        PlayerHealth pHealth = target.GetComponent<PlayerHealth>();
        if (pHealth != null && pHealth.isDead) return false; // Player specific death check

        // Check EnemyHealth component (for allies or potentially other enemies if charmed)
        EnemyHealth eHealth = target.GetComponent<EnemyHealth>();
        if (eHealth != null && !(eHealth.CurrentState == EnemyHealth.State.Alive_Enemy || eHealth.CurrentState == EnemyHealth.State.Alive_Ally)) return false; // Enemy specific state check

        return true; // Target is considered alive
    }


    // Determines the appropriate state based on target presence, distance, and attack readiness
    void UpdateState()
    {
        // Don't change state if Hurt or Dead
        if (currentState == State.Hurt || currentState == State.Dead) return;

        // --- Target Checks ---
        // If no target, switch to Idle
        if (currentTarget == null)
        {
            if (currentState != State.Idle)
            {
                currentState = State.Idle;
                // Debug.Log($"{gameObject.name} No target, going Idle."); // Less verbose logging
            }
            return; // Exit early if no target
        }

        // If target is too far away, lose it and switch to Idle
        float distanceToTargetSqr = (currentTarget.position - transform.position).sqrMagnitude;
        if (distanceToTargetSqr > loseTargetRadius * loseTargetRadius)
        {
            currentTarget = null; // Lose the target
            if (currentState != State.Idle)
            {
                currentState = State.Idle;
                Debug.Log($"{gameObject.name} Target out of lose range ({loseTargetRadius}m), going Idle.");
            }
            return; // Exit early after losing target
        }

        // --- State Transitions based on Distance and Attack Readiness ---
        bool canAttack = !isAttacking && Time.time >= nextAttackTime; // Can attack if not already animating and cooldown is over

        // If IN attack range:
        if (distanceToTargetSqr <= attackRange * attackRange)
        {
            if (canAttack) // If can attack, switch to Attacking state
            {
                if (currentState != State.Attacking)
                {
                    currentState = State.Attacking;
                    Debug.Log($"{gameObject.name} Entering Attack state (In Range & Can Attack).");
                }
                // Already in Attacking state, do nothing (AttackTarget will handle triggering)
            }
            else // If in range but cannot attack (cooldown or animating)
            {
                // Switch to Idle to stop chasing while waiting for cooldown/animation
                if (currentState != State.Attacking && currentState != State.Idle)
                {
                    currentState = State.Idle;
                    // Debug.Log($"{gameObject.name} In range but cannot attack (Cooldown/Animating), going Idle."); // Less verbose logging
                }
                // If already Attacking or Idle, stay in that state
            }
        }
        // If OUTSIDE attack range (but within lose radius):
        else
        {
            // Switch to Chasing state if not already chasing
            if (currentState != State.Chasing)
            {
                currentState = State.Chasing;
                // Debug.Log($"{gameObject.name} Target detected OUTSIDE attack range, entering Chasing state."); // Less verbose logging
            }
            // Already in Chasing state, do nothing
        }
    }


    // Calls the appropriate action method based on the current state
    void ExecuteState()
    {
        switch (currentState)
        {
            case State.Idle:
                Idle();
                break;
            case State.Chasing:
                ChaseTarget();
                break;
            case State.Attacking:
                AttackTarget(); // Note: AttackTarget only *initiates* the attack animation
                break;
                // Hurt and Dead states are handled elsewhere (don't need active execution)
        }
    }

    // --- Actions ---

    // Action performed in the Idle state: Stop horizontal movement
    void Idle()
    {
        if (rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // Use velocity close to zero instead of exactly zero to potentially allow slight physics interactions
            if (Mathf.Abs(rb.linearVelocity.x) > 0.01f)
            {
                rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            }
        }
    }

    // Action performed in the Chasing state: Move towards the target, avoiding edges
    void ChaseTarget()
    {
        // Safety checks
        if (currentTarget == null || rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
        {
            Idle(); // Revert to idle if something is wrong
            return;
        }

        // Determine horizontal direction towards the target
        float horizontalInput = Mathf.Sign(currentTarget.position.x - rb.position.x);

        // Check for edges before moving
        bool isGroundAhead = IsNearEdge(horizontalInput);
        if (!isGroundAhead)
        {
            // Stop if near an edge
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            // Don't immediately switch to Idle here, let UpdateState handle it if target remains out of range
            // This prevents flickering between Chase->Idle if boss is right at an edge but player is still nearby.
             if (currentState != State.Idle) {
                 Debug.Log($"{gameObject.name} detected edge while chasing, stopping horizontal movement.");
             }
            return;
        }

        // Apply movement velocity
        float targetVelocityX = horizontalInput * moveSpeed;
        rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);

        // Ensure the boss faces the direction of movement
        FaceDirection(horizontalInput);
    }

    // Checks if there is ground ahead in the direction of movement
    bool IsNearEdge(float moveDirection)
    {
        // If not moving horizontally, assume ground is fine
        if (Mathf.Approximately(moveDirection, 0f)) return true;

        // Calculate raycast origin based on offsets and move direction
        Vector2 rayOrigin = (Vector2)transform.position
                            + (Vector2.right * moveDirection * edgeCheckHorizontalOffset) // Horizontal offset
                            + (Vector2.up * edgeCheckVerticalOffset);                    // Vertical offset

        // Perform the raycast downwards
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, edgeRaycastDistance, groundLayer);

        // Draw a debug ray in the editor scene view
        Debug.DrawRay(rayOrigin, Vector2.down * edgeRaycastDistance, (hit.collider != null) ? Color.green : Color.red, 0.1f);

        // Return true if the ray hit something on the ground layer, false otherwise
        return hit.collider != null;
    }


    // Action performed in the Attacking state: Initiate the attack animation
    void AttackTarget()
    {
        // Conditions to *prevent* starting a new attack:
        if (currentTarget == null || rb == null || isAttacking || Time.time < nextAttackTime)
        {
            // If already attacking or on cooldown, ensure stopped horizontally
             if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return; // Don't start a new attack
        }

        // Stop horizontal movement before attacking
         if (rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Face the target before attacking
        FaceTarget(currentTarget.position);

        // --- Initiate Attack ---
        isAttacking = true; // Set the flag indicating the attack animation has started
        animator.SetTrigger(hashAttack); // Trigger the attack animation
        nextAttackTime = Time.time + attackCooldown; // Set the time when the next attack will be possible

        Debug.Log($"{gameObject.name} triggered Attack animation. Next attack available at {nextAttackTime:F2}");

        // Start coroutines for hitbox timing and animation end detection
        if (attackHitbox != null)
        {
            StartCoroutine(EnableHitboxDuringAttack());
        }
        StartCoroutine(AttackAnimationEnd()); // Coroutine to reset isAttacking flag
    }


    // --- Damage & Effects Handling ---

    // Called by an Animation Event during the attack animation to apply damage
    public void AnimationTrigger_DamageTarget()
    {
        // Safety checks
        if (currentTarget == null || !isAIActive || currentState == State.Dead || !isAttacking) return;

        Debug.Log($"{gameObject.name} Attack Animation Event: Attempting Damage.");

        // Check distance again (target might have moved during windup)
        // Use a slightly larger range check here to be more lenient
        if (Vector2.Distance(transform.position, currentTarget.position) <= attackRange * 1.1f)
        {
            ApplyDamageAndEffects(currentTarget.gameObject);
        }
        else
        {
            Debug.Log($"{gameObject.name} Attack Animation Event: Target moved out of range during attack windup.");
        }
    }

    // Alternative damage application via Trigger Collider (if using attackHitbox)
    void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the collision involves the designated attack hitbox and the attack animation is playing
        if (attackHitbox != null && collision.gameObject == attackHitbox.gameObject && isAttacking)
        {
            Debug.Log($"{gameObject.name} Attack Hitbox detected collision with {collision.gameObject.name}");

            // Ignore collisions with self or children
            if (collision.transform.IsChildOf(transform) || collision.gameObject == gameObject) return;

            // Check if the collided object is on the correct target layer
            if (((1 << collision.gameObject.layer) & currentTargetLayerMask) != 0)
            {
                // Check if the target is actually alive before applying effects
                if(IsTargetAlive(collision.transform))
                {
                    ApplyDamageAndEffects(collision.gameObject);
                    // Optionally disable hitbox immediately after first hit if desired:
                    // if(attackHitbox != null) attackHitbox.enabled = false;
                }
            }
        }
    }


    // --- REVISED ApplyDamageAndEffects ---
    // Applies damage, knockback (horizontal), and status effects to the target
    void ApplyDamageAndEffects(GameObject targetObject)
    {
        Debug.Log($"Applying Damage & Effects to {targetObject.name}");

        // --- Apply Damage ---
        PlayerHealth playerHealth = targetObject.GetComponent<PlayerHealth>();
        EnemyHealth enemyHealth = targetObject.GetComponent<EnemyHealth>(); // Could be ally or another enemy
        float actualDamage = attackDamage; // Can add modifiers here later if needed

        if (playerHealth != null)
        {
            playerHealth.TakeDamage(actualDamage);
        }
        else if (enemyHealth != null)
        {
            enemyHealth.TakeDamage(actualDamage);
        }

        // --- Apply Knockback ---
        Rigidbody2D targetRb = targetObject.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            // --- REVISED KNOCKBACK CALCULATION ---

            // Calculate raw direction vector from boss's Rigidbody position to target's position
            // Using rb.position is often more reliable in FixedUpdate than transform.position
            Vector2 rawDirection = (Vector2)targetObject.transform.position - rb.position;

            // Ensure there's some horizontal difference to avoid NaN from normalizing a zero vector if perfectly aligned
            // Also gives a direction if perfectly aligned vertically.
            if (Mathf.Approximately(rawDirection.x, 0f))
            {
                // Use boss facing direction to decide the horizontal nudge
                rawDirection.x = isFacingRight ? 0.01f : -0.01f;
                Debug.Log($"Target vertically aligned with boss, adding slight horizontal offset: {rawDirection.x}");
            }

            // Create knockback vector: Use raw direction's X, zero out Y, THEN normalize
            Vector2 knockbackDirection = new Vector2(rawDirection.x, 0f).normalized;

            // --- END REVISED KNOCKBACK CALCULATION ---

            // Reset current horizontal speed so knockback is more pronounced
            targetRb.linearVelocity = new Vector2(0f, targetRb.linearVelocity.y);

            // Scale impulse by target mass so all targets are pushed the same distance
            float scaledKnockback = knockbackForce * targetRb.mass;

            // Log before applying force for debugging
            Debug.Log($"Applying Force to {targetObject.name} Rigidbody (Type: {targetRb.bodyType}). "
                      + $"Force Vector: {knockbackDirection * scaledKnockback} (Direction: {knockbackDirection}, "
                      + $"Magnitude: {scaledKnockback})");

            // If the hit object has a Player script that manages knock‑back lock‑out,
            // delegate the shove to it so movement code doesn't overwrite the impulse.
            Player targetPlayer = targetObject.GetComponent<Player>();
            if (targetPlayer != null)
            {
                targetPlayer.ApplyKnockback(knockbackDirection, scaledKnockback);
                // We’re done — avoid double‑applying force below.
                return;
            }
        }
        else
        {
             // Log a warning if the target doesn't have a Rigidbody2D
             Debug.LogWarning($"Cannot apply knockback: Target {targetObject.name} has no Rigidbody2D.");
        }

        // --- Apply Status Effects ---
        if (attackEffect != null)
        {
            Debug.Log($"Applying effect '{attackEffect.name}' to {targetObject.name}");
            // Assuming EffectSO has an ApplyOnHit method
            attackEffect.ApplyOnHit(this.gameObject, targetObject, actualDamage);
        }
    }
    // --- END REVISED ApplyDamageAndEffects ---


    // --- Coroutines for Attack Timing ---

    // Enables the attack hitbox after the windup time, then disables it shortly after
    IEnumerator EnableHitboxDuringAttack()
    {
        if (attackHitbox == null) yield break; // Exit if no hitbox assigned

        yield return new WaitForSeconds(attackWindup); // Wait for the windup period

        // Only enable if still in the attacking state (might have been interrupted)
        if(isAttacking && currentState == State.Attacking)
        {
            Debug.Log("Enabling attack hitbox");
            attackHitbox.enabled = true;
        }

        // Wait for a short duration while the hitbox is active
        // Adjust this duration based on the attack animation
        yield return new WaitForSeconds(0.2f); // Example: hitbox active for 0.2 seconds

        // Disable the hitbox regardless of state (ensures it gets turned off)
        if(attackHitbox != null)
        {
            attackHitbox.enabled = false;
            Debug.Log("Disabling attack hitbox");
        }
    }

    // Resets the isAttacking flag after a delay related to the attack cooldown.
    IEnumerator AttackAnimationEnd()
    {
        // Wait for a duration slightly less than the cooldown
        float waitTime = Mathf.Max(0.1f, attackCooldown * 0.9f); // Wait 90% of cooldown, minimum 0.1s

        yield return new WaitForSeconds(waitTime);

        // Only reset if still marked as attacking (could have been interrupted by death/hurt)
        if (isAttacking)
        {
            isAttacking = false; // Reset the animation flag
            Debug.Log($"{gameObject.name} Attack cooldown period ended (waited {waitTime:F2}s), isAttacking = false. Ready for state re-evaluation.");
        }
        else
        {
             // Debug.Log($"{gameObject.name} AttackAnimationEnd coroutine finished, but was no longer attacking (likely interrupted)."); // Less verbose
        }
    }


    // --- Animation & Flipping ---

    // Updates the animator based on current velocity and state
    void UpdateAnimation()
    {
        if (animator == null) return;

        // Set IsMoving based on horizontal velocity, but only if not attacking and actually in Chasing state
        bool isMoving = Mathf.Abs(rb.linearVelocity.x) > 0.1f && !isAttacking && currentState == State.Chasing;
        animator.SetBool(hashIsMoving, isMoving);

        // Note: Attack, Hit, IsDead are handled by triggers or direct sets elsewhere
    }

    // Rotates the boss to face the target's position
    void FaceTarget(Vector2 targetPosition)
    {
        float directionX = targetPosition.x - transform.position.x;
        FaceDirection(directionX);
    }

    // Flips the boss's sprite based on the horizontal direction
    void FaceDirection(float horizontalDirection)
    {
        // Only flip if there's significant horizontal direction
        if (Mathf.Abs(horizontalDirection) > 0.01f)
        {
            bool shouldFaceRight = horizontalDirection > 0;
            if (shouldFaceRight != isFacingRight)
            {
                Flip();
            }
        }
    }

    // Flips the boss's local scale on the X-axis
    void Flip()
    {
        isFacingRight = !isFacingRight; // Toggle the state variable
        Vector3 theScale = transform.localScale;
        theScale.x *= -1; // Invert the x-scale
        transform.localScale = theScale;
    }

    // --- Public Methods (Called by EnemyHealth or other external scripts) ---

    // Called by EnemyHealth when the boss takes damage
    public void TriggerHitAnimation()
    {
        // Don't enter Hurt state if already dead, hurt, or AI is inactive
        if (!isAIActive || currentState == State.Dead || currentState == State.Hurt) return;

        currentState = State.Hurt; // Enter Hurt state
        animator.SetTrigger(hashHit); // Trigger the Hit animation
        Debug.Log($"{gameObject.name} triggered Hit animation, entering Hurt state.");

        // Stop any ongoing attack hitbox/animation coroutines
        StopCoroutine(nameof(EnableHitboxDuringAttack));
        StopCoroutine(nameof(AttackAnimationEnd));
        if (attackHitbox != null) attackHitbox.enabled = false; // Ensure hitbox is off
        isAttacking = false; // Reset attack flag

        // Start the recovery coroutine
        StopCoroutine(nameof(HurtRecoveryCoroutine)); // Stop previous one if any
        StartCoroutine(nameof(HurtRecoveryCoroutine));
    }

    // Coroutine to recover from the Hurt state after a short delay
    IEnumerator HurtRecoveryCoroutine()
    {
        // Wait for the duration of the hurt animation/stun
        yield return new WaitForSeconds(0.5f); // Adjust this time as needed

        // Only recover if still in Hurt state (haven't died in the meantime)
        if(currentState == State.Hurt && currentState != State.Dead)
        {
            currentState = State.Idle; // Return to Idle state after recovery
            Debug.Log($"{gameObject.name} recovered from Hurt state, returning to Idle.");
        }
    }

    // Called by EnemyHealth when the boss's health reaches zero
    public void TriggerDeath()
    {
        if (currentState == State.Dead) return; // Already dead

        Debug.Log($"{gameObject.name} triggered Death.");
        currentState = State.Dead; // Set state to Dead
        isAIActive = false; // Disable AI logic
        isAttacking = false; // Ensure not marked as attacking

        // Disable hitbox and stop movement
        if (attackHitbox != null) attackHitbox.enabled = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; // Stop all movement
            rb.bodyType = RigidbodyType2D.Static; // Make it static so it doesn't fall through world (optional)
            // Disable main collider slightly after becoming static to prevent physics issues
            StartCoroutine(DisableColliderAfterDelay(0.1f));
        }

        // Trigger death animation
        if (animator != null)
        {
            animator.SetBool(hashIsMoving, false); // Ensure moving animation is off
            animator.SetBool(hashIsDead, true); // Trigger the death animation state
        }

        StopAllCoroutines(); // Stop any running AI coroutines (like attack timing)

        // Destroy the GameObject after a delay to allow death animation to play
        Destroy(gameObject, 5f); // Adjust delay as needed
    }

    // Coroutine to disable the main collider shortly after death
    IEnumerator DisableColliderAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Collider2D col = GetComponent<Collider2D>(); // Get the main collider
        if (col != null) col.enabled = false; // Disable it
    }

    // --- BaseEnemyAI Overrides (Implementation for methods defined in BaseEnemyAI) ---

    // Public method to externally activate/deactivate the AI
    public override void SetActiveAI(bool isActive)
    {
        // Ignore if already in the desired state or if dead
        if (isAIActive == isActive || currentState == State.Dead) return;

        isAIActive = isActive;
        Debug.Log($"{gameObject.name} AI Active set to: {isActive}");

        if (!isActive) // Deactivating AI
        {
            // Stop movement and animations
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            if(animator != null) animator.SetBool(hashIsMoving, false);

            // Stop AI-related coroutines
            StopCoroutine(nameof(EnableHitboxDuringAttack));
            StopCoroutine(nameof(AttackAnimationEnd));
            StopCoroutine(nameof(HurtRecoveryCoroutine));

            // Reset state to Idle if not dead
            if(currentState != State.Dead) currentState = State.Idle;
            isAttacking = false; // Ensure attack flag is reset
            if(attackHitbox != null) attackHitbox.enabled = false; // Ensure hitbox is off
        }
        else // Activating AI
        {
            // Re-enable animator if it was disabled
            if(animator != null) animator.enabled = true;
            // Find a target immediately
            FindNewTarget();
            // Reset state to Idle if not dead
            if(currentState != State.Dead) currentState = State.Idle;
            // State machine will take over from Idle in the next FixedUpdate
        }
    }

    // Public method to switch between targeting enemies (allies) or hostiles (player/allies)
    public override void SetTargetingMode(bool targetEnemies)
    {
        // Determine the correct layer mask based on the input flag
        currentTargetLayerMask = targetEnemies ? targetLayerMask_Allied : targetLayerMask_Hostile;

        // Log the change and the active layers for debugging
        string layerNames = "";
        for(int i=0; i<32; i++) { if(((1 << i) & currentTargetLayerMask) != 0) { layerNames += LayerMask.LayerToName(i) + " "; } }
        Debug.Log($"{gameObject.name} targeting mode set. Targets Enemies: {targetEnemies}. Active Mask Layers: {layerNames}");

        // Reset current target and scan immediately with the new mask
        currentTarget = null;
        FindNewTarget();
        // State machine will adjust based on the new target (or lack thereof)
    }

    // --- Gizmos ---
    // Draw visual aids in the Scene view when the object is selected
    void OnDrawGizmosSelected()
    {
        // Draw Edge Detection Ray
        if(rb != null) // Check rb exists
        {
            // Determine direction based on isFacingRight for consistency
            float direction = isFacingRight ? 1f : -1f;
            Vector2 hOffset = Vector2.right * direction * edgeCheckHorizontalOffset;
            Vector2 vOffset = Vector2.up * edgeCheckVerticalOffset;
            Vector2 origin = (Vector2)transform.position + hOffset + vOffset;

            // Simulate raycast for gizmo color (use try-catch as Physics2D calls might error outside play mode)
            bool groundAhead = false;
            try { groundAhead = Physics2D.Raycast(origin, Vector2.down, edgeRaycastDistance, groundLayer); } catch {}

            Gizmos.color = groundAhead ? Color.green : Color.red; // Green if ground detected, Red if not
            Gizmos.DrawRay(origin, Vector2.down * edgeRaycastDistance);
        }

        // Draw Targeting Radii
        Gizmos.color = Color.yellow; // Detection Radius
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.gray; // Lose Target Radius
        Gizmos.DrawWireSphere(transform.position, loseTargetRadius);

        // Draw Attack Range
        Gizmos.color = Color.red; // Attack Range
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // Draw Line to Current Target
        if(currentTarget != null)
        {
            Gizmos.color = Color.cyan; // Line to target
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}
