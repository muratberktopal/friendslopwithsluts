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
    public float throwForce = 15f;

    [Header("İtme (Push) Ayarları")]
    public float pushForce = 10f;
    public float pushRange = 3.0f; // Menzili biraz artırdım
    public float pushUpwardModifier = 2f;

    [Header("Eğilme Ayarları")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;
    private CapsuleCollider myCollider;
    private Vector3 originalCameraPos;

    [Header("Ses / Gürültü Ayarları")]
    public float currentNoiseRange = 0f;

    [Header("Mouse Ayarları")]
    public float mouseSensitivity = 100f;
    private float xRotation = 0f;

    [Header("Bağlantılar")]
    public Transform cameraTransform;
    public Transform handPosition;

    private bool isRagdolled = false;
    private Rigidbody myRb;

    // Bunu herkesin bilmesi lazım, o yüzden logic değişti
    private Transform currentlyHeldObject;

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
        // --- DÜZELTME 1: EŞYA KONUMLANDIRMA (IsOwner'dan ÖNCE OLMALI) ---
        // Bu kısım "IsOwner" kontrolünden önce olmalı ki, diğer oyuncular da
        // senin elindeki eşyanın seninle geldiğini görsün.
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;
        }

        // --- SADECE SAHİBİ ÇALIŞTIRIR ---
        if (!IsOwner) return;

        // Zıplama her zaman çalışabilir (Ragdoll değilse)
        if (Input.GetKeyDown(KeyCode.Space) && !isRagdolled)
        {
            float rayLength = 1.3f;
            currentNoiseRange = 30f;
            bool isTooHeavy = currentMoveSpeed < (baseMoveSpeed * 0.3f);
            if (!isTooHeavy && Physics.Raycast(transform.position, Vector3.down, rayLength))
            {
                myRb.AddForce(Vector3.up * 800f, ForceMode.Impulse);
            }
        }

        if (isRagdolled) return;

        // --- MOUSE LOOK ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // --- HAREKET ---
        float finalSpeed = currentMoveSpeed;
        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        bool isMoving = (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f);

        currentNoiseRange = 0f;

        if (isCrouching)
        {
            finalSpeed *= crouchMultiplier;
            myCollider.height = Mathf.Lerp(myCollider.height, crouchHeight, Time.deltaTime * crouchTransitionSpeed);
            myCollider.center = Vector3.Lerp(myCollider.center, new Vector3(0, -0.5f, 0), Time.deltaTime * crouchTransitionSpeed);
            Vector3 crouchCamPos = new Vector3(originalCameraPos.x, originalCameraPos.y - 0.5f, originalCameraPos.z);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, crouchCamPos, Time.deltaTime * crouchTransitionSpeed);
            if (isMoving) currentNoiseRange = 2f;
        }
        else
        {
            myCollider.height = Mathf.Lerp(myCollider.height, standingHeight, Time.deltaTime * crouchTransitionSpeed);
            myCollider.center = Vector3.Lerp(myCollider.center, Vector3.zero, Time.deltaTime * crouchTransitionSpeed);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, originalCameraPos, Time.deltaTime * crouchTransitionSpeed);

            if (isRunning)
            {
                finalSpeed *= runMultiplier;
                if (isMoving) currentNoiseRange = 20f;
            }
            else
            {
                if (isMoving) currentNoiseRange = 10f;
            }
        }

        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * finalSpeed * Time.deltaTime;

        // --- ETKİLEŞİM ---
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();

        if (Input.GetMouseButtonDown(0))
        {
            if (currentlyHeldObject != null)
            {
                ThrowObjectServerRpc();
            }
            else
            {
                TryPushPlayer();
            }
        }
    }

    void TryPushPlayer()
    {
        RaycastHit hit;
        // --- DÜZELTME 2: RAYCAST ORIGIN ---
        // Işını tam kameranın içinden değil, 0.5 birim önünden başlatıyoruz.
        // Böylece kendi vücudumuza (CapsuleCollider) çarpıp durmaz.
        Vector3 rayOrigin = cameraTransform.position + (cameraTransform.forward * 0.5f);

        if (Physics.Raycast(rayOrigin, cameraTransform.forward, out hit, pushRange))
        {
            if (hit.transform.TryGetComponent(out PlayerController targetPlayer))
            {
                // Kendimizi itmeyelim
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
            if (targetScript != null)
            {
                targetScript.GetHitClientRpc(forceVector);
            }
        }
    }

    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        // Ragdoll efekti sadece o karakterin sahibinde çalışmalı (Physics hesabı için)
        // Ama görsel olarak herkes görsün istiyorsan burayı açabilirsin. 
        // Şimdilik sadece sahibi fizik uygulasın, NetworkTransform senkronize eder.
        if (!IsOwner) return;
        StartCoroutine(RagdollRoutine(impactForce));
    }

    IEnumerator RagdollRoutine(Vector3 force)
    {
        isRagdolled = true;
        if (currentlyHeldObject != null) DropItemServerRpc();

        myRb.constraints = RigidbodyConstraints.None;
        myRb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForSeconds(3.0f);

        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        transform.position += Vector3.up * 1.0f;
        myRb.constraints = RigidbodyConstraints.FreezeRotation;

        myRb.linearVelocity = Vector3.zero;
        myRb.angularVelocity = Vector3.zero;

        xRotation = 0f;
        cameraTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        isRagdolled = false;
        currentMoveSpeed = baseMoveSpeed;
    }

    [ServerRpc]
    void DropItemServerRpc()
    {
        if (currentlyHeldObject != null || handPosition.childCount > 0)
        {
            NetworkObject netObj = null;
            if (currentlyHeldObject != null) netObj = currentlyHeldObject.GetComponent<NetworkObject>();
            if (netObj == null && handPosition.childCount > 0) netObj = handPosition.GetChild(0).GetComponent<NetworkObject>();
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
        // Eşyayı bıraktığımızı herkes bilmeli, sadece Owner değil.
        currentlyHeldObject = null;

        if (IsOwner)
        {
            currentMoveSpeed = baseMoveSpeed;
        }
    }

    void TryPickup()
    {
        if (currentlyHeldObject != null) return;

        // Pickup için de Raycast'i biraz öne aldım
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
            NetworkObject netObj = currentlyHeldObject.GetComponent<NetworkObject>();
            DropItemServerRpc();
            if (netObj != null)
            {
                var rb = netObj.GetComponent<Rigidbody>();
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
                // --- DÜZELTME 3: HERKES GÖRSÜN ---
                // Eşyayı eline aldığında "currentlyHeldObject" değişkenini
                // SADECE Owner değil, BU scriptin olduğu HER client (diğer oyuncular) da atamalı.
                // Böylece Update içindeki kod çalışıp eşyayı eline yapıştıracak.
                currentlyHeldObject = netObj.transform;

                if (IsOwner)
                {
                    var itemWeight = netObj.GetComponent<ItemWeight>();
                    if (itemWeight != null)
                    {
                        currentMoveSpeed = baseMoveSpeed * (1.0f - itemWeight.slowdownPercentage);
                    }
                }
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;
            }
        }
    }
}