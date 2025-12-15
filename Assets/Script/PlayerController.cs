using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Ayarlar")]
    public float moveSpeed = 5f;
    public float throwForce = 15f;

    [Header("Bağlantılar")]
    public Transform cameraTransform;
    public Transform handPosition;

    // Ragdoll / Bayılma Durumu
    private bool isRagdolled = false;
    private Rigidbody myRb;

    // Şu an elimde tuttuğum objeyi burada saklayacağım
    private Transform currentlyHeldObject;

    void Start()
    {
        myRb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (!IsOwner) return;

        // --- RAGDOLL KONTROLÜ (YENİ) ---
        // Eğer baygnsak (yerde sürünüyorsak) hareket kodları çalışmasın
        if (isRagdolled) return;

        // --- MANYETİK YAPIŞTIRMA ---
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;
        }

        // --- HAREKET ---
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * moveSpeed * Time.deltaTime;

        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseX * 2f);

        // --- ETKİLEŞİM ---
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();
        if (Input.GetMouseButtonDown(0)) ThrowObjectServerRpc();
    }

    // ========================================================================
    // YENİ EKLENEN KISIM: DAYAK YEME & RAGDOLL
    // ========================================================================

    // Düşman bu fonksiyonu tetikleyecek
    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        if (!IsOwner) return; // Sadece kendi karakterimde çalışsın
        StartCoroutine(RagdollRoutine(impactForce));
    }

    IEnumerator RagdollRoutine(Vector3 force)
    {
        isRagdolled = true; // Kontrolleri kilitle

        // 1. ELİNDEKİ EŞYAYI DÜŞÜR
        if (currentlyHeldObject != null)
        {
            DropItemServerRpc(); // Server'a "Elimdekini sal" de
        }

        // 2. FİZİKSEL OLARAK YERE YIĞIL
        // Ayakta durma kilitlerini kaldırıyoruz (Artık devrilebilir)
        myRb.constraints = RigidbodyConstraints.None;

        // Darbeyi uygula (Uçuşa geç)
        myRb.AddForce(force, ForceMode.Impulse);

        // 3. YERDE BEKLE (3 Saniye baygınlık)
        yield return new WaitForSeconds(3.0f);

        // 4. AYAĞA KALK (Toparlanma)
        // Sadece Y eksenindeki dönüşü koru, X ve Z'yi (yatıklığı) düzelt
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);

        // Yerin içine girmemesi için hafif yukarı ışınla
        transform.position += Vector3.up * 1.0f;

        // 5. TEKRAR KİLİTLE (Eski haline dön)
        myRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Hızı sıfırla (Kaymayı durdur)
        myRb.linearVelocity = Vector3.zero;
        myRb.angularVelocity = Vector3.zero;

        isRagdolled = false; // Kontrolleri geri ver
    }

    [ServerRpc]
    void DropItemServerRpc()
    {
        // Elinde bir şey varsa serbest bırak
        if (currentlyHeldObject != null || handPosition.childCount > 0)
        {
            // Değişken üzerinden veya child üzerinden bulmaya çalış
            NetworkObject netObj = null;

            if (currentlyHeldObject != null)
                netObj = currentlyHeldObject.GetComponent<NetworkObject>();

            // Eğer değişkende yoksa (senkron sorunu varsa) elin içine bak
            if (netObj == null && handPosition.childCount > 0)
                netObj = handPosition.GetChild(0).GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.TryRemoveParent();
                TogglePhysicsClientRpc(netObj.NetworkObjectId, true);
            }
        }
        // Client tarafında değişkeni temizle
        ClearHeldObjectClientRpc();
    }

    [ClientRpc]
    void ClearHeldObjectClientRpc()
    {
        if (IsOwner) currentlyHeldObject = null;
    }

    // ========================================================================
    // MEVCUT KODLAR (Pickup / Throw)
    // ========================================================================

    void TryPickup()
    {
        if (currentlyHeldObject != null) return;

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
            var netTransform = netObj.GetComponent<NetworkTransform>();
            if (netTransform != null) netTransform.enabled = false;

            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            netObj.TrySetParent(handPosition, false);
            TogglePhysicsClientRpc(objectId, false);
        }
    }

    [ServerRpc]
    void ThrowObjectServerRpc()
    {
        if (currentlyHeldObject != null)
        {
            // DropItemServerRpc mantığını kullanıyoruz ama ekstra fırlatma gücü ekleyeceğiz
            NetworkObject netObj = currentlyHeldObject.GetComponent<NetworkObject>();

            DropItemServerRpc(); // Önce bağı kopar ve fiziği aç

            // Sonra itekle
            if (netObj != null)
            {
                var rb = netObj.GetComponent<Rigidbody>();
                // Fırlatma işlemini bir sonraki fizik karesinde yapmak daha sağlıklı olabilir ama şimdilik direkt itiyoruz
                if (rb != null) rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
            }
        }
    }

    [ClientRpc]
    void TogglePhysicsClientRpc(ulong objectId, bool isPhysicsOn)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            var netTransform = netObj.GetComponent<NetworkTransform>();
            if (netTransform != null) netTransform.enabled = isPhysicsOn;

            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = !isPhysicsOn; rb.useGravity = isPhysicsOn; }

            var colliders = netObj.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = isPhysicsOn;

            if (!isPhysicsOn)
            {
                if (IsOwner) currentlyHeldObject = netObj.transform;
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                if (IsOwner) currentlyHeldObject = null;
            }
        }
    }
}