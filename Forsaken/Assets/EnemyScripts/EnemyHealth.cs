using UnityEngine;
using System.Collections;
using TMPro;

public class EnemyHealth : MonoBehaviour
{
    // ... (All fields and other methods remain exactly the same as the last version) ...
    [Header("Stats")] [SerializeField] private int maxHealth = 50; private int currentHealth; [SerializeField] private string enemyTypeName = "Enemy";
    [Header("Revival")] [SerializeField] private float revivalManaCost = 50f; [SerializeField] private Color allyColor = Color.cyan; [Range(0f, 1f)] [SerializeField] private float allyDamageMultiplier = 0.75f; private string allyName = ""; public string AllyName => allyName;
    [Header("Decay")] [SerializeField] private float decayTime = 5.0f; private Coroutine decayCoroutine = null;
    public enum State { Alive_Enemy, Dead_Revivable, Alive_Ally, Dead_Permanent }
    [SerializeField] private State currentState = State.Alive_Enemy;
    public State CurrentState => currentState;
    [Header("Display")] [SerializeField] private TextMeshPro nameDisplayTMP;
    private BaseEnemyAI aiController;
    private SpriteRenderer spriteRenderer;
    private Collider2D enemyCollider;
    private Rigidbody2D rb;
    private PlayerMana cachedPlayerMana; // <<< Keep this variable
    [Header("Tags & Layers")] [SerializeField] private string enemyTag = "Enemy"; [SerializeField] private string allyTag = "Ally"; [SerializeField] private string deadLayerName = "DeadEnemy"; private int originalLayer; private int enemyLayerInt; private int allyLayerInt; private int deadLayerInt;
    [Header("Revival UI")] [SerializeField] private GameObject reviveUIPrefab; [SerializeField] private float uiVerticalOffset = 1.5f; private GameObject currentReviveUIInstance = null;

    void Awake()
    {
        // Cache components
        aiController = GetComponent<BaseEnemyAI>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();

        // --- Use newer FindFirstObjectByType for PlayerMana ---
        cachedPlayerMana = FindFirstObjectByType<PlayerMana>(); // <<< UPDATED LINE
        // --------------------------------------------------

        // Cache layers...
        enemyLayerInt = LayerMask.NameToLayer("Enemy"); allyLayerInt = LayerMask.NameToLayer("Ally"); deadLayerInt = LayerMask.NameToLayer(deadLayerName); originalLayer = enemyLayerInt;

        // Null checks...
        if (aiController == null) Debug.LogWarning($"{gameObject.name} is missing a BaseEnemyAI derived script component!");
        if (spriteRenderer == null) Debug.LogWarning($"{gameObject.name} is missing SpriteRenderer component!");
        if (enemyCollider == null) Debug.LogWarning($"{gameObject.name} is missing Collider2D component!");
        if (rb == null) Debug.LogWarning($"{gameObject.name} is missing Rigidbody2D component!");
        if (cachedPlayerMana == null) Debug.LogError("EnemyHealth could not find PlayerMana in scene!"); // Still error if essential
        if (deadLayerInt == -1) Debug.LogError($"Physics Layer '{deadLayerName}' not found! Please define it.");
        if (nameDisplayTMP == null) Debug.LogWarning($"Name Display TMP not assigned on {gameObject.name}. Ally names won't show.", this);
    }

