using UnityEngine;
using System.Collections; // Required for Coroutines

// Make sure necessary components are present
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))] // Ensure you have this script ready
public class FireWormAI : BaseEnemyAI // Ensure BaseEnemyAI script exists in your project
{
    // --- State Machine ---
    private enum State { Idle, Chasing, Attacking, Hurt, Dead } // Internal AI state
    private State currentState = State.Idle;

    [Header("References")]
    [Tooltip("Assign a child GameObject used as the spawn point for projectiles.")]
    [SerializeField] private Transform firePoint;
    [Tooltip("Assign the projectile prefab the worm shoots.")]
    [SerializeField] private GameObject fireballPrefab;

    [Header("Movement & Pathfinding")]
    [SerializeField] private float moveSpeed = 2.5f;
    [Tooltip("Upward impulse applied when jumping over obstacles as an ally.")]
    [SerializeField] private float jumpForce = 5f;
    // Removed pathUpdateRate as it wasn't used in the current physics-based movement
    [Tooltip("Select the layer(s) that represent obstacles (e.g., Walls, Ground).")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [Tooltip("How far ahead the AI looks for obstacles while moving.")]
    [SerializeField] private float obstacleCheckDistance = 0.7f;
    [Tooltip("How strongly the AI tries to steer away from detected obstacles.")]
    [SerializeField] private float wallAvoidanceForce = 2f;
    [Tooltip("How long (in seconds) the AI can be stationary while trying to move before attempting to unstick.")]
    [SerializeField] private float stuckTimeThreshold = 1.5f;
    private Vector2 lastPosition;
    private float timeStuck = 0f;
    // private bool isAvoidingObstacle = false; // Simple avoidance flag (optional)

    [Header("Targeting")]
    [Tooltip("How far the AI can detect potential targets.")]
    [SerializeField] private float detectionRadius = 15f;
    [Tooltip("If the target moves beyond this distance, the AI loses track.")]
    [SerializeField] private float loseTargetRadius = 18f;
    [Tooltip("Layers containing targets when AI is HOSTILE (Assign Player AND Ally layers).")]
    [SerializeField] private LayerMask targetLayerMask_Hostile;
    [Tooltip("Layers containing targets when AI is ALLIED (Assign Enemy layer).")]
    [SerializeField] private LayerMask targetLayerMask_Allied;

    // --- Suppress 'unused' warning for tags - might be used by projectile or future logic ---
    #pragma warning disable 0414
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string allyTag = "Ally"; // If allies can be targeted by other enemies
    #pragma warning restore 0414
    // -------------------------------------------------------------------------------------

    [Tooltip("How often (in seconds) the AI scans for new targets.")]
    [SerializeField] private float targetScanInterval = 0.5f;

    [Header("Ranged Attack")]
    [Tooltip("Maximum distance from which the AI can initiate an attack.")]
    [SerializeField] private float attackRange = 8f;
    [Tooltip("Damage dealt by each projectile attack.")]
    [SerializeField] private float attackDamage = 10f; // This value is passed to the projectile
    [Tooltip("Distance the AI tries to maintain from the target while attacking (should be <= attackRange).")]
    [SerializeField] private float stopDistance = 6f;
    [Tooltip("Time (in seconds) between attacks.")]
    [SerializeField] private float attackCooldown = 2.0f;
    [Tooltip("Speed of the fired projectile.")]
    [SerializeField] private float projectileSpeed = 10f; // This value is passed to the projectile
    private float nextAttackTime = 0f;

    [Header("Ally Behaviour")]
    [Tooltip("When allied, the distance at which the AI stops following the player.")]
    [SerializeField] private float followStopDistance = 3.5f;

    // --- Component & State References ---
    private Rigidbody2D rb;
    private Animator animator;
    private EnemyHealth health; // Reference to the health component
    private Transform currentTarget = null;
    private Transform playerTransform = null; // Specific reference to the player
    private LayerMask currentTargetLayerMask; // Dynamically set based on Hostile/Allied
    private float targetScanTimer;
    private bool isFacingRight = true;
    private bool isAIActive = true; // Controls whether the AI logic runs

    // --- Animation Parameter Hashes (for performance) ---
    private readonly int hashIsMoving = Animator.StringToHash("IsMoving");
    private readonly int hashAttack = Animator.StringToHash("Attack");
    private readonly int hashHit = Animator.StringToHash("Hit");
    private readonly int hashIsDead = Animator.StringToHash("IsDead");

    void Awake()
    {
        // Get references to essential components on this GameObject
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>(); // Get reference to health script

        // Error checking for missing components
        if (rb == null) Debug.LogError($"{gameObject.name} missing Rigidbody2D!", this);
        if (animator == null) Debug.LogError($"{gameObject.name} missing Animator!", this);
        if (health == null) Debug.LogError($"{gameObject.name} missing EnemyHealth! Ensure it's attached.", this);
        if (firePoint == null) Debug.LogError($"{gameObject.name} missing FirePoint reference! Assign in Inspector.", this);
        if (fireballPrefab == null) Debug.LogError($"{gameObject.name} missing Fireball Prefab reference! Assign in Inspector.", this);

        // Configure Rigidbody settings suitable for this AI
        if (rb != null)
        {
            rb.gravityScale = 0; // No gravity effect
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Prevent unwanted spinning
        }
        lastPosition = rb != null ? rb.position : (Vector2)transform.position; // Initialize last position
    }

    void Start()
    {
        FindPlayer(); // Locate the player GameObject at the start
        SetTargetingMode(false); // Initialize targeting hostile entities (Player/Allies)
        targetScanTimer = Random.Range(0f, targetScanInterval); // Stagger initial scans slightly
        nextAttackTime = Time.time + Random.Range(0f, attackCooldown * 0.5f); // Stagger initial attack readiness
        isFacingRight = transform.localScale.x > 0; // Determine initial facing direction
    }

    void FixedUpdate() // Use FixedUpdate for physics-based movement and checks
    {
        // Check if the AI should be running based on its active status and state
        if (!isAIActive || currentState == State.Dead || currentState == State.Hurt)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = Vector2.zero;
            return;
        }

        UpdateTargetScan();
        UpdateState();
        ExecuteCurrentState();
        UpdateAnimation();
    }

