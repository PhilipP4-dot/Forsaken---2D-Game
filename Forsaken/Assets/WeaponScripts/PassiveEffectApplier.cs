using UnityEngine;

// Attach this script to your Player GameObject
public class PassiveEffectApplier : MonoBehaviour
{
    private PlayerAttackController attackController; // Reference to the attack controller

    void Start()
    {
        // Get the PlayerAttackController component from the same GameObject
        attackController = GetComponent<PlayerAttackController>();
        if (attackController == null)
        {
            Debug.LogError("PassiveEffectApplier needs PlayerAttackController on the same GameObject!", this);
            enabled = false; // Disable this script if controller is missing
        }
    }

    void Update()
    {
        // Check if the attack controller reference is valid
        if (attackController == null)
        {
            return; // Exit if no controller found
        }

        // --- Use the public getter method ---
        WeaponSO currentWeapon = attackController.GetCurrentWeaponData();
        // ------------------------------------

        // Check if the current weapon data and its effects list are valid
        if (currentWeapon == null || currentWeapon.weaponEffects == null)
        {
            return; // Exit if no weapon or no effects list on the weapon
        }

        // Apply passive tick effects from the current weapon's effects list
        foreach (EffectSO effect in currentWeapon.weaponEffects) // Use the local 'currentWeapon' variable
        {
            // Use null-conditional operator ?. for safety in case effect is null in the list
            effect?.ApplyPassiveTick(this.gameObject, Time.deltaTime);
        }
    }
}
