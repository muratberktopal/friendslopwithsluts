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

    [Header("İtme / Etkileşim (RAGDOLL MODU)")]
    public float pushForce = 60f;           // Uçması için yüksek güç
    public float pushRange = 4.0f;
    public float pushUpwardModifier = 5f;   // Havaya kaldırma
    public float throwForce = 20f;
    public float ragdollDuration = 3.0f;    // Kaç saniye baygın kalacak?

    [Header("Bağlantılar")]
    public Transform handPosition;
    public Transform cameraTransform;

    private Rigidbody myRb;
    private Transform currentlyHeldObject;
    private GadgetBase currentGadget;

    // Ragdoll durumu (Baygın mı?)
    private bool isRagdolled = false;
    private CapsuleCollider myCollider;

    public float currentNoiseRange = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsOwner)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                var camScript = Camera.main.GetComponent<ThirdPersonCamera>();
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

        // Başlangıçta karakter dik dursun (Devrilmesin)
        myRb.freezeRotation = true;
    }

    void Update()
    {
        // Eşya Takibi (Her zaman çalışsın ki bayılınca eşya havada kalmasın)
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

        // --- RAGDOLL KONTROLÜ ---
        // Eğer baygınsak (Ragdoll), hareket kodlarını çalıştırma!
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
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, rayLength);

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        if (cameraTransform == null) return;

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
        int layerMask = ~LayerMask.GetMask("Player");

        if (Physics.Raycast(ray, out hit, 100f, layerMask))
        {
            if (hit.transform == transform || hit.transform.root == transform.root) return;

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
    void RequestPickupServerRpc(ulong objectId, ServerRpcParams rpcParams = default) // rpcParams ekledik
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            // --- EKSİK OLAN KISIM BURASI ---
            // Silahı alan kişiye (Sender) sahipliğini veriyoruz.
            // Bunu yapmazsan sadece HOST ateş edebilir, diğerleri edemez.
            netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);

            // Parent işlemleri (Mevcut kodun)
            netObj.TrySetParent(transform, false);
            TogglePhysicsClientRpc(objectId, false);
        }
    }
}

    [ServerRpc]
    void ThrowObjectServerRpc()
    {
        if (currentlyHeldObject != null)
        {
            Rigidbody rb = currentlyHeldObject.GetComponent<Rigidbody>();
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

            if (!isPhysicsOn)
            {
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
            if (targetScript != null) targetScript.GetHitClientRpc(forceVector);
        }
    }

    // --- RAGDOLL + UÇUŞ SİSTEMİ ---

    [ClientRpc]
    public void GetPushedClientRpc(Vector3 pushForce)
    {
        if (!IsOwner) return;
        // Ragdoll rutini başlat (Dönerek uçma)
        StartCoroutine(RagdollRoutine(pushForce));
    }

    // Eski kodlar hata vermesin diye
    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        GetPushedClientRpc(impactForce);
    }

    IEnumerator RagdollRoutine(Vector3 force)
    {
        // 1. Ragdoll Modunu Aç
        isRagdolled = true;

        // Elinde eşya varsa düşür (Opsiyonel: İstersen bu satırı kapatabilirsin)
        if (currentlyHeldObject != null) PerformDropLogic();

        // 2. Fiziği Serbest Bırak (Dönmeye başlasın)
        myRb.freezeRotation = false; // Artık dik durmak zorunda değil
        myRb.constraints = RigidbodyConstraints.None; // Tam serbestlik

        // 3. Mevcut hızı sıfırla ve GÜCÜ VER (UÇUŞ)
        myRb.linearVelocity = Vector3.zero;

        // Eğer güç çok aşağı bakıyorsa biraz yukarı kaldıralım ki yere yapışmasın
        Vector3 finalForce = force;
        if (finalForce.y < 5f) finalForce.y += 5f;

        myRb.AddForce(finalForce, ForceMode.Impulse);

        // 4. Belirli bir süre bekle (Yerde yuvarlansın)
        yield return new WaitForSeconds(ragdollDuration);

        // 5. Ayağa Kalk (Toparlanma)
        // Karakterin rotasyonunu düzelt (Sadece Y ekseni kalsın, X ve Z sıfırlansın)
        Vector3 currentEuler = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0, currentEuler.y, 0);

        // Yere gömülmemesi için hafif yukarı ışınla
        transform.position += Vector3.up * 1.0f;

        // Fiziği tekrar kilitle (Dik durması için)
        myRb.freezeRotation = true;
        myRb.constraints = RigidbodyConstraints.FreezeRotation; // Sadece Y'de dönebilsin

        // Hızları sıfırla ki kaymaya devam etmesin
        myRb.linearVelocity = Vector3.zero;
        myRb.angularVelocity = Vector3.zero;

        // Kontrolü geri ver
        isRagdolled = false;
    }
}