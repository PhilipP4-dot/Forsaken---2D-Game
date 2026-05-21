using UnityEngine;

[CreateAssetMenu(fileName = "Omnivamp_Effect", menuName = "Scriptable Objects/Effects/Omnivamp")]
public class OmnivampEffectSO : EffectSO
{
    [Range(0f, 1f)] // Percentage as a decimal
    public float vampPercent = 0.1f; // 10% default

    // This effect only cares about ApplyOnHit
    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        // Assuming Player has a PlayerHealth script/component
        PlayerHealth playerHealth = instigator.GetComponent<PlayerHealth>();
        if (playerHealth != null && damageDealt > 0)
        {
            float healthToRestore = damageDealt * vampPercent;
            playerHealth.RestoreHealth(healthToRestore); // Assuming RestoreHealth method exists
            // Debug.Log($"Omnivamp healed {healthToRestore}"); // Optional Debug
        }
    }

    // Implement other abstract methods with empty bodies
    public override void ApplyOnEquip(GameObject target) { }
    public override void RemoveOnUnequip(GameObject target) { }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { }
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim) { return baseDamage; }
    public override bool ApplyResourceCost(GameObject target) { return true; }
}