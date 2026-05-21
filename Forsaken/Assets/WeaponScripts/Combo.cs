using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ComboGauntlets_Effect", menuName = "Scriptable Objects/Effects/ComboGauntlets", order = 3)]
public class ComboGauntlets : EffectSO
{
    private class HitTracker
    {
        public int hitCount = 0;
        public float timer = 0f;
    }

    private Dictionary<GameObject, HitTracker> hitTrackers = new Dictionary<GameObject, HitTracker>();
    private Dictionary<GameObject, List<float>> buffTimers = new Dictionary<GameObject, List<float>>();

    public override void ApplyOnHit(GameObject instigator, GameObject victim, float damageDealt)
    {
        if (!hitTrackers.ContainsKey(instigator))
        {
            hitTrackers[instigator] = new HitTracker();
        }

        HitTracker tracker = hitTrackers[instigator];
        tracker.hitCount++;
        tracker.timer = 5f; // Reset timer to 5s on hit

        if (tracker.hitCount >= 3)
        {
            tracker.hitCount = 0;
            AddBuffToken(instigator);
            Debug.Log("Combo Gauntlets: Buff Token Gained!");
        }
    }

    private void AddBuffToken(GameObject instigator)
    {
        if (!buffTimers.ContainsKey(instigator))
        {
            buffTimers[instigator] = new List<float>();
        }

        buffTimers[instigator].Add(15f); // Each token lasts 15 seconds
    }

    public override float ModifyOutgoingDamage(float baseDamage, GameObject instigator, GameObject victim)
    {
        if (!buffTimers.ContainsKey(instigator))
        {
            return baseDamage;
        }

        int tokens = buffTimers[instigator].Count;
        float damageMultiplier = 1f + (0.1f * tokens);

        return baseDamage * damageMultiplier;
    }

    public override void ApplyPassiveTick(GameObject target, float deltaTime)
    {
        if (hitTrackers.ContainsKey(target))
        {
            HitTracker tracker = hitTrackers[target];
            tracker.timer -= deltaTime;
            if (tracker.timer <= 0)
            {
                tracker.hitCount = 0;
            }
        }

        if (buffTimers.ContainsKey(target))
        {
            List<float> timers = buffTimers[target];
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                timers[i] -= deltaTime;
                if (timers[i] <= 0)
                {
                    timers.RemoveAt(i);
                    Debug.Log("Combo Gauntlets: Buff Token Expired");
                }
            }
        }
    }

    public override void ApplyOnEquip(GameObject target) { }
    public override void RemoveOnUnequip(GameObject target)
    {
        hitTrackers.Remove(target);
        buffTimers.Remove(target);
    }

    public override bool ApplyResourceCost(GameObject target) { return true; }
}
