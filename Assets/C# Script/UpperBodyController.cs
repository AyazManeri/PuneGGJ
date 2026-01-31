using UnityEngine;
using System.Collections.Generic;

public class UpperBodyController : MonoBehaviour
{
    [Header("Grappling Settings")]
    public Camera mainCamera;
    public LineRenderer lineRenderer;
    public LayerMask grappleMask;
    
    public Transform grappleOrigin; // The origin point for the rope (e.g. hand or gun nozzle)
    
 
    public float initialRopeOffset = 1f; // Offset to subtract from projected vertical length 
    public float minRopeLength = 1f; // Minimum length the rope can contract to 

    [Header("Physics Settings")]
    public float climbSpeed = 5f;     
    public float swingForce = 80f;    
    public float damper = 5f;        
    public float wrapOffset = 0.1f;   

    private Rigidbody2D rb;
    private List<RopePoint> ropePoints = new List<RopePoint>();
    private float totalRopeLength;
    private bool isGrappling;

    // Gizmo Debug Variables
    private Vector2 debugLowerPoint;
    private Vector2 debugHitPoint;
    private Vector2 debugProjectedVector;
    
    [Header("Wall Climbing Settings")]
    public LayerMask wallMask; // Added separate mask for clarity
    public float wallClimbSpeed = 5f;
    public float wallCheckDistance = 0.6f;
    public float wallJumpForce = 10f;
    private bool isWallClimbing;
    private bool wallOnLeft;
    private bool wallOnRight;
    private float defaultGravity;
    private PlayerController playerController;
    private bool wasTouchingWall;
    private float wallStickCooldown;

    private struct RopePoint
    {
        public Vector2 position;
        public bool isClockwise;
        public float creationTime;
        
        public RopePoint(Vector2 pos, bool cw, float time) 
        { 
            position = pos; 
            isClockwise = cw; 
            creationTime = time; 
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerController = GetComponent<PlayerController>();
        defaultGravity = rb.gravityScale;
        lineRenderer.positionCount = 0;
        
        // Default to self if no origin assigned
        if (grappleOrigin == null) grappleOrigin = transform;

        // Validation
        if (wallMask.value == 0)
        {
            Debug.LogWarning("WallClimbing: Wall Mask is Set to Nothing! Defaulting to GrappleMask.");
            wallMask = grappleMask;
        }
        
        // Try to find collider on this object or any child
        if (GetComponentInChildren<CapsuleCollider2D>() == null)
        {
            Debug.LogError("WallClimbing: No CapsuleCollider2D found in children! Wall checks will fail.");
        }
    }

    void Update()
    {
        if (wallStickCooldown > 0)
            wallStickCooldown -= Time.deltaTime;

        HandleInput();
        CheckJumpInput(); // Replaced CheckForWall
        CheckWallExit();  // Raycast exit check

        if (isGrappling)
        {
            HandleWrapping();
            HandleUnwrapping();
            UpdateLineRenderer();
        }
    }

    void FixedUpdate()
    {
        if (isGrappling)
        {
            ApplyRopePhysics();
        }
        
        HandleWallClimbing(); // Apply climbing physics
    }

    void HandleInput()
    {
        // 1. Fire Grapple (Left Click)
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 originPos = grappleOrigin.position; // Use the grapple origin
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = (mousePos - originPos).normalized;
            
            // Raycast is Infinite so you can hit far objects
            RaycastHit2D hit = Physics2D.Raycast(originPos, direction, Mathf.Infinity, grappleMask);

            if (hit.collider != null)
            {
                isGrappling = true;
                ropePoints.Clear();
                ropePoints.Add(new RopePoint(hit.point, false, Time.time)); 
                
                // Calculate the vector from the character's lower point to the hit point (Green Line)
                CapsuleCollider2D col = GetComponentInChildren<CapsuleCollider2D>();
                Vector2 lowerPoint = transform.position;
                if (col != null)
                {
                    //lowerPoint = new Vector2(col.bounds.center.x, col.bounds.min.y);
                }
                
                Vector2 vectorToHit = hit.point - lowerPoint;

                // Project this vector onto the vertical axis (Orange Line)
                // Vector2.Project does not exist, use Vector3.Project
                Vector3 vectorToHit3D = vectorToHit;
                Vector3 projectedVerticalVector = Vector3.Project(vectorToHit3D, Vector2.up);
                float orangeLength = projectedVerticalVector.magnitude;
                
                // Visualization Data
                debugLowerPoint = lowerPoint;
                debugHitPoint = hit.point;
                debugProjectedVector = projectedVerticalVector;
                
                Debug.Log($"Grapple Hit. VectorToHit: {vectorToHit}, Vertical Projection: {orangeLength}");

                // Set rope length to this vertical length minus offset
                totalRopeLength = Mathf.Max(orangeLength - initialRopeOffset, minRopeLength);
                Debug.Log($"[Grapple] Initial Rope Length Set To: {totalRopeLength} (Orange: {orangeLength}, Offset: {initialRopeOffset})");
            }
        }

