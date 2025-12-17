using UnityEngine;

public class JumpPadLogic : MonoBehaviour
{
    public float jumpForce = 25f;

    private void OnTriggerEnter(Collider other)
    {
        // Üstüne basan þeyin Rigidbody'si var mý?
        Rigidbody rb = other.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Oyuncunun dikey hýzýný sýfýrla ki her seferinde ayný yüksekliðe uçsun
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

            // Fýrlat!
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

            // (Opsiyonel) Ses efekti veya animasyon buraya
            Debug.Log("BOING!");
        }
    }
}