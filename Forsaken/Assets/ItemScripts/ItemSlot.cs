using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

// NOTE: This script is not used in the current Hotbar-based system.
// Errors related to InventoryManager.FindItemSOByName have been commented out.
public class ItemSlot : MonoBehaviour, IPointerClickHandler
{
    //===========ITEM DATA===========//
    [SerializeField] public string itemName;
    public int quantity;
    public Sprite itemSprite;
    public bool isFull;
    [TextArea]
    public string itemDescription;
    public Sprite emptySprite;
    [SerializeField] int MaxNumberOfItems = 99;

    //===========ITEM SLOT UI ELEMENTS===========//
    [SerializeField] TMP_Text quantityText;
    [SerializeField] Image itemImage;
    public GameObject selectedShader;
    public bool thisItemSelected;

    //===========INVENTORY MANAGER REFERENCE===========//
    private InventoryManager inventoryManager; // Still references the old manager

    //===========ITEM DESCRIPTION PANEL UI ELEMENTS===========//
    public Image itemDescriptionImage;
    public TMP_Text itemDescriptionText;
    public TMP_Text itemDescriptionNameText;

    //===========DROP MECHANICS===========//
    private float dropCooldown = 0.3f;
    private float lastDropTime = -1.0f;


    //================================ METHODS ================================//

    private void Start()
    {
        // Use newer method, but still finds InventoryManager
        inventoryManager = FindFirstObjectByType<InventoryManager>(); // Ensure InventoryManager.cs fixes obsolete warning
        if (inventoryManager == null) { Debug.LogWarning("ItemSlot could not find InventoryManager (This might be okay if InventoryManager is unused)."); }

        UpdateSlotUI();
        if (selectedShader != null) selectedShader.SetActive(false);
        thisItemSelected = false;
    }

    /// <summary> Adds an item to this slot (part of the old system). </summary>
    public int AddItem(string name, int amount, Sprite sprite, string description)
    {
        if (quantity <= 0 || this.itemName == name || string.IsNullOrEmpty(this.itemName)) {
            this.itemName = name; this.itemSprite = sprite; this.itemDescription = description;
            int spaceAvailable = MaxNumberOfItems - quantity; int amountToAdd = Mathf.Min(amount, spaceAvailable);
            quantity += amountToAdd; isFull = (quantity >= MaxNumberOfItems); UpdateSlotUI();
            return amount - amountToAdd;
        } else { return amount; }
    }

    /// Updates the visual representation of the slot.
    void UpdateSlotUI()
    {
        if (itemImage != null) { itemImage.sprite = (quantity > 0 && itemSprite != null) ? itemSprite : emptySprite; }
        if (quantityText != null) { quantityText.text = quantity > 1 ? quantity.ToString() : ""; }
        if(itemImage != null) { itemImage.enabled = (itemImage.sprite != emptySprite && itemImage.sprite != null); }
        if(quantityText != null) { quantityText.enabled = (quantity > 1); }
    }

    /// Handles pointer clicks on the item slot.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventoryManager == null) return; // Won't work if InventoryManager is unused
        if (eventData.button == PointerEventData.InputButton.Left) { OnLeftClick(); }
        else if (eventData.button == PointerEventData.InputButton.Right) { OnRightClick(); }
    }

    /// Logic for handling a left click (select/use).
    public void OnLeftClick()
    {
        if (inventoryManager == null) return; // Won't work if InventoryManager is unused
        if (quantity <= 0) { inventoryManager?.DeSelectAllSlots(); ClearDescriptionPanel(); return; }
        if (thisItemSelected) {
            if (inventoryManager != null) {
                bool itemWasConsumed = inventoryManager.UseItem(itemName);
                if (itemWasConsumed) { this.quantity -= 1; if (this.quantity <= 0) { EmptySlot(); ClearDescriptionPanel(); } else { UpdateSlotUI(); } }
            }
        } else {
            inventoryManager?.DeSelectAllSlots(); if(selectedShader != null) selectedShader.SetActive(true);
            thisItemSelected = true; UpdateDescriptionPanel(); inventoryManager?.NotifyWeaponChange(this.itemName);
        }
    }

    /// Logic for handling a right click (drop item).
    public void OnRightClick()
    {
        if (inventoryManager == null) return; // Won't work if InventoryManager is unused
        if (quantity <= 0 || Time.unscaledTime < lastDropTime + dropCooldown) return;
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject == null) { Debug.LogError("Player object not found! Cannot drop item."); return; }
        Transform playerTransform = playerObject.transform;

        // --- TEMPORARY Fallback: Creating from scratch ---
        GameObject itemToDrop = new GameObject(itemName + "_Dropped");
        Item newItem = itemToDrop.AddComponent<Item>();
        newItem.quantity = 1; newItem.itemName = this.itemName;
        SpriteRenderer spriteRenderer = itemToDrop.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = this.itemSprite; spriteRenderer.sortingLayerName = "Default"; spriteRenderer.sortingOrder = 5;
        itemToDrop.AddComponent<BoxCollider2D>().isTrigger = true; itemToDrop.AddComponent<Rigidbody2D>();
        float dropDistance = 1.5f; Vector3 dropDirection = playerTransform.localScale.x > 0 ? playerTransform.right : -playerTransform.right;
        itemToDrop.transform.position = playerTransform.position + dropDirection * dropDistance + new Vector3(0, 0.2f, 0);
        itemToDrop.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        // --- End Fallback ---

        lastDropTime = Time.unscaledTime; bool wasSelectedBeforeDrop = this.thisItemSelected; this.quantity -= 1;
        if (this.quantity <= 0) { EmptySlot(); if (wasSelectedBeforeDrop) ClearDescriptionPanel(); }
        else { UpdateSlotUI(); }
    }


    /// Clears all data from this slot and updates its UI.
    public void EmptySlot()
    {
        bool wasSelected = this.thisItemSelected; itemName = ""; quantity = 0; itemSprite = null; itemDescription = "";
        isFull = false; thisItemSelected = false; if (selectedShader != null) selectedShader.SetActive(false);
        UpdateSlotUI(); if (wasSelected && inventoryManager != null) { inventoryManager.NotifyWeaponChange(null); } // Check inventoryManager
    }

    /// Updates the external item description panel.
    private void UpdateDescriptionPanel()
    {
        if (itemDescriptionImage == null || itemDescriptionNameText == null || itemDescriptionText == null) return;
        if (quantity > 0 && itemSprite != null) {
            // --- COMMENTED OUT problematic line ---
            // ItemSO itemSO = inventoryManager?.FindItemSOByName(itemName); // Error CS1061
            // --- Using locally stored data instead ---
            itemDescriptionImage.sprite = this.itemSprite;
            itemDescriptionText.text = this.itemDescription;
            // -----------------------------------------

            itemDescriptionNameText.text = itemName;
            itemDescriptionImage.color = Color.white;
            itemDescriptionImage.enabled = true;
        } else { ClearDescriptionPanel(); }
    }


    /// Clears the external item description panel.
    public void ClearDescriptionPanel()
    {
         if (itemDescriptionImage == null || itemDescriptionNameText == null || itemDescriptionText == null) return;
        itemDescriptionImage.sprite = emptySprite; itemDescriptionImage.enabled = (emptySprite != null);
        itemDescriptionImage.color = Color.white; itemDescriptionNameText.text = ""; itemDescriptionText.text = "";
    }

    /// Called by InventoryManager to deselect this slot.
    public void Deselect()
    {
        if (selectedShader != null) selectedShader.SetActive(false);
        thisItemSelected = false;
    }
}
