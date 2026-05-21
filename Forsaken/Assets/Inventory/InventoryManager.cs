using UnityEngine;
using System.Collections.Generic; // Keep if used elsewhere, AddItem doesn't strictly need it
using System.Collections; // Keep if used elsewhere

public class InventoryManager : MonoBehaviour
{
    public GameObject InventoryMenu;
    public bool menuActivated;
    public ItemSlot[] itemSlot; // Assign these in the Inspector!
    public ItemSO[] itemSOs; // Make sure W_Sword, W_Bow etc ARE in this list!

    private PlayerAttackController playerAttackController;

    void Start()
    {
        // Find the PlayerAttackController instance
        playerAttackController = FindObjectOfType<PlayerAttackController>();
        if (playerAttackController == null)
        {
             Debug.LogError("InventoryManager could not find PlayerAttackController!");
        }

        // Optional: Verify itemSlot array is assigned
        if (itemSlot == null || itemSlot.Length == 0)
        {
            Debug.LogError("InventoryManager: itemSlot array is not assigned or empty in the Inspector!");
        }
    }

    void Update()
    {
        // Inventory toggle logic
        if (Input.GetButtonDown("Inventory") && menuActivated)
        {
            Time.timeScale = 1;
            InventoryMenu.SetActive(false);
            menuActivated = false;
            // Optional: Notify controller when closing inventory
            // NotifyWeaponChange(GetCurrentlySelectedItemName());
        }
        else if (Input.GetButtonDown("Inventory") && !menuActivated)
        {
            Time.timeScale = 0;
            InventoryMenu.SetActive(true);
            menuActivated = true;
            // Ensure controller knows current weapon when opening
            NotifyWeaponChange(GetCurrentlySelectedItemName());
        }
    }

    /// <summary>
    /// Notifies the PlayerAttackController about the currently selected item.
    /// </summary>
    /// <param name="itemName">Name of the selected item, or null if none.</param>
    public void NotifyWeaponChange(string itemName)
    {
        // Pass the name of the selected item (or null if none) to the attack controller
        playerAttackController?.SetActiveWeapon(itemName); // Use null-conditional operator ?.
    }

    /// <summary>
    /// Helper method to find the name of the currently selected item slot.
    /// </summary>
    /// <returns>The item name if a slot is selected, otherwise null.</returns>
    private string GetCurrentlySelectedItemName()
    {
        if (itemSlot == null) return null;
        foreach (ItemSlot slot in itemSlot)
        {
            // Important: Check if slot reference itself is not null
            if (slot != null && slot.thisItemSelected)
            {
                return slot.itemName;
            }
        }
        return null; // Nothing selected
    }

    /// <summary>
    /// Attempts to add an item to the inventory, first trying to stack, then finding empty slots.
    /// </summary>
    /// <param name="itemName">Name of the item to add.</param>
    /// <param name="quantityToAdd">How many to add.</param>
    /// <param name="itemSprite">Sprite for the item.</param>
    /// <param name="itemDescription">Description of the item.</param>
    /// <returns>The number of items that could NOT be added (0 if all were added successfully).</returns>
    public int AddItem(string itemName, int quantityToAdd, Sprite itemSprite, string itemDescription)
    {
        if (itemSlot == null || itemSlot.Length == 0)
        {
             Debug.LogError("AddItem failed: itemSlot array is null or empty!");
             return quantityToAdd; // Cannot add if no slots exist
        }

        int remainingQuantity = quantityToAdd;

        // --- First Pass: Try to stack with existing, non-full slots ---
        for (int i = 0; i < itemSlot.Length; i++)
        {
            if (remainingQuantity <= 0) break; // Stop if all items are placed

            ItemSlot slot = itemSlot[i];
            // Check if slot exists, has the same item, and is not full
            if (slot != null && slot.quantity > 0 && !slot.isFull && slot.itemName == itemName)
            {
                 Debug.Log($"Attempting to stack {itemName} in existing slot {i}");
                 remainingQuantity = slot.AddItem(itemName, remainingQuantity, itemSprite, itemDescription);
            }
        }

        // --- Second Pass: Try to fill empty slots ---
        if (remainingQuantity > 0) // Only proceed if items still need placement
        {
             for (int i = 0; i < itemSlot.Length; i++)
             {
                 if (remainingQuantity <= 0) break; // Stop if all items are placed

                 ItemSlot slot = itemSlot[i];
                 // Check if slot exists and is empty
                 if (slot != null && slot.quantity <= 0)
                 {
                     Debug.Log($"Attempting to place {itemName} in empty slot {i}");
                     remainingQuantity = slot.AddItem(itemName, remainingQuantity, itemSprite, itemDescription);
                 }
             }
        }

        // After trying all slots
        if (remainingQuantity > 0)
        {
            Debug.LogWarning($"Inventory full or no suitable slot. Could not add {remainingQuantity} of {itemName}.");
        }
        else
        {
            Debug.Log($"Successfully added all {quantityToAdd} of {itemName} to inventory.");
        }

        // Return the final amount that could NOT be added
        return remainingQuantity;
    }


    /// <summary>
    /// Deselects all item slots and notifies the PlayerAttackController.
    /// </summary>
    public void DeSelectAllSlots()
    {
         if (itemSlot == null) return; // Safety check

         for (int i = 0; i < itemSlot.Length; i++)
         {
             if (itemSlot[i] != null) // Add null check for each slot
             {
                 itemSlot[i].selectedShader?.SetActive(false); // Use null-conditional ?.
                 itemSlot[i].thisItemSelected = false;
             }
         }
         // Notify controller that nothing is actively selected
         NotifyWeaponChange(null);
    }

    /// <summary>
    /// Handles the 'Use' action for an item, differentiating between consumables and weapons.
    /// </summary>
    /// <param name="itemName">Name of the item to use.</param>
    /// <returns>True if the item was successfully consumed, false otherwise.</returns>
     public bool UseItem(string itemName)
     {
         Debug.Log($"Attempting to use item: {itemName}");
         if (itemSOs == null)
         {
             Debug.LogError("itemSOs array is null in InventoryManager!");
             return false;
         }

         for (int i = 0; i < itemSOs.Length; i++)
         {
             if(itemSOs[i] != null && itemSOs[i].itemName == itemName) // Add null check
             {
                 // Check if it's a WeaponSO first
                 if (itemSOs[i] is WeaponSO)
                 {
                     // Debug.Log($"{itemName} is a weapon, 'Use' action likely means equip/attack, not consume.");
                     // Let the WeaponSO's UseItem decide (which currently returns false, preventing consumption)
                     return itemSOs[i].UseItem();
                 }
                 else // Otherwise, treat as a potentially consumable item
                 {
                     Debug.Log($"Using consumable/other item: {itemName}");
                     bool usable = itemSOs[i].UseItem(); // Call the ItemSO's UseItem logic
                     return usable; // Return whether the ItemSO reported it was successfully used/consumed
                 }
             }
         }
         Debug.LogWarning($"ItemSO not found for: {itemName}");
         return false;
     }

} // End of class