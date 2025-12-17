using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class FallingPlatform : NetworkBehaviour
{
    public float fallDelay = 1.0f;     // Kaç saniye sonra düþsün?
    public float respawnDelay = 5.0f;  // Kaç saniye sonra geri gelsin?

    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;
    private bool isFalling = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
        // Baþlangýçta havada asýlý dursun
        rb.isKinematic = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (isFalling) return;

        // Sadece oyuncu deðerse düþür
        if (collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(FallRoutine());
        }
    }

    IEnumerator FallRoutine()
    {
        isFalling = true;

        // Titreme efekti (Opsiyonel ClientRpc ile yapýlabilir)
        yield return new WaitForSeconds(fallDelay);

        // DÜÞÜÞ BAÞLASIN!
        rb.isKinematic = false; // Fizik açýlýr, yerçekimi onu aþaðý çeker
        rb.useGravity = true;

        // Bir süre bekle sonra resetle
        yield return new WaitForSeconds(respawnDelay);
        ResetPlatform();
    }

    void ResetPlatform()
    {
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero; // Unity 6 (Eski: velocity)
        rb.angularVelocity = Vector3.zero;
        transform.position = startPos;
        transform.rotation = startRot;
        isFalling = false;
    }
}