using UnityEngine;

/// <summary>
/// Base abstract class for all ScriptableObject-based effects (weapon, item, status, etc.).
/// Derived classes must implement the abstract methods and can override virtual ones.
/// </summary>
public abstract class EffectSO : ScriptableObject
{
    [Header("Effect Info")]
    [Tooltip("Display name of the effect.")]
    public string effectName = "New Effect";
    [TextArea]
    [Tooltip("Description of what the effect does.")]
    public string effectDescription = "Effect description.";

    // --- Abstract Trigger Methods (Must be implemented by derived classes) ---

    /// <summary>
    /// Logic executed once when the item/weapon holding this effect is equipped or becomes active.
    /// </summary>
    /// <param name="target">The GameObject equipping the item (usually the Player).</param>
    public abstract void ApplyOnEquip(GameObject target);

    /// <summary>
    /// Logic executed once when the item/weapon holding this effect is unequipped or deactivated.
    /// </summary>
    /// <param name="target">The GameObject un-equipping the item (usually the Player).</param>
    public abstract void RemoveOnUnequip(GameObject target);

    /// <summary>
    /// Logic executed potentially every frame/tick while the effect is active (for passive effects).
    /// </summary>
    /// <param name="target">The GameObject the passive effect applies to (usually the Player).</param>
    /// <param name="deltaTime">Time since the last frame/tick.</param>
    public abstract void ApplyPassiveTick(GameObject target, float deltaTime);

    /// <summary>
    /// Modifies outgoing damage *before* it is applied to the victim.
    /// Called during the damage calculation phase.
    /// </summary>
    /// <param name="baseDamage">The initial damage value before this effect.</param>
    /// <param name="instigator">The GameObject dealing the damage.</param>
    /// <param name="victim">The GameObject receiving the damage.</param>
    /// <returns>The modified damage value.</returns>
    public abstract float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim);

    /// <summary>
    /// Logic executed immediately *after* damage has been successfully dealt to a victim.
    /// Useful for effects like life steal, mana steal, status application, etc.
    /// </summary>
    /// <param name="instigator">The GameObject that dealt the damage.</param>
    /// <param name="victim">The GameObject that received the damage.</param>
    /// <param name="damageDealt">The final amount of damage dealt after all modifications.</param>
    public abstract void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt);

    /// <summary>
    /// Checks if the necessary resources (e.g., mana, stamina, HP) are available and consumes them if possible.
    /// Called *before* an attack or ability use is fully committed.
    /// </summary>
    /// <param name="target">The GameObject attempting the action (usually the Player).</param>
    /// <returns>True if the resource cost was met (or no cost applies), false otherwise (action should be cancelled).</returns>
    public abstract bool ApplyResourceCost(GameObject target);


    // --- Virtual Trigger Methods (Can be optionally overridden by derived classes) ---

    /// <summary>
    /// Called by the system after an attack/use is successfully initiated.
    /// Override this and return true if the item itself (the WeaponSO or ItemSO this effect is attached to)
    /// should be consumed (removed from inventory/hotbar).
    /// </summary>
    /// <returns>True if the source item should be consumed, false otherwise (default).</returns>
    public virtual bool ShouldConsumeSourceItem()
    {
        // Default behavior: effects do not consume the item they are on.
        return false;
    }
}
