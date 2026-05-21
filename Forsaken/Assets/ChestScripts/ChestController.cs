using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class ChestController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private string openTriggerName = "Open";

    [Header("Interaction")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool allowMultipleOpens = false; // Still only spawns loot once

    // Struct for defining loot prefabs and quantities
    [System.Serializable]
    public struct PrefabLootItem
    {
        [Tooltip("The Prefab of the item pickup (must have Rigidbody2D, Collider2D, and Item.cs script).")]
        public GameObject itemPrefab;
        [Range(1, 99)] public int quantity; // How many of this prefab to spawn
    }

    [Header("Loot Spawning")]
    [Tooltip("Prefabs to spawn when the chest opens.")]
    [SerializeField] private List<PrefabLootItem> lootPrefabs = new List<PrefabLootItem>();
    [Tooltip("Optional: Specific point where items spawn. If null, spawns slightly above chest.")]
    [SerializeField] private Transform itemSpawnPoint;
    [Tooltip("Base upward force applied to spawned items.")]
    [SerializeField] private float spawnUpwardForce = 3f;
    [Tooltip("Max random horizontal force (applied left or right).")] // Updated tooltip
    [SerializeField] private float spawnHorizontalForce = 1.5f;
    private bool lootSpawned = false; // Track if loot was spawned

    [Header("Visuals after Opening")]
    [SerializeField] private float delayBeforeFade = 2.5f;
    [SerializeField] private float fadeDuration = 2.0f;

    // Component References
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Collider2D interactionCollider;

    void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        interactionCollider = GetComponent<Collider2D>();

        if (animator == null || spriteRenderer == null || interactionCollider == null) {
            Debug.LogError($"ChestController on {gameObject.name} is missing required components (Animator, SpriteRenderer, or Collider2D)!", this);
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"OnTriggerEnter2D called by: {other.name} with tag: {other.tag}");

        // Prevent interaction if loot already spawned and multiple opens not allowed
        if (lootSpawned && !allowMultipleOpens) {
            Debug.Log("Chest already looted and allowMultipleOpens is false. Exiting interaction.");
            return;
        }

        if (other.CompareTag(playerTag)) {
            Debug.Log("Player detected. Attempting to call OpenChest...");
            OpenChest();
        }
    }

    private void OpenChest()
    {
        Debug.Log($"Inside OpenChest(). lootSpawned = {lootSpawned}");

        // Trigger Animation
        AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        bool isAlreadyOpenOrOpening = currentStateInfo.IsName("Chest_Opening") || currentStateInfo.IsName("Chest_Open_Idle");
        if (!isAlreadyOpenOrOpening) {
             Debug.Log($"Chest '{gameObject.name}' opening animation triggered!");
             if (animator != null) { animator.SetTrigger(openTriggerName); }
        } else {
             Debug.Log("Chest animation already open or opening.");
        }

        // Spawn Loot Prefabs (Only Once)
        if (!lootSpawned) {
            lootSpawned = true;
            Debug.Log($"Checking lootPrefabs. Count: {lootPrefabs.Count}");
            if (lootPrefabs.Count > 0) {
                Debug.Log("Loot count > 0. Starting SpawnLootRoutine...");
                StartCoroutine(SpawnLootRoutine());
            } else {
                 Debug.Log("Chest is empty (lootPrefabs count is 0). Proceeding to fade.");
                 ProceedToFadeOut();
            }
        }
        else {
             Debug.Log("Chest already looted. No new loot will spawn.");
             if (!allowMultipleOpens && interactionCollider != null && interactionCollider.enabled) {
                  Debug.Log("Disabling collider on already looted chest.");
                  interactionCollider.enabled = false;
             }
        }
    }

    private IEnumerator SpawnLootRoutine()
    {
        Debug.Log("SpawnLootRoutine started.");
        yield return new WaitForSeconds(0.1f);

        Vector3 spawnPosBase = (itemSpawnPoint != null) ? itemSpawnPoint.position : transform.position + Vector3.up * 0.5f;
        Debug.Log($"Base spawn position: {spawnPosBase}");

        foreach (PrefabLootItem loot in lootPrefabs) {
            Debug.Log($"Processing loot item: {(loot.itemPrefab != null ? loot.itemPrefab.name : "NULL PREFAB")}, Quantity: {loot.quantity}");

            if (loot.itemPrefab == null) {
                Debug.LogWarning("Skipping null item Prefab in chest loot table.");
                continue;
            }
             if (loot.quantity <= 0) {
                  Debug.LogWarning($"Skipping item {loot.itemPrefab.name} due to quantity <= 0.");
                  continue;
             }

            for (int i = 0; i < loot.quantity; i++) {
                Vector3 spawnPos = spawnPosBase + new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.1f, 0.1f), 0);
                Debug.Log($"Attempting to Instantiate {loot.itemPrefab.name} at {spawnPos}");

                GameObject spawnedItemObject = Instantiate(loot.itemPrefab, spawnPos, Quaternion.identity);

                if(spawnedItemObject == null) {
                     Debug.LogError($"Instantiation FAILED for prefab: {loot.itemPrefab.name}. The prefab might be invalid or corrupted.", loot.itemPrefab);
                     continue;
                }

                Rigidbody2D itemRb = spawnedItemObject.GetComponent<Rigidbody2D>();
                if (itemRb != null) {
                    // --- MODIFIED FORCE CALCULATION ---
                    // Calculate horizontal force randomly between -Max and +Max
                    float horizontalForce = Random.Range(-spawnHorizontalForce, spawnHorizontalForce);

                    // Calculate upward force with some variance (ensuring it's generally positive)
                    float upwardForceVariance = spawnUpwardForce * 0.2f; // Allow +/- 20% variance
                    float upwardForce = spawnUpwardForce + Random.Range(-upwardForceVariance, upwardForceVariance);
                    upwardForce = Mathf.Max(1.0f, upwardForce); // Ensure at least a small upward pop

                    // Combine into a Vector2 force
                    Vector2 forceVector = new Vector2(horizontalForce, upwardForce);
                    // --- END MODIFIED FORCE CALCULATION ---

                    Debug.Log($"Applying force {forceVector} to {spawnedItemObject.name}");
                    itemRb.AddForce(forceVector, ForceMode2D.Impulse); // Apply instantaneous force
                } else {
                    Debug.LogWarning($"Spawned loot item {spawnedItemObject.name} is missing a Rigidbody2D! Cannot apply force.", spawnedItemObject);
                }
            }
        }
         Debug.Log("Finished spawning loop. Proceeding to fade.");
         ProceedToFadeOut();
    }


    private void ProceedToFadeOut()
    {
        Debug.Log("ProceedToFadeOut called.");
        if (interactionCollider != null) {
            Debug.Log("Disabling interaction collider.");
            interactionCollider.enabled = false;
        }

        if (!allowMultipleOpens) {
             Debug.Log("Starting FadeOutAndDestroy coroutine...");
             StartCoroutine(FadeOutAndDestroy());
        } else {
             Debug.Log("allowMultipleOpens is true, chest will not fade or be destroyed.");
        }
    }


    private IEnumerator FadeOutAndDestroy()
    {
        yield return new WaitForSeconds(delayBeforeFade);
        Debug.Log($"Fading out chest {gameObject.name}...");

        float timer = 0f;
        Color startColor = spriteRenderer.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

        while (timer < fadeDuration) {
            timer += Time.deltaTime;
            spriteRenderer.color = Color.Lerp(startColor, endColor, timer / fadeDuration);
            yield return null;
        }
        spriteRenderer.color = endColor;

        Debug.Log($"Destroying chest {gameObject.name}.");
        Destroy(gameObject);
    }

    public void ResetChestVisual()
    {
        Debug.Log($"Attempting to reset chest '{gameObject.name}'.");
        lootSpawned = false;
        if(interactionCollider != null) interactionCollider.enabled = true;
        StopAllCoroutines();
        if(spriteRenderer != null) spriteRenderer.color = new Color(spriteRenderer.color.r, spriteRenderer.color.g, spriteRenderer.color.b, 1f);

        if (animator != null) {
            if (animator.HasState(0, Animator.StringToHash("Chest_Idle_Closed"))) {
                animator.Play("Chest_Idle_Closed", 0, 0f);
            } else {
                 Debug.LogWarning("State 'Chest_Idle_Closed' not found for reset.");
            }
            animator.ResetTrigger(openTriggerName);
        }
         Debug.Log($"Chest '{gameObject.name}' Reset.");
    }
}