        // 2. Release Grapple
        if (Input.GetMouseButtonUp(0))
        {
            isGrappling = false;
            lineRenderer.positionCount = 0;
        }

        // 3. Adjust Rope Length (W/S or Up/Down)
        if (isGrappling)
        {
            float verticalInput = Input.GetAxis("Vertical"); 
            if (verticalInput != 0)
            {
                totalRopeLength -= verticalInput * climbSpeed * Time.deltaTime;
                
                // Clamp length: Minimum minRopeLength, No Max Limit
                totalRopeLength = Mathf.Max(totalRopeLength, minRopeLength);
            }
        }
    }

    void HandleWrapping()
    {
        if (ropePoints.Count == 0) return;

        Vector2 playerPos = grappleOrigin.position; // Check wrapping from origin
        Vector2 lastAnchor = ropePoints[ropePoints.Count - 1].position;
        
        RaycastHit2D hit = Physics2D.Linecast(playerPos, lastAnchor, grappleMask);

        if (hit.collider != null && Vector2.Distance(hit.point, lastAnchor) > 0.1f)
        {
            Vector2 newPointPos = hit.point + (hit.normal * wrapOffset);

            // Calculate winding direction based on the player's position relative to the new segment.
            // This MUST match the logic in HandleUnwrapping to ensure the point isn't immediately removed.
            
            Vector2 segmentVector = newPointPos - lastAnchor;      // The new rope segment (Anchor -> Hit)
            Vector2 playerVector = playerPos - newPointPos;        // Vector from Hit -> Player
            
            float cross = (segmentVector.x * playerVector.y) - (segmentVector.y * playerVector.x);
            bool isCW = cross < 0;

            ropePoints.Add(new RopePoint(newPointPos, isCW, Time.time));
        }
    }

    void HandleUnwrapping()
    {
        if (ropePoints.Count <= 1) return;
        
        // Prevent immediate unwrapping to stop jitter/flicker
        // A point must exist for at least 0.1s before it can be removed.
        if (Time.time < ropePoints[ropePoints.Count - 1].creationTime + 0.1f) return;

        Vector2 playerPos = grappleOrigin.position; // Check unwrapping from origin
        Vector2 currentAnchor = ropePoints[ropePoints.Count - 1].position;
        Vector2 previousAnchor = ropePoints[ropePoints.Count - 2].position;

        Vector2 segmentVector = currentAnchor - previousAnchor;
        Vector2 playerVector = playerPos - currentAnchor;
        
        float cross = (segmentVector.x * playerVector.y) - (segmentVector.y * playerVector.x);
        bool currentCW = cross < 0;

        if (currentCW != ropePoints[ropePoints.Count - 1].isClockwise)
        {
            ropePoints.RemoveAt(ropePoints.Count - 1);
        }
    }

    void ApplyRopePhysics()
    {
        Vector2 anchor = ropePoints[ropePoints.Count - 1].position;
        Vector2 originPos = grappleOrigin.position;
        
        Vector2 direction = (anchor - originPos).normalized;
        float currentDistance = Vector2.Distance(originPos, anchor);

        float staticLength = 0;
        for(int i=0; i < ropePoints.Count - 1; i++)
        {
            staticLength += Vector2.Distance(ropePoints[i].position, ropePoints[i+1].position);
        }
        float availableLength = totalRopeLength - staticLength;

        // If we are further than the allowed length (e.g. we hit something far away but clamped length)
        if (currentDistance > availableLength)
        {
            float stretch = currentDistance - availableLength;
            float springForceMagnitude = stretch * swingForce;

            // Note: Use 'rb.velocity' if on Unity 2022 or older. 'rb.linearVelocity' for Unity 6.
            float velocityAlongRope = Vector2.Dot(rb.linearVelocity, direction);
            float damperForceMagnitude = velocityAlongRope * damper;

            float totalForce = springForceMagnitude - damperForceMagnitude;

            rb.AddForce(direction * totalForce);
        }
    }

