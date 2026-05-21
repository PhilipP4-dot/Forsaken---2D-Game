using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro; // Keep if HotbarSlot uses it
using UnityEngine.EventSystems;
using System.Linq; // Optional: for LINQ lookup

/// <summary>
/// Manages the player's hotbar UI, item data, selection, and basic interactions like dropping.
/// Coordinates with PlayerAttackController for weapon usage and item consumption.
/// Also provides a public method for looking up ItemSO data by name.
/// </summary>
public class HotbarManager : MonoBehaviour
{
    [Header("UI Setup")]
    [Tooltip("Number of slots in the hotbar.")]
    [SerializeField] private int numberOfSlots = 9;
    [Tooltip("Prefab for the individual hotbar slot UI element.")]
    [SerializeField] private GameObject hotbarSlotPrefab;
    [Tooltip("Parent transform where hotbar slot UI elements will be instantiated.")]
    [SerializeField] private Transform hotbarPanel;

    [Header("Selection & Input")]
    [Tooltip("Keys assigned to select hotbar slots 1 through 9.")]
    [SerializeField] private KeyCode[] slotKeys = {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
    };
    [Tooltip("Key used to drop the selected item.")]
    [SerializeField] private KeyCode dropKey = KeyCode.G;
    [Tooltip("Key to use consumable items (like potions) directly from the hotbar.")]
    [SerializeField] private KeyCode useKey = KeyCode.E;

    [Header("Item Data References")]
    [Tooltip("Assign ALL possible Item ScriptableObjects here so they can be identified by name when added.")]
    [SerializeField] private List<ItemSO> allItemSOs = new List<ItemSO>();

    [Header("External References")]
    [Tooltip("Assign or find the PlayerAttackController for weapon notifications.")]
    [SerializeField] private PlayerAttackController playerAttackController;

    // --- Runtime Data ---
    private List<HotbarSlot> uiSlots = new List<HotbarSlot>();
    private List<HotbarItemData> hotbarData = new List<HotbarItemData>();
    private int selectedSlotIndex = -1;
    private int MaxStackSize = 99;

    /// <summary> Internal helper class to store data for each hotbar slot. </summary>
    private class HotbarItemData {
        public ItemSO itemSO; public int quantity;
        public bool IsEmpty() => itemSO == null || quantity <= 0;
        public bool IsFull(int defaultMaxStack) => !IsEmpty() && quantity >= defaultMaxStack;
        public void Clear() { itemSO = null; quantity = 0; }
     }

    // --- Unity Methods ---
    void Awake()
    {
        // Reference Validation
        bool referencesOk = true;
        if (hotbarPanel == null) { Debug.LogError("Hotbar Panel not assigned!", this); referencesOk = false; }
        else if (!hotbarPanel.gameObject.scene.IsValid()) { Debug.LogError($"Assigned Hotbar Panel '{hotbarPanel.name}' is not a valid scene object!", this); referencesOk = false; }
        if (hotbarSlotPrefab == null) { Debug.LogError("Hotbar Slot Prefab not assigned!", this); referencesOk = false; }
        if (allItemSOs.Count == 0) { Debug.LogWarning("No ItemSOs assigned.", this); }
        if (playerAttackController == null) { playerAttackController = FindFirstObjectByType<PlayerAttackController>(); }
        if (playerAttackController == null) { Debug.LogWarning("PlayerAttackController not found.", this); }
        if (!referencesOk) { enabled = false; return; }
    }

    void Start()
    {
        InitializeHotbar();
        if (uiSlots.Count > 0) { SelectSlot(0); }
        else { Debug.LogError("Hotbar initialization failed.", this); }
    }

    void Update()
    {
        if (uiSlots.Count == 0) return;
        HandleSlotSelectionInput();
        HandleUseItemInput();
        HandleDropItemInput();
    }

