using UnityEngine;
using Unity.Netcode;

public class VoidManager : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 1. Çarpýþmayý SADECE SERVER algýlar
        if (!IsServer) return;

        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();

        if (netObj != null)
        {
            // --- OYUNCU ÝSE IÞINLA ---
            if (other.CompareTag("Player"))
            {
                Debug.Log($"Oyuncu (ID: {netObj.OwnerClientId}) düþtü. Iþýnlama emri gönderiliyor...");

                Vector3 targetPos = new Vector3(0, 5, 0);
                Quaternion targetRot = Quaternion.identity;

                GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
                if (spawnPoints.Length > 0)
                {
                    int randomIndex = Random.Range(0, spawnPoints.Length);
                    targetPos = spawnPoints[randomIndex].transform.position;
                    targetRot = spawnPoints[randomIndex].transform.rotation;
                }

                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { netObj.OwnerClientId }
                    }
                };

                TeleportPlayerClientRpc(targetPos, targetRot, clientRpcParams);
            }
            // --- EÞYA VEYA PLATFORM ÝSE ---
            else
            {
                // DÜZELTME BURADA:

                // 1. Eðer düþen þey bir "Düþen Platform" ise DOKUNMA!
                // Çünkü FallingPlatform.cs scripti onu yukarý geri ýþýnlayacak.
                // Eðer burada yok edersek oyun bozulur.
                if (netObj.GetComponent<FallingPlatform>() != null)
                {
                    return; // Ýþlem yapma, býrak düþsün.
                }

                // 2. Sahne Objesi Kontrolü (Aldýðýn hatayý çözer)
                // Editörde elle koyduðun objeleri tamamen yok edemezsin.
                if (netObj.IsSceneObject != null && netObj.IsSceneObject.Value == true)
                {
                    // Sadece networkten düþür (Destroy etme)
                    netObj.Despawn(false);
                }
                else
                {
                    // Balon, Mermi gibi Prefab'dan doðan þeyleri tamamen yok et.
                    netObj.Despawn(true);
                }
            }
        }
    }

    // Bu fonksiyon SADECE düþen oyuncunun bilgisayarýnda çalýþýr
    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 newPos, Quaternion newRot, ClientRpcParams clientRpcParams = default)
    {
        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

        if (localPlayer != null)
        {
            Rigidbody rb = localPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            localPlayer.transform.position = newPos;
            localPlayer.transform.rotation = newRot;

            Debug.Log("VoidManager emriyle güvenli bölgeye ýþýnlandým.");
        }
    }
}