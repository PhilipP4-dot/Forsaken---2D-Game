using UnityEngine;
using UnityEngine.UI;
using TMPro; // Make sure TextMeshPro is imported via Package Manager and included here

/// <summary>
/// Manages a simple UI tooltip panel that follows the mouse.
/// Shows/hides based on calls from other scripts (like HotbarSlot or Item).
/// Attach this script to the main TooltipPanel GameObject you created on your Canvas.
/// </summary>
public class TooltipManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Assign the child TextMeshProUGUI component used to display the tooltip text.")]
    [SerializeField] private TextMeshProUGUI tooltipText;

    [Tooltip("Assign the RectTransform of the TooltipPanel itself (the GameObject this script is attached to).")]
    [SerializeField] private RectTransform tooltipRectTransform;

    [Tooltip("Assign the RectTransform of the parent Canvas. Used for screen clamping.")]
    [SerializeField] private RectTransform canvasRectTransform;

    [Header("Positioning")]
    [Tooltip("Offset from the mouse position where the tooltip appears (adjust X/Y for desired placement).")]
    [SerializeField] private Vector2 positionOffset = new Vector2(15f, -20f); // Example: Offset slightly down and right

    // Internal reference for Singleton pattern (optional but common)
    // public static TooltipManager Instance { get; private set; }

    void Awake()
    {
        // --- Optional Singleton setup ---
        // if (Instance != null && Instance != this)
        // {
        //     Destroy(gameObject);
        //     return;
        // }
        // Instance = this;
        // ---------------------------------


        // --- Ensure references are set ---
        // If not assigned in Inspector, try to find them automatically
        if (tooltipText == null) {
            tooltipText = GetComponentInChildren<TextMeshProUGUI>();
            if(tooltipText == null) Debug.LogError("TooltipManager: Tooltip Text component not found or assigned! Assign the child TextMeshPro text.", this);
        }
        if (tooltipRectTransform == null) {
            tooltipRectTransform = GetComponent<RectTransform>();
             if(tooltipRectTransform == null) Debug.LogError("TooltipManager: Tooltip RectTransform component not found or assigned! Assign the panel itself.", this);
        }
         if (canvasRectTransform == null) {
            // Find the parent Canvas's RectTransform
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null) {
                canvasRectTransform = parentCanvas.GetComponent<RectTransform>();
            }
             if(canvasRectTransform == null) Debug.LogWarning("TooltipManager: Parent Canvas RectTransform not found. Tooltip might go off-screen.", this);
        }
        // ---------------------------------

        // Ensure the tooltip starts hidden
        HideTooltip();
    }

    /// <summary>
    /// Shows the tooltip panel, sets its text, and positions it near the specified screen position.
    /// Handles basic screen clamping to try and keep it visible.
    /// </summary>
    /// <param name="text">The text content to display (can include TextMeshPro rich text tags).</param>
    /// <param name="pivotPosition">The screen position (e.g., Input.mousePosition) to position the tooltip relative to.</param>
    public void ShowTooltip(string text, Vector2 pivotPosition)
    {
        // Exit if essential components are missing
        if (tooltipText == null || tooltipRectTransform == null) return;

        // Set the text content
        tooltipText.text = text;

        // --- Position Calculation ---

        // Immediately update the layout based on the new text content.
        // This forces the Content Size Fitter to calculate the preferred size.
        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRectTransform);
        // Alternative if the above doesn't work reliably with complex layouts:
        // Canvas.ForceUpdateCanvases();

        // Get the calculated size of the tooltip AFTER the layout rebuild
        // Use rect dimensions as they reflect the Content Size Fitter's result
        float tooltipWidth = tooltipRectTransform.rect.width;
        float tooltipHeight = tooltipRectTransform.rect.height;
        // Note: We don't multiply by lossyScale here because we're working within the Canvas coordinate system for clamping.

        // Determine initial pivot (default: bottom-left for standard screen coords)
        // We adjust this based on screen position to prevent going off edges.
        float pivotX = 0f; // 0 = left edge
        float pivotY = 0f; // 0 = bottom edge

        // Check against screen width/height (Input.mousePosition uses screen coords)
        if (pivotPosition.x + positionOffset.x + tooltipWidth > Screen.width)
        {
            pivotX = 1f; // Pivot on right edge if too close to screen right
        }
        if (pivotPosition.y + positionOffset.y - tooltipHeight < 0) // Check bottom edge (Y decreases downwards in screen space usually)
        {
             pivotY = 1f; // Pivot on top edge if too close to screen bottom
        }
        // Apply the calculated pivot
        tooltipRectTransform.pivot = new Vector2(pivotX, pivotY);


        // Calculate the final position based on the pivot and offset
        // Adjust offset direction based on pivot so the corner stays near the mouse
        Vector2 calculatedOffset = new Vector2(
            positionOffset.x * (pivotX == 0 ? 1 : -1), // Invert X offset if pivoting on right
            positionOffset.y * (pivotY == 0 ? 1 : -1)  // Invert Y offset if pivoting on top
        );
        Vector2 finalPosition = pivotPosition + calculatedOffset;


        // --- Optional: Screen Clamping (More robust clamping) ---
        // This ensures the tooltip stays fully within the screen boundaries
        // Calculate min/max allowed positions based on pivot and size
        float minX = tooltipWidth * tooltipRectTransform.pivot.x;
        float maxX = Screen.width - (tooltipWidth * (1f - tooltipRectTransform.pivot.x));
        float minY = tooltipHeight * tooltipRectTransform.pivot.y;
        float maxY = Screen.height - (tooltipHeight * (1f - tooltipRectTransform.pivot.y));

        // Clamp the final position
        finalPosition.x = Mathf.Clamp(finalPosition.x, minX, maxX);
        finalPosition.y = Mathf.Clamp(finalPosition.y, minY, maxY);
        // --------------------------------------------------------


        // Set the final anchored position of the tooltip panel
        // Important: Use position for Screen Space Overlay/Camera, anchoredPosition for World Space Canvas maybe
        tooltipRectTransform.position = finalPosition;

        // Activate the panel to make it visible
        if (!gameObject.activeSelf) {
             gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Hides the tooltip panel by deactivating its GameObject.Tool
    /// </summary>
    public void HideTooltip()
    {
        // Only deactivate if it's currently active to avoid unnecessary calls
        if (gameObject.activeSelf)
        {
             gameObject.SetActive(false);
        }
    }
}
