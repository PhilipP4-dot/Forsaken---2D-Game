using UnityEngine;

public class EnemyController : MonoBehaviour
{
    public bool isAlly { get; private set; } = false; // Read-only from outside, starts as enemy
    public float moveSpeed = 3f; // Basic movement speed

    private Transform currentTarget;
    private string targetTag;

    // Reference to visual components for potential changes
    private SpriteRenderer spriteRenderer;
    // Optional: Add reference to an Animator component later

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>(); // Get the renderer
    }

    void Start()
    {
        UpdateTargeting(); // Set initial target tag
    }

    void Update()
    {
        FindTarget(); // Constantly look for the closest target (inefficient, but simple for now)
        MoveTowardsTarget(); // Simple movement
    }

    void UpdateTargeting()
    {
        // Determine what tag this entity should target based on allegiance
        targetTag = isAlly ? "Enemy" : "Player";
        currentTarget = null; // Reset target when allegiance changes
        Debug.Log($"{gameObject.name} is now targeting objects with tag: {targetTag}");
    }

    void FindTarget()
    {
        // Simple, inefficient way to find the closest target with the correct tag
        GameObject[] potentialTargets = GameObject.FindGameObjectsWithTag(targetTag);
        float closestDistance = Mathf.Infinity;
        GameObject closestTarget = null;

        foreach (GameObject potentialTarget in potentialTargets)
        {
            // Ensure not targeting self if allies and enemies share a tag temporarily
            if (potentialTarget == this.gameObject) continue;

            float distance = Vector3.Distance(transform.position, potentialTarget.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = potentialTarget;
            }
        }

        currentTarget = closestTarget != null ? closestTarget.transform : null;

        // Debugging Line:
        // if (currentTarget != null) Debug.Log($"{gameObject.name} targeting {currentTarget.name}");
    }

    void MoveTowardsTarget()
    {
        if (currentTarget != null)
        {
            // Basic movement towards target - Replace with pathfinding later
            transform.position = Vector3.MoveTowards(transform.position, currentTarget.position, moveSpeed * Time.deltaTime);

            // Basic facing direction (optional)
            if (currentTarget.position.x < transform.position.x)
                transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
            else
                transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);

            // TODO: Add attack logic when in range
        }
    }

    /// Called by EnemyHealth to switch allegiance to the player's side.
    public void BecomeAlly()
    {
        if (isAlly) return; // Already an ally

        isAlly = true;

        // --- Change Allegiance Indicators ---
        // 1. Change Tag (Careful: FindGameObjectsWithTag might have issues if tags change rapidly)
        // gameObject.tag = "PlayerAlly"; // Create this tag first in the editor if you use it

        // 2. Change Layer (Recommended for physics/targeting)
        // Make sure you have created a "PlayerAlly" layer in Edit -> Project Settings -> Tags and Layers
        gameObject.layer = LayerMask.NameToLayer("PlayerAlly"); // Assign to PlayerAlly layer

        // 3. Change Visuals (Example: Tint Green)
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.green; // Example visual change
        }
        // ------------------------------------

        UpdateTargeting(); // Update what it should now target (Enemies)
        Debug.Log($"{gameObject.name} has been revived as an Ally!");
    }
}