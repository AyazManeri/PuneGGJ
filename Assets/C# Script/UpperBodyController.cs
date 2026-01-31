using UnityEngine;
using System.Collections.Generic;

public class UpperBodyController : MonoBehaviour
{
    [Header("Grappling Settings")]
    public Camera mainCamera;
    public LineRenderer lineRenderer;
    public LayerMask grappleMask;
    
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
            
            // Raycast is Infinite so you can hit far objects
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, Mathf.Infinity, grappleMask);

            if (hit.collider != null)
            {
                isGrappling = true;
                ropePoints.Clear();
                ropePoints.Add(new RopePoint(hit.point, false)); 
                
                float actualDistance = Vector2.Distance(transform.position, hit.point);

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

        Vector2 playerPos = transform.position;
        Vector2 lastAnchor = ropePoints[ropePoints.Count - 1].position;
        
        RaycastHit2D hit = Physics2D.Linecast(playerPos, lastAnchor, grappleMask);

        if (hit.collider != null && Vector2.Distance(hit.point, lastAnchor) > 0.1f)
        {
            Vector2 newPointPos = hit.point + (hit.normal * wrapOffset);

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
        Vector2 direction = (anchor - (Vector2)transform.position).normalized;
        float currentDistance = Vector2.Distance(transform.position, anchor);

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
        lineRenderer.SetPosition(ropePoints.Count, transform.position);
    }
}