    void UpdateLineRenderer()
    {
        lineRenderer.positionCount = ropePoints.Count + 1;
        for (int i = 0; i < ropePoints.Count; i++)
        {
            lineRenderer.SetPosition(i, ropePoints[i].position);
        }
        lineRenderer.SetPosition(ropePoints.Count, grappleOrigin.position); // Connect line to origin
    }

    // Replaced CheckForWall with Collision events as requested
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check Layer
        if (((1 << collision.gameObject.layer) & wallMask) != 0)
        {
            // Check Cooldown
            if (wallStickCooldown > 0) return;

            // Determine side based on normal
            // Normal points AWAY from the wall (Right if wall is on Left)
            Vector2 normal = collision.GetContact(0).normal;
            
            if (normal.x > 0.5f) 
            {
                wallOnLeft = true;
                wallOnRight = false;
            }
            else if (normal.x < -0.5f)
            {
                wallOnLeft = false;
                wallOnRight = true;
            }
            else
            {
                // Floor/Ceiling
                return;
            }

            if (!isWallClimbing)
            {
                 Debug.Log("Wall Hit: Sticking");
                 isWallClimbing = true;
                 
                 // Snap Logic
                 CapsuleCollider2D cc = GetComponentInChildren<CapsuleCollider2D>();
                 if(cc)
                 {
                     float offset = cc.bounds.extents.x + 0.02f;
                     float dir = wallOnLeft ? 1f : -1f;
                     // Use Contact Point X as wall surface X
                     float targetX = collision.GetContact(0).point.x + (dir * offset);
                     StartCoroutine(SmoothSnapToWall(targetX, 0.1f));
                 }
            }
        }
    }

    // Raycast Exit Logic: If we are climbing, check if we are still close to the wall.
    // If rays fail, it means we moved away or ran out of wall -> Exit climbing.
    void CheckWallExit()
    {
        if (!isWallClimbing) return;
        
        CapsuleCollider2D cc = GetComponentInChildren<CapsuleCollider2D>();
        if (cc == null) return;

        Vector2 topPoint = new Vector2(cc.bounds.center.x, cc.bounds.max.y - 0.1f);
        Vector2 middlePoint = cc.bounds.center;
        Vector2 bottomPoint = new Vector2(cc.bounds.center.x, cc.bounds.min.y + 0.1f);

        bool stillOnWall = false;

        if (wallOnLeft)
        {
            RaycastHit2D leftTop = Physics2D.Raycast(topPoint, Vector2.left, wallCheckDistance, wallMask);
            RaycastHit2D leftMiddle = Physics2D.Raycast(middlePoint, Vector2.left, wallCheckDistance, wallMask);
            RaycastHit2D leftBottom = Physics2D.Raycast(bottomPoint, Vector2.left, wallCheckDistance, wallMask);
            
            if (leftTop.collider != null || leftMiddle.collider != null || leftBottom.collider != null)
                stillOnWall = true;
        }
        else if (wallOnRight)
        {
             RaycastHit2D rightTop = Physics2D.Raycast(topPoint, Vector2.right, wallCheckDistance, wallMask);
             RaycastHit2D rightMiddle = Physics2D.Raycast(middlePoint, Vector2.right, wallCheckDistance, wallMask);
             RaycastHit2D rightBottom = Physics2D.Raycast(bottomPoint, Vector2.right, wallCheckDistance, wallMask);
             
             if (rightTop.collider != null || rightMiddle.collider != null || rightBottom.collider != null)
                stillOnWall = true;
        }

        if (!stillOnWall)
        {
            Debug.Log("Raycast Exit: Wall Lost");
            isWallClimbing = false;
            wallOnLeft = false;
            wallOnRight = false;
        }
    }
    
    // Manual CheckForWall removed, checking jump input in Update instead
    void CheckJumpInput()
    {
        // Jump off
        if (Input.GetKeyDown(KeyCode.Space) && isWallClimbing)
        {
            WallJump();
        }
    }

    System.Collections.IEnumerator SmoothSnapToWall(float targetX, float duration)
    {
        float elapsed = 0f;
        float startX = transform.position.x;
        
        while (elapsed < duration)
        {
            // If we stopped climbing, abort snap
            if (!isWallClimbing) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Smooth step for more natural feel
            t = t * t * (3f - 2f * t);
            
            float newX = Mathf.Lerp(startX, targetX, t);
            transform.position = new Vector3(newX, transform.position.y, transform.position.z);
            yield return null;
        }
        
        // Final ensure
        if (isWallClimbing)
        {
            transform.position = new Vector3(targetX, transform.position.y, transform.position.z);
        }
    }

    void HandleWallClimbing()
    {
        if (isWallClimbing)
        {
            if(playerController != null && playerController.enabled)
                playerController.enabled = false;

            rb.gravityScale = 0; 
            
            float vInput = Input.GetAxis("Vertical");
            
            float targetY = vInput * wallClimbSpeed;
            
            rb.linearVelocity = new Vector2(0, targetY);
        }
        else
        {
            if(playerController != null && !playerController.enabled)
                playerController.enabled = true;
                
            rb.gravityScale = defaultGravity;
        }
    }

    void WallJump()
    {
        isWallClimbing = false;
        float dir = wallOnLeft ? 1f : -1f;
        rb.linearVelocity = new Vector2(dir * wallJumpForce, wallJumpForce); 
        // Force un-stick by not re-entering immediately next frame if user holds keys?
        // With current CheckForWall logic, if wallOnLeft is true and they press 'A', it might re-stick.
        // Usually WallJump implies moving AWAY from wall, so 'A' wouldn't be pressed if jumping Right.
    }

    void OnDrawGizmos()
    {
        // Try to get collider even in Editor mode
        CapsuleCollider2D cc = GetComponentInChildren<CapsuleCollider2D>();
        if (cc == null) return;

        Vector2 topPoint = new Vector2(cc.bounds.center.x, cc.bounds.max.y - 0.1f);
        Vector2 middlePoint = cc.bounds.center;
        Vector2 bottomPoint = new Vector2(cc.bounds.center.x, cc.bounds.min.y + 0.1f);

        // Draw Left
        Gizmos.color = wallOnLeft ? Color.green : Color.red;
        Gizmos.DrawLine(topPoint, topPoint + Vector2.left * wallCheckDistance);
        Gizmos.DrawLine(middlePoint, middlePoint + Vector2.left * wallCheckDistance);
        Gizmos.DrawLine(bottomPoint, bottomPoint + Vector2.left * wallCheckDistance);
        
        // Add solid indicators
        Gizmos.DrawSphere(topPoint + Vector2.left * wallCheckDistance, 0.05f);

        // Draw Right
        Gizmos.color = wallOnRight ? Color.green : Color.red;
        Gizmos.DrawLine(topPoint, topPoint + Vector2.right * wallCheckDistance);
        Gizmos.DrawLine(middlePoint, middlePoint + Vector2.right * wallCheckDistance);
        Gizmos.DrawLine(bottomPoint, bottomPoint + Vector2.right * wallCheckDistance);
        
        // Add solid indicators
        Gizmos.DrawSphere(topPoint + Vector2.right * wallCheckDistance, 0.05f);
        
        // Grapple Debug Gizmos
        if (debugHitPoint != Vector2.zero)
        {
            // Green Line (Hypotenuse: Lower Point -> Hit Point)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(debugLowerPoint, debugHitPoint);

            // Orange Line (Projected Vertical: Lower Point -> Lower Point + Up * magnitude)
            // Or actually, projection starts from origin of vector (Lower Point)
            Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            Gizmos.DrawLine(debugLowerPoint, debugLowerPoint + debugProjectedVector);
            
             // Draw sphere at hit
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(debugHitPoint, 0.1f);
        }
    }
}