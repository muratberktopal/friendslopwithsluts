using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class FallingPlatform : NetworkBehaviour
{
    [Header("Zamanlama Ayarlarý")]
    public float fallDelay = 1.0f;      // Temastan kaç saniye sonra düþsün?
    public float disappearDelay = 2.0f; // Düþtükten kaç saniye sonra GÖRÜNMEZ olsun?
    public float respawnDelay = 5.0f;   // Görünmez olduktan kaç saniye sonra GERÝ GELSÝN?

    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;
    private Collider col;
    private MeshRenderer meshRenderer;
    private bool isFalling = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>(); // BoxCollider vs.
        meshRenderer = GetComponent<MeshRenderer>();

        startPos = transform.position;
        startRot = transform.rotation;

        // Baþlangýç ayarlarý
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (isFalling) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(FallRoutine());
        }
    }

    IEnumerator FallRoutine()
    {
        isFalling = true;

        // 1. ADIM: Titreme süresi (Oyuncu kaçsýn diye süre)
        // Burada istersen bir "Sallanma" animasyonu ClientRpc ile çaðrýlabilir.
        yield return new WaitForSeconds(fallDelay);

        // 2. ADIM: DÜÞÜÞ (Fizik Açýlýr)
        rb.isKinematic = false;
        rb.useGravity = true;

        // 3. ADIM: GÖRÜNMEZLÝK (Biraz düþtükten sonra yok olsun)
        yield return new WaitForSeconds(disappearDelay);

        // Fiziði durdur, görünmez yap, çarpýþmayý kapat
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero; // Unity 6 (Eski: velocity)

        ToggleVisibilityClientRpc(false); // Herkeste görünmez yap

        // 4. ADIM: RESPAWN BEKLEMESÝ (Geri gelmeden önceki bekleme)
        yield return new WaitForSeconds(respawnDelay);

        // 5. ADIM: RESET (Baþlangýca dön)
        ResetPlatform();
    }

    void ResetPlatform()
    {
        // Pozisyonu eski haline getir
        transform.position = startPos;
        transform.rotation = startRot;

        // Görünür yap ve çarpýþmayý aç
        ToggleVisibilityClientRpc(true);

        isFalling = false;
    }

    // Server emriyle tüm oyuncularda görüntüyü aç/kapa
    [ClientRpc]
    void ToggleVisibilityClientRpc(bool isVisible)
    {
        if (meshRenderer != null) meshRenderer.enabled = isVisible;
        if (col != null) col.enabled = isVisible;
    }
}