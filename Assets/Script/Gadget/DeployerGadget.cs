using UnityEngine;
using Unity.Netcode;

public class DeployerGadget : GadgetBase
{
    [Header("Kurulacak Obje")]
    public GameObject objectToDeployPrefab; // Jump Pad Prefabý
    public float deployRange = 10f;

    public override void OnUseStart()
    {
        if (!IsOwner) return;

        // 1. ÝÞLEM: YER SEÇÝMÝ (CLIENT)
        Transform cam = ownerPlayer.cameraTransform;
        if (cam == null) return;

        RaycastHit hit;
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(cam.position, cam.forward, out hit, deployRange, layerMask))
        {
            // Sadece yere veya duvara kurulabilsin (Rigidbody'si olmayan statik objeler)
            // Ýstersen bu kýsýtlamayý kaldýrabilirsin.
            RequestDeployServerRpc(hit.point, hit.normal);
        }
    }

    public override void OnUseStop() { }

    [ServerRpc]
    void RequestDeployServerRpc(Vector3 position, Vector3 normal)
    {
        // 2. ÝÞLEM: ÝNÞAAT (SERVER)

        // Eðimine göre döndür (Yere paralel olsun)
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);

        // Objeyi oluþtur
        GameObject spawnedObj = Instantiate(objectToDeployPrefab, position, rotation);

        // Network'te spawnla
        spawnedObj.GetComponent<NetworkObject>().Spawn();

        // 3. ÝÞLEM: EÞYAYI TÜKET
        // Elindeki aleti yok et (Gadget'ýn kendisini)
        GetComponent<NetworkObject>().Despawn();
    }
}