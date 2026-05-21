using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems; // Required for UI click check
using System.Linq; // Required for FirstOrDefault()

// Ensure WeaponType enum is defined (e.g., in WeaponSO.cs or a shared file)
// public enum WeaponType { Melee, Ranged }

public class PlayerAttackController : MonoBehaviour
{
    [Header("Weapon Handling")]
    [Tooltip("Default weapon ScriptableObject if nothing else is equipped (e.g., Fists). Assign this.")]
    [SerializeField] private WeaponSO defaultFistData;
    [Tooltip("Currently equipped weapon data (managed internally via SetActiveWeapon). Do not assign manually.")]
    [SerializeField] private WeaponSO currentWeaponData;

    [Header("Setup")]
    [Tooltip("Point where projectiles spawn from (Required for Ranged weapons). Assign this.")]
    [SerializeField] private Transform projectileSpawnPoint;
    [Tooltip("Center point for melee attack overlap check (Required for Melee weapons). Assign this.")]
    [SerializeField] private Transform meleeAttackPoint;
    [Tooltip("Layers considered as enemies for attacks. Set this in the Inspector.")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Input")]
    [Tooltip("Key used to initiate attacks/weapon actions.")]
    [SerializeField] private KeyCode attackKey = KeyCode.Mouse0; // Primary attack key

    // Internal State
    private float nextAttackTime = 0f; // Cooldown timer
    private WeaponSO weaponInitiatingAttack; // Stores weapon used for current attack sequence
    private float manaConsumedThisAttack = 0f; // Stores mana cost for current attack

    // References
    private Animator animator;
    private Player playerMovement; // Optional reference for player state/facing
    private HotbarManager hotbarManager; // Needed to consume items
    private PlayerHealth playerHealth; // Needed for effects that cost HP
    private PlayerMana playerMana;     // Needed for effects that cost Mana

    [SerializeField] private AudioClip meleeSound; // Optional: Sound for attacks
    [SerializeField] private AudioClip rangedSound; // Optional: Sound for ranged attacks
    [SerializeField] private AudioClip swordSound; // Optional: Sound for sword attacks

    [SerializeField] private float meleeInterval = 0.4f; // Optional: Time between melee attacks (if needed)

    void Awake()
    {
        // Get component references
        animator = GetComponent<Animator>();
        playerMovement = GetComponent<Player>(); // Assumes a Player script exists
        playerHealth = GetComponent<PlayerHealth>();
        playerMana = GetComponent<PlayerMana>();
        hotbarManager = FindFirstObjectByType<HotbarManager>(); // Use FindFirstObjectByType

        // --- Reference Validation ---
        bool referencesValid = true;
        if (hotbarManager == null) { Debug.LogError("PlayerAttackController could not find HotbarManager! Item consumption will fail.", this); referencesValid = false; }
        if (animator == null) { Debug.LogWarning("PlayerAttackController could not find Animator component. Animations won't play.", this); }
        if (defaultFistData == null) { Debug.LogError("Default Fist Data (WeaponSO) not assigned! Player will be unarmed if no weapon is equipped.", this); referencesValid = false; }
        if (meleeAttackPoint == null && projectileSpawnPoint == null) { Debug.LogError("Assign at least Melee Attack Point or Projectile Spawn Point!", this); referencesValid = false; }

        if (!referencesValid) {
             Debug.LogError("PlayerAttackController disabled due to missing critical references.", this);
             enabled = false; // Prevent Update from running if setup is invalid
        }
    }

    void Start()
    {
        SetActiveWeapon(null); // Set initial weapon (usually fists)
    }

    void Update()
    {
        HandleAttacking(); // Check for attack input each frame
    }

    /// <summary> Gets the currently equipped weapon data (or default fists). </summary>
    public WeaponSO GetCurrentWeaponData()
    {
        return (currentWeaponData != null) ? currentWeaponData : defaultFistData;
    }

    /// <summary> Updates the active weapon based on HotbarManager notification. </summary>
    public void SetActiveWeapon(string weaponItemName)
    {
        WeaponSO previousWeaponData = currentWeaponData;
        WeaponSO newWeaponData = null;

        if (!string.IsNullOrEmpty(weaponItemName) && hotbarManager != null) {
            ItemSO selectedItemSO = hotbarManager.GetSelectedItemSO();
            if (selectedItemSO != null && selectedItemSO is WeaponSO) {
                newWeaponData = (WeaponSO)selectedItemSO;
            }
        }
        if (newWeaponData == null) { newWeaponData = defaultFistData; } // Default to fists

        if (previousWeaponData != newWeaponData) { // Apply effects only if weapon changed
            ApplyUnequipEffects(previousWeaponData);
            ApplyEquipEffects(newWeaponData);
        }
        currentWeaponData = newWeaponData; // Update current weapon
    }

    /// <summary> Applies OnEquip effects from a WeaponSO. </summary>
    private void ApplyEquipEffects(WeaponSO weapon) {
        if (weapon != null && weapon.weaponEffects != null) {
            foreach (EffectSO effect in weapon.weaponEffects) { effect?.ApplyOnEquip(this.gameObject); }
        }
    }

    /// <summary> Removes OnUnequip effects from a WeaponSO. </summary>
    private void ApplyUnequipEffects(WeaponSO weapon) {
        if (weapon != null && weapon.weaponEffects != null) {
            foreach (EffectSO effect in weapon.weaponEffects) { effect?.RemoveOnUnequip(this.gameObject); }
        }
    }

    /// <summary> Handles attack input, costs, initiation, cooldown, and consumption. </summary>
    void HandleAttacking()
    {
        if (Time.time < nextAttackTime) return; // Check cooldown

        if (Input.GetKeyDown(attackKey)) // Check attack input
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { return; } // Ignore UI clicks

            WeaponSO weaponToUse = GetCurrentWeaponData();
            if (weaponToUse == null) { return; } // Should always have at least fists

            // Store weapon initiating this specific attack sequence
            weaponInitiatingAttack = weaponToUse;
            manaConsumedThisAttack = 0f; // Reset mana counter for this attack

            // --- Check Resource Costs ---
            if (weaponInitiatingAttack.weaponEffects != null) {
                foreach (EffectSO effect in weaponInitiatingAttack.weaponEffects) {
                    if (effect != null) {
                        // Store mana before cost application for mana-scaling effects
                        float manaBeforeCost = 0;
                        if (playerMana != null && effect is SingleUseManaAttackSO) {
                           manaBeforeCost = playerMana.GetCurrentMana();
                        }

                        // If any effect cost fails, abort the attack
                        if (!effect.ApplyResourceCost(this.gameObject)) {
                            weaponInitiatingAttack = null; // Clear stored weapon reference
                            manaConsumedThisAttack = 0f;   // Reset consumed mana
                            return; // Stop attack
                        }

                        // If cost was paid and it was the mana effect, record how much was used
                        if (playerMana != null && effect is SingleUseManaAttackSO) {
                            manaConsumedThisAttack = manaBeforeCost - playerMana.GetCurrentMana();
                        }
                    }
                }
            }
            // --- Costs OK ---


            // --- Initiate Attack ---
            bool attackPerformed = false;
            if (weaponInitiatingAttack.weaponType == WeaponType.Melee) {
                if (meleeAttackPoint == null) { Debug.LogError($"Melee Attack Point missing for {weaponInitiatingAttack.name}!", this); weaponInitiatingAttack = null; return; }
                PerformMeleeAttack(weaponInitiatingAttack);
                attackPerformed = true;
                SoundManager.Instance.PlaySound(meleeSound); // Play sound if assigned
            }
            else if (weaponInitiatingAttack.weaponType == WeaponType.Ranged) {
                 if (projectileSpawnPoint == null) { Debug.LogError($"Projectile Spawn Point missing for {weaponInitiatingAttack.name}!", this); weaponInitiatingAttack = null; return; }
                 if (weaponInitiatingAttack.projectilePrefab == null) { Debug.LogError($"Projectile Prefab missing for {weaponInitiatingAttack.name}!", weaponInitiatingAttack); weaponInitiatingAttack = null; return; }
                PerformRangedAttack(weaponInitiatingAttack);
                attackPerformed = true;
            }

            // --- Post-Attack Logic ---
            if (attackPerformed) {
                // Apply Cooldown
                if (weaponInitiatingAttack.attackRate > 0) { nextAttackTime = Time.time + 1f / weaponInitiatingAttack.attackRate; }
                else { nextAttackTime = Time.time + 1f; } // Default cooldown

                // Check for Item Consumption
                bool consumeItem = false;
                if (weaponInitiatingAttack.weaponEffects != null) {
                    foreach (EffectSO effect in weaponInitiatingAttack.weaponEffects) {
                        if (effect != null && effect.ShouldConsumeSourceItem()) {
                            consumeItem = true; break;
                        }
                    }
                }
                if (consumeItem) {
                    if (hotbarManager != null) { hotbarManager.ConsumeSelectedItemCompletely(); }
                    else { Debug.LogError("Cannot consume item: HotbarManager reference missing!", this); }
                    // Note: weaponInitiatingAttack still holds reference for SpawnProjectile if needed
                }
            } else {
                 weaponInitiatingAttack = null; // Clear if no attack performed
                 manaConsumedThisAttack = 0f;
            }
        }
    }

