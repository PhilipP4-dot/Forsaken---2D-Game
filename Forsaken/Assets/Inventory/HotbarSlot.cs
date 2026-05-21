using UnityEngine;
using UnityEngine.UI; // Needed for Image
using TMPro; // Needed for TextMeshProUGUI
using UnityEngine.EventSystems; // Required for Pointer Events Interfaces

/// <summary>
/// Represents a single slot in the Hotbar UI. Handles visuals, selection highlight,
/// click events (if Button component used), and tooltip display on hover.
/// </summary>
public class HotbarSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler // Implement interfaces for hover
{
    [Header("UI References (Assign in Prefab)")]
    [Tooltip("Image component to display the item's icon. DRAG THE IMAGE GAMEOBJECT HERE IN PREFAB EDITOR.")]
    [SerializeField] private Image itemIconImage;
    [Tooltip("TextMeshPro component to display the item quantity. DRAG THE TEXT GAMEOBJECT HERE.")]
    [SerializeField] private TextMeshProUGUI quantityText;
    [Tooltip("Optional: GameObject shown when this slot is selected. DRAG THE HIGHLIGHT GAMEOBJECT HERE.")]
    [SerializeField] private GameObject selectionHighlight;

    // --- References obtained at runtime ---
    private HotbarManager hotbarManager; // Reference to the main manager script
    private TooltipManager tooltipManager; // Reference to the tooltip system manager
    private int slotIndex;               // Index of this slot within the hotbar array
    private ItemSO currentItemSO;        // Store the ItemSO currently represented by this slot

    /// <summary>
    /// Initializes the slot with references to the manager and its index.
    /// Called by HotbarManager during setup. Finds the TooltipManager.
    /// </summary>
    public void Initialize(HotbarManager manager, int index)
    {
        hotbarManager = manager;
        slotIndex = index;

        // Find the TooltipManager instance in the scene when the slot is initialized
        tooltipManager = FindFirstObjectByType<TooltipManager>(); // Use FindFirstObjectByType
        if (tooltipManager == null)
        {
            Debug.LogError($"HotbarSlot {slotIndex}: Could not find TooltipManager in the scene! Hotbar tooltips won't work.", this);
        }

        // Ensure UI references are assigned (optional safety check)
        if (itemIconImage == null) Debug.LogError($"HotbarSlot {slotIndex}: Item Icon Image not assigned! Check the prefab!", this);
        if (quantityText == null) Debug.LogError($"HotbarSlot {slotIndex}: Quantity Text not assigned! Check the prefab!", this);
        if (selectionHighlight == null) Debug.LogWarning($"HotbarSlot {slotIndex}: Selection Highlight not assigned.", this);

        Deselect(); // Start deselected
        ClearSlotVisually(); // Start visually empty
    }

     /// <summary>
    /// Updates the slot's visuals (icon, quantity) AND stores the ItemSO reference for tooltips.
    /// This version should be called by HotbarManager's UpdateSingleSlotVisuals.
    /// </summary>
    /// <param name="itemSO">The ItemSO data for the item in this slot (can be null if empty).</param>
    /// <param name="quantity">The quantity of the item.</param>
    public void UpdateSlotData(ItemSO itemSO, int quantity) // <-- Method definition
    {
        currentItemSO = itemSO; // Store the ItemSO reference

        // Update Icon
        if (itemIconImage != null && currentItemSO != null && currentItemSO.itemSprite != null)
        {
            // Debug.Log($"[HotbarSlot {slotIndex}] Updating icon for '{currentItemSO.itemName}' with sprite: {currentItemSO.itemSprite.name}");
            itemIconImage.sprite = currentItemSO.itemSprite;
            itemIconImage.enabled = true; // Show icon
            itemIconImage.color = Color.white; // Ensure alpha is full
        }
        else if (itemIconImage != null)
        {
            // if (currentItemSO != null && currentItemSO.itemSprite == null) { Debug.LogWarning($"[HotbarSlot {slotIndex}] ItemSO '{currentItemSO.itemName}' missing sprite."); }
             itemIconImage.sprite = null; // Clear sprite reference
             itemIconImage.enabled = false; // Hide icon if no item or no sprite
        }
        else { Debug.LogError($"[HotbarSlot {slotIndex}] itemIconImage reference is missing!"); }


        // Update Quantity Text
        if (quantityText != null)
        {
            bool showQuantity = quantity > 1;
            quantityText.text = showQuantity ? quantity.ToString() : ""; // Set text or clear it
            quantityText.enabled = showQuantity; // Enable/disable the text component
            // Debug.Log($"[HotbarSlot {slotIndex}] Set Quantity Text to '{quantityText.text}', Enabled: {quantityText.enabled} (Raw Quantity: {quantity})");
        }
         else { Debug.LogError($"[HotbarSlot {slotIndex}] quantityText reference is missing!"); }
    }


    /// <summary>
    /// Clears the slot's visuals (icon, quantity) and stored ItemSO reference.
    /// </summary>
    public void ClearSlotVisually()
    {
        currentItemSO = null;
        if (itemIconImage != null) { itemIconImage.sprite = null; itemIconImage.enabled = false; }
        if (quantityText != null) { quantityText.text = ""; quantityText.enabled = false; }
    }

    /// <summary> Shows the selection highlight GameObject. </summary>
    public void Select() { if (selectionHighlight != null) selectionHighlight.SetActive(true); }

    /// <summary> Hides the selection highlight GameObject. </summary>
    public void Deselect() { if (selectionHighlight != null) selectionHighlight.SetActive(false); }

    /// <summary> Handles clicks on the slot itself (triggers selection in HotbarManager). </summary>
    public void OnSlotClicked() { hotbarManager?.HandleSlotClick(slotIndex); }

    // --- Tooltip Handlers ---
    public void OnPointerEnter(PointerEventData eventData) { if (tooltipManager != null && currentItemSO != null) { string tooltipContent = $"<b>{currentItemSO.itemName}</b>\n<size=90%>{currentItemSO.itemDescription}</size>"; tooltipManager.ShowTooltip(tooltipContent, Input.mousePosition); } }
    public void OnPointerExit(PointerEventData eventData) { tooltipManager?.HideTooltip(); }
    void OnDisable() { tooltipManager?.HideTooltip(); }
}
