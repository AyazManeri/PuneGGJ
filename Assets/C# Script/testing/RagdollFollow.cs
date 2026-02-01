using UnityEngine;

public class RagdollFollow : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Rigidbody2D targetRb; // Drag your INVISIBLE PLAYER CONTROLLER here

    [Header("Spring Settings")]
    [SerializeField] private float positionStrength = 1500f; // Force to pull towards target
    [SerializeField] private float velocityStrength = 100f;  // Force to match speed (Damping)

    [Header("Snap Settings")]
    [SerializeField] private float snapDistance = 2.0f; // Teleport if too far

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void FixedUpdate()
    {
        if (targetRb == null) return;

        // 1. Calculate Position Error (Where we are vs Where we want to be)
        Vector2 positionError = targetRb.position - rb.position;

        // 2. Teleport if we glitch out and get too far
        if (positionError.magnitude > snapDistance)
        {
            rb.position = targetRb.position;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // 3. Calculate Velocity Error (Our speed vs Target speed)
        // This is the magic part. If target stops, targetRb.velocity is 0.
        // The error becomes negative, applying braking force to the ragdoll.
        Vector2 velocityError = targetRb.linearVelocity - rb.linearVelocity;

        // 4. Calculate Final Force (PD Controller)
        // Force = (Distance * Strength) + (VelocityDiff * Damping)
        Vector2 finalForce = (positionError * positionStrength) + (velocityError * velocityStrength);

        rb.AddForce(finalForce);
    }
}