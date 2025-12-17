using UnityEngine;
using Unity.Netcode;

public class VoidManager : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // Iþýnlama iþini sadece Server yapar
        if (!IsServer) return;

        // Düþen objenin ana kökünü (Root) bul
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();

        if (netObj != null)
        {
            // 1. DÜÞEN BÝR OYUNCU MU?
            if (other.CompareTag("Player"))
            {
                Debug.Log("Oyuncu düþtü! Rastgele bir noktaya ýþýnlanýyor...");

                // Önce fiziðini sýfýrla (Hýzýný kes)
                Rigidbody rb = netObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero; // Unity 6 (Eski sürümse: rb.velocity)
                    rb.angularVelocity = Vector3.zero;
                }

                // --- YENÝ: RASTGELE SPAWN NOKTASINA GÖNDER ---
                GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");

                if (spawnPoints.Length > 0)
                {
                    int randomIndex = Random.Range(0, spawnPoints.Length);
                    netObj.transform.position = spawnPoints[randomIndex].transform.position;
                    netObj.transform.rotation = spawnPoints[randomIndex].transform.rotation;
                }
                else
                {
                    // Eðer spawn noktasý bulamazsa haritanýn ortasýna at (Yedek Plan)
                    netObj.transform.position = new Vector3(0, 5, 0);
                    Debug.LogWarning("Spawn noktasý bulunamadý, merkeze ýþýnlandý!");
                }
            }
            // 2. DÜÞEN BÝR EÞYA MI?
            else
            {
                // Eþyalar sonsuzluða düþerse yok olsun (Performans ve AltF4 mantýðý)
                netObj.Despawn();
            }
        }
    }
}