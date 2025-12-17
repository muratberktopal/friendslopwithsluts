using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    public float baseMoveSpeed = 5f;
    private float currentMoveSpeed;
    public float runMultiplier = 1.5f;
    public float crouchMultiplier = 0.5f;
    public float rotationSpeed = 10f; // Dönüş yumuşaklığı

    [Header("Zıplama Ayarları (Eski Koddan)")]
    public float jumpForce = 800f; // Çalışan koddaki değer!
    public float rayLength = 1.3f; // Çalışan koddaki zemin kontrol mesafesi

    [Header("İtme / Etkileşim")]
    public float pushForce = 15f;
    public float pushRange = 3.0f;
    public float pushUpwardModifier = 3f;
    public float throwForce = 15f;

    [Header("Bağlantılar")]
    public Transform handPosition; // Eşya tutma noktası

    // Private Değişkenler
    private Transform mainCameraTransform; // Sahnedeki kamera
    private Rigidbody myRb;
    private Transform currentlyHeldObject;
    private GadgetBase currentGadget;
    private bool isRagdolled = false;
    private CapsuleCollider myCollider;

    // Ses ve Gizlilik (EnemyAI için)
    public float currentNoiseRange = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // 1. Sahnedeki Kamerayı Bul
            if (Camera.main != null)
            {
                mainCameraTransform = Camera.main.transform;
                var camScript = Camera.main.GetComponent<ThirdPersonCamera>();

                // 2. Kameraya "Beni takip et" de
                if (camScript != null)
                {
                    camScript.target = this.transform;
                }
            }

            // Mouse'u kilitle
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Start()
    {
        myRb = GetComponent<Rigidbody>();
        myCollider = GetComponent<CapsuleCollider>();
        currentMoveSpeed = baseMoveSpeed;

        // Karakter fiziksel olarak devrilmesin
        myRb.freezeRotation = true;
    }

    void Update()
    {
        // Eşya pozisyonunu güncelle
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;

            // Gadget ise özel offset uygula
            if (currentGadget != null)
            {
                currentlyHeldObject.localPosition = currentGadget.holdPositionOffset;
                currentlyHeldObject.localRotation = Quaternion.Euler(currentGadget.holdRotationOffset);
            }
        }

        if (!IsOwner) return;
        if (isRagdolled) return;

        HandleMovement();
        HandleInteraction();

        // Gadget Kullanımı (Sağ Tık)
        if (currentGadget != null)
        {
            if (Input.GetMouseButtonDown(1)) currentGadget.OnUseStart();
            if (Input.GetMouseButtonUp(1)) currentGadget.OnUseStop();
        }
    }

    void HandleMovement()
    {
        // --- 1. ZEMİN KONTROLÜ (Çalışan Koddan Aynen Alındı) ---
        // Sadece kendi layerımız hariç her şeye çarpsın diyebiliriz ama
        // çalışan kodunda düz raycast vardı, aynısını yapıyorum.
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, rayLength);

        // Debug için sahne penceresinde çizgiyi görelim
        Debug.DrawRay(transform.position, Vector3.down * rayLength, isGrounded ? Color.green : Color.red);

        // --- 2. HAREKET YÖNÜ (TPS KAMERA SİSTEMİ) ---
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Kameranın baktığı yönü al
        Vector3 camForward = mainCameraTransform.forward;
        Vector3 camRight = mainCameraTransform.right;

        // Y eksenini sıfırla (Kafamızı yukarı kaldırınca havaya uçmayalım)
        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        // Gideceğimiz yönü hesapla
        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        // Hız Ayarları
        float targetSpeed = baseMoveSpeed;
        if (Input.GetKey(KeyCode.LeftControl)) targetSpeed *= crouchMultiplier;
        else if (Input.GetKey(KeyCode.LeftShift)) targetSpeed *= runMultiplier;

        // Ağırlık kontrolü (ItemWeight varsa)
        if (currentlyHeldObject != null)
        {
            var w = currentlyHeldObject.GetComponent<ItemWeight>();
            if (w != null) targetSpeed *= (1.0f - w.slowdownPercentage);
        }

        // --- 3. HAREKET UYGULAMA ---
        if (moveDir.magnitude >= 0.1f)
        {
            // A) Karakteri gittiği yöne döndür
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            // B) Pozisyonu değiştir (Çalışan kodun hareket mantığı: Transform)
            // Rigidbody.velocity yerine transform kullanıyorum çünkü çalışan kodun böyleydi.
            transform.position += moveDir * targetSpeed * Time.deltaTime;

            // Ses (AI için)
            currentNoiseRange = Input.GetKey(KeyCode.LeftShift) ? 20f : 10f;
        }
        else
        {
            currentNoiseRange = 0f;
        }

        // --- 4. ZIPLAMA (Çalışan Koddan Aynen Alındı) ---
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            // Eski kodunda 800 Impulse gücü vardı, aynısını koruyoruz.
            myRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            currentNoiseRange = 30f;
        }
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();

        if (Input.GetMouseButtonDown(0))
        {
            if (currentlyHeldObject != null)
                ThrowObjectServerRpc();
            else
                TryPushPlayer();
        }
    }

    // --- EŞYA SİSTEMİ (TPS Uyumlu Raycast) ---
    void TryPickup()
    {
        if (currentlyHeldObject != null) return;

        // Göğüs hizasından (1.0f yukarıdan) karakterin baktığı yöne ışın at
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;

        if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, 2.5f))
        {
            if (hit.transform.TryGetComponent(out NetworkObject netObj))
            {
                RequestPickupServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    // --- RPC FONKSİYONLARI (Aynen Korundu) ---

    [ServerRpc]
    void RequestPickupServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            netObj.TrySetParent(handPosition, false);
            TogglePhysicsClientRpc(objectId, false);
        }
    }

    [ServerRpc]
    void ThrowObjectServerRpc()
    {
        if (currentlyHeldObject != null)
        {
            Rigidbody rb = currentlyHeldObject.GetComponent<Rigidbody>();
            NetworkObject netObj = currentlyHeldObject.GetComponent<NetworkObject>();

            PerformDropLogic();

            if (rb != null)
            {
                Vector3 throwDir = (transform.forward + Vector3.up * 0.3f).normalized;
                rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
            }
        }
    }

    void PerformDropLogic()
    {
        if (currentlyHeldObject != null)
        {
            NetworkObject netObj = currentlyHeldObject.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.TryRemoveParent();
                TogglePhysicsClientRpc(netObj.NetworkObjectId, true);
            }
        }
        ClearHeldObjectClientRpc();
    }

    [ClientRpc]
    void ClearHeldObjectClientRpc()
    {
        currentlyHeldObject = null;
        if (currentGadget != null) { currentGadget.OnDrop(); currentGadget = null; }
    }

    [ClientRpc]
    void TogglePhysicsClientRpc(ulong objectId, bool isPhysicsOn)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            var rb = netObj.GetComponent<Rigidbody>();
            var colliders = netObj.GetComponentsInChildren<Collider>();

            if (rb != null) { rb.isKinematic = !isPhysicsOn; rb.useGravity = isPhysicsOn; }
            foreach (var col in colliders) col.enabled = isPhysicsOn;

            if (!isPhysicsOn)
            {
                currentlyHeldObject = netObj.transform;
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;

                GadgetBase gadget = netObj.GetComponent<GadgetBase>();
                if (gadget != null) { currentGadget = gadget; currentGadget.OnEquip(this); }
            }
        }
    }

    // --- İTME SİSTEMİ ---
    void TryPushPlayer()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(rayOrigin, transform.forward, out RaycastHit hit, pushRange))
        {
            if (hit.transform.TryGetComponent(out PlayerController targetPlayer))
            {
                if (targetPlayer.NetworkObjectId != NetworkObjectId)
                {
                    Vector3 pushDir = transform.forward * pushForce + Vector3.up * pushUpwardModifier;
                    RequestPushPlayerServerRpc(targetPlayer.NetworkObjectId, pushDir);
                }
            }
        }
    }

    [ServerRpc]
    void RequestPushPlayerServerRpc(ulong targetId, Vector3 forceVector)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetNetObj))
        {
            var targetScript = targetNetObj.GetComponent<PlayerController>();
            if (targetScript != null) targetScript.GetPushedClientRpc(forceVector);
        }
    }

    [ClientRpc]
    public void GetPushedClientRpc(Vector3 pushForce)
    {
        if (!IsOwner) return;
        myRb.AddForce(pushForce, ForceMode.Impulse);
    }

    // Eski scriptler hata vermesin diye (Geriye Dönük Uyumluluk)
    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        GetPushedClientRpc(impactForce);
    }
}