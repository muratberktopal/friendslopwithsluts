using UnityEngine;
using Unity.Netcode;

public class BalloonGun : GadgetBase
{
    [Header("Silah Ayarlarý")]
    public GameObject balloonPrefab;
    public float range = 50f;

    public override void OnUseStart()
    {
        if (!IsOwner) return;

        // 1. ÝÞLEM: NÝÞAN ALMA (CLIENT)
        // Kamerayý PlayerController'dan al
        Transform cam = ownerPlayer.cameraTransform;
        if (cam == null) return;

        RaycastHit hit;
        // Oyuncu katmaný hariç her þeye niþan al
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(cam.position, cam.forward, out hit, range, layerMask))
        {
            // Eðer vurduðumuz þeyin bir Rigidbody'si ve NetworkObject'i varsa (Oyuncu veya Kutu)
            if (hit.rigidbody != null && hit.transform.TryGetComponent(out NetworkObject targetNetObj))
            {
                // Server'a "Bu ID'li objeye balon yapýþtýr" de
                SpawnBalloonServerRpc(targetNetObj.NetworkObjectId, hit.point);
            }
        }
    }

    public override void OnUseStop() { }

    [ServerRpc]
    void SpawnBalloonServerRpc(ulong targetId, Vector3 hitPoint)
    {
        // 2. ÝÞLEM: BALON OLUÞTURMA (SERVER)
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObj))
        {
            // Balonu oluþtur
            GameObject balloon = Instantiate(balloonPrefab, hitPoint, Quaternion.identity);

            // Network'te herkese göster
            var balloonNetObj = balloon.GetComponent<NetworkObject>();
            balloonNetObj.Spawn();

            // Balonu hedefe yapýþtýr (Parent)
            balloonNetObj.TrySetParent(targetObj.transform, true);

            // Balon mantýðýný çalýþtýr (Uçurma kuvveti)
            var logic = balloon.GetComponent<BalloonLogic>();
            if (logic != null)
            {
                logic.AttachTo(targetObj.GetComponent<Rigidbody>());
            }
        }
    }
}