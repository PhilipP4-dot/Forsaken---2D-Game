using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Stat Bar References")]
    [Tooltip("Assign the 'Fill' Image GameObject of your Health Bar")]
    [SerializeField] private Image healthBarFill;
    [Tooltip("Assign the 'Fill' Image GameObject of your Mana Bar")]
    [SerializeField] private Image manaBarFill;
    // [SerializeField] private TextMeshProUGUI healthText; // Optional
    // [SerializeField] private TextMeshProUGUI manaText;   // Optional

    [Header("Player Component References")]
    [Tooltip("Assign the GameObject with PlayerHealth script")]
    [SerializeField] private PlayerHealth playerHealth; // Reference PlayerHealth
    [Tooltip("Assign the GameObject with PlayerMana script")]
    [SerializeField] private PlayerMana playerMana;     // Reference PlayerMana

    void Start()
    {
        // --- Attempt to find components if not assigned ---
        if (playerHealth == null || playerMana == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player"); // Assumes player has "Player" tag
            if (playerObj != null)
            {
                if (playerHealth == null) playerHealth = playerObj.GetComponent<PlayerHealth>();
                if (playerMana == null) playerMana = playerObj.GetComponent<PlayerMana>();
            }
        }

        // --- Subscribe to Events & Initial Update ---
        bool healthSetup = false;
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHealthBar;
            UpdateHealthBar(playerHealth.GetCurrentHealth(), playerHealth.GetMaxHealth()); // Initial call
            healthSetup = true;
        }
        else { Debug.LogError("PlayerHealth reference not found for UIManager!", this); }

        bool manaSetup = false;
        if (playerMana != null)
        {
            playerMana.OnManaChanged += UpdateManaBar;
            UpdateManaBar(playerMana.GetCurrentMana(), playerMana.GetMaxMana()); // Initial call
            manaSetup = true;
        }
        else { Debug.LogError("PlayerMana reference not found for UIManager!", this); }

        if (!healthSetup || !manaSetup)
        {
             Debug.LogError("UIManager setup incomplete. Stat bars may not update correctly.");
        }

        // --- Validate Bar References ---
        if (healthBarFill == null) Debug.LogError("Health Bar Fill Image not assigned to UIManager!", this);
        if (manaBarFill == null) Debug.LogError("Mana Bar Fill Image not assigned to UIManager!", this);
    }

    void OnDestroy()
    {
        // --- Unsubscribe from events ---
        if (playerHealth != null) { playerHealth.OnHealthChanged -= UpdateHealthBar; }
        if (playerMana != null) { playerMana.OnManaChanged -= UpdateManaBar; }
    }

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBarFill != null)
        {
            if (maxHealth <= 0) { healthBarFill.fillAmount = 0; }
            else { healthBarFill.fillAmount = Mathf.Clamp01(currentHealth / maxHealth); }
        }
        // Optional Text Update
        // if (healthText != null) { healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}"; }
    }

    private void UpdateManaBar(float currentMana, float maxMana)
    {
         if (manaBarFill != null)
        {
            if (maxMana <= 0) { manaBarFill.fillAmount = 0; }
            else { manaBarFill.fillAmount = Mathf.Clamp01(currentMana / maxMana); }
        }
        // Optional Text Update
        // if (manaText != null) { manaText.text = $"{Mathf.CeilToInt(currentMana)} / {Mathf.CeilToInt(maxMana)}"; }
    }
}
