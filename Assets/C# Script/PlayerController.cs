using System;
using System.Collections;
using UnityEngine;


public enum FlipType
{
    SpriteRenderer,
    Scale
}
public enum JumpType
{
    fixedJump,
    variableJump
}
public struct FrameInput
{
    public bool JumpDown;
    public bool JumpHeld;
    public bool JumpUp;
    public bool DashDown;
    public Vector2 Move;
}

public class PlayerController : MonoBehaviour
{
    [Header("Flip Settings")]
    [SerializeField] private FlipType flipType = FlipType.Scale;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 15f;
    [SerializeField] private float acceleration = 75f;
    [SerializeField] private float deceleration = 70f;
    [SerializeField] private float airdeceleration = 35f;
    [SerializeField] private float groundingForce = -1f;
    [Range(0f, 1f)]
    [SerializeField] private float groundDistance = 0.05f;

    [Header("Jump Settings")]
    [SerializeField] private JumpType jumpType = JumpType.fixedJump;
    [SerializeField] private float jumpForce = 30f;
    [SerializeField] private float maxFallSpeed = 40f;
    [SerializeField] private float fallAcceleration = 70f;
    [Range(0f, 1f)]
    [SerializeField] private float coyoteTime = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpBufferTime = 0.2f;
    [SerializeField] private float jumpEndEarlyMultiplier = 5f;
    private float variableJumpMultiplier = 0.5f;

    [Header("Abilities")]
    [SerializeField] private bool allowDoubleJump = true;
    [SerializeField] private bool allowDash = true;
    [SerializeField] private bool allowWallJump = true;
    [SerializeField] private bool allowWallClimb = true;
    [SerializeField] private bool allowWallSlide = true;
    [SerializeField] private bool allowWallStick = true; // New separate toggle

    [Header("Double Jump Settings")]
    // [SerializeField] bool enableDoubleJump = true; // Refactored to allowDoubleJump
    [SerializeField] int maxAirJumps = 1;
    [SerializeField] float airJumpPower = 32f;
    [SerializeField] bool airJumpsResetVelocity = true;
    [SerializeField] float airJumpVelocityResetThreshold = -5f;

    [Header("Wall Settings")]
    [SerializeField] LayerMask wallLayer;
    [Range(0.1f, 2f)]
    [SerializeField] float WallCheckDistance = 0.7f;
    [Range(0.5f, 5f)]
    [SerializeField] float wallSlideSpeed = 2f;

    [Range(10f, 90f)]
    [SerializeField] private float wallJumpAngle = 60f;
    [Range(0f, 1f)]
    [SerializeField] private float wallJumpTime = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float wallJumpInputBuffer = 0.1f;
    [SerializeField] private bool enableWallJumpChaining = true;
    [SerializeField] bool canDoDoubleJumpFromWall = false;

    [SerializeField] float wallClimbForceH = 30f;
    [SerializeField] float wallClimbForceV = 30f;
    [Range(10f, 90f)]
    [SerializeField] float wallClimbAngle = 60f;

    [Header("Wall Stick Settings")]
    [SerializeField] float wallClimbSpeed = 5f;
    [SerializeField] float wallStickCooldownDuration = 0.2f; 
    [SerializeField] float wallStickJumpForce = 10f; // New variable for Stick Jump
    private float currentWallStickCooldown;
    private bool isWallClimbing;


    [Header("Simple Dash Settings")]
    [SerializeField] private float dashSpeed = 25f;
    [Range(0f, 1f)]
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCoolDown = 0.6f;
    [SerializeField] bool canDashInAir = true;
    [SerializeField] int maxAirDashes = 1;



    //Component Reference
    Rigidbody2D rb;
    CapsuleCollider2D cc;
    SpriteRenderer sr;
    Vector3 originalScale;
    bool facingRight = true;

    private float gameTime;

