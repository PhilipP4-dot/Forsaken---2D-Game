using UnityEngine;

[CreateAssetMenu(fileName = "ManaCost_Effect", menuName = "Scriptable Objects/Effects/Mana Cost")]
public class ManaCostEffectSO : EffectSO
{
    public float manaCost = 5.0f;

    // This effect only cares about resource costs
    public override bool ApplyResourceCost(GameObject target)
    {
        PlayerMana playerMana = target.GetComponent<PlayerMana>();
        if (playerMana != null)
        {
            return playerMana.ConsumeMana(manaCost); // Assume ConsumeMana returns true if successful
        }
        return true; // If no mana system, cost is effectively paid (or change to false?)
    }

    // Implement other abstract methods with empty bodies
    public override void ApplyOnEquip(GameObject target) { }
    public override void RemoveOnUnequip(GameObject target) { }
    public override void ApplyPassiveTick(GameObject target, float deltaTime) { }
    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim) { return baseDamage; }
    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt) { }
}