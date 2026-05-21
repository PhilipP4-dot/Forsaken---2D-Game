using UnityEngine;
using UnityEngine.UI; // If you have a mana bar UI

public class PlayerMana : MonoBehaviour
{
    [SerializeField] public float maxMana = 100f;
    [SerializeField] public float currentMana;
    // [SerializeField] private float manaRegenPerSecond = 0.5f; // Optional regen

    public System.Action<float, float> OnManaChanged; // current, max

    void Start()
    {
        currentMana = maxMana;
        OnManaChanged?.Invoke(currentMana, maxMana);
    }

    /* // Optional passive regen
    void Update()
    {
        RegenerateMana(manaRegenPerSecond * Time.deltaTime);
    }
    */

    public float GetCurrentMana() => currentMana;
    public float GetMaxMana() => maxMana;

    /// <summary>
    /// Increases the player's current mana by the specified amount.
    /// Clamps the value so mana doesn't exceed maxMana.
    /// </summary>
    /// <param name="amount">The amount of mana to regenerate.</param>
    /// <returns>True if mana was actually regenerated, false if mana was already full.</returns>
    public bool RegenerateMana(float amount) // <--- Ensure return type is bool
    {
        // --- ADD THIS CHECK ---
        // If mana is already full or amount is invalid, do nothing and report failure
        if (currentMana >= maxMana || amount <= 0)
        {
            Debug.Log("Mana is already full. Cannot regenerate mana.");
            return false; // Indicate no mana was regenerated
        }
        // ---------------------

        // Calculate new mana, clamping to maxMana
        currentMana = Mathf.Min(currentMana + amount, maxMana);

        // Debug.Log($"Regenerated {amount} mana. Current Mana: {currentMana}/{maxMana}");
        OnManaChanged?.Invoke(currentMana, maxMana); // Invoke event for UI

        return true; // Indicate mana was successfully regenerated
    }

    public bool HasEnoughMana(float amount)
    {
        return currentMana >= amount;
    }

    /// <summary>
    /// Reduces the player's current mana by the specified amount.
    /// Clamps the value so mana doesn't go below zero.
    /// </summary>
    /// <param name="amount">The amount of mana to use.</param>
    /// <returns>True if mana was successfully consumed, false otherwise (e.g., not enough mana).</returns>
    public bool ConsumeMana(float amount) // Renamed from UseMana in previous examples
    {
        if (amount <= 0) return true; // Consuming zero or less costs nothing

        if (currentMana >= amount)
        {
            currentMana -= amount;
            // Debug.Log($"Consumed {amount} mana. Current mana: {currentMana}/{maxMana}");
            OnManaChanged?.Invoke(currentMana, maxMana); // Invoke event
            return true;
        }
        else
        {
            // Debug.Log($"Not enough mana! Tried to consume {amount}, only have {currentMana}.");
            return false;
        }
    }
}
