using UnityEngine;
using System.Collections; // Added for potential cooldown coroutine

/// <summary>
/// Attached to item prefabs instantiated in the game world (dropped items).
/// Handles basic item data, pickup triggers, and tooltips on hover.
/// Requires a Collider2D (set to Is Trigger for pickup) and Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Item : MonoBehaviour
{
    [Header("Item Identification")]
    [Tooltip("The unique name/ID matching the ItemSO's itemName. MUST be set correctly in the prefab.")]
    public string itemName;
    [Tooltip("Quantity of this item stack (usually 1 for dropped items).")]
    public int quantity = 1;

    // --- References (obtained at runtime) ---
    private TooltipManager tooltipManager;
    private ItemSO itemDataSO; // Reference to the full ScriptableObject data

    // --- State ---
    private bool canBePickedUp = true; // Flag to prevent multiple pickup attempts per trigger

    void Awake()
    {
        // Ensure Rigidbody is present
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) { Debug.LogError($"Item '{itemName}' prefab is missing Rigidbody2D!", this); }
        // Note: BodyType and Gravity should be set correctly on the prefab itself now.

        // Ensure Collider is set to trigger for pickup detection
        Collider2D col = GetComponent<Collider2D>();
        if (col == null) { Debug.LogError($"Item '{itemName}' prefab is missing a Collider2D!", this); }
        else if (!col.isTrigger) {
            // You might have multiple colliders. Ensure at least ONE is a trigger.
            // It's generally better to configure this on the prefab directly.
            // This check might need refinement if using multiple colliders.
            Debug.LogWarning($"Item '{itemName}' collider was not set to IsTrigger. Ensure the PICKUP collider is a trigger.", this);
            // Consider finding a specific collider meant for pickup if needed:
            // Collider2D pickupCollider = GetComponents<Collider2D>().FirstOrDefault(c => c.isTrigger);
            // if (pickupCollider == null) Debug.LogError("No trigger collider found for pickup!");
        }

        // Find Tooltip Manager
        tooltipManager = FindFirstObjectByType<TooltipManager>(); // Use FindFirstObjectByType
        if (tooltipManager == null) { Debug.LogWarning($"Item '{itemName}': Could not find TooltipManager!", this); }

        // Find ItemSO data
        FindItemData();
    }

    /// <summary> Finds the corresponding ItemSO based on the itemName. </summary>
    private void FindItemData()
    {
        if (string.IsNullOrEmpty(itemName)) { Debug.LogError("Item script on " + gameObject.name + " missing itemName!", this); return; }
        HotbarManager manager = FindFirstObjectByType<HotbarManager>(); // Use FindFirstObjectByType
        if (manager != null) { itemDataSO = manager.FindItemSOByName_Public(itemName); }
        else { Debug.LogError($"Item '{itemName}': Could not find HotbarManager!", this); }
        if (itemDataSO == null) { Debug.LogError($"Item '{itemName}': Could not find matching ItemSO data!", this); }
    }

    // --- Mouse Hover Events ---
    void OnMouseEnter()
    {
        // Show tooltip only if data and manager are valid
        if (tooltipManager != null && itemDataSO != null) {
            string tooltipContent = $"<b>{itemDataSO.itemName}</b>\n<size=90%>{itemDataSO.itemDescription}</size>";
            tooltipManager.ShowTooltip(tooltipContent, Input.mousePosition);
        } else if (tooltipManager != null) {
             tooltipManager.ShowTooltip($"<b>{itemName}</b>\n<size=90%>(Item data lookup failed)</size>", Input.mousePosition);
        }
    }
    void OnMouseExit() { tooltipManager?.HideTooltip(); }
    void OnDisable() { tooltipManager?.HideTooltip(); }


    // --- Pickup Logic ---

    /// <summary>
    /// Called by Unity's physics system when another Collider2D enters this object's trigger collider.
    /// </summary>
    void OnTriggerEnter2D(Collider2D other)
    {
        // Debug.Log($"[Item '{itemName}'] OnTriggerEnter2D detected collision with: {other.gameObject.name} (Tag: {other.tag})");

        // Check if pickup is allowed AND if the entering object is the Player
        if (canBePickedUp && other.CompareTag("Player"))
        {
            // Debug.Log($"[Item '{itemName}'] Player entered trigger. Attempting pickup...");
            // Immediately try to pick up
            bool pickedUp = AttemptPickup(other.gameObject);

            // If pickup was successful (even partially), disable further attempts for this instance
            if (pickedUp)
            {
                canBePickedUp = false; // Prevent re-triggering before destruction
                // Optional: Start a short cooldown coroutine if needed, but disabling might be enough
            }
        }
    }

    /// <summary>
    /// Attempts to add this item to the player's inventory (HotbarManager).
    /// Destroys the item GameObject if fully picked up.
    /// </summary>
    /// <returns>True if *any* amount of the item was successfully picked up, false otherwise.</returns>
    public bool AttemptPickup(GameObject picker)
    {
        HotbarManager hotbar = FindFirstObjectByType<HotbarManager>();
        if (hotbar == null) {
             Debug.LogError($"Item '{itemName}': Cannot attempt pickup, HotbarManager not found!", this);
             return false;
        }

        // Debug.Log($"[Item '{itemName}'] Calling HotbarManager.AddItem('{itemName}', {quantity})");
        int quantityBeforeAdding = quantity; // Store original quantity
        int remaining = hotbar.AddItem(itemName, quantity); // Try to add

        // Check if any amount was successfully added (remaining < original quantity)
        if (remaining < quantityBeforeAdding)
        {
            // Debug.Log($"[Item '{itemName}'] Successfully picked up {quantityBeforeAdding - remaining} of {itemName}.");
            if (remaining <= 0) {
                // All items were picked up
                tooltipManager?.HideTooltip(); // Hide tooltip before destroying
                Destroy(gameObject); // Destroy this item GameObject
            } else {
                // Partially picked up (e.g., stackable item filled a partial slot)
                quantity = remaining; // Update the quantity remaining on the ground
                // Debug.Log($"[Item '{itemName}'] {remaining} quantity of {itemName} remains on ground.");
            }
            return true; // Pickup was successful (at least partially)
        }
        else
        {
            // No items could be added (e.g., hotbar full)
            // Debug.Log($"[Item '{itemName}'] Could not pick up (HotbarManager.AddItem returned {remaining}). Hotbar likely full.");
            return false; // Pickup failed
        }
    }
}
