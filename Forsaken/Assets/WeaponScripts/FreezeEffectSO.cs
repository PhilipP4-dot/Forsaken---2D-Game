using UnityEngine;
using System.Collections; // Required for Coroutines

// Note: This script relies on an EnemyMovement script existing on the target.
// Ensure EnemyMovement.cs is created and attached to your enemy prefabs.

[CreateAssetMenu(fileName = "New Freeze Effect", menuName = "Effects/Status/Freeze")]
public class FreezeEffectSO : EffectSO // Inherit from the base EffectSO
{
    [Header("Freeze Settings")]
    [Tooltip("How long the enemy should be frozen.")]
    public float freezeDuration = 1.0f;

    // --- Implementations ---

    public override void ApplyOnEquip(GameObject target) { /* Nothing needed for this specific effect */ }
    public override void RemoveOnUnequip(GameObject target) { /* Nothing needed for this specific effect */ }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { /* Nothing needed for this specific effect */ }

    /// <summary>
    /// Modifies outgoing damage. This effect doesn't change damage.
    /// </summary>
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim)
    {
        // This effect doesn't change damage
        return baseDamage;
    }

    /// <summary>
    /// Called after damage is dealt. Attempts to find EnemyMovement on the victim and freeze it.
    /// </summary>
    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        // --- DEBUG LOG ---
        Debug.Log($"[FreezeEffectSO] ApplyOnHit called for FreezeEffect on victim: {victim?.name ?? "null"}");
        // ---------------

        if (victim == null) return;

        // Try to get the EnemyMovement component from the victim
        EnemyMovement enemyMovement = victim.GetComponent<EnemyMovement>();
        if (enemyMovement != null)
        {
            // --- DEBUG LOG ---
            Debug.Log($"[FreezeEffectSO] Found EnemyMovement on {victim.name}. Calling Freeze({freezeDuration}).");
            // ---------------
            // Apply the freeze effect via the EnemyMovement script
            enemyMovement.Freeze(freezeDuration);
        }
        else
        {
            // --- DEBUG LOG ---
            // Log a warning if the component is missing, as the effect cannot work without it.
            Debug.LogWarning($"[FreezeEffectSO] Victim '{victim.name}' does not have an EnemyMovement component attached. Freeze effect cannot be applied.");
            // ---------------
        }
    }

    /// <summary>
    /// Checks resource cost. This effect has no cost.
    /// </summary>
    /// <returns>True, as no cost is incurred.</returns>
    public override bool ApplyResourceCost(GameObject target)
    {
        // No cost for this effect
        return true;
    }
}
