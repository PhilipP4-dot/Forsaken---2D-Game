using UnityEngine;
using UnityEngine.UI;
using TMPro;
// using UnityEngine.EventSystems; // Might need this for input field focus

public class ReviveOptionUI : MonoBehaviour
{
    [Header("UI Groups")]
    [Tooltip("Assign the GameObject holding the initial Revive button and cost text.")]
    [SerializeField] private GameObject initialPromptGroup;
    [Tooltip("Assign the GameObject holding the name input field and confirm button.")]
    [SerializeField] private GameObject namingPromptGroup;

    [Header("UI Elements")]
    [SerializeField] private Button reviveButton;           // Button in InitialPromptGroup
    [SerializeField] private TextMeshProUGUI manaCostText;  // Text in InitialPromptGroup
    [SerializeField] private TMP_InputField nameInputField; // InputField in NamingPromptGroup
    [SerializeField] private Button confirmNameButton;      // Button in NamingPromptGroup

    [Header("Interaction Logic")]
    [SerializeField] private float maxReviveDistance = 3.0f;

    // Internal References
    private EnemyHealth targetEnemy;
    private PlayerMana playerMana;
    private float requiredMana;
    // private bool isNamingState = false; // State tracked by active group instead

    public void Initialize(EnemyHealth enemyToRevive, PlayerMana currentPlayerMana)
    {
        targetEnemy = enemyToRevive;
        playerMana = currentPlayerMana;

        // Null Checks for critical elements
        if (targetEnemy == null || playerMana == null || reviveButton == null || confirmNameButton == null || initialPromptGroup == null || namingPromptGroup == null || nameInputField == null)
        {
            Debug.LogError("ReviveOptionUI is missing one or more essential references! Check prefab Inspector.", this);
            Destroy(gameObject); return;
        }

        // Set initial UI state
        initialPromptGroup.SetActive(true);
        namingPromptGroup.SetActive(false);

        // Setup initial prompt
        requiredMana = targetEnemy.GetRevivalCost();
        if (manaCostText != null) { manaCostText.text = $"({requiredMana} Mana)"; }
        nameInputField.text = ""; // Clear name field

        // Check initial interactability for the first button
        UpdateInitialButtonState();

        // Add listeners (ensure they aren't added multiple times)
        reviveButton.onClick.RemoveAllListeners();
        reviveButton.onClick.AddListener(OnClickShowNamePrompt); // Changed target method

        confirmNameButton.onClick.RemoveAllListeners();
        confirmNameButton.onClick.AddListener(OnClickConfirmNameButton); // New target method
    }

    /// <summary>
    /// Checks only the conditions for showing the initial revive prompt button.
    /// </summary>
    void UpdateInitialButtonState()
    {
        if (reviveButton == null || playerMana == null || targetEnemy == null) return;
        bool canAfford = playerMana.HasEnoughMana(requiredMana);
        bool isRevivableState = (targetEnemy.CurrentState == EnemyHealth.State.Dead_Revivable);
        reviveButton.interactable = canAfford && isRevivableState;
    }

    /// <summary>
    /// Called when the *first* "Revive" button is clicked. Checks conditions and switches to naming UI.
    /// </summary>
    public void OnClickShowNamePrompt()
    {
        // Double check references
        if (targetEnemy == null || playerMana == null) { Destroy(gameObject); return; }

        Debug.Log($"Initial Revive button clicked for {targetEnemy.GetOriginalName()}");

        // 1. Check Distance
        float distanceToEnemy = Vector2.Distance(playerMana.transform.position, targetEnemy.transform.position);
        if (distanceToEnemy > maxReviveDistance)
        {
            Debug.Log($"Cannot revive {targetEnemy.GetOriginalName()}: Player is too far away!");
            // TODO: Show feedback to player (e.g., UI message, sound)
            return; // Stop
        }

        // 2. Check Mana (again, in case it changed since UI appeared)
        if (!playerMana.HasEnoughMana(requiredMana))
        {
             Debug.Log($"Cannot revive {targetEnemy.GetOriginalName()}: Not enough mana ({playerMana.currentMana}/{requiredMana}).");
             // TODO: Show feedback to player
             reviveButton.interactable = false; // Disable button if mana check fails now
             return; // Stop
        }

        // --- Checks Passed - Switch to Naming UI ---
        initialPromptGroup.SetActive(false);
        namingPromptGroup.SetActive(true);

        // Optional: Automatically select the input field so player can type immediately
        nameInputField.Select();
        nameInputField.ActivateInputField(); // Required for focus sometimes
    }

    /// <summary>
    /// Method assigned to the "Confirm Name" Button's OnClick event.
    /// Gets name and performs the actual revival.
    /// </summary>
    public void OnClickConfirmNameButton()
    {
        if (targetEnemy == null || playerMana == null) { Destroy(gameObject); return; }

        // --- Get Name from Input Field ---
        string chosenName = "";
        if (!string.IsNullOrWhiteSpace(nameInputField.text)) {
            chosenName = nameInputField.text.Trim();
        } else {
            chosenName = "Revived " + targetEnemy.GetOriginalName();
            Debug.Log($"No name entered, using default: {chosenName}");
        }
        // --------------------------------

        Debug.Log($"Confirming revival for {targetEnemy.GetOriginalName()} with name '{chosenName}'");

        // --- Attempt Revival (This will consume mana inside EnemyHealth) ---
        bool success = targetEnemy.ReviveAsAlly(playerMana, chosenName);
        // -------------------------------------------------------------

        if (success)
        {
            // If revival works, EnemyHealth->HideReviveUI will destroy this UI.
            Debug.Log("Revival successful call acknowledged by UI.");
        }
        else
        {
            // If it failed (edge case, maybe mana changed *again*?)
            Debug.LogWarning("Revival failed AFTER confirm click (check EnemyHealth.ReviveAsAlly logs). Hiding UI anyway.");
            // Hide/Destroy the UI even on failure here, as the attempt was made.
            Destroy(gameObject);
        }
    }
}