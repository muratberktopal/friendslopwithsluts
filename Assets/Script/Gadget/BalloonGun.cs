using UnityEngine;
using Unity.Netcode;

public class BalloonGun : GadgetBase
{
    [Header("Silah Ayarlarý")]
    public GameObject balloonPrefab; // Inspector'dan Balon Prefabýný sürükle
    public float range = 20f;

    public override void OnUseStart()
    {
        if (!IsOwner) return;
        ShootServerRpc();
    }

    public override void OnUseStop() { }

    [ServerRpc]
    void ShootServerRpc()
    {
        // Server tarafýndan Raycast atýyoruz (Hile korumasý ve senkronizasyon için daha güvenli)
        RaycastHit hit;
        // Kameranýn pozisyonunu ve yönünü PlayerController üzerinden alabiliriz
        // Ama basitlik adýna silahýn namlusundan ileri sýkalým:
        if (Physics.Raycast(ownerPlayer.cameraTransform.position, ownerPlayer.cameraTransform.forward, out hit, range))
        {
            if (hit.rigidbody != null)
            {
                SpawnBalloon(hit.point, hit.transform, hit.rigidbody);
            }
        }
    }

    void SpawnBalloon(Vector3 position, Transform targetTransform, Rigidbody targetRb)
    {
        // Balonu oluþtur
        GameObject balloon = Instantiate(balloonPrefab, position, Quaternion.identity);

        // Network'te spawnla
        var netObj = balloon.GetComponent<NetworkObject>();
        netObj.Spawn();

        // Balonu vurulan objeye yapýþtýr (Parent yap)
        // WorldPositionStays = true (Olduðu yerde kalsýn)
        netObj.TrySetParent(targetTransform, true);

        // Balona "Kimi uçuracaðýný" söyle
        var logic = balloon.GetComponent<BalloonLogic>();
        if (logic != null)
        {
            logic.AttachTo(targetRb);
        }
    }
}