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
    public float rotationSpeed = 10f;

    [Header("Zıplama Ayarları")]
    public float jumpForce = 800f;
    public float rayLength = 1.3f;

    [Header("İtme / Etkileşim")]
    public float pushForce = 15f;
    public float pushRange = 3.0f;
    public float pushUpwardModifier = 3f;
    public float throwForce = 15f;

    [Header("Bağlantılar")]
    public Transform handPosition;

    // --- DÜZELTME BURADA ---
    // HATA ÇÖZÜMÜ: Gadget'lar buna ulaşmaya çalışıyor, o yüzden Public yaptık
    // ve ismini mainCameraTransform'dan cameraTransform'a çevirdik.
    public Transform cameraTransform;

    private Rigidbody myRb;
    private Transform currentlyHeldObject;
    private GadgetBase currentGadget;
    private bool isRagdolled = false;
    private CapsuleCollider myCollider;

    public float currentNoiseRange = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            // 1. Sahnedeki Kamerayı Bul ve Değişkene Ata
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform; // Düzeltildi
                var camScript = Camera.main.GetComponent<ThirdPersonCamera>();

                // 2. Kameraya "Beni takip et" de
                if (camScript != null)
                {
                    camScript.target = this.transform;
                }
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Start()
    {
        myRb = GetComponent<Rigidbody>();
        myCollider = GetComponent<CapsuleCollider>();
        currentMoveSpeed = baseMoveSpeed;
        myRb.freezeRotation = true;
    }

    void Update()
    {
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;

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

        if (currentGadget != null)
        {
            if (Input.GetMouseButtonDown(1)) currentGadget.OnUseStart();
            if (Input.GetMouseButtonUp(1)) currentGadget.OnUseStop();
        }
    }

    void HandleMovement()
    {
        // 1. ZEMİN KONTROLÜ
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, rayLength);
        Debug.DrawRay(transform.position, Vector3.down * rayLength, isGrounded ? Color.green : Color.red);

        // 2. HAREKET YÖNÜ (TPS KAMERA SİSTEMİ)
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // DÜZELTME: Artık 'cameraTransform' kullanıyoruz
        if (cameraTransform == null) return; // Güvenlik kontrolü

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;

        camForward.y = 0;
        camRight.y = 0;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = (camForward * v + camRight * h).normalized;

        float targetSpeed = baseMoveSpeed;
        if (Input.GetKey(KeyCode.LeftControl)) targetSpeed *= crouchMultiplier;
        else if (Input.GetKey(KeyCode.LeftShift)) targetSpeed *= runMultiplier;

        if (currentlyHeldObject != null)
        {
            var w = currentlyHeldObject.GetComponent<ItemWeight>();
            if (w != null) targetSpeed *= (1.0f - w.slowdownPercentage);
        }

        // 3. HAREKET UYGULAMA
        if (moveDir.magnitude >= 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            transform.position += moveDir * targetSpeed * Time.deltaTime;
            currentNoiseRange = Input.GetKey(KeyCode.LeftShift) ? 20f : 10f;
        }
        else
        {
            currentNoiseRange = 0f;
        }

        // 4. ZIPLAMA
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
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

    void TryPickup()
    {
        if (currentlyHeldObject != null || cameraTransform == null) return;

        RaycastHit hit;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        // --- DÜZELTME BURADA ---
        // 1. Bir "Maske" oluşturuyoruz. 
        // "Player" layerı HARİÇ her şeye çarp demek. (~ işareti 'hariç' demektir)
        int layerMask = ~LayerMask.GetMask("Player");

        // 2. Raycast atarken bu maskeyi kullanıyoruz
        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            // EKSTRA GÜVENLİK: Kendimize çarpıp çarpmadığımızı yine de kontrol edelim
            if (hit.transform == transform || hit.transform.root == transform.root)
                return;

            // Mesafe kontrolü (4 birim)
            float distanceToItem = Vector3.Distance(transform.position, hit.point);

            if (distanceToItem <= 4.0f)
            {
                if (hit.transform.TryGetComponent(out NetworkObject netObj))
                {
                    RequestPickupServerRpc(netObj.NetworkObjectId);
                }
            }
        }
    }

    [ServerRpc]
    void RequestPickupServerRpc(ulong objectId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            // DÜZELTME: HandPosition yerine 'transform' (Player Root) kullanıyoruz.
            // Netcode sadece NetworkObject -> NetworkObject bağlantısına izin verir.
            netObj.TrySetParent(transform, false);

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
            var netTransform = netObj.GetComponent<NetworkTransform>();

            if (rb != null) { rb.isKinematic = !isPhysicsOn; rb.useGravity = isPhysicsOn; }
            foreach (var col in colliders) col.enabled = isPhysicsOn;

            if (netTransform != null) netTransform.enabled = isPhysicsOn;

            // --- EŞYA ALINDI ---
            if (!isPhysicsOn)
            {
                // DÜZELTME: handPosition yerine 'transform' (Player) yaptık.
                // Merak etme, Update fonksiyonu eşyayı eline ışınlamaya devam edecek.
                netObj.transform.SetParent(transform);

                currentlyHeldObject = netObj.transform;
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;

                GadgetBase gadget = netObj.GetComponent<GadgetBase>();
                if (gadget != null)
                {
                    currentGadget = gadget;
                    currentGadget.OnEquip(this);
                    netObj.transform.localPosition = currentGadget.holdPositionOffset;
                    netObj.transform.localRotation = Quaternion.Euler(currentGadget.holdRotationOffset);
                }
            }
            // --- EŞYA BIRAKILDI ---
            else
            {
                netObj.transform.SetParent(null);
            }
        }
    }

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

    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        GetPushedClientRpc(impactForce);
    }
}