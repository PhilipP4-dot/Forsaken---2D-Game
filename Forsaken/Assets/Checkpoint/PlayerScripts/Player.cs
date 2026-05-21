using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class Player : MonoBehaviour
{
    // Public Fields - Exposed in Inspector
    [Header("Movement")]
    [SerializeField] public float speed = 8f;
    [SerializeField] float runSpeedMultiplier = 1.5f;
    [SerializeField] public float groundJumpPower = 18f;
    [SerializeField] float crouchSpeedModifier = 0.5f;

    [Header("Physics & Jump Feel")]
    [SerializeField] LayerMask groundLayer;
    [SerializeField] public float maxFallSpeed = 40f;
    [SerializeField] float fallMultiplier = 3.0f;
    [SerializeField] float lowJumpMultiplier = 2.5f;
    [Tooltip("Gravity multiplier when RISING AND jump button IS HELD. Default=1. >1 = less floaty ascent.")]
    [SerializeField] float heldJumpAscentMultiplier = 1.0f;

    [Header("Checks")]
    [SerializeField] Transform groundCheckCollider;
    [SerializeField] Transform overheadCheckCollider;
    [SerializeField] Transform wallCheckCollider;
    [SerializeField] Collider2D standingCollider;

    [Header("Ledge Behaviour")]
    [SerializeField] float coyoteTime = 0.1f;
    private float coyoteTimeCounter;

    [Header("Jump Buffering")]
    [SerializeField] float jumpBufferTime = 0.1f;
    private float jumpBufferCounter;

    [Header("Wall Interaction")]
    [SerializeField] float wallCheckDistance = 0.5f;
    [SerializeField] LayerMask wallLayer;
    [SerializeField] float wallJumpHorizontalPower = 10f;
    [SerializeField] float wallJumpVerticalPower = 16f;
    [SerializeField] float wallJumpLockoutTime = 0.15f;
    private bool isTouchingWall = false;
    private bool isWallJumping = false;
    private float wallJumpTimer;

    // Constants
    const float groundCheckRadius = 0.15f;
    const float overheadCheckRadius = 0.2f;

    // Private State
    private float horizontalValue;
    private Rigidbody2D rb;
    private Animator animator;
    private bool facingRight = true;
    private bool isGrounded = false;
    private bool jumpHeld = false;
    private bool crouchPressed = false;
    private bool isRunning = false;
    private bool canMove = true; // For attack restriction

    // Knock‑back control
    [SerializeField] private float knockbackLockoutTime = 0.25f;
    private bool  isKnockback   = false;
    private float knockbackTimer = 0f;

    [SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    [SerializeField] private AudioClip wallJumpSound;
    [SerializeField] private AudioClip crouchSound;
    [SerializeField] private AudioClip runSound;
    [SerializeField] private AudioClip walkSound;

    [SerializeField] float footstepIntervalWalk = 0.4f;
    [SerializeField] float footstepIntervalRun = 0.25f;
    private float footstepTimer = 0f;

    private bool wasGroundedLastFrame = false;
    private bool wasCrouchingLastFrame = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();

        if (groundCheckCollider == null) Debug.LogError("Ground Check Collider not assigned!", this);
        if (overheadCheckCollider == null) Debug.LogError("Overhead Check Collider not assigned!", this);
        if (wallCheckCollider == null) Debug.LogError("Wall Check Collider not assigned!", this);
        if (standingCollider == null) Debug.LogError("Standing Collider not assigned!", this);
        if (rb == null) Debug.LogError("Rigidbody2D component missing!", this);

        if (!Mathf.Approximately(rb.gravityScale, 1f)) { rb.gravityScale = 1f; }
        rb.linearDamping = 0.1f;
        rb.angularDamping = 0.05f;
    }
    
    void Update()
    {
        // Input Gathering... (same as before)
        horizontalValue = Input.GetAxisRaw("Horizontal");
        jumpHeld = Input.GetButton("Jump");
        crouchPressed = Input.GetButton("Crouch");
        isRunning = Input.GetButton("Fire3");
        // Jump Buffer... (same as before)
        if (Input.GetButtonDown("Jump")) { jumpBufferCounter = jumpBufferTime; } else { if (jumpBufferCounter > 0f) { jumpBufferCounter -= Time.deltaTime; } }
        // Animator Input Speed... (same as before)
        if (animator != null) { animator.SetFloat("Speed", Mathf.Abs(horizontalValue)); }
    }

    void FixedUpdate()
    {
        // State Updates
        HandleWallJumpLockout();
        CheckIfAttacking();

        // Knock‑back timer countdown
        if (isKnockback)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            if (knockbackTimer <= 0f) isKnockback = false;
        }

        // Physics Checks
        GroundCheck();
        WallCheck();

        // Coyote Time
        if (!isGrounded) { coyoteTimeCounter -= Time.fixedDeltaTime; }

        // AUDIO TRIGGERS
        if (!wasGroundedLastFrame && isGrounded)
        {
            SoundManager.Instance.PlaySound(landSound);
        }
        wasGroundedLastFrame = isGrounded;

        // Crouch sound trigger (once)
        bool headBlocked = Physics2D.OverlapCircle(overheadCheckCollider.position, overheadCheckRadius, groundLayer);
        bool canStand = !crouchPressed && !headBlocked;
        bool actualCrouchState = isGrounded && (crouchPressed || !canStand);

        if (!wasCrouchingLastFrame && actualCrouchState)
        {
            SoundManager.Instance.PlaySound(crouchSound);
        }
        wasCrouchingLastFrame = actualCrouchState;
        // Actions
        HandleJump();
        if (!isKnockback)
            Move(horizontalValue, isRunning, actualCrouchState); // Pass states to Move
        ApplyBetterJumpPhysics();
        ClampFallSpeed();

        // Animator State... (same as before)
        if (animator != null) { 
            animator.SetBool("IsGrounded", isGrounded); 
            animator.SetBool("IsTouchingWall", isTouchingWall); 
            animator.SetFloat("VerticalVelocity", rb.linearVelocity.y); 
            animator.SetBool("IsCrouching", actualCrouchState); 
            bool currentlyRunning = isRunning && isGrounded && !actualCrouchState && horizontalValue != 0 && !isWallJumping; 
            animator.SetBool("IsRunning", currentlyRunning); 

            
        }
        // FOOTSTEP SOUND LOGIC
        bool shouldPlayFootstep = isGrounded && canMove && !isWallJumping && Mathf.Abs(horizontalValue) > 0.01f && !actualCrouchState;

        if (shouldPlayFootstep)
        {
            footstepTimer -= Time.fixedDeltaTime;
            float currentInterval = isRunning ? footstepIntervalRun : footstepIntervalWalk;

            if (footstepTimer <= 0f)
            {
                AudioClip clip = isRunning ? runSound : walkSound;
                SoundManager.Instance.PlaySound(clip);
                footstepTimer = currentInterval;
            }
        }
        else
        {
            footstepTimer = 0f; // Reset if not moving
        }
    }

    void CheckIfAttacking() { canMove = true; if (animator != null) { AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0); if (stateInfo.IsName("Ranged") || stateInfo.IsName("Player_Punch")) { canMove = false; } } }
    void HandleWallJumpLockout() { if (isWallJumping) { wallJumpTimer -= Time.fixedDeltaTime; if (wallJumpTimer <= 0f) { isWallJumping = false; } } }
    void GroundCheck() { isGrounded = Physics2D.OverlapCircle(groundCheckCollider.position, groundCheckRadius, groundLayer); if (isGrounded) { coyoteTimeCounter = coyoteTime; } }
    void WallCheck() { Vector2 checkDirection = facingRight ? Vector2.right : Vector2.left; Vector3 checkOrigin = wallCheckCollider.position + (Vector3)(checkDirection * 0.01f); RaycastHit2D hit = Physics2D.Raycast(checkOrigin, checkDirection, wallCheckDistance, wallLayer); isTouchingWall = hit.collider != null; Color rayColor = isTouchingWall ? Color.green : Color.red; Debug.DrawRay(checkOrigin, checkDirection * wallCheckDistance, rayColor); }

    // --- UPDATED: Move method speed calculation ---
    void Move(float direction, bool isRunningInput, bool actualCrouchState)
    {
        // Collider enabling
        if (standingCollider != null && standingCollider.enabled == actualCrouchState) { standingCollider.enabled = !actualCrouchState; }

        // Determine Speed based on state
        float currentSpeed;
        if (actualCrouchState) // If crouching, overrides running
        {
            currentSpeed = speed * crouchSpeedModifier;
        }
        // --- MODIFIED LINE BELOW: Removed '&& isGrounded' ---
        else if (isRunningInput) // If holding run (apply multiplier even in air)
        {
            currentSpeed = speed * runSpeedMultiplier;
        }
        // -------------------------------------------------
        else // Otherwise, normal walking speed
        {
            currentSpeed = speed;
        }

        float targetHorizontalVelocity = direction * currentSpeed;

        // Apply Velocity (respecting wall jump & attack lock)
        if (canMove && !isWallJumping)
        {
            // Prevent pushing into wall if airborne
            if (isTouchingWall && !isGrounded && direction != 0 && Mathf.Sign(direction) == (facingRight ? 1f : -1f))
            { targetHorizontalVelocity = 0f; }

            if (rb != null){
                rb.linearVelocity = new Vector2(targetHorizontalVelocity, rb.linearVelocity.y);
            }
        }
        else if (!canMove && !isWallJumping) // If movement locked (attacking)
        {
             if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y); // Stop horizontal motion
        }

        // Flipping (respecting locks)
        if (canMove && !isWallJumping) { if ((direction > 0 && !facingRight) || (direction < 0 && facingRight)) { Flip(); } }
    }
    // ---------------------------------------------

    // ===== Knock‑back API =====
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (rb == null) return;

        // Reset horizontal velocity so impulse is noticeable
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        // Apply impulse
        rb.AddForce(direction.normalized * force, ForceMode2D.Impulse);

        // Activate lockout
        isKnockback   = true;
        knockbackTimer = knockbackLockoutTime;
    }

    // HandleJump, ApplyBetterJumpPhysics, ClampFallSpeed, Flip, OnDrawGizmosSelected remain the same
    void HandleJump()
    {
        if (jumpBufferCounter > 0f && !isWallJumping && canMove)
        {
            if (isTouchingWall && !isGrounded)
            {
                float jumpDirection = facingRight ? -1f : 1f;
                if (rb != null) rb.linearVelocity = new Vector2(jumpDirection * wallJumpHorizontalPower, wallJumpVerticalPower);
                isWallJumping = true;
                wallJumpTimer = wallJumpLockoutTime;
                Flip();
                jumpBufferCounter = 0f;
                coyoteTimeCounter = 0f;
                SoundManager.Instance.PlaySound(wallJumpSound);
                return;
            }
            else if (isGrounded || coyoteTimeCounter > 0f)
            {
                if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, groundJumpPower);
                SoundManager.Instance.PlaySound(jumpSound); // Play landing sound
                jumpBufferCounter = 0f;
                coyoteTimeCounter = 0f;
            }
            jumpBufferCounter = 0f;
        }
    }    
    void ApplyBetterJumpPhysics() { if (rb != null && !isGrounded) { if (rb.linearVelocity.y < 0) { rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime; } else if (rb.linearVelocity.y > 0) { float currentMultiplier = jumpHeld ? heldJumpAscentMultiplier : lowJumpMultiplier; if (currentMultiplier > 1f) { rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (currentMultiplier - 1) * Time.fixedDeltaTime; } } } }
    void ClampFallSpeed() { if (rb != null && rb.linearVelocity.y < -maxFallSpeed) { rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed); } }
    void Flip() { facingRight = !facingRight; transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z); }
    void OnDrawGizmosSelected() { if (groundCheckCollider != null) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(groundCheckCollider.position, groundCheckRadius); } if (overheadCheckCollider != null) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(overheadCheckCollider.position, overheadCheckRadius); } if (wallCheckCollider != null) { Gizmos.color = Color.blue; Gizmos.DrawWireSphere(wallCheckCollider.position, 0.05f); } }

} // End of class Player