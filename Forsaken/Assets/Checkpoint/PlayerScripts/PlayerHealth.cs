using UnityEngine;
using System.Collections; // For IEnumerator
using UnityEngine.Events; // Optional: For UnityEvents like OnDeath
using UnityEngine.UI; // Optional: If using UI elements like a health bar
using System; // Optional: If using Actions/Events

/// <summary>
/// Manages the player's health, including taking damage, healing, and death/respawn logic.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Health Stats")]
    [Tooltip("Maximum health points the player can have.")]
    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Current health points (can be monitored in Inspector for debugging).")]
    [SerializeField] private float currentHealth;

    [Header("State")]
    [Tooltip("Is the player currently dead?")]
    public bool isDead = false; // Public for other scripts to check easily

    [Header("References")]
    [Tooltip("Optional: Animator for death/respawn animations.")]
    [SerializeField] private Animator animator;
    [Tooltip("Components to disable on death and re-enable on respawn.")]
    [SerializeField] private Behaviour[] componentsToDisableOnDeath; // Renamed for clarity

    [Header("UI (Optional)")]
    [Tooltip("Reference to a UI Image used as a health bar fill.")]
    [SerializeField] private Image healthBarFill; // Example UI element

    [Header("Respawn")]
    [Tooltip("Delay in seconds before triggering respawn logic after death.")]
    [SerializeField] private float respawnDelay = 1.5f;

    // --- Events ---
    public Action<float, float> OnHealthChanged; // Action<currentHealth, maxHealth>
    public Action OnPlayerDied;
    public Action OnPlayerRespawned; // Optional: Event for respawn

    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField] private AudioClip damageSound;

    void Awake()
    {
        // Try to get Animator if not assigned in Inspector
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        currentHealth = maxHealth;
        isDead = false;
    }

    void Start()
    {
        UpdateHealthUI();
        // Initial health update event
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;

    /// <summary>
    /// Reduces player health by the specified amount.
    /// </summary>
    /// <param name="damageAmount">The amount of damage to take.</param>
    public void TakeDamage(float damageAmount)
    {
        if (isDead || damageAmount <= 0) return; // Don't take damage if dead or damage is non-positive

        currentHealth -= damageAmount;
        currentHealth = Mathf.Max(currentHealth, 0); // Clamp health to minimum 0

        SoundManager.Instance.PlaySound(damageSound); // Play damage sound

        Debug.Log($"Player took {damageAmount} damage. Current Health: {currentHealth}/{maxHealth}");
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // Notify listeners

        // Potentially trigger "OnDamaged" visual/audio effects here

        CheckForDeath(); // Check if the damage killed the player
    }

    /// <summary>
    /// Increases player health by the specified amount, up to the maximum.
    /// </summary>
    /// <param name="amount">Amount to heal.</param>
    /// <returns>True if health was actually restored, false otherwise (e.g., already full).</returns>
    public bool RestoreHealth(float amount)
    {
        if (isDead || currentHealth >= maxHealth || amount <= 0) return false; // Can't heal if dead, full, or amount is invalid

        float healthBefore = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth); // Clamp health to maximum
        float healthRestored = currentHealth - healthBefore;

        SoundManager.Instance.PlaySound(healSound); // Play heal sound
        if (healthRestored > 0)
        {
            Debug.Log($"Player restored {healthRestored} health. Current Health: {currentHealth}/{maxHealth}");
            UpdateHealthUI();
            OnHealthChanged?.Invoke(currentHealth, maxHealth); // Notify listeners
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks if health is at or below zero and triggers the death sequence.
    /// </summary>
    private void CheckForDeath()
    {
        if (!isDead && currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Handles the player's death logic.
    /// </summary>
    private void Die()
    {
        if (isDead) return; // Prevent multiple death calls

        isDead = true;
        Debug.Log("Player has died!");

        // Trigger death animation if Animator exists
        if (animator != null)
        {
            animator.SetBool("isDead", true); // Use the bool if that's how your Animator is set up
            SoundManager.Instance.PlaySound(deathSound); // Play death sound
        }

        // Disable specified components (like movement, attack controllers)
        foreach (Behaviour component in componentsToDisableOnDeath)
        {
            if (component != null) component.enabled = false;
        }

        OnPlayerDied?.Invoke(); // Invoke death event for other systems (GameManager, etc.)

        // Start the respawn process after a delay
        StartCoroutine(RespawnDelayCoroutine(respawnDelay));
    }

    /// <summary>
    /// Coroutine to wait before triggering the actual respawn location logic.
    /// </summary>
    private IEnumerator RespawnDelayCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Attempt to find the PlayerRespawn script
        PlayerRespawn playerRespawn = GetComponent<PlayerRespawn>();
        if (playerRespawn != null)
        {
            // *** CORRECTED LINE: Call the method that likely exists in PlayerRespawn.cs ***
            playerRespawn.Respawn_0(); // Changed from Respawn() to Respawn_0()
        }
        else
        {
             Debug.LogWarning("PlayerRespawn script not found on Player. Cannot trigger respawn movement/logic.", this);
             // As a fallback, you could directly call this script's Respawn() method,
             // but it won't handle moving the player to the checkpoint.
             // Respawn();
        }
    }

    /// <summary>
    /// Resets player health, state, and re-enables components for respawning.
    /// Called by PlayerRespawn script or GameManager AFTER moving the player.
    /// </summary>
    public void Respawn() // Keep this public so other scripts can call it
    {
        Debug.Log("Player Respawn logic initiated in PlayerHealth (Resetting stats/components).");
        isDead = false;
        currentHealth = maxHealth; // Restore to full health

        // Reset death animation state
        if (animator != null)
        {
            animator.SetBool("isDead", false);
            // Optionally reset to idle state or another state
            animator.Play("Player_Idle"); // Or animator.SetTrigger("RespawnTrigger");
            // Optional: Trigger a specific respawn animation/state if you have one
            // animator.Play("Player_Idle"); // Or animator.SetTrigger("RespawnTrigger");
        }

        // Re-enable specified components
        foreach (Behaviour component in componentsToDisableOnDeath)
        {
             if (component != null)
             {
                component.enabled = true;
             }
        }

        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth); // Update listeners
        OnPlayerRespawned?.Invoke(); // Notify listeners of respawn
    }

    /// <summary>
    /// Updates the visual health bar (if assigned).
    /// </summary>
    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = currentHealth / maxHealth;
        }
    }
}