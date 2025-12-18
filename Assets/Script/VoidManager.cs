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
            // ====================================================
            // DURUM 1: DÜÞEN ÞEY OYUNCU ÝSE (CHECKPOINT'E GÝT)
            // ====================================================
            if (other.CompareTag("Player"))
            {
                Debug.Log($"Oyuncu (ID: {netObj.OwnerClientId}) düþtü.");

                Vector3 targetPos = new Vector3(0, 5, 0); // Varsayýlan güvenli nokta
                Quaternion targetRot = Quaternion.identity;

                // PlayerController'dan son checkpoint verisini çekiyoruz
                PlayerController pc = other.GetComponent<PlayerController>();

                if (pc != null)
                {
                    // Son kaydedilen noktaya git + Yere gömülmemesi için 2 birim yukarý
                    targetPos = pc.lastCheckpointPos + Vector3.up * 2f;
                }
                else
                {
                    // Yedek Plan: Eðer script yoksa "Respawn" tag'li objeye git
                    GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
                    if (spawnPoints.Length > 0)
                    {
                        targetPos = spawnPoints[0].transform.position;
                    }
                }

                // Sadece düþen oyuncuya ýþýnlanma emri gönder
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { netObj.OwnerClientId }
                    }
                };

                TeleportPlayerClientRpc(targetPos, targetRot, clientRpcParams);
            }
            // ====================================================
            // DURUM 2: EÞYA, PLATFORM VEYA TUZAK ÝSE
            // ====================================================
            else
            {
                // A) DÜÞEN PLATFORM ÝSE DOKUNMA!
                // Onun kendi scripti (FallingPlatform.cs) onu yukarý geri ýþýnlayacak.
                if (netObj.GetComponent<FallingPlatform>() != null)
                {
                    return;
                }

                // B) SAHNE OBJESÝ ÝSE (Editörle koyduðun kutular vb.)
                // Bunlarý "Destroy" edemezsin, hata verir. Sadece Network'ten düþür.
                if (netObj.IsSceneObject != null && netObj.IsSceneObject.Value == true)
                {
                    netObj.Despawn(false);
                }
                // C) SONRADAN YARATILANLAR (Mermi, Balon, Kaya vb.)
                // Bunlarý tamamen yok et.
                else
                {
                    netObj.Despawn(true);
                }
            }
        }
    }

    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 newPos, Quaternion newRot, ClientRpcParams clientRpcParams = default)
    {
        // Sadece kendi karakterini bul ve taþý
        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

        if (localPlayer != null)
        {
            Rigidbody rb = localPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Hýzý sýfýrla ki ýþýnlandýðý yerde kaymaya devam etmesin
                rb.linearVelocity = Vector3.zero; // Unity 6 (Eski sürümse: velocity)
                rb.angularVelocity = Vector3.zero;
            }

            localPlayer.transform.position = newPos;
            localPlayer.transform.rotation = newRot;

            Debug.Log("Son Checkpoint noktasýna ýþýnlandýn!");
        }
    }
}