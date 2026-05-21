using UnityEngine;

// Assuming this inherits from EffectSO
// Replace with your actual class name and menu path if different
[CreateAssetMenu(fileName = "New Self Damage Effect", menuName = "Effects/Custom/Self Damage")]
public class selfdamage : EffectSO // Ensure correct inheritance and class name
{
    [Header("Self Damage Settings")]
    [Tooltip("Amount of HP consumed per attack/use.")]
    public float hpCost = 10f;
    [Tooltip("Minimum HP the player must have AFTER the cost to perform the action.")]
    public float minimumHpThreshold = 1f; // e.g., Don't allow if cost would kill player

    // Add fields for any BONUS this effect gives (e.g., bonus damage) if applicable
    // public float damageBonus = 0f;

    // --- Implementations ---

    public override void ApplyOnEquip(GameObject target) { /* Nothing */ }
    public override void RemoveOnUnequip(GameObject target) { /* Nothing */ }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { /* Nothing */ }

    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim)
    {
        // Example: If this effect also adds damage
        // if (costPaidSuccessfully) { // Need a way to track if cost was paid if bonus depends on it
        //    return baseDamage + damageBonus;
        // }
        return baseDamage; // Default: no damage modification
    }

    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        // This effect's cost is usually applied BEFORE the hit via ApplyResourceCost
    }

    // Apply HP cost BEFORE the attack happens
    public override bool ApplyResourceCost(GameObject target)
    {
        if (target == null) return false;

        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            Debug.LogError($"{effectName}: Target missing PlayerHealth component.", target);
            return false;
        }

        // Use GetCurrentHealth() instead of accessing 'health' directly
        float currentHp = playerHealth.GetCurrentHealth();

        // Check if player has enough health to pay the cost AND remain above the threshold
        if (currentHp > hpCost && (currentHp - hpCost) >= minimumHpThreshold)
        {
            // Use TakeDamage instead of DecreaseHealth
            playerHealth.TakeDamage(hpCost);
            Debug.Log($"SelfDamage Effect: Paid {hpCost} HP cost.");
            // costPaidSuccessfully = true; // Set flag if needed by ModifyOutgoingDamage
            return true; // Cost paid
        }
        else
        {
            Debug.Log($"SelfDamage Effect: Not enough HP ({currentHp}) to pay cost ({hpCost}) or would fall below threshold ({minimumHpThreshold}).");
            // costPaidSuccessfully = false; // Reset flag
            return false; // Cannot afford cost
        }
    }

    // Optional: Override ShouldConsumeSourceItem if needed
    // public override bool ShouldConsumeSourceItem() { return false; }
}
