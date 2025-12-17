using UnityEngine;
using Unity.Netcode;

public class ObjectSpawner : NetworkBehaviour
{
    public GameObject prefabToSpawn; // Kaya Prefabý
    public float spawnInterval = 3f; // Kaç saniyede bir?
    public float spawnForce = 5f;    // Ýlk çýkýþ hýzý (Ýleri fýrlatmak için)

    private float timer;

    void Update()
    {
        if (!IsServer) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            SpawnObject();
            timer = spawnInterval;
        }
    }

    void SpawnObject()
    {
        // Spawner'ýn olduðu yerde yarat
        GameObject obj = Instantiate(prefabToSpawn, transform.position, transform.rotation);

        // Network'te spawnla
        obj.GetComponent<NetworkObject>().Spawn();

        // Hafifçe ileri it ki rampadan aþaðý baþlasýn
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(transform.forward * spawnForce, ForceMode.Impulse);
        }
    }
}