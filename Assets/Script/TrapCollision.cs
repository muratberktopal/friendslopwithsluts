using UnityEngine;
using Unity.Netcode;

public class TrapCollision : NetworkBehaviour
{
    [Header("Tokat Ayarlarý")]
    public float knockbackForce = 80f; // Vuruþ gücü
    public float liftForce = 15f;      // Havaya kaldýrma gücü (Þut çekme hissi)

    // Sadece Server çarpýþmayý yönetir
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();

        if (player != null)
        {
            // --- VURUÞ YÖNÜ HESABI ---

            // YÖNTEM 1: Temas Noktasýndan Ýtme (En Gerçekçi)
            // Sarkaç sana nereden deðdiyse, tam tersi yöne iter.
            Vector3 hitDir = -collision.contacts[0].normal;

            // YÖNTEM 2 (Alternatif): Merkezden Dýþarý Ýtme
            // Eðer Yöntem 1 bazen saçma yönlere atarsa bunu açabilirsin:
            // Vector3 hitDir = (collision.transform.position - transform.position).normalized;

            // Yere çakýlmasýný önlemek için Y eksenini sýfýrla
            hitDir.y = 0;
            hitDir = hitDir.normalized;

            // Kuvveti oluþtur: Geriye + Biraz Yukarý
            Vector3 finalForce = (hitDir * knockbackForce) + (Vector3.up * liftForce);

            // Oyuncuyu uçur
            player.GetHitClientRpc(finalForce);

            Debug.Log("Oyuncu sarkaca dokundu ve uçtu!");
        }
    }
}