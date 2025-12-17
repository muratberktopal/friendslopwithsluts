using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları (Stabil)")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float crouchSpeed = 2.5f;
    public float jumpForce = 5f;

    [Header("Etkileşim Ayarları")]
    public float pushForce = 20f;   // Arkadaşını ne kadar sert iteceksin?
    public float pushRange = 2.5f;  // Ne kadar yakından itebilirsin?
    public float throwForce = 15f;

    [Header("Diğer Ayarlar")]
    public float mouseSensitivity = 100f;
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;

    // Düşman için ses verisi
    public float currentNoiseRange = 0f;

    [Header("Bağlantılar")]
    public Transform cameraTransform;
    public Transform handPosition;

    // Private Değişkenler
    private float xRotation = 0f;
    private Rigidbody myRb;
    private CapsuleCollider myCollider;
    private Transform currentlyHeldObject;
    private bool isRagdolled = false;
    private bool isGrounded;
    private float weightMultiplier = 1.0f;

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

    void Start()
    {
        myRb = GetComponent<Rigidbody>();
        myCollider = GetComponent<CapsuleCollider>();

        // Stabil fizik ayarları (Kaymayı önler)
        myRb.freezeRotation = true;
        myRb.useGravity = true;
        myRb.linearDamping = 5f; // Sürekli sürtünme olsun ki kaymasın

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner || isRagdolled) return;

        // --- HAREKET (Bhop Yok, Stabil Yürüme) ---
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.2f);

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float targetSpeed = walkSpeed;
        if (isCrouching) targetSpeed = crouchSpeed;
        else if (isRunning) targetSpeed = runSpeed;

        targetSpeed *= weightMultiplier;

        Vector3 moveDir = (transform.right * x + transform.forward * z).normalized;

        if (moveDir.magnitude > 0)
        {
            // Direkt hız kontrolü (Velocity Change) en stabil yöntemdir
            Vector3 targetVelocity = moveDir * targetSpeed;

            // Y hızını koru (Zıplama bozulmasın diye)
            targetVelocity.y = myRb.linearVelocity.y;

            // Hızı uygula
            myRb.linearVelocity = Vector3.Lerp(myRb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 10f);
        }

        // --- GÜRÜLTÜ HESABI ---
        currentNoiseRange = 0f;
        if (moveDir.magnitude > 0 && isGrounded)
        {
            if (isRunning) currentNoiseRange = 20f;
            else if (isCrouching) currentNoiseRange = 2f;
            else currentNoiseRange = 10f;
        }
    }

    void Update()
    {
        if (!IsOwner || isRagdolled) return;

        // --- ZIPLAMA ---
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            bool isTooHeavy = weightMultiplier < 0.3f;
            if (!isTooHeavy)
            {
                myRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                currentNoiseRange = 30f;
            }
        }

        // --- MOUSE LOOK ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // --- EĞİLME ---
        if (Input.GetKey(KeyCode.LeftControl))
        {
            myCollider.height = Mathf.Lerp(myCollider.height, crouchHeight, Time.deltaTime * crouchTransitionSpeed);
            myCollider.center = Vector3.Lerp(myCollider.center, new Vector3(0, -0.5f, 0), Time.deltaTime * crouchTransitionSpeed);
        }
        else
        {
            myCollider.height = Mathf.Lerp(myCollider.height, standingHeight, Time.deltaTime * crouchTransitionSpeed);
            myCollider.center = Vector3.Lerp(myCollider.center, Vector3.zero, Time.deltaTime * crouchTransitionSpeed);
        }

        // --- ETKİLEŞİM ---
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;
        }

        if (Input.GetKeyDown(KeyCode.E)) TryPickup();

        // --- SOL TIK: FIRLATMA VEYA İTME ---
        if (Input.GetMouseButtonDown(0))
        {
            if (currentlyHeldObject != null)
            {
                // Elin doluysa FIRLAT
                ThrowObjectServerRpc();
            }
            else
            {
                // Elin boşsa İT (SHOVE)
                TryPushPlayer();
            }
        }
    }

    // --- YENİ: İTME SİSTEMİ ---
    void TryPushPlayer()
    {
        RaycastHit hit;
        // Kameradan ileri doğru ışın at
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, pushRange))
        {
            // Çarptığımız şey bir Oyuncu mu?
            PlayerController victim = hit.transform.GetComponent<PlayerController>();

            if (victim != null)
            {
                // Vuruş yönünü hesapla (Bakış açımıza göre ileri + biraz yukarı)
                Vector3 pushDirection = cameraTransform.forward * pushForce + Vector3.up * (pushForce * 0.2f);

                // Server'a söyle: "Bu adamı it!"
                // Not: victim.NetworkObjectId ile kimi ittiğimizi söylüyoruz
                RequestPushServerRpc(victim.NetworkObjectId, pushDirection);
            }
        }
    }

    [ServerRpc]
    void RequestPushServerRpc(ulong victimId, Vector3 force)
    {
        // Server, ID'den oyuncuyu bulur
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(victimId, out NetworkObject victimNetObj))
        {
            PlayerController victimScript = victimNetObj.GetComponent<PlayerController>();
            if (victimScript != null)
            {
                // Kurbana "Vurulma" emrini gönder (Ragdoll + Kuvvet)
                victimScript.GetHitClientRpc(force);
            }
        }
    }

    // --- RPC & YARDIMCI FONKSİYONLAR (Eskisiyle aynı) ---

    [ClientRpc]
    void TogglePhysicsClientRpc(ulong objectId, bool isPhysicsOn)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            var rb = netObj.GetComponent<Rigidbody>();
            var itemWeight = netObj.GetComponent<ItemWeight>();

            if (!isPhysicsOn)
            {
                if (IsOwner) currentlyHeldObject = netObj.transform;
                if (rb) rb.isKinematic = true;
                if (IsOwner && itemWeight != null) weightMultiplier = 1.0f - itemWeight.slowdownPercentage;
            }
            else
            {
                if (IsOwner) currentlyHeldObject = null;
                if (rb) rb.isKinematic = false;
                if (IsOwner) weightMultiplier = 1.0f;
            }
        }
    }

    void MoveToRandomSpawnPoint()
    {
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
        if (spawnPoints.Length > 0)
        {
            int r = Random.Range(0, spawnPoints.Length);
            transform.position = spawnPoints[r].transform.position;
        }
    }

    void TryPickup()
    {
        if (currentlyHeldObject != null) return;
        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, 3f))
        {
            if (hit.transform.TryGetComponent(out NetworkObject netObj)) RequestPickupServerRpc(netObj.NetworkObjectId);
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
            DropItemServerRpc();
            if (netObj != null)
            {
                var rb = netObj.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
            }
        }
    }

    [ServerRpc]
    void DropItemServerRpc()
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
    }

    [ClientRpc]
    public void GetHitClientRpc(Vector3 impactForce)
    {
        if (!IsOwner) return;
        StartCoroutine(RagdollRoutine(impactForce));
    }

    IEnumerator RagdollRoutine(Vector3 force)
    {
        isRagdolled = true;
        if (currentlyHeldObject != null) DropItemServerRpc();
        myRb.freezeRotation = false;
        myRb.constraints = RigidbodyConstraints.None; // Tüm kilitleri aç
        myRb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForSeconds(4.0f); // 4 saniye yerde kal

        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        myRb.constraints = RigidbodyConstraints.FreezeRotation; // Kilitleri geri tak
        myRb.linearVelocity = Vector3.zero;
        myRb.angularVelocity = Vector3.zero;
        isRagdolled = false;
    }
}