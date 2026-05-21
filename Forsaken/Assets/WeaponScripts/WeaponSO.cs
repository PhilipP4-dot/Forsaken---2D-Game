using UnityEngine;
using System.Collections.Generic; // Needed for List<>

// --- Define Enum outside the class ---
// This is the correct place for the definition.
// Ensure this definition does NOT exist in any other script file.
public enum WeaponType { Melee, Ranged }
// ------------------------------------

/// <summary>
/// Scriptable Object defining the data for a weapon, inheriting basic info from ItemSO.
/// </summary>
[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Inventory/WeaponSO")] // Updated menu path slightly
public class WeaponSO : ItemSO // Inherits from ItemSO (ensure ItemSO.cs exists)
{
    [Header("Weapon Stats")]
    [Tooltip("The type of weapon (determines attack logic).")]
    public WeaponType weaponType = WeaponType.Melee; // Use the enum defined above

    [Tooltip("Base damage dealt by the weapon.")]
    public float damage = 10f;

    [Tooltip("For Melee: Attack reach distance from the attack point. For Ranged: Projectile speed.")]
    public float rangeOrSpeed = 1.5f;

    [Tooltip("How many attacks can be performed per second.")]
    public float attackRate = 2f;

    [Header("Ranged Specific")]
    [Tooltip("Assign the projectile prefab ONLY if Weapon Type is Ranged.")]
    public GameObject projectilePrefab; // Assign in Inspector for bows, guns, etc.

    [Header("Weapon Effects")]
    [Tooltip("Optional list of effects (Scriptable Objects inheriting from EffectSO) applied by this weapon.")]
    public List<EffectSO> weaponEffects; // Assign Effect SO assets here in the Inspector

    /// <summary>
    /// Overrides the base UseItem. Weapons typically aren't "consumed" by pressing the 'Use' key.
    /// Actual weapon actions (attacking) are handled by PlayerAttackController based on its 'attackKey'.
    /// </summary>
    /// <returns>False, indicating the weapon item itself wasn't consumed.</returns>
    public override bool UseItem()
    {
        // Equipping/attacking is handled by HotbarManager selection and PlayerAttackController input.
        // Returning false prevents the HotbarManager from trying to decrement quantity when 'Use' key is pressed on a weapon.
        // Debug.Log($"WeaponSO {itemName}: UseItem called (returns false). Action handled by AttackController.");
        return false;
    }

    // Note: Ensure your ItemSO base class exists and has the fields this script might implicitly use
    // (like itemName, itemSprite, itemDescription, droppedItemPrefab, dropScaleMultiplier).
}
