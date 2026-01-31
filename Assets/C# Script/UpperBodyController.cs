using UnityEngine;
using System.Collections.Generic;

public class UpperBodyController : MonoBehaviour
{
    [Header("Grappling Settings")]
    public Camera mainCamera;
    public LineRenderer lineRenderer;
    public LayerMask grappleMask;
    
    [Header("Physics Settings")]
    public float climbSpeed = 5f;     // How fast you retract/extend
    public float swingForce = 40f;    // Force pulling you towards the anchor
    public float damper = 10f;        // Resistance to stop the "springy" bouncing
    public float wrapOffset = 0.1f;   // Offset to prevent rope clipping into corners

    private Rigidbody2D rb;
    private List<RopePoint> ropePoints = new List<RopePoint>();
    private float totalRopeLength;
    private bool isGrappling;

    private struct RopePoint
    {
        public Vector2 position;
        public bool isClockwise;
        
        public RopePoint(Vector2 pos, bool cw) { position = pos; isClockwise = cw; }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        lineRenderer.positionCount = 0;
    }

    void Update()
    {
        HandleInput();
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
    }

    void HandleInput()
    {
        // 1. Fire Grapple (Left Click)
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = (mousePos - (Vector2)transform.position).normalized;
            
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, Mathf.Infinity, grappleMask);

            if (hit.collider != null)
            {
                isGrappling = true;
                ropePoints.Clear();
                ropePoints.Add(new RopePoint(hit.point, false)); 
                totalRopeLength = Vector2.Distance(transform.position, hit.point);
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
                totalRopeLength = Mathf.Max(totalRopeLength, 1f); // Min length 1 unit
            }
        }
    }

    void HandleWrapping()
    {
        if (ropePoints.Count == 0) return;

        Vector2 playerPos = transform.position;
        Vector2 lastAnchor = ropePoints[ropePoints.Count - 1].position;
        
        // Raycast to check if line of sight is blocked
        RaycastHit2D hit = Physics2D.Linecast(playerPos, lastAnchor, grappleMask);

        // If we hit an obstacle that isn't the current anchor point
        if (hit.collider != null && Vector2.Distance(hit.point, lastAnchor) > 0.1f)
        {
            // Offset the point slightly to avoid sticking inside geometry
            Vector2 newPointPos = hit.point + (hit.normal * wrapOffset);

            // Calculate Winding Direction (Clockwise or Anti-Clockwise)
            Vector2 previousSegment = lastAnchor - playerPos; 
            if (ropePoints.Count > 1)
            {
                previousSegment = lastAnchor - ropePoints[ropePoints.Count - 2].position;
            }
            
            Vector2 currentSegment = newPointPos - lastAnchor;
            float cross = (previousSegment.x * currentSegment.y) - (previousSegment.y * currentSegment.x);
            bool isCW = cross < 0;

            ropePoints.Add(new RopePoint(newPointPos, isCW));
        }
    }

    void HandleUnwrapping()
    {
        if (ropePoints.Count <= 1) return;

        Vector2 playerPos = transform.position;
        Vector2 currentAnchor = ropePoints[ropePoints.Count - 1].position;
        Vector2 previousAnchor = ropePoints[ropePoints.Count - 2].position;

        // Check if we have swung back past the wrapping angle
        Vector2 segmentVector = currentAnchor - previousAnchor;
        Vector2 playerVector = playerPos - currentAnchor;
        
        float cross = (segmentVector.x * playerVector.y) - (segmentVector.y * playerVector.x);
        bool currentCW = cross < 0;

        // If winding direction flipped compared to when we wrapped, remove the point
        if (currentCW != ropePoints[ropePoints.Count - 1].isClockwise)
        {
            ropePoints.RemoveAt(ropePoints.Count - 1);
        }
    }

    void ApplyRopePhysics()
    {
        Vector2 anchor = ropePoints[ropePoints.Count - 1].position;
        Vector2 direction = (anchor - (Vector2)transform.position).normalized;
        float currentDistance = Vector2.Distance(transform.position, anchor);

        // Calculate active rope length (Total - wrapped segments)
        float staticLength = 0;
        for(int i=0; i < ropePoints.Count - 1; i++)
        {
            staticLength += Vector2.Distance(ropePoints[i].position, ropePoints[i+1].position);
        }
        float availableLength = totalRopeLength - staticLength;

        // Apply forces only if rope is fully stretched
        if (currentDistance > availableLength)
        {
            // A. Spring Force (Hooke's Law): Pulls you up
            float stretch = currentDistance - availableLength;
            float springForceMagnitude = stretch * swingForce;

            // B. Damper Force: Resists velocity ALONG the rope to stop bouncing
            // Note: Use 'rb.linearVelocity' instead of 'velocity' if using Unity 6+
            float velocityAlongRope = Vector2.Dot(rb.linearVelocity, direction);
            float damperForceMagnitude = velocityAlongRope * damper;

            // Combine forces
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
        lineRenderer.SetPosition(ropePoints.Count, transform.position);
    }
}