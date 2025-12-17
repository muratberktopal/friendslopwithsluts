using UnityEngine;
using Unity.Netcode;

public class BalloonLogic : NetworkBehaviour
{
    [Header("Balon Ayarlarý")]
    public float liftForce = 15f; // Kaldýrma kuvveti
    public float lifeTime = 10f;  // Balon ne kadar süre sonra patlar?

    private Rigidbody targetRb;

    public override void OnNetworkSpawn()
    {
        // 10 saniye sonra balonu patlat (yok et)
        if (IsServer)
        {
            Invoke(nameof(DestroyBalloon), lifeTime);
        }
    }

    // Balon bir þeye yapýþtýðýnda bu fonksiyon çaðrýlacak
    public void AttachTo(Rigidbody rb)
    {
        targetRb = rb;
    }

    void FixedUpdate()
    {
        // Sadece Server fiziði yönetir (veya Owner authoritative ise o)
        // Karýþýklýk olmamasý için fizik gücünü Server uygulasýn.
        if (!IsServer || targetRb == null) return;

        // Yerçekiminin tersine sürekli güç uygula (Acceleration = Kütleden baðýmsýz hýzlanma)
        // Böylece aðýr oyuncu da hafif kutu da uçar.
        targetRb.AddForce(Vector3.up * liftForce, ForceMode.Acceleration);
    }

    void DestroyBalloon()
    {
        if (GetComponent<NetworkObject>().IsSpawned)
        {
            GetComponent<NetworkObject>().Despawn();
        }
    }
}