    // --- Initialization ---
    void InitializeHotbar()
    {
        uiSlots.Clear(); hotbarData.Clear();
        if (hotbarPanel == null || !hotbarPanel.gameObject.scene.IsValid() || hotbarSlotPrefab == null) { Debug.LogError("Cannot initialize hotbar.", this); return; }
        foreach (Transform child in hotbarPanel) { if (child != null && child.gameObject != null && child.gameObject.scene.IsValid()) { Destroy(child.gameObject); } }
        for (int i = 0; i < numberOfSlots; i++) {
            GameObject slotGO = null;
            try {
                slotGO = Instantiate(hotbarSlotPrefab, hotbarPanel);
                if (slotGO == null) throw new System.Exception("Instantiate returned null.");
                slotGO.transform.localScale = Vector3.one; slotGO.transform.localPosition = Vector3.zero; slotGO.transform.localRotation = Quaternion.identity;
                slotGO.name = $"HotbarSlot_{i}";
            }
            catch (System.Exception e) {
                 Debug.LogError($"Error instantiating/parenting slot {i}: {e.Message}\n{e.StackTrace}", this);
                 uiSlots.Clear(); hotbarData.Clear();
                 foreach (Transform child in hotbarPanel) { if (child != null) Destroy(child.gameObject); }
                 return;
             }
            HotbarSlot slotUI = slotGO.GetComponent<HotbarSlot>();
            if (slotUI != null) { uiSlots.Add(slotUI); slotUI.Initialize(this, i); }
            else { Debug.LogError($"Slot prefab missing HotbarSlot script!", slotGO); }
            hotbarData.Add(new HotbarItemData());
        }
        if(uiSlots.Count != numberOfSlots || hotbarData.Count != numberOfSlots) { Debug.LogWarning($"Hotbar init mismatch."); }
    }

    // --- UI Updates ---
    public void UpdateAllSlots() { for (int i = 0; i < uiSlots.Count; i++) { if (i < hotbarData.Count) { UpdateSingleSlotVisuals(i); } } }

    /// <summary> Updates the visual appearance and stored data reference of a single slot. </summary>
    public void UpdateSingleSlotVisuals(int index) {
        if (index < 0 || index >= uiSlots.Count || index >= hotbarData.Count) return;
        HotbarItemData data = hotbarData[index];
        HotbarSlot uiSlot = uiSlots[index];
        if (uiSlot == null) { Debug.LogError($"UI Slot at index {index} is null!", this); return; }

        if (!data.IsEmpty()) {
            if (data.itemSO != null) {
                // *** CALL UpdateSlotData - Ensure HotbarSlot.cs has this method ***
                uiSlot.UpdateSlotData(data.itemSO, data.quantity);
            } else {
                Debug.LogWarning($"Item data at index {index} invalid (missing ItemSO). Clearing.", this);
                uiSlot.ClearSlotVisually();
            }
        } else {
            uiSlot.ClearSlotVisually();
        }
     }

    // --- Input Handling ---
    void HandleSlotSelectionInput() { /* ... existing ... */ for (int i = 0; i < slotKeys.Length; i++) { if (i < numberOfSlots && Input.GetKeyDown(slotKeys[i])) { SelectSlot(i); return; } } float scroll = Input.GetAxis("Mouse ScrollWheel"); if (Mathf.Abs(scroll) > 0.05f) { int direction = scroll > 0f ? -1 : 1; int nextSlot = (selectedSlotIndex + direction + numberOfSlots) % numberOfSlots; SelectSlot(nextSlot); } }
    void HandleDropItemInput() { if (Input.GetKeyDown(dropKey)) { DropSelectedItem(); } }
    void HandleUseItemInput() { if (Input.GetKeyDown(useKey)) { if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { return; } UseSelectedItem(); } }

    // --- Method Called by HotbarSlot ---
    public void HandleSlotClick(int index) { SelectSlot(index); }

