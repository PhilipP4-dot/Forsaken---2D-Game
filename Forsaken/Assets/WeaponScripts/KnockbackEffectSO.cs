using UnityEngine;

[CreateAssetMenu(fileName = "New Knockback Effect", menuName = "Effects/Movement/Knockback")]
public class KnockbackEffectSO : EffectSO
{
    [Header("Knockback Settings")]
    [Tooltip("The amount of force applied.")]
    public float knockbackForce = 15f;
    [Tooltip("How long the enemy's movement AI is suppressed after knockback (seconds). Only applies if target has EnemyMovement.")]
    public float knockbackStateDuration = 0.3f; // Duration to prevent AI override
    [Tooltip("How the force is applied (e.g., Impulse for sudden push).")]
    public ForceMode2D forceMode = ForceMode2D.Impulse;
    [Tooltip("Add some upward force to the knockback?")]
    public bool addUpwardForce = true;
    [Tooltip("Multiplier for the upward force component.")]
    public float upwardForceMultiplier = 0.5f;

    // --- Implementations ---

    public override void ApplyOnEquip(GameObject target) { /* Nothing */ }
    public override void RemoveOnUnequip(GameObject target) { /* Nothing */ }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { /* Nothing */ }
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim)
    {
        return baseDamage; // Doesn't change damage
    }

    // Apply knockback AFTER damage is dealt
    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        Debug.Log($"[KnockbackEffectSO] ApplyOnHit called on victim: {victim?.name ?? "null"} by instigator: {instigator?.name ?? "null"}");

        if (victim == null || instigator == null) return;

        // --- Calculate Knockback Direction ---
        Vector2 knockbackDirection = (victim.transform.position - instigator.transform.position);
        if (knockbackDirection.sqrMagnitude < 0.001f)
        {
            // If positions are too close, use attacker's facing direction (assuming Rigidbody exists)
            Rigidbody2D instigatorRb = instigator.GetComponent<Rigidbody2D>();
            if (instigatorRb != null) {
                 // Use velocity direction or fallback to transform direction
                 knockbackDirection = (instigatorRb.linearVelocity.magnitude > 0.1f) ? instigatorRb.linearVelocity.normalized : (Vector2)instigator.transform.right; // Assumes sprite faces right
            } else {
                knockbackDirection = Vector2.right; // Default fallback
            }
            Debug.LogWarning("[KnockbackEffectSO] Victim and Instigator too close, using fallback knockback direction.");
        }
        knockbackDirection.Normalize();

        // Optional: Add upward force
        if (addUpwardForce)
        {
            knockbackDirection = (knockbackDirection + Vector2.up * upwardForceMultiplier).normalized;
        }
        // -----------------------------------


        // --- Apply Knockback based on Victim Type ---

        // Attempt 1: Check for EnemyMovement (for specific AI state handling)
        EnemyMovement enemyMovement = victim.GetComponent<EnemyMovement>();
        if (enemyMovement != null)
        {
            Debug.Log($"[KnockbackEffectSO] Found EnemyMovement on {victim.name}. Calling ApplyKnockback.");
            // Call the method on EnemyMovement to handle the force and state
            enemyMovement.ApplyKnockback(knockbackDirection, knockbackForce, knockbackStateDuration, forceMode);
            return; // Knockback handled by EnemyMovement
        }

        // Attempt 2: Check for PlayerMovement (if you have a similar state system for player)
        // PlayerMovement playerMovement = victim.GetComponent<PlayerMovement>();
        // if (playerMovement != null) {
        //     Debug.Log($"[KnockbackEffectSO] Found PlayerMovement on {victim.name}. Applying force via PlayerMovement.");
        //     playerMovement.ApplyKnockback(knockbackDirection, knockbackForce, knockbackStateDuration, forceMode); // Assuming similar method exists
        //     return;
        // }

        // Attempt 3: Fallback to applying force directly to Rigidbody2D (for Player or simple enemies)
        Rigidbody2D targetRb = victim.GetComponent<Rigidbody2D>();
        if (targetRb != null)
        {
            Debug.Log($"[KnockbackEffectSO] Found Rigidbody2D on {victim.name}. Applying force directly.");
            targetRb.linearVelocity = Vector2.zero; // Optional: Reset velocity before applying force for consistent results
            targetRb.AddForce(knockbackDirection * knockbackForce, forceMode);
            return; // Knockback applied directly
        }

        // If none of the above components were found
        Debug.LogWarning($"[KnockbackEffectSO] Victim '{victim.name}' has neither EnemyMovement nor Rigidbody2D. Knockback cannot be applied.");
    }

    public override bool ApplyResourceCost(GameObject target)
    {
        return true; // No cost
    }
}