    //Ground Detection
    private bool isGrounded;
    private float timeLeftGround = float.MinValue;
    private bool jumpEndedEarly;
    private bool canUseCoyote;
    private bool canUseJumpBuffer;
    private float timeJumpPressed;
    private bool shouldJump;
    private Vector2 currentVelocity;
    private FrameInput currentInput;
    private bool oldQuerySetting;
    private bool currentlyJumping;  //variable Jump stuff
    private bool jumpButtonWasReleased;

    private int airJumpsUsed = 0;


    private bool isSlidingOnWall;
    private bool canSlideOnWall;
    private bool wallOnLeft;
    private bool wallOnRight;
    private bool currentlyWallJumping;
    private bool hasWallJumped = false;
    float wallJumpDirection;
    float walljumpTimer;
    Vector2 wallJumpPower;
    Vector2 wallClimbPower;

    private bool currentlyDashing;
    private bool canDash = true;
    private float dashCooldownTimer;
    private int dashesUsed;
    private bool shouldDash;

    private float defaultGravity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravity = rb.gravityScale;
        cc = GetComponent<CapsuleCollider2D>();

        sr = GetComponent<SpriteRenderer>();

        originalScale = transform.localScale;

        oldQuerySetting = Physics2D.queriesStartInColliders;
        timeJumpPressed = float.MinValue;

        float angle = wallJumpAngle * Mathf.Deg2Rad;
        wallJumpPower = new Vector2(wallClimbForceH * Mathf.Cos(angle), wallClimbForceV * MathF.Sin(angle));

