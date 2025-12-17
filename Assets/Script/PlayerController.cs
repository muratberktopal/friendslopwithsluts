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

    [Header("Hava Kontrolü (Air Control)")]
    public float airMoveForce = 15f;
    public float maxAirSpeed = 5f;

    [Header("Fırlatma / İtme")]
    public float throwForce = 15f;
    public float pushForce = 15f;
    public float pushRange = 3.0f;
    public float pushUpwardModifier = 3f;

    [Header("Ses / Gürültü (EnemyAI İçin)")]
    // Düşman scriptinin aradığı değişken burası:
    public float currentNoiseRange = 0f;

    [Header("Eğilme & Fizik")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;
    private CapsuleCollider myCollider;
    private Vector3 originalCameraPos;

    [Header("Bağlantılar")]
    public Transform cameraTransform;
    public Transform handPosition;

    private Transform currentlyHeldObject;
    private GadgetBase currentGadget;

    private bool isRagdolled = false;
    private Rigidbody myRb;

    public float mouseSensitivity = 100f;
    private float xRotation = 0f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Camera myCam = cameraTransform.GetComponent<Camera>();
        AudioListener myListener = cameraTransform.GetComponent<AudioListener>();

        if (IsOwner)
        {
            if (myCam != null) myCam.enabled = true;
            if (myListener != null) myListener.enabled = true;
            GameObject sceneCam = GameObject.Find("Main Camera");
            if (sceneCam != null) sceneCam.SetActive(false);
            MoveToRandomSpawnPoint();
        }
        else
        {
            if (myCam != null) myCam.enabled = false;
            if (myListener != null) myListener.enabled = false;
        }
    }

    void MoveToRandomSpawnPoint()
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
        if (spawnPoints.Length > 0)
        {
            int randomIndex = Random.Range(0, spawnPoints.Length);
            transform.position = spawnPoints[randomIndex].transform.position;
            transform.rotation = spawnPoints[randomIndex].transform.rotation;
        }
    }

    void Start()
    {
        myRb = GetComponent<Rigidbody>();
        myCollider = GetComponent<CapsuleCollider>();
        currentMoveSpeed = baseMoveSpeed;
        if (cameraTransform != null) originalCameraPos = cameraTransform.localPosition;

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
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
    }

    void HandleMovement()
    {
        bool isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        bool isMoving = (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f);

        // --- MOUSE ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // --- ZIPLAMA ---
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            myRb.AddForce(Vector3.up * 800f, ForceMode.Impulse);
            currentNoiseRange = 30f; // Zıplama sesi
        }

        // --- HIZ ---
        float targetSpeed = currentMoveSpeed;
        currentNoiseRange = 0f; // Sesi sıfırla

        if (Input.GetKey(KeyCode.LeftControl))
        {
            targetSpeed *= crouchMultiplier;
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, originalCameraPos - new Vector3(0, 0.5f, 0), Time.deltaTime * 10f);
            if (isMoving) currentNoiseRange = 2f;
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftShift))
            {
                targetSpeed *= runMultiplier;
                if (isMoving) currentNoiseRange = 20f;
            }
            else
            {
                if (isMoving) currentNoiseRange = 10f;
            }
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, originalCameraPos, Time.deltaTime * 10f);
        }

        Vector3 moveDir = transform.right * x + transform.forward * z;

        if (isGrounded)
        {
            transform.position += moveDir * targetSpeed * Time.deltaTime;
        }
        else
        {
            Vector3 flatVelocity = new Vector3(myRb.linearVelocity.x, 0, myRb.linearVelocity.z);
            if (flatVelocity.magnitude < maxAirSpeed || Vector3.Dot(flatVelocity.normalized, moveDir) < 0)
            {
                myRb.AddForce(moveDir * airMoveForce, ForceMode.Acceleration);
            }
        }
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();

        if (currentGadget != null)
        {
            if (Input.GetMouseButtonDown(1)) currentGadget.OnUseStart();
            if (Input.GetMouseButtonUp(1)) currentGadget.OnUseStop();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (currentlyHeldObject != null)
                ThrowObjectServerRpc();
            else
                TryPushPlayer();
        }
    }

    void TryPushPlayer()
    {
        RaycastHit hit;
        Vector3 rayOrigin = cameraTransform.position + (cameraTransform.forward * 0.5f);

        if (Physics.Raycast(rayOrigin, cameraTransform.forward, out hit, pushRange))
        {
            if (hit.transform.TryGetComponent(out PlayerController targetPlayer))
            {
                if (targetPlayer.NetworkObjectId != NetworkObjectId)
                {
                    Vector3 pushDir = cameraTransform.forward * pushForce + Vector3.up * pushUpwardModifier;
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

    // --- YENİ İTME SİSTEMİ ---
    [ClientRpc]
    public void GetPushedClientRpc(Vector3 pushForce)
    {
        if (!IsOwner) return;
        myRb.AddForce(pushForce, ForceMode.Impulse);
    }

    // --- ESKİ SİSTEMLER İÇİN UYUMLULUK (DÜZELTME BURADA) ---
    // TrapCollision ve EnemyAI hala bu ismi arıyor.
    // Biz de bu çağrıyı alıp yeni sisteme yönlendiriyoruz.
    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        // Eski "Ragdoll" yerine artık "Push" çalıştırıyoruz.
        // Böylece düşman sana vurduğunda veya tuzağa bastığında bayılmak yerine fırlarsın.
        GetPushedClientRpc(impactForce);
    }

    // --- EŞYA YÖNETİMİ ---

    void TryPickup()
    {
        if (currentlyHeldObject != null) return;
        Vector3 rayOrigin = cameraTransform.position + (cameraTransform.forward * 0.5f);
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, cameraTransform.forward, out hit, 3f))
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
            netObj.TrySetParent(handPosition, false);
            TogglePhysicsClientRpc(objectId, false);
        }
    }

    [ServerRpc]
    void ThrowObjectServerRpc()
    {
        if (currentlyHeldObject != null)
        {
            NetworkObject netObj = currentlyHeldObject.GetComponent<NetworkObject>();
            Rigidbody rbToThrow = currentlyHeldObject.GetComponent<Rigidbody>();

            PerformDropLogic();

            if (netObj != null && rbToThrow != null)
            {
                rbToThrow.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
            }
        }
    }

    void PerformDropLogic()
    {
        if (currentlyHeldObject != null || handPosition.childCount > 0)
        {
            NetworkObject netObj = null;
            if (currentlyHeldObject != null) netObj = currentlyHeldObject.GetComponent<NetworkObject>();

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
        if (currentGadget != null)
        {
            currentGadget.OnDrop();
            currentGadget = null;
        }
        if (IsOwner) currentMoveSpeed = baseMoveSpeed;
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

            foreach (var col in netObj.GetComponentsInChildren<Collider>()) col.enabled = isPhysicsOn;

            if (!isPhysicsOn)
            {
                currentlyHeldObject = netObj.transform;
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;

                GadgetBase gadgetScript = netObj.GetComponent<GadgetBase>();
                if (gadgetScript != null)
                {
                    currentGadget = gadgetScript;
                    currentGadget.OnEquip(this);
                    netObj.transform.localPosition = gadgetScript.holdPositionOffset;
                    netObj.transform.localRotation = Quaternion.Euler(gadgetScript.holdRotationOffset);
                }

                if (IsOwner)
                {
                    var itemWeight = netObj.GetComponent<ItemWeight>();
                    if (itemWeight != null) currentMoveSpeed = baseMoveSpeed * (1.0f - itemWeight.slowdownPercentage);
                }
            }
        }
    }
}