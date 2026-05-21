using UnityEngine;
using System.Collections.Generic; // Needed for List

[RequireComponent(typeof(Rigidbody2D))]
public class ProjectileController : MonoBehaviour
{
    [Header("Stats")]
    public float speed; // Value (+/-) set by the shooter
    public float lifetime = 3f;
    public float damage; // Damage value set by the shooter (PlayerAttackController calculates final damage)

    [Header("Collision")]
    [Tooltip("Layers that the projectile should be destroyed by without dealing damage (e.g., Ground, Walls).")]
    [SerializeField] private LayerMask environmentLayers; // Assign Ground/Wall layers in Inspector

    // --- References from Shooter ---
    [HideInInspector] public string shooterTag;
    [HideInInspector] public GameObject shooterObject; // Reference to the GameObject that shot this
    [HideInInspector] public List<EffectSO> weaponEffects; // Reference to the effects list from the WeaponSO

    private Rigidbody2D rb;
    private bool hitOccurred = false; // Prevent multiple hit processing

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) {
             Debug.LogError("Projectile has no Rigidbody2D!", this);
             Destroy(gameObject);
             return;
        }
        // Use bodyType instead of isKinematic
        rb.bodyType = RigidbodyType2D.Dynamic; // Ensure it's dynamic to move via velocity
        rb.gravityScale = 0f; // Typically no gravity for arrows/bullets
    }

    void Start()
    {
        if (rb == null) return; // Exit if Rigidbody setup failed in Awake

        // Set initial velocity using the projectile's local rotation (transform.right)
        rb.linearVelocity = transform.right * speed;

        // Destroy after lifetime if it hasn't hit anything
        Destroy(gameObject, lifetime);
    }

    // --- UPDATED WITH MORE LOGS ---
    void OnTriggerEnter2D(Collider2D collision)
    {
        // Log entry point and the damage value THIS projectile instance holds
        Debug.Log($"[Projectile] OnTriggerEnter2D with '{collision.gameObject.name}' (Tag: {collision.tag}, Layer: {LayerMask.LayerToName(collision.gameObject.layer)}) - Projectile Damage Value: {this.damage}");

        // Prevent processing multiple hits from the same projectile instance
        if (hitOccurred) {
            Debug.Log("[Projectile] Hit already occurred for this instance, exiting.");
            return;
        }

        GameObject hitObject = collision.gameObject;
        string hitTag = hitObject.tag;
        int hitLayer = hitObject.layer;

        // 1. Ignore hitting the entity that shot it
        if (hitTag == shooterTag)
        {
            Debug.Log("[Projectile] Hit shooter tag, ignoring collision.");
            // Note: Consider using Physics2D Layer Collision Matrix to prevent this interaction entirely
            return; // Don't process self-hits
        }

        // 2. Check for environmental collision (non-damaging)
        // Uses bitwise operation to check if the hit object's layer is in the environment mask
        if (((1 << hitLayer) & environmentLayers) != 0)
        {
            Debug.Log("[Projectile] Hit environment layer. Destroying projectile.");
            hitOccurred = true; // Mark hit to prevent further processing
            // TODO: Instantiate impact effect/sound at transform.position?
            Destroy(gameObject); // Destroy projectile
            return;
        }

        // 3. Determine if the target *should* be damaged based on shooter/hit tags
        bool damageTarget = false;
        if (shooterTag == "Player" && hitTag == "Enemy") damageTarget = true;
        else if (shooterTag == "Enemy" && (hitTag == "Player" || hitTag == "Ally")) damageTarget = true;
        else if (shooterTag == "Ally" && hitTag == "Enemy") damageTarget = true;
        // Add more rules if needed (e.g., environment damaging objects?)

        Debug.Log($"[Projectile] DamageTarget check result: {damageTarget} (Shooter Tag: '{shooterTag}', Hit Tag: '{hitTag}')");

        // 4. If it's a valid target to damage, try to apply damage
        if (damageTarget)
        {
             // Try to find a health component (Player or Enemy) on the hit object
             PlayerHealth playerHealth = hitObject.GetComponent<PlayerHealth>();
             EnemyHealth enemyHealth = hitObject.GetComponent<EnemyHealth>();

             Debug.Log($"[Projectile] Attempting damage. Found PlayerHealth: {(playerHealth != null)}. Found EnemyHealth: {(enemyHealth != null)}");

             bool damageApplied = false;
             float actualDamageDealt = 0f; // Store the damage value used

             // Apply damage to the appropriate health component
             if (playerHealth != null)
             {
                  Debug.Log($"[Projectile] Applying {this.damage} damage to PlayerHealth on '{hitObject.name}'.");
                  actualDamageDealt = this.damage; // Record the damage value
                  playerHealth.TakeDamage(this.damage); // Use the damage value stored in this projectile instance
                  damageApplied = true;
             }
             else if (enemyHealth != null)
             {
                  Debug.Log($"[Projectile] Applying {this.damage} damage to EnemyHealth on '{hitObject.name}'.");
                  actualDamageDealt = this.damage; // Record the damage value
                  enemyHealth.TakeDamage(this.damage); // Use the damage value stored in this projectile instance
                  damageApplied = true;
             }

             // 5. If damage was successfully applied
             if(damageApplied)
             {
                 Debug.Log("[Projectile] Damage applied successfully. Processing OnHit effects and destroying projectile.");
                 hitOccurred = true; // Mark hit

                 // Trigger OnHit effects from the original weapon
                 if (weaponEffects != null && shooterObject != null) {
                     foreach (EffectSO effect in weaponEffects) {
                          // Call ApplyOnHit from the original weapon's effects list
                          effect?.ApplyOnHit(shooterObject, hitObject, actualDamageDealt);
                     }
                 }
                 else { Debug.LogWarning("[Projectile] weaponEffects list or shooterObject reference was null. Cannot apply OnHit effects.", this); }

                 // TODO: Instantiate impact effect/sound?
                 Destroy(gameObject); // Destroy projectile after applying damage and effects
                 return;
             }
             else
             {
                 // Hit something with a potentially damageable tag, but no health script was found
                 Debug.LogWarning($"[Projectile] Target '{hitObject.name}' matched damage criteria (Tag: '{hitTag}') but had no applicable health component (PlayerHealth/EnemyHealth).");
                 // Decide if projectile should be destroyed anyway? Optional.
                 // hitOccurred = true; Destroy(gameObject); return;
             }
        }
        else
        {
             // Hit something non-damaging and not environment (e.g., player projectile hits ally, enemy hits enemy)
             Debug.Log("[Projectile] Target did not match damage criteria based on tags.");
             // Decide if it should be destroyed or pass through? Let's destroy for now to prevent clutter.
             // hitOccurred = true; Destroy(gameObject); return;
        }

        // If no specific condition above destroyed the projectile, it continues until lifetime expires.
    }
}