        float climbAngle = wallClimbAngle * Mathf.Deg2Rad;
        wallClimbPower = new Vector2(wallClimbForceH * Mathf.Cos(climbAngle), wallClimbForceV * Mathf.Sin(climbAngle));

    }

    private void Start()
    {
        if (flipType == FlipType.SpriteRenderer && sr == null)
        {
            Debug.LogWarning("No SpriteRenderer found so using default Scale");
            flipType = FlipType.Scale;
        }
    }
    private void Update()
    {
        gameTime += Time.deltaTime;

        UpdateDashCooldown();
        UpdateWallStickCooldown();
    }

    void FixedUpdate()
    {
        // 1. If we want to disable gravity (e.g. Grappling), we usually want to
        // respect external forces. Sync currentVelocity with Rigidbody to not fight it.
        if (disableGravity)
        {
            currentVelocity = rb.linearVelocity;
        }

        // ApplyVelocityToRigidbody(); // MOVED TO END

        CheckIfGrounded();
        CheckWallDetection();

        if (isWallClimbing)
        {
            HandleWallClimbing();
        }
        else
        {
            HandleWallSliding();
            HandleMovenment();
            ApplyGravity();
        }

        HandleJumping();
        HandleDashing();
        
        ApplyVelocityToRigidbody(); // Applied AFTER physics/logic calculation
    }

    public void SetInput(FrameInput input)
    {
        currentInput = input;

        if (currentInput.JumpDown)
        {
            shouldJump = true;
            timeJumpPressed = gameTime;
        }

        if (currentInput.JumpUp)
        {
            jumpButtonWasReleased = true;
        }

        if (currentInput.DashDown)
        {
            shouldDash = true;
        }

    }
    public void SetMovementInput(Vector2 movement)
    {
        currentInput.Move = movement;
    }

    public void SetJumpInput(bool jumpDown, bool jumpHeld, bool jumpUp)
    {
        if (jumpDown)
        {
            shouldJump = true;
            timeJumpPressed = gameTime;
            currentInput.JumpDown = true;
        }

        if (jumpUp)
        {
            jumpButtonWasReleased = true;
            currentInput.JumpUp = true;
        }

        currentInput.JumpHeld = jumpHeld;
    }
    public void SetDashInput(bool dashPressed)
    {
        if (dashPressed)
        {
            shouldDash = true;
            currentInput.DashDown = true;
        }
    }

    private void TurnCharacter(bool turnRight)
    {
        if (facingRight == turnRight) return;

        facingRight = turnRight;
        if (flipType == FlipType.SpriteRenderer)
        {
            if (sr != null)
            {
                sr.flipX = !facingRight;
            }
        }
        else if (flipType == FlipType.Scale)
        {
            transform.localScale = new Vector3(originalScale.x * (facingRight ? 1 : -1), originalScale.y, originalScale.z);
        }
    }

    private void CheckIfGrounded()
    {
        Physics2D.queriesStartInColliders = false;

        bool hitGround = Physics2D.CapsuleCast(cc.bounds.center, cc.bounds.size, cc.direction, 0, Vector2.down, groundDistance);
        bool hitCeiling = Physics2D.CapsuleCast(cc.bounds.center, cc.bounds.size, cc.direction, 0, Vector2.up, groundDistance);

        if (hitCeiling)
        {
            currentVelocity.y = Mathf.Min(currentVelocity.y, 0);
        }
        if (!isGrounded && hitGround)
        {
            isGrounded = true;
            canUseCoyote = true;
            canUseJumpBuffer = true;
            jumpEndedEarly = false;
            currentlyJumping = false;
            jumpButtonWasReleased = false;
            currentlyWallJumping = false;
            hasWallJumped = false;
            dashesUsed = 0;

            airJumpsUsed = 0;



        }
        else if (isGrounded && !hitGround)
        {
            isGrounded = false;
            timeLeftGround = gameTime;

        }
        Physics2D.queriesStartInColliders = oldQuerySetting;
    }
    bool HasBufferedJump()
    {
        return canUseJumpBuffer && gameTime < timeJumpPressed + jumpBufferTime;
    }
    bool CanUseCoyoteTime()
    {
        return canUseCoyote && !isGrounded && gameTime < timeLeftGround + coyoteTime;
    }
    void HandleJumping()
    {
        if (!shouldJump && !HasBufferedJump())
        {
            return;
        }

        // New Priority: Stick Jump
        if (isWallClimbing)
        {
            WallStickJump();
        }
        else if (CanDoWallJump())
        {
            DoWallJump();
        }

        else if (isGrounded || canUseCoyote)
        {
            DoNormalJump();
        }
        else if (CanDoAirJump())
        {
            DoAirJump();
        }

        shouldJump = false;
        jumpButtonWasReleased = false;
    }

    private void DoNormalJump()
    {
        currentVelocity.y = jumpForce;

        jumpEndedEarly = false;
        timeJumpPressed = 0;
        canUseJumpBuffer = false;
        canUseCoyote = false;

        currentlyJumping = true;
        jumpButtonWasReleased = false;


    }
    bool CanDoAirJump()
    {
        if (!allowDoubleJump || airJumpsUsed >= maxAirJumps || isSlidingOnWall || currentlyWallJumping || currentlyDashing || isGrounded)
        {
            return false;
        }
        if (hasWallJumped && !canDoDoubleJumpFromWall)
        {
            return false;
        }


        return true;
    }

    void DoAirJump()
    {
        if (airJumpsResetVelocity && currentVelocity.y < airJumpVelocityResetThreshold)
            currentVelocity.y = 0f;

        jumpEndedEarly = false;
        timeJumpPressed = 0;
        canUseJumpBuffer = false;
        canUseCoyote = false;
        currentVelocity.y = airJumpPower;
        currentlyJumping = true;
        jumpButtonWasReleased = false;

        airJumpsUsed++;



    }





    void HandleMovenment()
    {
        if (currentlyWallJumping || currentlyDashing || isSlidingOnWall)
        {
            return;
        }

        if (currentlyWallJumping || isSlidingOnWall)
        {
            return;
        }


        if (currentInput.Move.x == 0)
        {
            float decel;
            if (isGrounded)
            {
                decel = deceleration;
            }
            else
            {
                decel = airdeceleration;
            }
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0, decel * Time.fixedDeltaTime);
        }
        else
        {
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, currentInput.Move.x * moveSpeed, acceleration * Time.fixedDeltaTime);
            TurnCharacter(currentInput.Move.x > 0);

        }

    }

    private void ApplyGravity()
    {
        // If DisableGravity is on, DO NOT apply manual gravity.
        if (disableGravity) return;

        if (isGrounded)
        {
            currentVelocity.y = Mathf.Max(currentVelocity.y, groundingForce);
        }
        else if (isSlidingOnWall)
        {
            return;
        }
        else if (currentlyDashing)
        {
            return;
        }
        else
        {
            if (jumpType == JumpType.variableJump && currentlyJumping && jumpButtonWasReleased && currentVelocity.y > 0)
            {
                currentVelocity.y += variableJumpMultiplier;
                jumpEndedEarly = true;
                jumpButtonWasReleased = false;
            }
            float gravityMult;

            if (jumpEndedEarly && currentVelocity.y > 0)
            {
                gravityMult = jumpEndEarlyMultiplier;
            }
            else
            {
                gravityMult = 1f;
            }

            currentVelocity.y = Mathf.MoveTowards(currentVelocity.y, -maxFallSpeed, gravityMult * fallAcceleration * Time.fixedDeltaTime);
        }
    }
    void ApplyVelocityToRigidbody()
    {

        if (!currentlyDashing)
        {
            rb.linearVelocity = currentVelocity;
        }

    }

    //Wall releated code

    void CheckWallDetection()
    {
        Vector2 topPoint = new Vector2(transform.position.x, cc.bounds.max.y - 0.1f);
        Vector2 middlePoint = transform.position;
        Vector2 bottomPoint = new Vector2(transform.position.x, cc.bounds.min.y + 0.1f);

        RaycastHit2D leftTop = Physics2D.Raycast(topPoint, Vector2.left, WallCheckDistance, wallLayer);
        RaycastHit2D leftMiddle = Physics2D.Raycast(middlePoint, Vector2.left, WallCheckDistance, wallLayer);
        RaycastHit2D leftBottom = Physics2D.Raycast(bottomPoint, Vector2.left, WallCheckDistance, wallLayer);

        RaycastHit2D rightTop = Physics2D.Raycast(topPoint, Vector2.right, WallCheckDistance, wallLayer);
        RaycastHit2D rigthMiddle = Physics2D.Raycast(middlePoint, Vector2.right, WallCheckDistance, wallLayer);
        RaycastHit2D rigthbottom = Physics2D.Raycast(bottomPoint, Vector2.right, WallCheckDistance, wallLayer);


        // Determine if a wall is on the left
        if (leftTop.collider != null || leftMiddle.collider != null || leftBottom.collider != null)
        {
            wallOnLeft = true;
        }
        else
        {
            wallOnLeft = false;
        }

        if (rightTop.collider != null || rigthMiddle.collider != null || rigthbottom.collider != null)
        {
            wallOnRight = true;
        }
        else
        {
            wallOnRight = false;
        }

        bool wasSliding = isSlidingOnWall;
        bool holdingTowardWall;

        if (wallOnLeft && currentInput.Move.x < 0)
        {
            holdingTowardWall = true;
        }
        else if (wallOnRight && currentInput.Move.x > 0)
        {
            holdingTowardWall = true;
        }
        else
        {
            holdingTowardWall = false;
        }

        canSlideOnWall = allowWallSlide && !isGrounded && (wallOnLeft || wallOnRight) && currentVelocity.y <= 0 && !currentlyDashing;
        isSlidingOnWall = canSlideOnWall && holdingTowardWall && !currentlyWallJumping && !currentlyDashing;


        if (isSlidingOnWall)
        {
            if (wallOnLeft)
                TurnCharacter(false);
            if (wallOnRight)
                TurnCharacter(true);
        }

    }
    private void HandleWallSliding()
    {
        if (isSlidingOnWall)
        {
            currentVelocity.y = Mathf.Max(currentVelocity.y, -wallSlideSpeed);
            if (!currentlyWallJumping && !currentlyDashing)
            {
                currentVelocity.x = 0;
            }
        }
        if (!isSlidingOnWall && walljumpTimer > 0)
        {
            walljumpTimer -= Time.fixedDeltaTime;
        }
        if (isSlidingOnWall)
            walljumpTimer = wallJumpInputBuffer;

    }

    private bool ShouldClimbSameWall()
    {
        return allowWallClimb && currentInput.Move.y > 0.1f; // in order to climb same wall player need to hold W or up arrow key 
    }
    private void DoWallJump()
    {
        bool climbingUp = ShouldClimbSameWall();

        if (wallOnLeft)
        {
            wallJumpDirection = 1f;
        }
        else
        {
            wallJumpDirection = -1f;
        }

        if (climbingUp)
        {
            currentVelocity.x = wallJumpDirection * wallClimbPower.x;
            currentVelocity.y = wallClimbPower.y;

        }
        else
        {
            currentVelocity.x = wallJumpDirection * wallJumpPower.x;
            currentVelocity.y = wallJumpPower.y;
            TurnCharacter(wallJumpDirection > 0);

        }
        currentlyWallJumping = true;
        hasWallJumped = true;
        isSlidingOnWall = false;
        
        // Wall Stick Break
        isWallClimbing = false;
        currentWallStickCooldown = wallStickCooldownDuration; // Set cooldown
        
        currentlyJumping = true;
        jumpButtonWasReleased = false;

        airJumpsUsed = 0;

        Invoke(nameof(StopWallJump), wallJumpTime);
        canUseJumpBuffer = false;
        canUseCoyote = false;


    }
    void StopWallJump()
    {
        currentlyWallJumping = false;
    }
    bool CanDoWallJump()
    {
        if (!allowWallJump) return false;

        bool onWallOrRecent = isSlidingOnWall || (walljumpTimer > 0 && (wallOnLeft || wallOnRight));
        if (enableWallJumpChaining && !isGrounded)
        {
            return onWallOrRecent;
        }

        return onWallOrRecent;
    }




    // Dash related code 
    void UpdateDashCooldown()
    {
        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }

        if (dashCooldownTimer <= 0 && !canDash)
        { canDash = true; }

    }
    void HandleDashing()
    {
        if (!shouldDash || currentlyDashing)
            return;

        if (!CanPlayerDash())
        {
            shouldDash = false;
            return;
        }

        StartDash();
        shouldDash = false;
    }
    bool CanPlayerDash()
    {
        if (!allowDash || !canDash || dashCooldownTimer > 0)
            return false;

        if (!isGrounded && dashesUsed >= maxAirDashes && canDashInAir)
            return false;

        if (currentlyWallJumping)
            return false;

        return true;
    }


    void StartDash()
    {
        currentlyDashing = true;
        canDash = false;
        dashCooldownTimer = dashCoolDown;

        if (!isGrounded && canDashInAir)
            dashesUsed++;

        StartCoroutine(DoDashMovement());

    }

    IEnumerator DoDashMovement()
    {
        float timeElapsed = 0f;
        Vector2 dashDir = facingRight ? Vector2.right : Vector2.left;

        while (timeElapsed < dashDuration)
        {
            rb.linearVelocity = dashDir * dashSpeed;
            timeElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        FinishDash();
    }

    void FinishDash()
    {
        currentlyDashing = false;
        float momentum = 0.3f;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x * momentum, rb.linearVelocity.y);

    }

    // Wall Stick Logic

    private void UpdateWallStickCooldown()
    {
        if (currentWallStickCooldown > 0)
            currentWallStickCooldown -= Time.deltaTime;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check Layer
        if (((1 << collision.gameObject.layer) & wallLayer) != 0)
        {
            // Check Cooldown
            if (currentWallStickCooldown > 0) return;

            Vector2 normal = collision.GetContact(0).normal;
            
            bool hitWallOnLeft = normal.x > 0.5f;
            bool hitWallOnRight = normal.x < -0.5f;

            if (hitWallOnLeft || hitWallOnRight)
            {
                if (!isWallClimbing)
                {
                    // Only stick if we have the ability
                    if (!allowWallStick) return; // REPLACED allowWallClimb with allowWallStick

                    Debug.Log("Wall Hit: Sticking");
                    isWallClimbing = true;
                    
                    // Snap Logic
                    if(cc)
                    {
                        float offset = cc.bounds.extents.x + 0.02f;
                        float dir = hitWallOnLeft ? 1f : -1f;
                        float targetX = collision.GetContact(0).point.x + (dir * offset);
                        StartCoroutine(SmoothSnapToWall(targetX, 0.1f));
                    }
                    
                    // Reset vertical velocity usually
                    currentVelocity.y = 0;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0); 
                }
            }
        }
    }

    System.Collections.IEnumerator SmoothSnapToWall(float targetX, float duration)
    {
        float elapsed = 0f;
        float startX = transform.position.x;
        
        while (elapsed < duration)
        {
            if (!isWallClimbing) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = t * t * (3f - 2f * t); // Smooth step
            
            float newX = Mathf.Lerp(startX, targetX, t);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
            yield return null;
        }
        
        if (isWallClimbing)
        {
            transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
        }
    }

    void HandleWallClimbing()
    {
        if (isWallClimbing)
        {
            float vInput = currentInput.Move.y;
            float targetY = vInput * wallClimbSpeed;
            
            // Explicitly overwrite velocity for Stick mode
            currentVelocity = new Vector2(0, targetY);
            
            CheckWallExit();
        }
    }

    void WallStickJump()
    {
        // Logic copied from UpperBodyController.WallJump
        Debug.Log("Wall Stick Jump");
        isWallClimbing = false;
        
        // Break stick state
        // currentWallStickCooldown = wallStickCooldownDuration; // REMOVED to match UpperBodyController (it never set this)

        float dir = wallOnLeft ? 1f : -1f;
        
        currentVelocity = new Vector2(dir * wallStickJumpForce, wallStickJumpForce); 
        rb.linearVelocity = currentVelocity; 
        
        TurnCharacter(dir > 0);

        currentlyJumping = true;
        jumpButtonWasReleased = false;
        canUseJumpBuffer = false;
        canUseCoyote = false;
    }

    void CheckWallExit()
    {
        if (!isWallClimbing) return;
        
        if (!wallOnLeft && !wallOnRight)
        {
             Debug.Log("Raycast Exit: Wall Lost");
             isWallClimbing = false;
        }
    }

    // Grapple Support
    [Header("Grapple Settings")]
    public bool disableGravity; // Renamed from isGrappling

    // Removed SetGrapplingState method to simplify as requested. 
    // User can toggle disableGravity directly.

    void OnDrawGizmos()
    {
        if (cc == null)
            return;

        Vector2 topPoint = new Vector2(transform.position.x, cc.bounds.max.y - 0.1f);
        Vector2 middlePoint = transform.position;
        Vector2 bottomPoint = new Vector2(transform.position.x, cc.bounds.min.y + 0.1f);

        Gizmos.color = wallOnLeft ? Color.green : Color.red;
        Gizmos.DrawRay(topPoint, Vector2.left * WallCheckDistance);
        Gizmos.DrawRay(middlePoint, Vector2.left * WallCheckDistance);
        Gizmos.DrawRay(bottomPoint, Vector2.left * WallCheckDistance);

        Gizmos.color = wallOnRight ? Color.green : Color.red;
        Gizmos.DrawRay(topPoint, Vector2.right * WallCheckDistance);
        Gizmos.DrawRay(middlePoint, Vector2.right * WallCheckDistance);
        Gizmos.DrawRay(bottomPoint, Vector2.right * WallCheckDistance);

    }






}