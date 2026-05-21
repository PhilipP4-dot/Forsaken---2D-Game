using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/ItemSO")]
public class ItemSO : ScriptableObject
{
    [Header("Basic Item Info")]
    public string itemName = "New Item Name";
    [Tooltip("The icon displayed in the inventory UI")]
    public Sprite itemSprite;
    [TextArea]
    [Tooltip("Description shown in the inventory")]
    public string itemDescription = "Item Description";

    [Header("Consumable Effect (If Applicable)")]
    public StatToChange statToAffect = StatToChange.none;
    [Tooltip("The amount to restore (e.g., health points, mana points).")]
    public int amountToChangeAttribute; // Consider renaming to amountToRestore

    [Header("Drop Settings")]
    [Tooltip("Multiplier for the item's scale when dropped. 1 = original size.")]
    public float dropScaleMultiplier = 0.65f;

    [Header("World Representation")]
    [Tooltip("The prefab to instantiate when this item is dropped.")]
    public GameObject droppedItemPrefab;


    /// <summary>
    /// Called when the item is used (e.g., from the hotbar).
    /// Primarily for non-weapon consumables like potions.
    /// </summary>
    /// <returns>True if the item was successfully consumed/used (e.g., health/mana was restored), False otherwise (e.g., resource was already full).</returns>
    public virtual bool UseItem()
    {
        // Find components only if needed based on statToAffect
        if (statToAffect == StatToChange.Health)
        {
            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                // Call RestoreHealth and return its result directly
                return playerHealth.RestoreHealth(amountToChangeAttribute);
            }
            else
            {
                 Debug.LogError($"ItemSO ({itemName}): PlayerHealth component not found in scene! Cannot apply health effect.", this);
                 return false; // Failed to find component
            }
        }
        else if (statToAffect == StatToChange.Mana)
        {
            PlayerMana playerMana = FindFirstObjectByType<PlayerMana>();
             if (playerMana != null)
             {
                 // Call RegenerateMana and return its result directly
                 return playerMana.RegenerateMana(amountToChangeAttribute);
             }
             else
             {
                  Debug.LogError($"ItemSO ({itemName}): PlayerMana component not found in scene! Cannot apply mana effect.", this);
                  return false; // Failed to find component
             }
        }

        // If statToAffect is 'none' or target component wasn't found
        // Debug.Log($"ItemSO ({itemName}): UseItem() called but no effect defined or target component missing.");
        return false; // No action taken, so return false
    }

    // Enum defined within the class
    public enum StatToChange { none, Mana, Health }

    // Consider adding other item properties here:
    // public bool isStackable = true;
    // public int maxStackSize = 99;
    // public ItemType itemType = ItemType.Consumable;
}
