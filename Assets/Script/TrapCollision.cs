using UnityEngine;
using Unity.Netcode;

public class TrapCollision : MonoBehaviour
{
    [Header("Tokat Ayarlarý")]
    public float knockbackForce = 50f; // Ne kadar sert vursun? (AltF4 için 50-100 arasý yap)
    public float liftForce = 10f;      // Ne kadar havaya kaldýrsýn?

    // Sadece Server çarpýþmayý yönetir (Hile olmasýn)
    private void OnCollisionEnter(Collision collision)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Çarpan þey bir Oyuncu mu?
        PlayerController player = collision.gameObject.GetComponent<PlayerController>();

        if (player != null)
        {
            // Vuruþ yönünü hesapla (Engelin merkezinden oyuncuya doðru)
            Vector3 direction = (collision.transform.position - transform.position).normalized;

            // Eðer dönen bir þeyse, çarpýþma noktasýna göre teðet kuvveti hesaplamak daha iyidir
            // Ama þimdilik basit "Merkezden Dýþarý Ýtme" yapýyoruz.

            // Kuvveti oluþtur: Ýleri + Yukarý
            Vector3 finalForce = (direction * knockbackForce) + (Vector3.up * liftForce);

            // Oyuncuyu Ragdoll yap ve fýrlat
            player.GetHitClientRpc(finalForce);

            Debug.Log("Oyuncu AltF4 usulü uçuruldu!");
        }
    }
}