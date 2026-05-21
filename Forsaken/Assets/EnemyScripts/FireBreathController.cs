using UnityEngine;
using System.Collections.Generic; // Needed for Dictionary

public class FireBreathController : MonoBehaviour
{
    // --- Configurable Properties (Set by FlyingDemonAI when spawned) ---
    public float damagePerTick = 5f;   // Damage applied each tick
    public float tickInterval = 0.25f; // How often damage ticks (e.g., 4 times per second)
    public float effectDuration = 1.5f; // How long the effect lasts
    public LayerMask targetMask;      // Which layers to damage (Player and/or Ally if enemy, Enemy if ally)
    public string attackerTag;        // Tag of the demon that fired this ("Enemy" or "Ally")
    // -------------------------------------------------------------------

    // Internal tracking
    private Dictionary<Collider2D, float> targetsInTrigger; // Track targets and time until next damage tick
    private float lifeTimer; // How long until this effect destroys itself

    void Awake()
    {
        targetsInTrigger = new Dictionary<Collider2D, float>();
        lifeTimer = effectDuration;
        // Automatically destroy the effect after its duration + small buffer
        Destroy(gameObject, effectDuration + 0.1f);
    }

    void Update()
    {
        // --- Apply Damage Ticks ---
        // Create a temporary list of keys to iterate over, as we might modify the dictionary
        List<Collider2D> currentTargets = new List<Collider2D>(targetsInTrigger.Keys);

        foreach (Collider2D targetCollider in currentTargets)
        {
            if (targetCollider == null) // Target might have been destroyed
            {
                targetsInTrigger.Remove(targetCollider); // Clean up dictionary
                continue;
            }

            // Decrease timer for this target
            targetsInTrigger[targetCollider] -= Time.deltaTime;

            // If timer reaches zero, apply damage and reset timer
            if (targetsInTrigger[targetCollider] <= 0f)
            {
                ApplyBurstDamage(targetCollider.gameObject);
                targetsInTrigger[targetCollider] = tickInterval; // Reset timer for next tick
            }
        }
        // ------------------------

        // --- Effect Lifetime --- (Optional: Could shrink/fade particles here)
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            // Particles should ideally have a limited lifetime themselves,
            // but we can force stop emission before destruction if needed.
            // var emission = GetComponent<ParticleSystem>()?.emission;
            // if (emission != null) emission.enabled = false;
        }
        // -----------------------
    }

    // --- Detect Targets Entering Fire ---
    void OnTriggerEnter2D(Collider2D other)
    {
        GameObject hitObject = other.gameObject;

        // Check if the hit object is on a target layer AND does NOT have the same tag as the attacker
        if (((1 << hitObject.layer) & targetMask) != 0 && !hitObject.CompareTag(attackerTag))
        {
             // Add target to dictionary if not already present
             if (!targetsInTrigger.ContainsKey(other))
             {
                 targetsInTrigger.Add(other, 0f); // Add target, damage immediately on next Update tick
                 Debug.Log($"{gameObject.name} starting DOT on {other.name}");
             }
        }
    }
    // ------------------------------------

    // --- Detect Targets Leaving Fire ---
    void OnTriggerExit2D(Collider2D other)
    {
        // Remove target from dictionary when it leaves the trigger
        if (targetsInTrigger.ContainsKey(other))
        {
            targetsInTrigger.Remove(other);
            Debug.Log($"{gameObject.name} DOT stopped on {other.name}");
        }
    }
    // -----------------------------------

    // --- Apply Damage ---
    void ApplyBurstDamage(GameObject target)
    {
        if (target == null) return;

        // Debug.Log($"Applying {damagePerTick} fire burst damage to {target.name}"); // Slightly spammy

        // Try damaging PlayerHealth or EnemyHealth
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();

        if (playerHealth != null) { playerHealth.TakeDamage(damagePerTick); }
        else if (enemyHealth != null) { enemyHealth.TakeDamage(damagePerTick); }
    }
    // --------------------
}