    using UnityEngine;
    using System.Collections; // Required for Coroutines
    // using UnityEngine.AI; // Uncomment if using NavMeshAgent

    public class EnemyMovement : MonoBehaviour
    {
        [Tooltip("Base movement speed of the enemy.")]
        public float moveSpeed = 3.5f;

        // --- State ---
        private bool isFrozen = false;
        private bool isKnockedBack = false;
        private float originalSpeed;
        private RigidbodyType2D originalBodyType; // Store original body type

        // --- Component References ---
        // [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Rigidbody2D rb;
        // [SerializeField] private EnemyAI enemyAI;

        void Awake()
        {
            // agent = GetComponent<NavMeshAgent>();
            rb = GetComponent<Rigidbody2D>();
            // enemyAI = GetComponent<EnemyAI>();

            originalSpeed = moveSpeed;
            if (rb != null) {
                originalBodyType = rb.bodyType; // Store the initial body type
            }
            // if (agent != null) { originalSpeed = agent.speed; }
        }

        // Change Update OR FixedUpdate depending on where your movement logic is
        void Update() // Or FixedUpdate()
        {
            // Only process movement if the enemy is NOT frozen AND NOT being knocked back
            if (!isFrozen && !isKnockedBack)
            {
                // --- Basic Movement Logic Placeholder ---
                // TODO: Implement your enemy's movement logic here.
                // Example: Simple Rigidbody movement towards a target
                /*
                if (rb != null && target != null) {
                    Vector2 direction = ((Vector2)target.position - rb.position).normalized;
                    // Ensure bodyType is Dynamic if using velocity for movement
                    if(rb.bodyType != RigidbodyType2D.Dynamic) rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.velocity = direction * moveSpeed;
                }
                */
                // ----------------------------------------
            }
        }

        // --- Freeze Methods ---
        public void Freeze(float duration)
        {
            Debug.Log($"[EnemyMovement] Freeze({duration}) called on {gameObject.name}. Current isFrozen state: {isFrozen}");
            if (!isFrozen)
            {
                StartCoroutine(FreezeCoroutine(duration));
            }
            else { Debug.Log($"[EnemyMovement] Already frozen, request ignored."); }
        }

        private IEnumerator FreezeCoroutine(float duration)
        {
            Debug.Log($"[EnemyMovement] FreezeCoroutine START on {gameObject.name}. Duration: {duration}");
            isFrozen = true;
            isKnockedBack = false; // Freeze overrides knockback

            // --- Stop Movement Logic ---
            Debug.Log($"[EnemyMovement] Attempting to STOP movement logic (Freeze) on {gameObject.name}");
            if (rb != null) {
                rb.linearVelocity = Vector2.zero;
                // Optional: Make kinematic during freeze if needed, store original type first
                // originalBodyType = rb.bodyType; // Store before changing
                // rb.bodyType = RigidbodyType2D.Kinematic;
                Debug.Log($"[EnemyMovement] Rigidbody velocity set to zero for {gameObject.name}");
            }
            // (Add other stop logic: agent.isStopped = true; enemyAI.enabled = false; etc.)
            // ---

            yield return new WaitForSeconds(duration);

            // --- Resume Movement Logic ---
            Debug.Log($"[EnemyMovement] Attempting to RESUME movement logic (Freeze) on {gameObject.name}");
            if (rb != null) {
                // Restore original body type if changed
                // rb.bodyType = originalBodyType;
            }
            // (Add resume logic: agent.isStopped = false; enemyAI.enabled = true; etc.)
            // ---

            isFrozen = false;
            Debug.Log($"[EnemyMovement] FreezeCoroutine END on {gameObject.name}. isFrozen state: {isFrozen}");
        }


        // --- Knockback Methods ---
        public void ApplyKnockback(Vector2 direction, float force, float duration, ForceMode2D mode)
        {
            if (isFrozen) {
                Debug.Log($"[EnemyMovement] Knockback ignored on {gameObject.name} because enemy is frozen.");
                return;
            }
            Debug.Log($"[EnemyMovement] ApplyKnockback called on {gameObject.name}. Force: {force}, Duration: {duration}");

            if (rb != null)
            {
                // *** Check bodyType instead of isKinematic ***
                if (rb.bodyType == RigidbodyType2D.Static) {
                     Debug.LogWarning($"[EnemyMovement] Cannot apply knockback to {gameObject.name}: Rigidbody2D is Static.", this);
                     return; // Cannot AddForce to Static
                }
                // Ensure it's dynamic to accept force properly
                rb.bodyType = RigidbodyType2D.Dynamic;
                // ---------------------------------------------

                rb.linearVelocity = Vector2.zero; // Stop current movement
                rb.AddForce(direction * force, mode); // Apply force
                StartCoroutine(KnockbackStateCoroutine(duration)); // Manage state
            }
            else { Debug.LogWarning($"[EnemyMovement] Cannot apply knockback to {gameObject.name}: Rigidbody2D not found."); }
        }

        private IEnumerator KnockbackStateCoroutine(float duration)
        {
            Debug.Log($"[EnemyMovement] KnockbackStateCoroutine START on {gameObject.name}. Duration: {duration}");
            isKnockedBack = true;

            yield return new WaitForSeconds(duration);

            isKnockedBack = false;
            // Optional: Reset velocity after knockback state ends
            // if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic) { rb.velocity = Vector2.zero; }
            Debug.Log($"[EnemyMovement] KnockbackStateCoroutine END on {gameObject.name}. isKnockedBack state: {isKnockedBack}");
        }
        // ----------------------------

        public bool IsFrozen() => isFrozen;
        public bool IsKnockedBack() => isKnockedBack;
    }
    