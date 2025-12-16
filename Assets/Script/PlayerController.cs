using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("Hareket Ayarları")]
    public float moveSpeed = 5f;
    private float defaultMoveSpeed; // Orijinal hızı hafızada tutacağız (YENİ)
    public float throwForce = 15f;

    [Header("Mouse Ayarları")]
    public float mouseSensitivity = 100f;
    private float xRotation = 0f;

    [Header("Bağlantılar")]
    public Transform cameraTransform;
    public Transform handPosition;

    // Ragdoll / Bayılma Durumu
    private bool isRagdolled = false;
    private Rigidbody myRb;

    // Şu an elimde tuttuğum obje
    private Transform currentlyHeldObject;

    [Header("Gizlilik Ayarları")]
    public float runNoiseRange = 20f;   // Koşarken duyulma mesafesi
    public float walkNoiseRange = 10f;  // Yürürken duyulma mesafesi
    public float crouchNoiseRange = 3f; // Eğilirken duyulma mesafesi

    public float crouchHeight = 0.5f;   // Eğilince karakter boyu
    public float normalHeight = 1.8f;   // Normal boy
    public float crouchSpeed = 2f;      // Eğilme hızı

    // Anlık olarak ne kadar ses çıkarıyorum? (AI bunu okuyacak)
    public float currentNoiseRange { get; private set; }

    private bool isCrouching = false;
    private CapsuleCollider myCollider; // Karakterin şekli


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
        defaultMoveSpeed = moveSpeed; // Başlangıç hızını kaydet (YENİ)

        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // --- ZIPLAMA ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            float rayLength = 1.3f;
            Debug.DrawRay(transform.position, Vector3.down * rayLength, Color.red, 2f);

            // YENİ: Eğer çok ağır bir şey taşıyorsan (Hızın %30'un altına düştüyse) zıplayama!
            bool isTooHeavy = moveSpeed < (defaultMoveSpeed * 0.3f);

            if (!isTooHeavy && Physics.Raycast(transform.position, Vector3.down, rayLength))
            {
                GetComponent<Rigidbody>().AddForce(Vector3.up * 800f, ForceMode.Impulse);
                Debug.Log("Zıpladım!");
            }
            else
            {
                if (isTooHeavy) Debug.Log("Çok ağırım, zıplayamam!");
                else Debug.Log("Zıplayamam, havada görünüyorum.");
            }
        }

        if (!IsOwner) return;
        if (isRagdolled) return;

        HandleCrouch();  // Eğilme kontrolü
        CalculateNoise(); // Gürültü hesabı

        if (!IsOwner) return;

        if (isRagdolled) return;

        // --- MANYETİK YAPIŞTIRMA ---
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;
        }

        // --- MOUSE LOOK ---
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // --- HAREKET ---
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * moveSpeed * Time.deltaTime;

        // --- ETKİLEŞİM ---
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();
        if (Input.GetMouseButtonDown(0)) ThrowObjectServerRpc();
    }

    // --- BURADAN AŞAĞISI AYNI (RAGDOLL, PICKUP, THROW) ---

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

        myRb.constraints = RigidbodyConstraints.None;
        myRb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForSeconds(3.0f);

        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        transform.position += Vector3.up * 1.0f;

        myRb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        myRb.linearVelocity = Vector3.zero; // Unity 6 için linearVelocity, eski sürümler için velocity
        myRb.angularVelocity = Vector3.zero;

        // Kalkınca kamerayı düzelt
        xRotation = 0f;
        cameraTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        isRagdolled = false;

        // YENİ: Kalkınca hızını sıfırla (Bazen yavaş kalma bug'ı olmasın diye)
        moveSpeed = defaultMoveSpeed;
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
        if (IsOwner)
        {
            currentlyHeldObject = null;
            // YENİ: Eşyayı bırakınca hızımızı normale döndür
            moveSpeed = defaultMoveSpeed;
            Debug.Log("Eşya bırakıldı, hız normale döndü: " + moveSpeed);
        }
    }

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
                if (IsOwner)
                {
                    currentlyHeldObject = netObj.transform;

                    // --- YENİ: Ağırlık Kontrolü ---
                    var itemWeight = netObj.GetComponent<ItemWeight>();
                    if (itemWeight != null)
                    {
                        // Hızı düşür (Örn: 5 * (1 - 0.5) = 2.5)
                        moveSpeed = defaultMoveSpeed * (1.0f - itemWeight.slowdownPercentage);
                        Debug.Log("Ağır eşya alındı! Yeni hız: " + moveSpeed);
                    }
                }
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // Bırakma işlemi ClearHeldObjectClientRpc içinde yapılıyor
            }
        }
    }

    void HandleCrouch()
    {
        // Sol Ctrl tuşuna basılı tutunca eğil
        if (Input.GetKey(KeyCode.LeftControl))
        {
            isCrouching = true;

            // Boyunu küçült (Collider)
            if (myCollider != null) myCollider.height = crouchHeight;

            // Kamerayı aşağı indir (Göz hizası değişsin)
            // Kamera normalde 0.6f yükseklikteyse, eğilince 0.0f'a insin gibi
            cameraTransform.localPosition = new Vector3(0, crouchHeight / 2, 0);
        }
        else
        {
            isCrouching = false;

            // Boyunu düzelt
            if (myCollider != null) myCollider.height = normalHeight;

            // Kamerayı düzelt
            cameraTransform.localPosition = new Vector3(0, 0.6f, 0); // Eski kameranın orijinal yeri
        }
    }

    void CalculateNoise()
    {
        // Hareket ediyor muyuz? (WASD tuşlarına basılıyor mu?)
        bool isMoving = Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;

        if (isMoving)
        {
            if (isCrouching)
            {
                currentNoiseRange = crouchNoiseRange; // En sessiz
            }
            else
            {
                // Shift'e basıyorsan koşma, basmıyorsan yürüme (Örnek)
                // Senin kodunda şimdilik sadece düz hareket var, o yüzden direkt koşma sayalım
                currentNoiseRange = runNoiseRange;
            }
        }
        else
        {
            currentNoiseRange = 0f; // Duruyorsak ses yok
        }

        // Burayı ileride "Eşya yere düştü" sesiyle birleştirebiliriz.
    }

}