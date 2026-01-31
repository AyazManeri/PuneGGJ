using UnityEngine;

public class bodyAttachManager : MonoBehaviour
{
    [Header("Body Parts")]
    public GameObject upperBody;
    public GameObject lowerBody;
    public GameObject fullBody;
    
    [Header("VFX")]
    public GameObject attachVFX;
    
    [Header("Trigger Settings")]
    public LayerMask targetLayer;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the colliding object is on the target layer
        if (((1 << other.gameObject.layer) & targetLayer) == 0)
            return;
        // Disable upper and lower body parts
        if (upperBody != null)
            upperBody.SetActive(false);
        
        if (lowerBody != null)
            lowerBody.SetActive(false);
        
        // Enable full body and set its position to this transform's position
        if (fullBody != null)
        {
            fullBody.SetActive(true);
            fullBody.transform.position = transform.position;
        }
        
        // Spawn VFX at this transform's position
        if (attachVFX != null)
        {
            Instantiate(attachVFX, transform.position, Quaternion.identity);
        }
    }
}