    /// <summary> Performs melee attack logic using the specified weapon data. </summary>
    void PerformMeleeAttack(WeaponSO weaponData) {
        if (animator != null) { 
            animator.SetTrigger("Punch");
            
        } // Use appropriate trigger

     
            
         
            
            
    
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(meleeAttackPoint.position, weaponData.rangeOrSpeed, enemyLayer);
        foreach (Collider2D enemyCollider in hitEnemies) {
            GameObject enemy = enemyCollider.gameObject;
            float finalDamage = weaponData.damage;
            // Apply damage mods
            if (weaponData.weaponEffects != null) { foreach (EffectSO effect in weaponData.weaponEffects) { if (effect != null) { finalDamage = effect.ModifyOutgoingDamage(finalDamage, this.gameObject, enemy); } } }
            // Deal damage
            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null) { enemyHealth.TakeDamage(finalDamage); }
            // Apply OnHit effects
            if (weaponData.weaponEffects != null) { foreach (EffectSO effect in weaponData.weaponEffects) { effect?.ApplyOnHit(this.gameObject, enemy, finalDamage); } }
        }
    }

    /// <summary> Triggers ranged attack animation or directly spawns projectile. </summary>
    void PerformRangedAttack(WeaponSO weaponData) {
        if (animator != null) { animator.SetTrigger("Ranged"); } // Use appropriate trigger
        else { SpawnProjectileFromAnimation(); } // Fallback
        SoundManager.Instance.PlaySound(rangedSound); // Play sound if assigned
    }

    /// <summary> Spawns projectile using data from 'weaponInitiatingAttack'. Called by animation event or directly. </summary>
    public void SpawnProjectileFromAnimation() {
        WeaponSO weaponToUse = weaponInitiatingAttack; // Use the stored weapon reference

        // --- Debug Logs (Optional - keep for troubleshooting) ---
        // Debug.Log($"--- SpawnProjectile Check ---");
        // Debug.Log($"[SpawnCheck] weaponToUse (from weaponInitiatingAttack) is null? : {(weaponToUse == null)}");
        // if (weaponToUse != null) { Debug.Log($"[SpawnCheck] weaponToUse Name: {weaponToUse.itemName}"); Debug.Log($"[SpawnCheck] weaponType == Ranged? : {weaponToUse.weaponType == WeaponType.Ranged}..."); Debug.Log($"[SpawnCheck] projectilePrefab is null? : {(weaponToUse.projectilePrefab == null)}..."); }
        // Debug.Log($"[SpawnCheck] projectileSpawnPoint is null? : {(projectileSpawnPoint == null)}");
        // if (projectileSpawnPoint != null) { Debug.Log($"[SpawnCheck] projectileSpawnPoint GameObject active? : {projectileSpawnPoint.gameObject.activeInHierarchy}"); }
        // Debug.Log($"---------------------------");
        // -------------------------------------------------------

        // Check conditions using the stored weapon reference
        if (weaponToUse != null && weaponToUse.weaponType == WeaponType.Ranged &&
            weaponToUse.projectilePrefab != null && projectileSpawnPoint != null &&
            projectileSpawnPoint.gameObject.activeInHierarchy)
        {
            // --- Calculate Final Damage (Including Mana Scaling) ---
            float finalDamage = weaponToUse.damage;
            SingleUseManaAttackSO manaEffect = weaponToUse.weaponEffects?.OfType<SingleUseManaAttackSO>().FirstOrDefault();
            if (manaEffect != null) {
                 finalDamage += manaConsumedThisAttack * manaEffect.damagePerManaPoint;
                 // Debug.Log($"Calculated Final Damage: {weaponToUse.damage}(base) + {manaConsumedThisAttack}(mana) * {manaEffect.damagePerManaPoint}(mult) = {finalDamage}");
            }
            finalDamage = Mathf.Max(0, finalDamage); // Ensure non-negative damage
            // ------------------------------------------------------

            // --- Instantiate & Configure Projectile ---
            GameObject projectile = Instantiate(weaponToUse.projectilePrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);
            ProjectileController projControl = projectile.GetComponent<ProjectileController>();
            if (projControl != null) {
                projControl.damage = finalDamage; // Assign FINAL calculated damage
                projControl.speed = weaponToUse.rangeOrSpeed;
                projControl.shooterTag = this.gameObject.tag;
                projControl.shooterObject = this.gameObject;
                projControl.weaponEffects = weaponToUse.weaponEffects; // Pass effects list
            } else { Debug.LogError($"Projectile prefab '{weaponToUse.projectilePrefab.name}' missing ProjectileController script!", weaponToUse.projectilePrefab); }

            // --- Adjust Facing for 2D ---
            if (transform.localScale.x < 0f)
            {
                // Rotate around Z so transform.right points left
                projectile.transform.Rotate(0f, 0f, 180f);
            }
        }
        else {
             Debug.LogWarning("SpawnProjectileFromAnimation called but conditions not met. Check [SpawnCheck] logs if enabled.", this);
        }

        // --- Clear Stored References ---
        weaponInitiatingAttack = null;
        manaConsumedThisAttack = 0f;
        // -----------------------------
    }

    /// <summary> Draws gizmos for melee range in the editor. </summary>
    void OnDrawGizmosSelected() {
        if (meleeAttackPoint != null) {
             WeaponSO gizmoWeapon = GetCurrentWeaponData();
             if (gizmoWeapon != null && gizmoWeapon.weaponType == WeaponType.Melee) {
                 Gizmos.color = Color.red;
                 Gizmos.DrawWireSphere(meleeAttackPoint.position, gizmoWeapon.rangeOrSpeed);
             }
        }
    }

} // End of class PlayerAttackController
