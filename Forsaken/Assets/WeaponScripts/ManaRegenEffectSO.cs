using UnityEngine;

[CreateAssetMenu(fileName = "ManaRegen_Effect", menuName = "Scriptable Objects/Effects/Mana Regen")]
public class ManaRegenEffectSO : EffectSO
{
    public float manaPerSecond = 1.0f;

    // This effect only cares about passive ticks
    public override void ApplyPassiveTick(GameObject target, float deltaTime)
    {
        // Assuming Player has a PlayerMana script/component
        PlayerMana playerMana = target.GetComponent<PlayerMana>();
        if (playerMana != null)
        {
            playerMana.RegenerateMana(manaPerSecond * deltaTime);
            // Debug.Log($"Regen Mana: {manaPerSecond * deltaTime}"); // Optional Debug
        }
    }

    // Implement other abstract methods with empty bodies as they don't apply here
    public override void ApplyOnEquip(GameObject target) { }
    public override void RemoveOnUnequip(GameObject target) { }
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim) { return baseDamage; } // No change
    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt) { }
    public override bool ApplyResourceCost(GameObject target) { return true; } // No cost
}