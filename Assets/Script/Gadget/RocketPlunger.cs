using UnityEngine;
using Unity.Netcode;

public class RocketPlunger : GadgetBase
{
    [Header("Ayarlar")]
    public float explosionForce = 20f;
    public float explosionRadius = 5f;
    public ParticleSystem explosionEffect; // Varsa efekt

    public override void OnUseStart()
    {
        // Sadece sahibi (elinde tutan) isteði gönderebilir
        if (!IsOwner) return;

        // Server'a "Patlat" sinyali gönder
        FireServerRpc();
    }

    public override void OnUseStop()
    {
        // Tek seferlik patlama olduðu için burasý boþ kalabilir
    }

    [ServerRpc]
    void FireServerRpc()
    {
        // Server'da patlamayý hesapla ve herkese bildir
        FireClientRpc();
    }

    [ClientRpc]
    void FireClientRpc()
    {
        // 1. Görsel Efekt
        if (explosionEffect != null) explosionEffect.Play();

        // 2. Etraftaki Herkesi Ýt (PlayerController'larý bul)
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
        foreach (var hit in colliders)
        {
            // Eðer bir oyuncuysa (veya hareketli kutuysa)
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Patlama merkezinden objeye doðru vektör
                rb.AddExplosionForce(explosionForce * 100, transform.position, explosionRadius, 3.0f);
            }
        }
    }
}