    // --- State Machine Logic ---

    void UpdateState()
    {
        if (currentTarget == null)
        {
            if (health != null && health.CurrentState == EnemyHealth.State.Alive_Ally && playerTransform != null)
            {
                if (Vector2.Distance(rb.position, playerTransform.position) > followStopDistance)
                {
                    currentState = State.Chasing;
                    currentTarget = playerTransform;
                }
                else
                {
                    currentState = State.Idle;
                }
            }
            else
            {
                 currentState = State.Idle;
            }
            return;
        }

        float distanceToTargetSqr = (currentTarget.position - transform.position).sqrMagnitude;

        if (distanceToTargetSqr > loseTargetRadius * loseTargetRadius && !(health != null && health.CurrentState == EnemyHealth.State.Alive_Ally && currentTarget == playerTransform))
        {
            currentTarget = null;
            currentState = State.Idle;
            return;
        }

        if (distanceToTargetSqr <= attackRange * attackRange)
        {
            currentState = State.Attacking;
        }
        else
        {
            currentState = State.Chasing;
        }
    }

    void ExecuteCurrentState()
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
                AttackTarget();
                break;
        }
    }

    // --- State Actions ---

    void Idle()
    {
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * 10f);
        }
    }

    void ChaseTarget()
    {
        if (currentTarget == null || rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;

        if (Vector2.Distance(rb.position, currentTarget.position) <= stopDistance)
        {
            currentState = State.Attacking;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 directionToTarget = ((Vector3)currentTarget.position - transform.position).normalized;
        Vector2 desiredVelocity = directionToTarget * moveSpeed;
        Vector2 rayOrigin = (Vector2)transform.position + directionToTarget * 0.1f;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, directionToTarget, obstacleCheckDistance, obstacleLayerMask);

        if (hit.collider != null)
        {
            Debug.DrawRay(rayOrigin, directionToTarget * obstacleCheckDistance, Color.red);
            if (health != null && health.CurrentState == EnemyHealth.State.Alive_Ally && currentTarget == playerTransform)
            {
                // As an ally, jump over obstacle toward player
                rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
                return; // skip normal steering this frame
            }
            // existing wall avoidance code below
            Vector2 avoidanceDirection = Vector2.Perpendicular(hit.normal).normalized;
            if (Vector2.Dot(avoidanceDirection, directionToTarget) < 0)
            {
                avoidanceDirection *= -1;
            }
            desiredVelocity = Vector2.Lerp(desiredVelocity, avoidanceDirection * moveSpeed, wallAvoidanceForce * Time.fixedDeltaTime).normalized * moveSpeed;
        }
        else
        {
            Debug.DrawRay(rayOrigin, directionToTarget * obstacleCheckDistance, Color.green);
        }

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * 5f);

        if (Vector2.Distance(rb.position, lastPosition) < 0.05f)
        {
            timeStuck += Time.fixedDeltaTime;
        }
        else
        {
            timeStuck = 0f;
            lastPosition = rb.position;
        }

        if (timeStuck > stuckTimeThreshold)
        {
            Debug.LogWarning($"{gameObject.name} might be stuck, attempting corrective nudge.");
            rb.AddForce(Random.insideUnitCircle.normalized * moveSpeed * 2f, ForceMode2D.Impulse);
            timeStuck = 0f;
        }

        FaceTarget(currentTarget.position);
    }

    void AttackTarget()
    {
         if (currentTarget == null || rb == null)
         {
            currentState = State.Idle;
            return;
         }

        if (rb.bodyType == RigidbodyType2D.Dynamic)
        {
             rb.linearVelocity = Vector2.zero;
        }

        FaceTarget(currentTarget.position);

        if (Time.time >= nextAttackTime)
        {
            animator.SetTrigger(hashAttack); // Ensure "Attack" Trigger exists in Animator
            nextAttackTime = Time.time + attackCooldown;
            Debug.Log($"{gameObject.name} triggered Attack animation.");
        }
    }

    // --- Called by an Animation Event on the Attack animation ---
    // --- Connects to YOUR ProjectileController ---
    public void AnimationTrigger_ShootProjectile()
    {
        // Only prevent shooting when revived and targeting the player
        if (health != null && health.CurrentState == EnemyHealth.State.Alive_Ally && currentTarget == playerTransform) return;
        if (fireballPrefab == null || firePoint == null || currentTarget == null || !isAIActive || currentState == State.Dead) return;

        Debug.Log($"{gameObject.name} executing Shoot via animation event towards {currentTarget.name}");

        // Calculate direction from the fire point towards the target (with slight vertical offset)
        Vector3 targetPos = currentTarget.position + Vector3.up * 0.5f;
        Vector2 direction = ((Vector2)targetPos - (Vector2)firePoint.position).normalized;
        Debug.Log($"[FireWormAI Debug] FirePoint: {firePoint.position}, Calculated Direction: {direction}, Speed: {this.projectileSpeed}");

        // Instantiate the projectile
        GameObject projectileGO = Instantiate(fireballPrefab, firePoint.position, firePoint.rotation);

        // Get YOUR projectile controller script
        ProjectileController projectileScript = projectileGO.GetComponent<ProjectileController>();

        if (projectileScript != null)
        {
            // Configure the projectile's properties
            projectileScript.damage = this.attackDamage;       // Use damage from FireWormAI
            projectileScript.shooterObject = this.gameObject;  // Set the shooter reference
            projectileScript.shooterTag = this.gameObject.tag; // Set the shooter tag ("Enemy" or "Ally")
            projectileScript.weaponEffects = null;             // No weapon effects for enemy shots

            // Get the projectile's Rigidbody to set velocity
            Rigidbody2D projectileRb = projectileGO.GetComponent<Rigidbody2D>();
            if (projectileRb != null)
            {
                // Set velocity based on calculated direction and FireWormAI's speed setting
                projectileRb.linearVelocity = direction * this.projectileSpeed;
                Debug.Log($"[FireWormAI Debug] Setting Velocity: {projectileRb.linearVelocity}");

                // Optional: Rotate projectile sprite to face the direction of travel
                if (direction != Vector2.zero)
                {
                   float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                   // Adjust angle offset if your sprite isn't facing right (0 degrees) by default
                   projectileGO.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
                   Debug.Log($"[FireWormAI Debug] Setting Rotation Angle: {angle}");
                }
            }
            else { Debug.LogError($"Projectile prefab '{fireballPrefab.name}' is missing Rigidbody2D!", projectileGO); }

            Debug.Log($"Projectile Configured: Damage={projectileScript.damage}, Shooter={projectileScript.shooterTag}, Velocity={projectileRb?.linearVelocity}");
        }
        else
        {
            Debug.LogError($"Instantiated projectile '{projectileGO.name}' is missing ProjectileController script!", projectileGO);
            // Fallback if script is missing
             Rigidbody2D projectileRb = projectileGO.GetComponent<Rigidbody2D>();
             if (projectileRb != null) { projectileRb.linearVelocity = direction * this.projectileSpeed; }
        }
        // Optional: Play sound effect here
    }


    // --- Targeting Logic ---

    void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null) { playerTransform = playerObj.transform; }
        else { Debug.LogWarning($"{gameObject.name} could not find GameObject with tag '{playerTag}'!", this); }
    }

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

        if (potentialTarget != null)
        {
            if(currentTarget != potentialTarget) {
                 Debug.Log($"{gameObject.name} (Mode: {health?.CurrentState}) found new target: {potentialTarget.name}");
                 currentTarget = potentialTarget;
            }
        }
        else
        {
            if (!(health != null && health.CurrentState == EnemyHealth.State.Alive_Ally && currentTarget == playerTransform))
            {
                 if(currentTarget != null) {
                    Debug.Log($"{gameObject.name} (Mode: {health?.CurrentState}) lost target or none in range.");
                    currentTarget = null;
                 }
            }
        }
    }

    Transform FindClosestTarget(LayerMask layer)
    {
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, layer);
        Transform closestTarget = null;
        float minDistanceSqr = Mathf.Infinity;

        foreach (Collider2D col in potentialTargets)
        {
            if (col.gameObject == this.gameObject) continue;
            if (!IsTargetAlive(col.transform)) continue;

            float distanceSqr = (col.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closestTarget = col.transform;
            }
        }
        return closestTarget;
    }

    bool IsTargetAlive(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.isDead) return false;

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            // Assumes EnemyHealth.State has Alive_Enemy and Alive_Ally
            bool isAlive = enemyHealth.CurrentState == EnemyHealth.State.Alive_Enemy ||
                           enemyHealth.CurrentState == EnemyHealth.State.Alive_Ally;
            if (!isAlive) return false;
        }
        return true;
    }

    // --- Movement & Animation Helpers ---

    void UpdateAnimation()
    {
        if (animator == null) return;
        bool isMoving = (currentState == State.Chasing && rb.linearVelocity.magnitude > 0.1f);
        animator.SetBool(hashIsMoving, isMoving); // Ensure "IsMoving" Bool exists in Animator
    }

    void FaceTarget(Vector3 targetPosition)
    {
        if ((targetPosition.x > transform.position.x && !isFacingRight) || (targetPosition.x < transform.position.x && isFacingRight))
        {
            Flip();
        }
    }

    // --- THE KEY METHOD FOR DIRECTION ---
    void Flip()
    {
        isFacingRight = !isFacingRight; // Toggle the facing direction flag
        Vector3 theScale = transform.localScale;
        theScale.x *= -1; // Invert the X scale of the parent worm
        transform.localScale = theScale;
    }

    // --- Public Methods for External Interaction (e.g., called by EnemyHealth) ---

    public void TriggerHitAnimation()
    {
        if (!isAIActive || currentState == State.Dead) return;
        currentState = State.Hurt;
        animator.SetTrigger(hashHit); // Ensure "Hit" Trigger exists in Animator
        Debug.Log($"{gameObject.name} triggered Hit animation.");
        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) rb.linearVelocity = Vector2.zero;
        StopCoroutine(nameof(HurtRecoveryCoroutine));
        StartCoroutine(nameof(HurtRecoveryCoroutine));
    }

    IEnumerator HurtRecoveryCoroutine()
    {
        yield return new WaitForSeconds(0.4f);
        if(currentState != State.Dead)
        {
             currentState = State.Idle;
             Debug.Log($"{gameObject.name} recovered from Hurt state.");
        }
    }

    public void TriggerDeath()
    {
        if (currentState == State.Dead) return;
        Debug.Log($"{gameObject.name} triggered Death.");
        currentState = State.Dead;
        isAIActive = false;
        if (rb != null)
        {
             rb.linearVelocity = Vector2.zero;
             rb.bodyType = RigidbodyType2D.Static; // Use bodyType instead of isKinematic
             // Disable collider slightly later to allow death animation to play without physics issues
             StartCoroutine(DisableColliderAfterDelay(0.1f));
        }
        if (animator != null)
        {
            animator.SetBool(hashIsMoving, false);
            animator.SetBool(hashIsDead, true); // Ensure "IsDead" Bool exists in Animator
        }
        StopAllCoroutines();
        Destroy(gameObject, 5f); // Destroy after 5 seconds (adjust time)
    }

    // Helper coroutine to disable collider after a short delay
    IEnumerator DisableColliderAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }


    // --- IMPLEMENT ABSTRACT METHODS from BaseEnemyAI ---

    public override void SetActiveAI(bool isActive)
    {
        if (isAIActive == isActive || currentState == State.Dead) return;
        isAIActive = isActive;
        Debug.Log($"{gameObject.name} AI Active set to: {isActive}");
        if (!isActive)
        {
            if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) { rb.linearVelocity = Vector2.zero; }
            if(animator != null) { animator.SetBool(hashIsMoving, false); }
            StopAllCoroutines();
            if(currentState != State.Dead) currentState = State.Idle;
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
        string layerNames = "";
        for(int i=0; i<32; i++) { if(((1 << i) & currentTargetLayerMask) != 0) { layerNames += LayerMask.LayerToName(i) + " "; } }
        Debug.Log($"{gameObject.name} targeting mode set. Targets Enemies: {targetEnemies}. Layers: {layerNames}");
        currentTarget = null;
        if (playerTransform == null) FindPlayer();
        FindNewTarget();
    }

    // --- Gizmos for Editor Visualization ---
    void OnDrawGizmosSelected()
    {
        if (health == null) health = GetComponent<EnemyHealth>();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseTargetRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawWireSphere(transform.position, stopDistance);

        if(health != null && health.CurrentState == EnemyHealth.State.Alive_Ally)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, followStopDistance);
        }

        if(rb != null && isAIActive && currentState == State.Chasing)
        {
            Gizmos.color = Color.magenta;
            Vector2 checkDirection = (rb.linearVelocity.magnitude > 0.1f) ? rb.linearVelocity.normalized : (isFacingRight ? Vector2.right : Vector2.left);
             Vector2 rayOrigin = (Vector2)transform.position + checkDirection * 0.1f;
            Gizmos.DrawLine(rayOrigin, rayOrigin + checkDirection * obstacleCheckDistance);
        }

        if(currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }
}