    // --- Core Actions ---
    void SelectSlot(int index) { /* ... existing ... */ if (index < 0 || index >= numberOfSlots) return; if (uiSlots.Count <= index || hotbarData.Count <= index || uiSlots[index] == null) { return; } if (index == selectedSlotIndex) return; if (selectedSlotIndex >= 0 && selectedSlotIndex < uiSlots.Count && uiSlots[selectedSlotIndex] != null) { uiSlots[selectedSlotIndex].Deselect(); } selectedSlotIndex = index; if (uiSlots[selectedSlotIndex] != null) { uiSlots[selectedSlotIndex].Select(); } HotbarItemData selectedData = hotbarData[selectedSlotIndex]; NotifyWeaponChange(selectedData?.itemSO?.itemName); }
    public void NotifyWeaponChange(string itemName) { playerAttackController?.SetActiveWeapon(itemName); }
    public void DropSelectedItem() { /* ... Use the version from hotbar_manager_revert_pickup_complete ... */ if (selectedSlotIndex < 0 || selectedSlotIndex >= hotbarData.Count) return; HotbarItemData selectedData = hotbarData[selectedSlotIndex]; if (selectedData.IsEmpty() || selectedData.itemSO == null) { return; } if (selectedData.itemSO.droppedItemPrefab == null) { Debug.LogError($"...", selectedData.itemSO); return; } GameObject playerObject = GameObject.FindWithTag("Player"); if (playerObject == null) { Debug.LogError("...", this); return; } Transform playerTransform = playerObject.transform; float dropDistance = 2.0f; float dropHeightOffset = 0.2f; Vector3 dropDirection = playerTransform.localScale.x > 0 ? playerTransform.right : -playerTransform.right; Vector3 dropPosition = playerTransform.position + dropDirection * dropDistance + Vector3.up * dropHeightOffset; GameObject itemToDrop = Instantiate(selectedData.itemSO.droppedItemPrefab, dropPosition, Quaternion.identity); itemToDrop.name = selectedData.itemSO.itemName + "_DroppedInstance"; float itemSpecificMultiplier = selectedData.itemSO.dropScaleMultiplier; itemToDrop.transform.localScale = Vector3.one * itemSpecificMultiplier; Item itemComponent = itemToDrop.GetComponent<Item>(); if (itemComponent != null) { itemComponent.itemName = selectedData.itemSO.itemName; itemComponent.quantity = 1; } else { Debug.LogError($"...", itemToDrop); } selectedData.quantity--; if (selectedData.quantity <= 0) { ConsumeSelectedItemCompletely(); } else { UpdateSingleSlotVisuals(selectedSlotIndex); } }
    public void ConsumeSelectedItemCompletely() { /* ... Use the version from hotbar_manager_revert_pickup_complete ... */ if (selectedSlotIndex < 0 || selectedSlotIndex >= hotbarData.Count) { Debug.LogWarning("..."); return; } HotbarItemData selectedData = hotbarData[selectedSlotIndex]; selectedData.Clear(); UpdateSingleSlotVisuals(selectedSlotIndex); NotifyWeaponChange(null); }
    public void UseSelectedItem() { /* ... Use the version from hotbar_manager_revert_pickup_complete ... */ if (selectedSlotIndex < 0 || selectedSlotIndex >= hotbarData.Count) return; HotbarItemData selectedData = hotbarData[selectedSlotIndex]; if (!selectedData.IsEmpty() && selectedData.itemSO != null) { if (selectedData.itemSO is WeaponSO) { /* No action via E */ } else { bool consumed = selectedData.itemSO.UseItem(); if (consumed) { selectedData.quantity--; if (selectedData.quantity <= 0) { ConsumeSelectedItemCompletely(); } else { UpdateSingleSlotVisuals(selectedSlotIndex); } } } } }

