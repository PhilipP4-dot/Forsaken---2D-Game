using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CurrentWeaponUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private TextMeshProUGUI weaponNameText;

    [Header("Default/Empty Display")]
    [SerializeField] private Sprite defaultSprite;

    public void UpdateDisplay(WeaponSO weaponData)
    {
        // --- Debug Start ---
        string weaponName = (weaponData != null) ? weaponData.itemName : "NULL";
        Debug.Log($"CurrentWeaponUI.UpdateDisplay called. Weapon: {weaponName}");
        // --- Debug End ---

        if (weaponData != null)
        {
            // --- Debug Sprite Check ---
            bool hasSprite = (weaponData.itemSprite != null);
            string spriteName = hasSprite ? weaponData.itemSprite.name : "NULL";
            Debug.Log($"  - WeaponSO has sprite? {hasSprite}. Sprite Name: {spriteName}");
            // --- Debug End ---

            if (weaponIconImage != null)
            {
                // Assign sprite logic
                weaponIconImage.sprite = hasSprite ? weaponData.itemSprite : defaultSprite;
                weaponIconImage.enabled = (weaponIconImage.sprite != null);

                // --- Debug Assignment Check ---
                string assignedSpriteName = (weaponIconImage.sprite != null) ? weaponIconImage.sprite.name : "NULL";
                Debug.Log($"  - Assigned sprite '{assignedSpriteName}' to UI Image component.");
                // --- Debug End ---
            }
            else { Debug.LogError("  - weaponIconImage reference is MISSING in Inspector!"); } // Error if reference missing

            if (weaponNameText != null)
            {
                weaponNameText.text = weaponData.itemName;
            }
             else { Debug.LogWarning("  - weaponNameText reference is missing in Inspector."); }
        }
        else // weaponData is null
        {
            Debug.Log("  - weaponData is NULL. Displaying default/fists.");
            if (weaponIconImage != null)
            {
                weaponIconImage.sprite = defaultSprite;
                weaponIconImage.enabled = (defaultSprite != null);

                // --- Debug Default Assign Check ---
                string assignedDefSpriteName = (weaponIconImage.sprite != null) ? weaponIconImage.sprite.name : "NULL";
                Debug.Log($"  - Assigned default sprite '{assignedDefSpriteName}' to UI Image component.");
                // --- Debug End ---
            }
            else { Debug.LogError("  - weaponIconImage reference is MISSING in Inspector!"); } // Error if reference missing

            if (weaponNameText != null)
            {
                weaponNameText.text = "Fists"; // Or blank: ""
            }
             else { Debug.LogWarning("  - weaponNameText reference is missing in Inspector."); }
        }
    }

    void Start()
    {
         // Ensure references are checked at start (optional additional check)
        if (weaponIconImage == null) Debug.LogError("CurrentWeaponUI Start: weaponIconImage is not assigned!");
        if (weaponNameText == null) Debug.LogError("CurrentWeaponUI Start: weaponNameText is not assigned!");

        // Initialize display
         UpdateDisplay(null); // Assuming starting with fists
    }
}