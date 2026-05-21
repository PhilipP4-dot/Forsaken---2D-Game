using UnityEngine;

// Assuming this inherits from EffectSO
// Replace with your actual class name and menu path if different
[CreateAssetMenu(fileName = "New Repentance Effect", menuName = "Effects/Custom/Repentance")]
public class repentance : EffectSO // Ensure correct inheritance and class name
{
    [Header("Repentance Settings")]
    [Tooltip("Damage taken by the player when this effect triggers.")]
    public float selfDamageAmount = 5f;
    // Add other fields for the actual repentance buff/effect if needed

    public override void ApplyOnEquip(GameObject target) { /* Implement if needed */ }
    public override void RemoveOnUnequip(GameObject target) { /* Implement if needed */ }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { /* Implement if needed */ }
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim) { return baseDamage; } // Does not modify damage directly

    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        // Example: Apply self-damage AFTER hitting an enemy
        if (instigator != null)
        {
            PlayerHealth playerHealth = instigator.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // Use TakeDamage instead of DecreaseHealth
                // Use GetCurrentHealth() instead of health
                if (playerHealth.GetCurrentHealth() > selfDamageAmount) // Avoid instant death?
                {
                    Debug.Log($"Repentance: Applying {selfDamageAmount} self damage.");
                    playerHealth.TakeDamage(selfDamageAmount);

                    // TODO: Apply the actual "Repentance" buff/effect here
                    // e.g., playerHealth.RestoreHealth(damageDealt * 0.1f); // Heal % of damage dealt
                    // e.g., instigator.AddComponent<TemporaryDefenseBuff>(); // Add a buff component
                }
                else
                {
                    Debug.Log("Repentance: Not enough health to apply self damage.");
                }
            }
        }
    }

    public override bool ApplyResourceCost(GameObject target)
    {
        // This effect might not have a pre-cost, maybe the self-damage IS the cost (handled in ApplyOnHit)
        return true;
    }

    // Optional: Override ShouldConsumeSourceItem if needed
    // public override bool ShouldConsumeSourceItem() { return false; }
}
