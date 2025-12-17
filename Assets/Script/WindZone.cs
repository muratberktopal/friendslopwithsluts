using UnityEngine;

public class WindZone : MonoBehaviour
{
    public float windStrength = 50f;
    public Vector3 windDirection = Vector3.up; // Veya transform.forward

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Rüzgar sürekli iter (Acceleration)
            rb.AddForce(windDirection.normalized * windStrength, ForceMode.Acceleration);
        }
    }
}