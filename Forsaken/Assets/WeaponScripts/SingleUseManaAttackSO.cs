using UnityEngine;

/// <summary>
/// A weapon effect that consumes all the user's mana (above a minimum threshold)
/// to add bonus damage to a ranged attack, and then consumes the item itself.
/// </summary>
// IMPORTANT: This effect's logic assumes it's only placed on RANGED weapons.
// It does not inherently prevent usage on melee weapons via code. Assign carefully.
[CreateAssetMenu(fileName = "New Single Use Mana Attack", menuName = "Effects/Damage/Single Use Mana Attack (Ranged)")]
public class SingleUseManaAttackSO : EffectSO // Inherit from the base EffectSO
{
    [Header("Mana Scaling Settings")]
    [Tooltip("Damage multiplier per point of mana consumed.")]
    public float damagePerManaPoint = 0.5f;
    [Tooltip("Minimum mana required to even attempt the attack.")]
    public float minimumManaCost = 1f; // Prevent firing with zero mana

    // Internal state to pass consumed mana amount from ApplyResourceCost to ModifyOutgoingDamage
    private float lastManaConsumed = 0f;

    // --- Implementations ---

    // No special logic needed on equip/unequip/passive tick for this effect
    public override void ApplyOnEquip(GameObject target) { /* Nothing */ }
    public override void RemoveOnUnequip(GameObject target) { /* Nothing */ }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { /* Nothing */ }

    /// <summary>
    /// Modifies outgoing damage by adding a bonus based on the mana consumed in ApplyResourceCost.
    /// </summary>
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim)
    {
        // Calculate bonus damage based on the mana we *just* consumed
        float bonusDamage = lastManaConsumed * damagePerManaPoint;

        // Calculate the final damage
        float finalDamage = baseDamage + bonusDamage;

        // Reset temporary storage AFTER using it for the calculation
        // Ensures the bonus is only applied once per mana consumption event
        lastManaConsumed = 0f;

        // Return the modified damage
        return finalDamage;
    }

    /// <summary>
    /// No special logic needed after the hit for this effect.
    /// </summary>
    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        // Nothing needed after the hit
    }

    /// <summary>
    /// Checks if the player has enough mana and consumes all mana if the minimum cost is met.
    /// Stores the consumed amount for ModifyOutgoingDamage.
    /// </summary>
    /// <returns>True if the cost was successfully paid, false otherwise.</returns>
    public override bool ApplyResourceCost(GameObject target)
    {
        // Reset the consumed amount at the start of every cost check
        lastManaConsumed = 0f;
        if (target == null) return false;

        PlayerMana playerMana = target.GetComponent<PlayerMana>();
        if (playerMana == null)
        {
            Debug.LogError($"{effectName}: Target does not have a PlayerMana component.", target);
            return false; // Cannot apply cost if no mana component
        }

        // Use the GetCurrentMana method from PlayerMana
        float currentMana = playerMana.GetCurrentMana();

        // Check if player has the minimum required mana
        if (currentMana < minimumManaCost)
        {
            // Debug.Log("Not enough mana for Mana Scaling attack.");
            return false; // Cannot afford minimum cost
        }

        // Store the amount to be consumed (which is all current mana)
        // Store it BEFORE consuming, just in case ConsumeMana had an issue
        lastManaConsumed = currentMana;

        // Call the ConsumeMana method from PlayerMana.cs
        bool consumed = playerMana.ConsumeMana(lastManaConsumed);

        // Double-check the result from ConsumeMana
        if (!consumed) {
             // This case should technically not be reached if currentMana >= minimumManaCost >= 0
             // but it's safe error handling.
             Debug.LogError($"{effectName}: ConsumeMana({lastManaConsumed}) failed unexpectedly even though current mana ({currentMana}) was sufficient.", target);
             lastManaConsumed = 0f; // Ensure no bonus damage if consumption somehow failed
             return false;
        }

        // Debug.Log($"Consumed {lastManaConsumed} mana for scaling damage.");
        return true; // Cost paid successfully
    }

    /// <summary>
    /// Signals that the item this effect is attached to should be consumed after use.
    /// </summary>
    /// <returns>True</returns>
    public override bool ShouldConsumeSourceItem()
    {
        // Yes, this effect consumes the item it's on.
        return true;
    }
}
