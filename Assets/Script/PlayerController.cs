using UnityEngine;
using Unity.Netcode;

public class PlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float throwForce = 15f;

    [Header("Pickup Settings")]
    public Transform cameraTransform;
    public Transform handPosition; // K�p�n duraca�� hedef nokta
    public float pickupRange = 4f;
    public float holdForce = 150f; // Nesneyi tutma g�c� (H�zlanma)
    public float holdDamper = 10f; // Titremeyi �nleyen s�rt�nme
    public float rotationSpeed = 10f; // Nesnenin d�nme h�z�

    // Sunucu taraf�nda hangi objeyi tuttu�umuzu bilmek i�in
    private NetworkVariable<NetworkObjectReference> currentHeldObjectRef = new NetworkVariable<NetworkObjectReference>();

    // Lokal referanslar
    private Rigidbody heldRb;
    private Collider playerCollider;

    void Awake()
    {
        playerCollider = GetComponent<Collider>();
    }

    public override void OnNetworkSpawn()
    {
        // Held object de�i�ti�inde tetiklenecek olay
        currentHeldObjectRef.OnValueChanged += OnHeldObjectChanged;
    }

    void Update()
    {
        if (!IsOwner) return;

        HandleMovement();
        HandleInteraction();
    }

    void FixedUpdate()
    {
        // Fizik i�lemleri sadece Sunucuda �al���r (Server Authoritative Physics)
        if (IsServer && heldRb != null)
        {
            MoveObjectToHand();
        }
    }

    // --- Hareket ---
    void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * moveSpeed * Time.deltaTime;

        float mouseX = Input.GetAxis("Mouse X");
        transform.Rotate(Vector3.up * mouseX * 2f);
    }

    // --- Etkile�im ---
    void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // E�er elimizde bir �ey varsa b�rak, yoksa almay� dene
            bool isHolding = currentHeldObjectRef.Value.TryGet(out NetworkObject result);

            if (isHolding)
            {
                RequestDropServerRpc();
            }
            else
            {
                TryPickup();
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            RequestThrowServerRpc();
        }
    }

    void TryPickup()
    {
        // Raycast atarken kendi Layer'�n� (Player) ignore etmek iyi fikirdir.
        // �imdilik basit�e maske kullanmadan at�yoruz ama LayerMask eklemen �nerilir.
        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, pickupRange))
        {
            if (hit.transform.TryGetComponent(out NetworkObject netObj))
            {
                RequestPickupServerRpc(netObj.NetworkObjectId);
            }
        }
    }

    // --- Server Mant��� ---

    [ServerRpc]
    void RequestPickupServerRpc(ulong objectId)
    {
        // Zaten bir �ey tutuyorsak alma
        NetworkObject currentObj;
        if (currentHeldObjectRef.Value.TryGet(out currentObj)) return;

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            // Nesneyi tutulan olarak i�aretle
            currentHeldObjectRef.Value = netObj;

            // Rigidbody ayarlar�n� yap
            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false; // Yer�ekimini kapat ki d��mesin
                rb.linearDamping = holdDamper; // Havada s�z�l�rken titremesin diye s�rt�nme ekle
                rb.angularDamping = 5f; // D�nmeyi yava�lat

                // �OK �NEML�: Nesne oyuncuya �arp�p oyuncuyu itmesin diye collision'� ignore et
                Collider objCol = netObj.GetComponent<Collider>();
                if (playerCollider != null && objCol != null)
                {
                    Physics.IgnoreCollision(playerCollider, objCol, true);
                }
            }
        }
    }

    [ServerRpc]
    void RequestDropServerRpc()
    {
        ClearHeldObject(false);
    }

    [ServerRpc]
    void RequestThrowServerRpc()
    {
        // F�rlatma i�lemi i�in nesne referans�n� al ve temizle
        if (currentHeldObjectRef.Value.TryGet(out NetworkObject netObj))
        {
            var rb = netObj.GetComponent<Rigidbody>();
            ClearHeldObject(true); // �nce ba�lant�y� kopar

            if (rb != null)
            {
                // �leri do�ru f�rlat
                rb.AddForce(cameraTransform.forward * throwForce, ForceMode.Impulse);
            }
        }
    }

    // Sunucuda her fizik ad�m�nda �al���r: Nesneyi el pozisyonuna �eker
    void MoveObjectToHand()
    {
        if (heldRb == null) return;

        // Hedef pozisyon (El) ile Mevcut pozisyon aras�ndaki fark
        Vector3 direction = handPosition.position - heldRb.position;
        float distance = direction.magnitude;

        // Nesneyi hedefe do�ru it (Velocity kullanarak)
        heldRb.linearVelocity = direction * holdForce * Time.fixedDeltaTime;

        // Rotasyonu da kameran�n bakt��� y�ne yava��a �evir
        Quaternion targetRot = handPosition.rotation;
        heldRb.MoveRotation(Quaternion.Slerp(heldRb.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime));
    }

    // Nesneyi serbest b�rakma mant��� (Drop veya Throw)
    void ClearHeldObject(bool isThrowing)
    {
        if (currentHeldObjectRef.Value.TryGet(out NetworkObject netObj))
        {
            var rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = true; // Yer�ekimini geri a�
                rb.linearDamping = 0f; // S�rt�nmeyi s�f�rla
                rb.angularDamping = 0.05f;

                // Collision ignore'u kald�r (Art�k oyuncuya �arpabilir)
                Collider objCol = netObj.GetComponent<Collider>();
                if (playerCollider != null && objCol != null)
                {
                    Physics.IgnoreCollision(playerCollider, objCol, false);
                }
            }

            // Referans� bo�alt
            currentHeldObjectRef.Value = new NetworkObjectReference();
        }
    }

    // NetworkVariable de�i�ti�inde (Pickup/Drop oldu�unda) Sunucuda cache g�ncellemesi
    // Bu, FixedUpdate d�ng�s�nde s�rekli GetComponent �a��rmamak i�in optimizasyon
    void OnHeldObjectChanged(NetworkObjectReference oldVal, NetworkObjectReference newVal)
    {
        if (IsServer)
        {
            if (newVal.TryGet(out NetworkObject netObj))
            {
                heldRb = netObj.GetComponent<Rigidbody>();
            }
            else
            {
                heldRb = null;
            }
        }
    }
}