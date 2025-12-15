using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Ayarlar")]
    public float moveSpeed = 5f;
    public float throwForce = 15f; // Fýrlatma gücünü biraz artýrdým

    [Header("Baðlantýlar")]
    public Transform cameraTransform;
    public Transform handPosition;

    // Oyuncu kendi karakterini kontrol ediyorsa Update çalýþsýn
    void Update()
    {
        if (!IsOwner) return;

        Move();
        HandleInteraction();
    }

    void Move()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // W ileri, S geri, A sol, D sað
        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * moveSpeed * Time.deltaTime;

        // Mouse ile dönüþ
        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseX * 2f);
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPickup();
        }

        if (Input.GetMouseButtonDown(0))
        {
            ThrowObjectServerRpc();
        }
    }

    void TryPickup()
    {
        if (handPosition.childCount > 0) return; // Elim doluysa alma

        RaycastHit hit;
        // Kameradan ileriye ýþýn at (3 metre)
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, 3f))
        {
            if (hit.transform.TryGetComponent(out NetworkObject netObj))
            {
                RequestPickupServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    // ---------------------------------------------------------
    // SERVER TARAFI (Emir Merkezi)
    // ---------------------------------------------------------
    [ServerRpc]
    void RequestPickupServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            // 1. Önce Parenting yap (Bu otomatik senkronize olur)
            netObj.TrySetParent(handPosition);

            // 2. HERKESE HABER VER: "Bu objenin fiziðini kapatýn!"
            TogglePhysicsClientRpc(objectId, false);
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
                // 1. Ebeveynliði kaldýr
                netObj.TryRemoveParent();

                // 2. HERKESE HABER VER: "Bu objenin fiziðini geri açýn!"
                TogglePhysicsClientRpc(netObj.NetworkObjectId, true);

                // 3. Fýrlatma kuvvetini Server uygular
                var rb = netObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
                }
            }
        }
    }

    // ---------------------------------------------------------
    // CLIENT TARAFI (Senin ve Arkadaþýnýn Ekraný)
    // ---------------------------------------------------------
    [ClientRpc]
    void TogglePhysicsClientRpc(ulong objectId, bool isPhysicsOn)
    {
        // ID'den objeyi bul
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            // --- 1. Rigidbody (Aðýrlýk) Ayarý ---
            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = !isPhysicsOn; // Fizik açýksa Kinematic kapalý olmalý
                rb.useGravity = isPhysicsOn;   // Fizik açýksa Yerçekimi açýk olmalý
            }

            // --- 2. Collider (Katýlýk) Ayarý ---
            // Tüm alt parçalardaki colliderlarý bul
            var colliders = netObj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                col.enabled = isPhysicsOn; // True ise aç, False ise kapat
            }

            // --- 3. Pozisyonu Elle Sabitle (Sadece alýrken) ---
            if (!isPhysicsOn)
            {
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;
            }
        }
    }
}