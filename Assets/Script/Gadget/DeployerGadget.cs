using UnityEngine;
using Unity.Netcode;

public class DeployerGadget : GadgetBase
{
    [Header("Kurulacak Obje")]
    public GameObject objectToDeployPrefab; // Jump Pad Prefabý buraya
    public float deployRange = 5f;

    public override void OnUseStart()
    {
        if (!IsOwner) return;
        DeployServerRpc();
    }

    public override void OnUseStop() { }

    [ServerRpc]
    void DeployServerRpc()
    {
        RaycastHit hit;
        // Yere doðru bakýyor mu?
        if (Physics.Raycast(ownerPlayer.cameraTransform.position, ownerPlayer.cameraTransform.forward, out hit, deployRange))
        {
            // Kurulacak yerin açýsýna göre rotasyon hesapla (Yere paralel olsun)
            Quaternion slopeRotation = Quaternion.FromToRotation(Vector3.up, hit.normal);

            // Objeyi oluþtur
            GameObject spawnedObj = Instantiate(objectToDeployPrefab, hit.point, slopeRotation);

            // Network'te herkes görsün
            spawnedObj.GetComponent<NetworkObject>().Spawn();

            // EÞYAYI TÜKET (Elindeki aleti yok et)
            // Bu scriptin baðlý olduðu objeyi (Gadget'ý) yok ediyoruz.
            GetComponent<NetworkObject>().Despawn();
        }
    }
}