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

    [Header("İtme (Tokat) Ayarları")]
    public float pushForce = 25f;   // İtme gücü
    public float pushRange = 2.5f;  // İtme mesafesi

    [Header("Diğer Ayarlar")]
    public float throwForce = 15f;
    public float mouseSensitivity = 100f;
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;

    // --- HATAYI ÇÖZEN DEĞİŞKEN BURADA ---
    [Header("Ses Verisi")]
    public float currentNoiseRange = 0f; // EnemyAI bunu okuyacak

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

        // Süzülmeyi ve kaymayı önleyen ayarlar
        myRb.freezeRotation = true;
        myRb.useGravity = true;
        myRb.linearDamping = 0f;
        myRb.angularDamping = 0.05f;

        // Başlangıç değerlerini ayarla
        if (cameraTransform == null) cameraTransform = transform.GetComponentInChildren<Camera>().transform;

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void FixedUpdate()
    {
        if (!IsOwner || isRagdolled) return;

        // --- ZEMİN KONTROLÜ ---
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 1.2f);

        // --- HAREKET ---
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // Hız Belirleme
        float targetSpeed = walkSpeed;
        if (isCrouching) targetSpeed = crouchSpeed;
        else if (isRunning) targetSpeed = runSpeed;

        targetSpeed *= weightMultiplier;

        // Hareket Yönü
        Vector3 moveDir = (transform.right * x + transform.forward * z).normalized;

        // --- GÜRÜLTÜ HESABI (EnemyAI Hatası İçin Şart) ---
        currentNoiseRange = 0f;

        if (moveDir.magnitude > 0)
        {
            // Yeni hızı hesapla (Y eksenini koru ki düşebilelim!)
            Vector3 targetVelocity = moveDir * targetSpeed;
            targetVelocity.y = myRb.linearVelocity.y; // Mevcut düşüş hızını koru

            // Hızı uygula (Anında tepki verir, kayma yapmaz)
            myRb.linearVelocity = targetVelocity;

            // Ses hesabı (Sadece yerdeyken)
            if (isGrounded)
            {
                if (isRunning) currentNoiseRange = 20f;      // Koşma sesi
                else if (isCrouching) currentNoiseRange = 2f; // Eğilme sesi
                else currentNoiseRange = 10f;                // Yürüme sesi
            }
        }
        else
        {
            // Tuşa basmıyorsak dur, ama Y eksenini elleme (Düşmeye devam et)
            myRb.linearVelocity = new Vector3(0, myRb.linearVelocity.y, 0);
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
                // Zıplamadan önce Y hızını sıfırla
                myRb.linearVelocity = new Vector3(myRb.linearVelocity.x, 0f, myRb.linearVelocity.z);
                myRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

                // Zıplama çok ses çıkarır
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

    // --- İTME SİSTEMİ (SHOVE) ---
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
                // İtme yönü: İleri + Hafif Yukarı
                Vector3 pushDirection = cameraTransform.forward * pushForce + Vector3.up * 5f;

                // Server'a bildir
                RequestPushServerRpc(victim.NetworkObjectId, pushDirection);
            }
        }
    }

    [ServerRpc]
    void RequestPushServerRpc(ulong victimId, Vector3 force)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(victimId, out NetworkObject victimNetObj))
        {
            PlayerController victimScript = victimNetObj.GetComponent<PlayerController>();
            if (victimScript != null)
            {
                // Kurbana vur
                victimScript.GetHitClientRpc(force);
            }
        }
    }

    // --- MEVCUT RPC FONKSİYONLARI ---

    [ClientRpc]
    void TogglePhysicsClientRpc(ulong objectId, bool isPhysicsOn)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            var rb = netObj.GetComponent<Rigidbody>();
            var itemWeight = netObj.GetComponent<ItemWeight>();

            if (!isPhysicsOn) // ALDIĞINDA
            {
                if (IsOwner) currentlyHeldObject = netObj.transform;
                if (rb) rb.isKinematic = true;
                if (IsOwner && itemWeight != null) weightMultiplier = 1.0f - itemWeight.slowdownPercentage;
            }
            else // BIRAKTIĞINDA
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
        myRb.constraints = RigidbodyConstraints.None;

        myRb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForSeconds(4.0f); // 4 saniye baygın kal

        // Kalkış işlemleri
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        myRb.constraints = RigidbodyConstraints.FreezeRotation;
        myRb.linearVelocity = Vector3.zero;
        myRb.angularVelocity = Vector3.zero;

        xRotation = 0f;
        cameraTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        isRagdolled = false;

        // Kalkınca hızı sıfırla ki bug'a girmesin
        weightMultiplier = 1.0f;
    }
}