using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    // Inspector'dan buraya Cube Prefabýný sürüklemeyi unutma!
    public GameObject cubePrefab;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 300));

        // Eðer henüz baðlanmadýysak butonlarý göster
        if (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (GUILayout.Button("Host Baþlat"))
            {
                NetworkManager.Singleton.StartHost();
                SpawnCube(); // Host olunca küpü yarat
            }

            if (GUILayout.Button("Client Baþlat"))
            {
                NetworkManager.Singleton.StartClient();
            }
        }
        else
        {
            // Baðlandýysak durumu göster
            string status = NetworkManager.Singleton.IsHost ? "Host (Sunucu)" : "Client (Oyuncu)";
            GUILayout.Label("Durum: " + status);
        }

        GUILayout.EndArea();
    }

    void SpawnCube()
    {
        if (cubePrefab != null)
        {
            // Küpü karakterin önünde, havada yarat
            GameObject go = Instantiate(cubePrefab, new Vector3(0, 3, 2), Quaternion.identity);

            // Að üzerinde herkese göster
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();
        }
        else
        {
            Debug.LogError("Cube Prefab atanmamýþ! NetworkUI scriptine küpü sürükle.");
        }
    }
}