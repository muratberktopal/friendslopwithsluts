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
            if (other.CompareTag("Player"))
            {
                Debug.Log($"Oyuncu (ID: {netObj.OwnerClientId}) düþtü. Iþýnlama emri gönderiliyor...");

                // --- HEDEF NOKTAYI SEÇ ---
                Vector3 targetPos = new Vector3(0, 5, 0); // Yedek nokta
                Quaternion targetRot = Quaternion.identity;

                GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
                if (spawnPoints.Length > 0)
                {
                    int randomIndex = Random.Range(0, spawnPoints.Length);
                    targetPos = spawnPoints[randomIndex].transform.position;
                    targetRot = spawnPoints[randomIndex].transform.rotation;
                }

                // --- KRÝTÝK KISIM: CLIENT'A EMÝR VER ---
                // Server direkt taþýyamaz, "Sen kendini taþý" demesi lazým.
                // Bu RPC mesajýný SADECE düþen oyuncuya yolluyoruz.
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { netObj.OwnerClientId }
                    }
                };

                TeleportPlayerClientRpc(targetPos, targetRot, clientRpcParams);
            }
            else
            {
                // Eþya ise direkt yok et (Server yetkilidir)
                netObj.Despawn();
            }
        }
    }

    // Bu fonksiyon SADECE düþen oyuncunun bilgisayarýnda çalýþýr
    [ClientRpc]
    private void TeleportPlayerClientRpc(Vector3 newPos, Quaternion newRot, ClientRpcParams clientRpcParams = default)
    {
        // Kendi lokal oyuncumuzu buluyoruz
        var localPlayer = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();

        if (localPlayer != null)
        {
            // 1. Fiziði Sýfýrla (Hýzýný kes)
            Rigidbody rb = localPlayer.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero; // Unity 6 (Eski sürümse: rb.velocity)
                rb.angularVelocity = Vector3.zero;
            }

            // 2. Pozisyonu Güncelle (Yetki bizde olduðu için bunu yapabiliriz)
            localPlayer.transform.position = newPos;
            localPlayer.transform.rotation = newRot;

            Debug.Log("VoidManager emriyle güvenli bölgeye ýþýnlandým.");
        }
    }
}