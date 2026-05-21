using UnityEngine;
using System.Linq;

// Inherits from BaseEnemyAI instead of MonoBehaviour
public class EnemyAI : BaseEnemyAI
{
    public enum AIMode { TargetPlayer, TargetEnemies }
    [SerializeField] private AIMode currentMode = AIMode.TargetPlayer;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;

    [Header("Targeting")]
    [SerializeField] private float detectionRadius = 10f;
    [Tooltip("Layers containing targets when AI is HOSTILE (Assign Player AND Ally layers).")]
    [SerializeField] private LayerMask targetLayerMask_Hostile;
    [Tooltip("Layers containing targets when AI is ALLIED (Assign Enemy layer).")]
    [SerializeField] private LayerMask targetLayerMask_Allied;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string enemyTag = "Enemy";
    [SerializeField] private string allyTag = "Ally";

    [Header("Ally Behaviour")]
    [SerializeField] private float followStopDistance = 2.5f;
    [SerializeField] private float targetScanInterval = 0.5f;

    [Header("Attacking")]
    [SerializeField] private float attackRange = 1.0f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    private float nextAttackTime = 0f;

    // Private state variables
    private Transform currentTarget = null;
    private Transform playerTransform = null;
    private Rigidbody2D rb;
    private EnemyHealth health; // Keep reference if AI logic depends on health state
    private float targetScanTimer;
    private bool isFacingRight = true;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        if (rb == null) Debug.LogError($"{gameObject.name} is missing a Rigidbody2D component!");
        if (health == null) Debug.LogError($"{gameObject.name} is missing an EnemyHealth component!");
        if (rb != null)
        {
            if (rb.bodyType != RigidbodyType2D.Dynamic) Debug.LogWarning($"{gameObject.name}'s Rigidbody2D is not Dynamic!", this);
            if (!rb.freezeRotation) Debug.LogWarning($"{gameObject.name}'s Rigidbody2D needs Freeze Rotation Z checked!", this);
        }
    }

    void Start()
    {
        FindPlayer();
        FindNewTarget();
        targetScanTimer = targetScanInterval;
        isFacingRight = transform.localScale.x > 0;
        nextAttackTime = Time.time;
    }

    void FixedUpdate()
    {
        // Check if AI should be active based on health state
        if (health != null && health.CurrentState != EnemyHealth.State.Alive_Enemy && health.CurrentState != EnemyHealth.State.Alive_Ally)
        {
            if (rb != null) rb.linearVelocity = Vector2.zero; return;
        }

        // Target Scanning Logic...
         bool scanNeeded = false;
        if (currentMode == AIMode.TargetEnemies && !IsTargetValid(currentTarget)) { if (currentTarget == null || !currentTarget.CompareTag(enemyTag)) { scanNeeded = true; } }
        else if (currentMode == AIMode.TargetPlayer && !IsTargetValid(currentTarget)) { scanNeeded = true; }
        if (scanNeeded) { targetScanTimer -= Time.fixedDeltaTime; if (targetScanTimer <= 0f) { FindNewTarget(); targetScanTimer = targetScanInterval; } }


        // Decision Making Logic...
        Transform targetToMoveTowards = null;
        bool shouldAttack = false;
        if (IsTargetValid(currentTarget)) { float distanceToTarget = Vector2.Distance(rb.position, currentTarget.position); if (distanceToTarget <= attackRange) { if (Time.time >= nextAttackTime) { Debug.Log($"!!! {gameObject.name} ATTACK CONDITION MET !!! Target: {currentTarget.name}"); shouldAttack = true; targetToMoveTowards = null; nextAttackTime = Time.time + attackCooldown; } else { shouldAttack = false; targetToMoveTowards = null; } } else { shouldAttack = false; targetToMoveTowards = currentTarget; } }
        else { currentTarget = null; shouldAttack = false; if (currentMode == AIMode.TargetEnemies) { if (playerTransform != null && Vector2.Distance(rb.position, playerTransform.position) > followStopDistance) { targetToMoveTowards = playerTransform; } } }

        // Action Execution...
        if (shouldAttack && currentTarget != null) { Attack(currentTarget); }
        if (targetToMoveTowards != null) { MoveTowardsTarget(targetToMoveTowards); }
        else if (!shouldAttack) { Idle(); }
    }

    // --- IMPLEMENT ABSTRACT METHODS from BaseEnemyAI ---

    /// <summary>
    /// Enables or disables this AI script and stops movement when disabled.
    /// </summary>
    public override void SetActiveAI(bool isActive)
    {
        if (this.enabled == isActive) return; // No change needed

        Debug.Log($"{gameObject.name} AI Active set to: {isActive}");
        this.enabled = isActive; // Enable/disable this script

        // Stop movement immediately if disabling
        if (!isActive && rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        // Add logic here to enable/disable other components controlled by this specific AI if needed
        // e.g., maybe disable a weapon component, particle systems etc.
    }

    /// <summary>
    /// Switches the AI's targeting mode between targeting Player/Allies or targeting Enemies.
    /// </summary>
    public override void SetTargetingMode(bool targetEnemies)
    {
        AIMode newMode = targetEnemies ? AIMode.TargetEnemies : AIMode.TargetPlayer;
        // Call the internal method safely
        InternalSetMode(newMode);
    }

    // --- Renamed original SetMode to avoid conflict and made private ---
    private void InternalSetMode(AIMode newMode) // Changed public to private
    {
        if (currentMode == newMode) return;
        Debug.Log($"{gameObject.name} AI mode changing internally to: {newMode}");
        currentMode = newMode;
        currentTarget = null; // Clear target on mode change
        if (playerTransform == null) FindPlayer(); // Ensure player ref if switching mode
        FindNewTarget(); // Find appropriate target for new mode
    }
    // ----------------------------------------------------------


    // --- Existing Helper Methods (Keep all these as they were) ---
    private void MoveTowardsTarget(Transform target) { if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || target == null) return; float horizontalDirection = Mathf.Sign(target.position.x - rb.position.x); float targetVelocityX = horizontalDirection * moveSpeed; rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y); if ((horizontalDirection > 0 && !isFacingRight) || (horizontalDirection < 0 && isFacingRight)) { Flip(); } }
    private void Idle() { if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) { rb.linearVelocity = new Vector2(0, rb.linearVelocity.y); } }
    private void FindPlayer() { GameObject player = GameObject.FindWithTag(playerTag); if (player != null) { playerTransform = player.transform; } else { Debug.LogWarning($"{gameObject.name} could not find GameObject with tag '{playerTag}'!"); } }
    private void FindNewTarget() { if (currentMode == AIMode.TargetPlayer) { currentTarget = FindClosestOnLayer(targetLayerMask_Hostile, playerTag, allyTag); if (currentTarget != null) Debug.Log($"{gameObject.name} (Mode: {currentMode}) targeting: {currentTarget.name} (Tag: {currentTarget.tag})"); else Debug.Log($"{gameObject.name} (Mode: {currentMode}) couldn't find Player or Ally target."); } else if (currentMode == AIMode.TargetEnemies) { currentTarget = FindClosestOnLayer(targetLayerMask_Allied, enemyTag, null); } }
    private Transform FindClosestOnLayer(LayerMask layer, string tag1, string tag2 = null) { Collider2D[] potentialTargets = Physics2D.OverlapCircleAll(transform.position, detectionRadius, layer); Transform closestTarget = null; float minDistance = Mathf.Infinity; foreach (Collider2D col in potentialTargets) { if (col.gameObject == this.gameObject) continue; bool tagMatch = col.CompareTag(tag1) || (tag2 != null && col.CompareTag(tag2)); if (!tagMatch) continue; if (!IsTargetAlive(col.transform)) continue; float distance = Vector2.Distance(transform.position, col.transform.position); if (distance < minDistance) { minDistance = distance; closestTarget = col.transform; } } return closestTarget; }
    private bool IsTargetValid(Transform target) { if (target == null || !target.gameObject.activeInHierarchy) return false; if (!IsTargetAlive(target)) return false; return true; }
    private bool IsTargetAlive(Transform target) { if (target == null) return false; EnemyHealth targetEnemyHealth = target.GetComponent<EnemyHealth>(); if (targetEnemyHealth != null && targetEnemyHealth.CurrentState != EnemyHealth.State.Alive_Enemy && targetEnemyHealth.CurrentState != EnemyHealth.State.Alive_Ally) return false; PlayerHealth playerHealthTarget = target.GetComponent<PlayerHealth>(); if (playerHealthTarget != null && playerHealthTarget.isDead) return false; return true; }
    private void Attack(Transform target) { Debug.Log($"--- {gameObject.name} EXECUTE ATTACK on {target.name} ---"); PlayerHealth playerHealth = target.GetComponent<PlayerHealth>(); EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>(); if (playerHealth != null) { Debug.Log($"... Found PlayerHealth. Calling TakeDamage({attackDamage})"); playerHealth.TakeDamage(attackDamage); } else if (enemyHealth != null) { Debug.Log($"... Found EnemyHealth. Calling TakeDamage({attackDamage})"); enemyHealth.TakeDamage(attackDamage); } else { Debug.LogWarning($"{gameObject.name} attacked {target.name}, but it has no recognizable health component!"); } }
    private void Flip() { isFacingRight = !isFacingRight; transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z); }
    void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, detectionRadius); if (currentMode == AIMode.TargetEnemies) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(transform.position, followStopDistance); } Gizmos.color = Color.red; Gizmos.DrawWireSphere(transform.position, attackRange); }

} // End of class EnemyAI