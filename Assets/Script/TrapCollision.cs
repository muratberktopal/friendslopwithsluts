using UnityEngine;
using Unity.Netcode;

public class TrapCollision : NetworkBehaviour
{
    [Header("Tokat Ayarlarý")]
    public float knockbackForce = 100f; // Vuruþ gücünü artýrdým (Daha sert vursun)
    public float liftForce = 25f;       // Havaya kaldýrma gücünü artýrdým

    // Sadece Server çarpýþmayý yönetir
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();

        if (player != null)
        {
            // --- VURUÞ YÖNÜ HESABI (GÜNCELLENDÝ) ---

            // YÖNTEM: Merkezden Dýþarý Ýtme (En Garantisi)
            // Tuzaðýn merkezinden oyuncuya doðru bir çizgi çekiyoruz.
            // Bu sayede tuzak oyuncuya neresinden çarparsa çarpsýn, oyuncu merkezden uzaða fýrlar.
            Vector3 pushDir = (player.transform.position - transform.position).normalized;

            // Oyuncuyu yere çakmamasý için yönün Y eksenini sýfýrlýyoruz (Sadece yatay yön)
            pushDir.y = 0;
            pushDir = pushDir.normalized;

            // --- KUVVETÝ OLUÞTUR ---
            // Yatayda 100 birim, Dikeyde 25 birim güç uygula.
            // Vector3.up * liftForce eklemek çok önemli, yoksa yer sürtünmesi oyuncuyu durdurur.
            Vector3 finalForce = (pushDir * knockbackForce) + (Vector3.up * liftForce);

            // Oyuncunun üzerindeki Ragdoll + Fýrlatma fonksiyonunu tetikle
            // PlayerController'daki GetHitClientRpc, GetPushedClientRpc'ye yönlendiriyor.
            player.GetHitClientRpc(finalForce);

            Debug.Log("GÜM! Oyuncu fýrlatýldý.");
        }
    }
}