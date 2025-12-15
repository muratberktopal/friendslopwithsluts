using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

public class PlayerController : NetworkBehaviour
{
    public float moveSpeed = 5f;
    public float throwForce = 15f;
    public Transform cameraTransform;
    public Transform handPosition;

    void Update()
    {
        if (!IsOwner) return;

        // Hareket
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * moveSpeed * Time.deltaTime;

        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseX * 2f);

        // Etkileþim
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();
        if (Input.GetMouseButtonDown(0)) ThrowObjectServerRpc();
    }

    void TryPickup()
    {
        if (handPosition.childCount > 0) return;

        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, 3f))
        {
            if (hit.transform.TryGetComponent(out NetworkObject netObj))
            {
                RequestPickupServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    [ServerRpc]
    void RequestPickupServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            // 1. NetworkTransform'u SUSTUR
            var netTransform = netObj.GetComponent<NetworkTransform>();
            if (netTransform != null) netTransform.enabled = false;

            // 2. Fiziði Kapat (Rigidbody)
            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; }

            // 3. ÇARPIÞMAYI KAPAT (YENÝ EKLENEN KISIM - SENÝ KURTARACAK OLAN BU)
            // Küpün içindeki tüm colliderlarý bul ve kapat
            var colliders = netObj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            // 4. Parent Yap
            netObj.TrySetParent(handPosition);

            // 5. Clientlara Haber Ver
            ForceSnapToHandClientRpc(objectId);
        }
    }

    [ClientRpc]
    void ForceSnapToHandClientRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            var netTransform = netObj.GetComponent<NetworkTransform>();
            if (netTransform != null) netTransform.enabled = false;

            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; }

            // CLIENT TARAFINDA DA COLLIDER KAPAT (Çok Önemli)
            var colliders = netObj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = false;
            }

            netObj.transform.localPosition = Vector3.zero;
            netObj.transform.localRotation = Quaternion.identity;
        }
    }

    [ServerRpc]
    void ThrowObjectServerRpc()
    {
        if (handPosition.childCount > 0)
        {
            NetworkObject netObj = handPosition.GetChild(0).GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.TryRemoveParent();

                // NetworkTransform geri aç
                var netTransform = netObj.GetComponent<NetworkTransform>();
                if (netTransform != null) netTransform.enabled = true;

                // Fiziði aç
                var rb = netObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
                }

                // ÇARPIÞMAYI GERÝ AÇ (YENÝ EKLENEN KISIM)
                var colliders = netObj.GetComponentsInChildren<Collider>();
                foreach (var col in colliders)
                {
                    col.enabled = true;
                }
            }
        }
    }
}