    // --- REVERTED AddItem Method ---
    /// <summary>
    /// Adds an item to the hotbar by name. Tries to stack first, then finds an empty slot.
    /// NOTE: This version DOES NOT prevent weapon stacking.
    /// </summary>
    /// <returns>The quantity of the item that could NOT be added (0 if all added successfully).</returns>
    public int AddItem(string itemNameToAdd, int quantityToAdd) {
        if (uiSlots.Count == 0) { Debug.LogError("...", this); return quantityToAdd; }
        ItemSO itemSOToAdd = FindItemSOByName(itemNameToAdd); // Use private method
        if (itemSOToAdd == null) { Debug.LogWarning($"...", this); return quantityToAdd; }

        int remainingQuantity = quantityToAdd;
        bool changedSelectedSlot = false;
        // Weapons should occupy individual slots (no stacking)
        int itemMaxStack = (itemSOToAdd is WeaponSO) ? 1 : MaxStackSize;

        // Loop 1: Try to stack
        for (int i = 0; i < hotbarData.Count; i++) {
            if (remainingQuantity <= 0) break;
            HotbarItemData slotData = hotbarData[i];
            if (!slotData.IsEmpty() && slotData.itemSO == itemSOToAdd && !slotData.IsFull(itemMaxStack)) {
                int spaceAvailable = itemMaxStack - slotData.quantity;
                int amountToStack = Mathf.Min(remainingQuantity, spaceAvailable);
                slotData.quantity += amountToStack; remainingQuantity -= amountToStack;
                UpdateSingleSlotVisuals(i); if (i == selectedSlotIndex) { changedSelectedSlot = true; }
            }
        }

        // Loop 2: Try to fill empty slots
        if (remainingQuantity > 0) {
            for (int i = 0; i < hotbarData.Count; i++) {
                if (remainingQuantity <= 0) break;
                HotbarItemData slotData = hotbarData[i];
                if (slotData.IsEmpty()) {
                    int amountToAdd = Mathf.Min(remainingQuantity, itemMaxStack);
                    if(remainingQuantity >= amountToAdd) { // Check we have enough left
                        slotData.itemSO = itemSOToAdd; slotData.quantity = amountToAdd;
                        remainingQuantity -= amountToAdd; UpdateSingleSlotVisuals(i);
                        if (i == selectedSlotIndex) { changedSelectedSlot = true; }
                    }
                }
            }
        }

        if (changedSelectedSlot) { NotifyWeaponChange(hotbarData[selectedSlotIndex]?.itemSO?.itemName); }
        if (remainingQuantity > 0) { Debug.LogWarning($"...", this); }

        // --- FIX CS0161: Ensure a value is always returned ---
        return remainingQuantity;
        // ----------------------------------------------------
     }

    // --- Helper Methods ---
    /// <summary> Finds an ItemSO from the internal 'allItemSOs' list based on its itemName. </summary>
    private ItemSO FindItemSOByName(string name) {
        if (string.IsNullOrEmpty(name)) return null; // Return null if name is invalid
        foreach (ItemSO itemSO in allItemSOs) {
            if (itemSO != null && itemSO.itemName == name) {
                return itemSO; // Return the found item
            }
        }
        // --- FIX CS0161: Return null if loop finishes without finding ---
        return null;
        // -------------------------------------------------------------
     }

     /// <summary> Public wrapper to find ItemSO data. </summary>
     public ItemSO FindItemSOByName_Public(string name) { return FindItemSOByName(name); }

    /// <summary> Returns the ItemSO of the currently selected hotbar item. </summary>
    public ItemSO GetSelectedItemSO() {
         if (selectedSlotIndex < 0 || selectedSlotIndex >= hotbarData.Count) {
             // --- FIX CS0161: Return null if index is invalid ---
             return null;
             // --------------------------------------------------
         }
         return hotbarData[selectedSlotIndex]?.itemSO; // Returns null if slot is valid but empty
         // This path already correctly returns null if the slot is empty, so no extra return needed here.
     }

} // End of class HotbarManager
