using UnityEngine;
using System.Collections.Generic;

public class UpperBodyController : MonoBehaviour
{
    [Header("Grappling Settings")]
    public Camera mainCamera;
    public LineRenderer lineRenderer;
    public LayerMask grappleMask;
    
    public Transform grappleOrigin; // The origin point for the rope (e.g. hand or gun nozzle)
    
    [Tooltip("The max length of the rope. If you grapple something further than this, you will be pulled in.")]
    public float maxRopeDistance = 20f; 

    [Header("Physics Settings")]
    public float climbSpeed = 5f;     
    public float swingForce = 40f;    
    public float damper = 10f;        
    public float wrapOffset = 0.1f;   

    private Rigidbody2D rb;
    private List<RopePoint> ropePoints = new List<RopePoint>();
    private float totalRopeLength;
    private bool isGrappling;
    
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
        HandleInput();
        CheckForWall(); // Constantly check for walls

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
                
                float actualDistance = Vector2.Distance(originPos, hit.point);

                // CLAMP LOGIC: If actual distance > max, set rope length to max (causes immediate pull)
                // Otherwise, just use the actual distance.
                totalRopeLength = Mathf.Min(actualDistance, maxRopeDistance);
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
                
                // Clamp length: Minimum 1.0f, Maximum is maxRopeDistance
                totalRopeLength = Mathf.Clamp(totalRopeLength, 1f, maxRopeDistance);
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

    void CheckForWall()
    {
        // Use CapsuleCollider bounds for more accurate detection (like PlayerController)
        CapsuleCollider2D cc = GetComponentInChildren<CapsuleCollider2D>();
        if (cc == null) return;

        Vector2 topPoint = new Vector2(cc.bounds.center.x, cc.bounds.max.y - 0.1f);
        Vector2 middlePoint = cc.bounds.center;
        Vector2 bottomPoint = new Vector2(cc.bounds.center.x, cc.bounds.min.y + 0.1f);

        // Check Left - Use wallMask
        RaycastHit2D leftTop = Physics2D.Raycast(topPoint, Vector2.left, wallCheckDistance, wallMask);
        RaycastHit2D leftMiddle = Physics2D.Raycast(middlePoint, Vector2.left, wallCheckDistance, wallMask);
        RaycastHit2D leftBottom = Physics2D.Raycast(bottomPoint, Vector2.left, wallCheckDistance, wallMask);
        
        wallOnLeft = leftTop.collider != null || leftMiddle.collider != null || leftBottom.collider != null;

        // Check Right - Use wallMask
        RaycastHit2D rightTop = Physics2D.Raycast(topPoint, Vector2.right, wallCheckDistance, wallMask);
        RaycastHit2D rightMiddle = Physics2D.Raycast(middlePoint, Vector2.right, wallCheckDistance, wallMask);
        RaycastHit2D rightBottom = Physics2D.Raycast(bottomPoint, Vector2.right, wallCheckDistance, wallMask);
        
        wallOnRight = rightTop.collider != null || rightMiddle.collider != null || rightBottom.collider != null;

        bool isTouchingWall = wallOnLeft || wallOnRight;

        // Event-Based Logic:
        // 1. Enter Wall Connection
        if (isTouchingWall && !wasTouchingWall)
        {
            Debug.Log("Wall Detected: Sticking");
            isWallClimbing = true;
            
            // SNAP TO WALL
            // Find the closest hit to snap validly
            RaycastHit2D closestHit = new RaycastHit2D();
            float closeDist = float.MaxValue;
            
            // Helper to check hits
            void CheckHit(RaycastHit2D h) 
            {
               if(h.collider != null && h.distance < closeDist) 
               {
                   closeDist = h.distance;
                   closestHit = h;
               }
            }

            if (wallOnLeft)
            {
                CheckHit(leftTop); CheckHit(leftMiddle); CheckHit(leftBottom);
            }
            else
            {
                CheckHit(rightTop); CheckHit(rightMiddle); CheckHit(rightBottom);
            }

            if (closestHit.collider != null)
            {
                 // We need to adjust the TRANSFORM position based on where the collider center is
                 Vector2 currentCenter = cc.bounds.center;
                 
                 float offset = cc.bounds.extents.x + 0.02f; // Half width + small buffer
                 float dir = wallOnLeft ? 1f : -1f;
                 
                 // Calculate target X based on wall hit
                 float targetX = closestHit.point.x + (dir * offset);
                 
                 // Creating snap position: Target X, but Current Y (don't move up/down)
                 Vector2 snapPos = new Vector2(targetX, currentCenter.y);
                 
                 Vector3 shift = snapPos - currentCenter;
                 
                 transform.position += shift;
                 
                 // Kill horizontal velocity immediately
                 rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }

        // 2. Stay on Wall (Optional: Check manual release?) 
        // Currently, we just keep isWallClimbing true unless something sets it false.
        
        // 3. Exit Wall Connection
        if (!isTouchingWall && wasTouchingWall)
        {
            Debug.Log("Left Wall");
            isWallClimbing = false;
        }

        // Update state for next frame
        wasTouchingWall = isTouchingWall;
        
        // Safety: If somehow we are climbing but not touching (e.g. slight gap), unstick? 
        // The Exit logic covers it, but 'isTouchingWall' generally handles it.
        // However, if we jumped (isWallClimbing set false), but are still touching, we don't want to re-enter.
        // The above "Enter" logic (isTouchingWall && !wasTouchingWall) handles that perfectly.
        // It won't fire again until we leave and return.

        // Jump off
        if (Input.GetKeyDown(KeyCode.Space) && isWallClimbing)
        {
            WallJump();
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
    }
}