    // ... Start(), TakeDamage(), Die(), DecayTimer(), ShowReviveUI(), HideReviveUI(), GetOriginalName(), GetRevivalCost(), ReviveAsAlly(), OnDestroy() ...
    // ... (All these methods remain unchanged from the previous version) ...
     void Start() { currentHealth = maxHealth; currentState = State.Alive_Enemy; gameObject.tag = enemyTag; gameObject.layer = originalLayer; if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.StartsWith("Enemy") || gameObject.name.Contains("(Clone)")) { gameObject.name = enemyTypeName; } if (nameDisplayTMP != null) { nameDisplayTMP.gameObject.SetActive(false); } }
     public void TakeDamage(float damageAmount) { if ((currentState != State.Alive_Enemy && currentState != State.Alive_Ally) || currentHealth <= 0 || damageAmount <= 0) return; float actualDamage = damageAmount; if (currentState == State.Alive_Ally) { actualDamage *= allyDamageMultiplier; /*Log reduction*/ } int damageTaken = Mathf.Max(0, Mathf.FloorToInt(actualDamage)); currentHealth -= damageTaken; currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); Debug.Log($"-- {gameObject.name} health UPDATED to: {currentHealth}/{maxHealth} --"); if (currentHealth <= 0) { Die(); } }
     private void Die() { State previousState = currentState; if (previousState != State.Alive_Enemy && previousState != State.Alive_Ally) return; HideReviveUI(); if (decayCoroutine != null) { StopCoroutine(decayCoroutine); decayCoroutine = null; } if (nameDisplayTMP != null) { nameDisplayTMP.gameObject.SetActive(false); } if (previousState == State.Alive_Ally) { currentState = State.Dead_Permanent; Debug.Log($"ALLY {gameObject.name} has fallen!"); aiController?.SetActiveAI(false); if (enemyCollider != null) enemyCollider.enabled = false; if (rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; rb.simulated = false; } Destroy(gameObject, 2f); return; } if (previousState == State.Alive_Enemy) { currentState = State.Dead_Revivable; Debug.Log($"{gameObject.name} has died and is now revivable. Will decay in {decayTime}s."); aiController?.SetActiveAI(false); if (deadLayerInt != -1) gameObject.layer = deadLayerInt; else Debug.LogWarning($"Dead layer '{deadLayerName}' not found."); if (rb != null) { rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; } if (spriteRenderer != null) spriteRenderer.color = Color.grey; ShowReviveUI(); decayCoroutine = StartCoroutine(DecayTimer(decayTime)); } }
     private IEnumerator DecayTimer(float delay) { yield return new WaitForSeconds(delay); if (currentState == State.Dead_Revivable) { Debug.Log($"{gameObject.name} decayed."); HideReviveUI(); Destroy(gameObject); } decayCoroutine = null; }
     private void ShowReviveUI() { if (reviveUIPrefab != null && cachedPlayerMana != null && currentReviveUIInstance == null) { Vector3 spawnPosition = transform.position + Vector3.up * uiVerticalOffset; currentReviveUIInstance = Instantiate(reviveUIPrefab, spawnPosition, Quaternion.identity); ReviveOptionUI uiController = currentReviveUIInstance.GetComponent<ReviveOptionUI>(); if (uiController != null) { uiController.Initialize(this, cachedPlayerMana); } else { Debug.LogError("Revive UI Prefab missing ReviveOptionUI script!", reviveUIPrefab); Destroy(currentReviveUIInstance); } } else if (reviveUIPrefab == null) { Debug.LogWarning("Revive UI Prefab not assigned.", this); } else if (cachedPlayerMana == null) { Debug.LogWarning("PlayerMana not found.", this); } }
     private void HideReviveUI() { if (currentReviveUIInstance != null) { Destroy(currentReviveUIInstance); currentReviveUIInstance = null; } }
     public string GetOriginalName() { return string.IsNullOrEmpty(enemyTypeName) ? "Enemy" : enemyTypeName; }
     public float GetRevivalCost() { return revivalManaCost; }
     public bool ReviveAsAlly(PlayerMana playerManaComponent, string chosenName) { if (currentState != State.Dead_Revivable) { return false; } if (playerManaComponent == null || !playerManaComponent.ConsumeMana(revivalManaCost)) { Debug.LogWarning($"ReviveAsAlly: Failed mana check/consumption."); return false; } if (decayCoroutine != null) { StopCoroutine(decayCoroutine); decayCoroutine = null; Debug.Log("Stopped decay timer due to revival."); } this.allyName = chosenName; this.gameObject.name = this.allyName; Debug.Log($"Reviving {this.allyName} (originally {enemyTypeName}) as an Ally! Mana Cost: {revivalManaCost}"); if (nameDisplayTMP != null) { nameDisplayTMP.SetText(this.allyName); nameDisplayTMP.gameObject.SetActive(true); } else { Debug.LogWarning($"Cannot show name for {this.allyName}, NameDisplayTMP reference missing!"); } currentState = State.Alive_Ally; currentHealth = maxHealth; gameObject.tag = allyTag; gameObject.layer = allyLayerInt != -1 ? allyLayerInt : originalLayer; if (enemyCollider != null) enemyCollider.enabled = true; if (spriteRenderer != null) spriteRenderer.color = allyColor; if (rb != null) { rb.simulated = true; } if (aiController != null) { aiController.SetActiveAI(true); aiController.SetTargetingMode(true); } else { Debug.LogWarning("No BaseEnemyAI found to activate/set mode on revival!"); } HideReviveUI(); return true; }
     void OnDestroy() { HideReviveUI(); if (decayCoroutine != null) { StopCoroutine(decayCoroutine); } }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Deadzone"))
        {
            Die();
        }
    } 

} // End of class EnemyHealth