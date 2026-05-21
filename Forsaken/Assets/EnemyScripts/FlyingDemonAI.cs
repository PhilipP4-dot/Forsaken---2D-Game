using UnityEngine;
using System.Collections;
using System.Linq;

// Base class assumed to exist elsewhere in your project
// public abstract class BaseEnemyAI : MonoBehaviour { ... }

[RequireComponent(typeof(EnemyMovement))] // Ensure EnemyMovement is present
public class FlyingDemonAI : BaseEnemyAI
{
    private enum AIState { Patrolling, EngagingTarget, Attacking }
    [SerializeField] private AIState currentState = AIState.Patrolling;

    [SerializeField] private AIMode currentTargetingMode = AIMode.TargetPlayerOrAlly;

    [Header("Movement")]
    [SerializeField] private float flySpeed = 3f;
    [SerializeField] private float hoverDistanceMin = 4f;
    [SerializeField] private float hoverDistanceMax = 7f;
    [SerializeField] private float repositionSpeed = 1.5f;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField] private float patrolWaitTime = 2.0f;
    private int currentPatrolIndex = 0;
    private float patrolWaitTimer = 0f;
    private bool isPatrolWaiting = false;

    [Header("Targeting")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private LayerMask targetLayerMask_Hostile;
    [SerializeField] private LayerMask targetLayerMask_Allied;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string allyTag = "Ally";
    [SerializeField] private float targetScanInterval = 0.5f;
    private float targetScanTimer;
    private Transform currentTarget = null;

    [Header("Ally Behaviour")]
    [SerializeField] private float followStopDistance = 2.5f;

    [Header("Attacking")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float fireBreathRange = 6f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float fireBreathCooldown = 4.0f;
    [SerializeField] private string fireBreathTrigger = "FireBreath";
    private float nextFireBreathTime = 0f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float avoidanceRaycastDistance = 1.0f;
    [SerializeField] private float avoidanceAngle = 30f;

    // Components & References
    private Rigidbody2D rb;
    private Animator animator;
    private EnemyHealth health;
    private Transform playerTransform;
    private Collider2D ownCollider;
    private EnemyMovement enemyMovement; // <-- ADDED REFERENCE

    // Internal State
    private bool isFacingRight = true;
    private bool isAIActive = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();
        ownCollider = GetComponent<Collider2D>();
        enemyMovement = GetComponent<EnemyMovement>(); // <-- GET REFERENCE

        // Null checks
        if (rb == null) Debug.LogError($"{gameObject.name} missing Rigidbody2D!", this);
        if (animator == null) Debug.LogWarning($"{gameObject.name} missing Animator!", this);
        if (health == null) Debug.LogError($"{gameObject.name} missing EnemyHealth script!", this);
        if (attackPoint == null) Debug.LogError($"Attack Point transform not assigned on {gameObject.name}!", this);
        if (ownCollider == null) Debug.LogError($"{gameObject.name} missing Collider2D!", this);
        if (obstacleLayerMask == 0) Debug.LogWarning($"Obstacle Layer Mask not assigned on {gameObject.name}. Avoidance will not work.", this);
        if (enemyMovement == null) Debug.LogError($"{gameObject.name} missing EnemyMovement script!", this); // <-- CHECK REFERENCE


        // Rigidbody settings
        if (rb != null) {
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }
    }

    void Start()
    {
        isAIActive = true;
        FindPlayer();
        FindNewTarget();
        targetScanTimer = 0f;
        isFacingRight = transform.localScale.x > 0;
        nextFireBreathTime = Time.time;
        ChangeState(IsTargetValid(currentTarget) ? AIState.EngagingTarget : AIState.Patrolling);
    }

    void FixedUpdate()
    {
        // --- Pre-computation Checks ---
        // Use EnemyMovement states to halt AI logic
        if (!isAIActive ||
            (health != null && health.CurrentState != EnemyHealth.State.Alive_Enemy && health.CurrentState != EnemyHealth.State.Alive_Ally) ||
            (enemyMovement != null && (enemyMovement.IsFrozen() || enemyMovement.IsKnockedBack())) // <-- CHECK STATES HERE
           )
        {
            // If AI shouldn't run (dead, inactive, frozen, or knocked back), ensure velocity is zeroed
            // Note: Knockback itself applies force, but AI shouldn't fight it.
            if (rb != null && rb.simulated && enemyMovement != null && !enemyMovement.IsKnockedBack()) // Only zero velocity if not being actively knocked back
            {
                 rb.linearVelocity = Vector2.zero;
            }
            return; // Stop further AI logic this frame
        }


        // --- Target Scanning (Runs even if frozen/knocked back to keep target updated) ---
        ScanForTargets();

        // --- Centralized Target Validity Check & State Transition ---
        if ((currentState == AIState.EngagingTarget || currentState == AIState.Attacking) && !IsTargetValid(currentTarget))
        {
            ChangeState(AIState.Patrolling);
        }

        // --- State Machine Execution ---
        // This part now only runs if the pre-computation checks passed (not frozen/knocked back)
        switch (currentState)
        {
            case AIState.Patrolling:
                ExecutePatrol();
                if (IsTargetValid(currentTarget))
                {
                    ChangeState(AIState.EngagingTarget);
                }
                break;

            case AIState.EngagingTarget:
                ExecuteEngage();
                break;

             case AIState.Attacking:
                Idle(); // Stop movement during attack animation
                break;
        }
    }

    // --- Public Methods from Base Class (Implementation) ---
    // SetActiveAI, SetTargetingMode remain the same
    public override void SetActiveAI(bool isActive)
    {
        if (this.enabled == isActive && isAIActive == isActive) return;
        isAIActive = isActive;
        this.enabled = isActive;
        if (!isActive && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }
    public override void SetTargetingMode(bool targetEnemies)
    {
        AIMode newMode = targetEnemies ? AIMode.TargetEnemies : AIMode.TargetPlayerOrAlly;
        if (currentTargetingMode == newMode) return;

        Debug.Log($"{gameObject.name} switching targeting mode to: {newMode}");
        currentTargetingMode = newMode;
        currentTarget = null;
        FindNewTarget();
        ChangeState(IsTargetValid(currentTarget) ? AIState.EngagingTarget : AIState.Patrolling);
    }


    // --- State Management ---
    // ChangeState remains the same
    void ChangeState(AIState newState)
    {
        if(currentState == newState) return;
        currentState = newState;
        if (newState == AIState.Patrolling) { InitializePatrol(); }
    }

    // --- Targeting Logic ---
    // ScanForTargets, FindPlayer, FindNewTarget, FindClosestOnLayer, IsTargetValid, IsTargetAlive remain the same
    void ScanForTargets()
    {
        targetScanTimer -= Time.fixedDeltaTime;
        if (targetScanTimer <= 0f)
        {
            bool currentStillValid = IsTargetValid(currentTarget);
            if (!currentStillValid || (currentTargetingMode == AIMode.TargetEnemies && currentTarget != null && !currentTarget.CompareTag(enemyTag)))
            {
                 FindNewTarget();
            }
            targetScanTimer = targetScanInterval;
        }
     }
    private void FindPlayer()
    {
        GameObject player = GameObject.FindWithTag(playerTag);
        if (player != null) { playerTransform = player.transform; }
        else { Debug.LogWarning($"{gameObject.name}: Player object with tag '{playerTag}' not found!"); }
    }
    private void FindNewTarget()
    {
        Transform previousTarget = currentTarget;
        Transform foundTarget = null;

        if (currentTargetingMode == AIMode.TargetPlayerOrAlly) {
            foundTarget = FindClosestOnLayer(targetLayerMask_Hostile, playerTag, allyTag);
        } else if (currentTargetingMode == AIMode.TargetEnemies) {
            foundTarget = FindClosestOnLayer(targetLayerMask_Allied, enemyTag, null);
        }

        if (currentTarget != foundTarget) {
            currentTarget = foundTarget;
        }
    }
    private Transform FindClosestOnLayer(LayerMask layer, string tag1, string tag2 = null)
    {
        Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, layer);
        Transform closestTarget = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider2D col in potentialTargets)
        {
            if (col.gameObject == this.gameObject) continue;
            bool tagMatch = col.CompareTag(tag1) || (tag2 != null && col.CompareTag(tag2));
            if (!tagMatch) continue;
            if (!IsTargetAlive(col.transform)) continue;

            float distance = Vector2.Distance(transform.position, col.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestTarget = col.transform;
            }
        }
        return closestTarget;
    }
     private bool IsTargetValid(Transform target)
    {
        if (target == null || !target.gameObject.activeInHierarchy) { return false; }
        if (!IsTargetAlive(target)) { return false; }
        float distanceToTarget = Vector2.Distance(transform.position, target.position);
        if (distanceToTarget > detectionRadius) { return false; }
        return true;
    }
    private bool IsTargetAlive(Transform target)
    {
        if (target == null) return false;
        EnemyHealth targetEHealth = target.GetComponent<EnemyHealth>();
        if (targetEHealth != null) { return targetEHealth.CurrentState == EnemyHealth.State.Alive_Enemy || targetEHealth.CurrentState == EnemyHealth.State.Alive_Ally; }
        PlayerHealth targetPHealth = target.GetComponent<PlayerHealth>();
        if (targetPHealth != null) { return !targetPHealth.isDead; }
        return false;
    }


    // --- State Execution Logic ---
    // InitializePatrol, ExecutePatrol, ExecuteEngage remain the same
    void InitializePatrol()
    {
        patrolWaitTimer = 0f;
        isPatrolWaiting = false;
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        currentPatrolIndex = Mathf.Clamp(currentPatrolIndex, 0, patrolPoints.Length - 1);
    }
    void ExecutePatrol()
    {
        if (!isAIActive) return; // Redundant check now handled in FixedUpdate

        if (currentTargetingMode == AIMode.TargetEnemies) {
            if (playerTransform != null) {
                if (Vector2.Distance(rb.position, playerTransform.position) > followStopDistance) {
                    MoveTowardsPosition(playerTransform.position, flySpeed * 0.8f);
                } else {
                    Idle();
                }
            } else {
                Idle();
            }
            return;
        }

        if (patrolPoints == null || patrolPoints.Length == 0) { Idle(); return; }
        if (isPatrolWaiting) {
            patrolWaitTimer -= Time.fixedDeltaTime;
            if (patrolWaitTimer <= 0f) {
                isPatrolWaiting = false;
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            } else { Idle(); }
        } else {
            Transform targetPoint = patrolPoints[currentPatrolIndex];
            if (targetPoint == null) {
                isPatrolWaiting = true; patrolWaitTimer = patrolWaitTime; return;
            }
            float distanceToPoint = Vector2.Distance(rb.position, targetPoint.position);
            if (distanceToPoint <= 0.5f) {
                isPatrolWaiting = true; patrolWaitTimer = patrolWaitTime; Idle();
            } else {
                MoveTowardsPosition(targetPoint.position, flySpeed * 0.7f);
            }
        }
     }
     void ExecuteEngage()
    {
        if (!isAIActive || currentTarget == null) { ChangeState(AIState.Patrolling); return; } // Redundant check

        FaceTarget(currentTarget.position);
        bool initiatedAttack = ExecuteAttackDecision();

        if (!initiatedAttack && currentState != AIState.Attacking) {
            float distanceToTarget = Vector2.Distance(rb.position, currentTarget.position);
            if (currentTargetingMode == AIMode.TargetEnemies) {
                if (distanceToTarget > attackRange * 0.9f) {
                    MoveTowardsPosition(currentTarget.position, flySpeed);
                } else {
                     Idle();
                }
            } else {
                if (distanceToTarget < hoverDistanceMin) {
                    Vector2 awayDirection = (rb.position - (Vector2)currentTarget.position).normalized;
                    MoveInDirection(awayDirection, flySpeed);
                } else if (distanceToTarget > hoverDistanceMax) {
                    MoveTowardsPosition(currentTarget.position, flySpeed);
                } else {
                    if (distanceToTarget <= attackRange) { Idle(); }
                    else { MoveTowardsPosition(currentTarget.position, repositionSpeed); }
                }
            }
        }
     }


    // --- Attacking Logic ---
    // ExecuteAttackDecision, AttackFireBreath, SpawnFireBreathEffect, AttackAnimationFinished remain the same
    bool ExecuteAttackDecision()
    {
        if (!isAIActive || currentTarget == null || currentState == AIState.Attacking) { return false; } // Redundant check
        float distanceToTarget = Vector2.Distance(rb.position, currentTarget.position);
        bool canFireBreath = Time.time >= nextFireBreathTime;
        if (distanceToTarget <= attackRange && canFireBreath) { AttackFireBreath(); nextFireBreathTime = Time.time + fireBreathCooldown; ChangeState(AIState.Attacking); return true; }
        return false;
     }
     void AttackFireBreath()
    {
        if(currentTarget!=null) FaceTarget(currentTarget.position);
        Idle();
        if (animator != null) animator.SetTrigger(fireBreathTrigger);
     }
     public void SpawnFireBreathEffect()
    {
        if (!isAIActive || !IsTargetValid(currentTarget)) { return; }

        PlayerHealth playerHealth = currentTarget.GetComponent<PlayerHealth>();
        EnemyHealth enemyHealth = currentTarget.GetComponent<EnemyHealth>();

        if (playerHealth != null && (currentTargetingMode == AIMode.TargetPlayerOrAlly)) {
            playerHealth.TakeDamage(attackDamage);
        } else if (enemyHealth != null) {
             bool isTargetEnemy = currentTarget.CompareTag(enemyTag);
             bool isTargetAlly = currentTarget.CompareTag(allyTag);
             if ((currentTargetingMode == AIMode.TargetEnemies && isTargetEnemy) ||
                 (currentTargetingMode == AIMode.TargetPlayerOrAlly && isTargetAlly)) {
                enemyHealth.TakeDamage(attackDamage);
             }
        }
     }
    public void AttackAnimationFinished()
    {
        if (currentState == AIState.Attacking) {
            ChangeState(IsTargetValid(currentTarget) ? AIState.EngagingTarget : AIState.Patrolling);
        }
     }


    // --- Movement Helpers ---
    // GetAvoidanceDirection, MoveTowardsPosition, MoveInDirection, Idle remain the same
    private Vector2 GetAvoidanceDirection(Vector2 intendedDirection)
    {
        if (obstacleLayerMask == 0 || intendedDirection == Vector2.zero) return intendedDirection;
        Vector2 avoidanceDirection = intendedDirection;
        Vector2 rayOrigin = transform.position;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, intendedDirection, avoidanceRaycastDistance, obstacleLayerMask);
        if (hit.collider != null)
        {
            Vector2 rightCheckDir = Quaternion.Euler(0, 0, -avoidanceAngle) * intendedDirection;
            Vector2 leftCheckDir = Quaternion.Euler(0, 0, avoidanceAngle) * intendedDirection;
            RaycastHit2D hitRight = Physics2D.Raycast(rayOrigin, rightCheckDir, avoidanceRaycastDistance * 0.8f, obstacleLayerMask);
            RaycastHit2D hitLeft = Physics2D.Raycast(rayOrigin, leftCheckDir, avoidanceRaycastDistance * 0.8f, obstacleLayerMask);
            if (hitRight.collider == null && hitLeft.collider != null) { avoidanceDirection = rightCheckDir; }
            else if (hitLeft.collider == null && hitRight.collider != null) { avoidanceDirection = leftCheckDir; }
            else if (hitLeft.collider == null && hitRight.collider == null) { avoidanceDirection = rightCheckDir; }
            else { avoidanceDirection = hit.normal; }
        }
        if (avoidanceDirection.sqrMagnitude < 0.01f) { return intendedDirection; }
        return avoidanceDirection.normalized;
    }
    private void MoveTowardsPosition(Vector2 targetPosition, float speed)
    {
        if (rb == null || !isAIActive) return; // Redundant check
        Vector2 intendedDirection = (targetPosition - rb.position).normalized;
        if (intendedDirection.sqrMagnitude < 0.01f) { Idle(); return; }
        Vector2 finalDirection = GetAvoidanceDirection(intendedDirection);
        rb.linearVelocity = finalDirection * speed; // Use velocity for physics-based movement
        FaceMovementDirection(finalDirection);
    }
     private void MoveInDirection(Vector2 intendedDirection, float speed)
    {
        if (rb == null || !isAIActive) return; // Redundant check
         if (intendedDirection.sqrMagnitude < 0.01f) { Idle(); return; }
        Vector2 finalDirection = GetAvoidanceDirection(intendedDirection.normalized);
        rb.linearVelocity = finalDirection * speed; // Use velocity
        FaceMovementDirection(finalDirection);
    }
    private void Idle()
    {
        if (rb != null && isAIActive) { // Redundant check
            rb.linearVelocity *= 0.9f;
            if (rb.linearVelocity.sqrMagnitude < 0.01f) {
                rb.linearVelocity = Vector2.zero;
            }
        }
     }

    // --- Visuals ---
    // Flip, FaceTarget, FaceMovementDirection remain the same
    private void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 theScale = transform.localScale;
        theScale.x *= -1;
        transform.localScale = theScale;
     }
    private void FaceTarget(Vector2 targetPosition)
    {
        if (!isAIActive) return; // Redundant check
        bool shouldBeFacingRight = targetPosition.x > transform.position.x;
        if (shouldBeFacingRight != isFacingRight) {
            Flip();
        }
     }
    private void FaceMovementDirection(Vector2 direction)
    {
        if (!isAIActive) return; // Redundant check
        if (Mathf.Abs(direction.x) > 0.01f) {
             bool shouldBeFacingRight = direction.x > 0;
             if (shouldBeFacingRight != isFacingRight) {
                 Flip();
             }
        }
     }

    // --- Gizmos ---
    // OnDrawGizmosSelected remains the same
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRadius);
        if (currentTargetingMode == AIMode.TargetEnemies) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(transform.position, followStopDistance); }
        Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange);
        if (currentTargetingMode == AIMode.TargetPlayerOrAlly) { Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(transform.position, hoverDistanceMin); Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, hoverDistanceMax); }
        if(attackPoint != null) { Gizmos.color = Color.red; Vector2 dir = transform.localScale.x > 0 ? Vector2.right : Vector2.left; Gizmos.DrawLine(attackPoint.position, (Vector2)attackPoint.position + dir * fireBreathRange); }
        if (currentTarget != null) { Gizmos.color = IsTargetValid(currentTarget) ? Color.cyan : Color.gray; Gizmos.DrawLine(transform.position, currentTarget.position); }
        if (currentTargetingMode == AIMode.TargetEnemies && currentTarget == null && playerTransform != null) { Gizmos.color = Color.white; Gizmos.DrawLine(transform.position, playerTransform.position); }
        if (Application.isPlaying && rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
             Vector2 currentVelDir = rb.linearVelocity.normalized; Vector2 rayOrigin = transform.position;
             RaycastHit2D hit = Physics2D.Raycast(rayOrigin, currentVelDir, avoidanceRaycastDistance, obstacleLayerMask);
             Gizmos.color = hit.collider != null ? Color.red : Color.green; Gizmos.DrawRay(rayOrigin, currentVelDir * avoidanceRaycastDistance);
             Vector2 rightCheck = Quaternion.Euler(0, 0, -avoidanceAngle) * currentVelDir; Vector2 leftCheck = Quaternion.Euler(0, 0, avoidanceAngle) * currentVelDir;
             RaycastHit2D hitRight = Physics2D.Raycast(rayOrigin, rightCheck, avoidanceRaycastDistance * 0.8f, obstacleLayerMask); RaycastHit2D hitLeft = Physics2D.Raycast(rayOrigin, leftCheck, avoidanceRaycastDistance * 0.8f, obstacleLayerMask);
             Gizmos.color = hitRight.collider != null ? Color.magenta : Color.yellow; Gizmos.DrawRay(rayOrigin, rightCheck * avoidanceRaycastDistance * 0.8f);
             Gizmos.color = hitLeft.collider != null ? Color.magenta : Color.yellow; Gizmos.DrawRay(rayOrigin, leftCheck * avoidanceRaycastDistance * 0.8f);
        }
     }
}
