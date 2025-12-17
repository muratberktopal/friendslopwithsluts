using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

public class PlayerController : NetworkBehaviour
{
    [Header("--- HAREKET AYARLARI ---")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 40f;
    [SerializeField] private float airAcceleration = 20f;
    [SerializeField] private float rotationSpeed = 15f;

    [Header("--- MUHTEŞEM ZIPLAMA (CELESTE FEEL) ---")]
    [SerializeField] private float jumpForce = 14f;
    [SerializeField] private float fallMultiplier = 2.5f;
    [SerializeField] private float lowJumpMultiplier = 3f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float coyoteTime = 0.15f;
    [SerializeField] private float apexGravityMultiplier = 0.5f;
    [SerializeField] private float rayLength = 1.3f;

    [Header("--- ETKİLEŞİM & RAGDOLL ---")]
    public float pushForce = 60f;           // Diğer scriptler erişebilsin diye public
    public float throwForce = 20f;          // Diğer scriptler erişebilsin diye public
    public float pushRange = 4.0f;
    public float pushUpwardModifier = 5f;
    public float ragdollDuration = 3.0f;

    [Header("--- BAĞLANTILAR ---")]
    public Transform handPosition;          // Diğer scriptler erişebilsin diye public
    public Transform cameraTransform;       // Hata veren 'cameraTransform' burası!
    [SerializeField] private LayerMask groundLayer;

    // --- AI ve DİĞER SCRİPTLER İÇİN VERİLER ---
    public float currentNoiseRange { get; private set; } // Hata veren 'currentNoiseRange' burası!

    // İç Değişkenler
    private Rigidbody myRb;
    private Transform currentlyHeldObject;
    private GadgetBase currentGadget;
    private bool isRagdolled = false;
    private float jumpBufferCounter;
    private float coyoteTimeCounter;
    private bool isGrounded;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                var camScript = Camera.main.GetComponent<ThirdPersonCamera>();
                if (camScript != null) camScript.target = this.transform;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Awake()
    {
        myRb = GetComponent<Rigidbody>();
        myRb.freezeRotation = true;
        myRb.useGravity = false; // Custom gravity kullanıyoruz
    }

    void Update()
    {
        // Eşya Takibi
        if (currentlyHeldObject != null)
        {
            currentlyHeldObject.position = handPosition.position;
            currentlyHeldObject.rotation = handPosition.rotation;
        }

        if (!IsOwner || isRagdolled) return;

        CheckStates();
        HandleInputs();
    }

    void FixedUpdate()
    {
        if (!IsOwner || isRagdolled) return;

        ApplyMovement();
        ApplyCustomGravity();
    }

    private void CheckStates()
    {
        isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, rayLength, groundLayer);

        if (isGrounded) coyoteTimeCounter = coyoteTime;
        else coyoteTimeCounter -= Time.deltaTime;

        if (Input.GetButtonDown("Jump")) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;
    }

    private void HandleInputs()
    {
        if (jumpBufferCounter > 0f && coyoteTimeCounter > 0f) Jump();

        if (Input.GetKeyDown(KeyCode.E)) TryPickup();
        if (Input.GetMouseButtonDown(0)) HandleAction();

        if (currentGadget != null)
        {
            if (Input.GetMouseButtonDown(1)) currentGadget.OnUseStart();
            if (Input.GetMouseButtonUp(1)) currentGadget.OnUseStop();
        }
    }

    private void ApplyMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (cameraTransform == null) return;

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();

        Vector3 targetDir = (camForward * v + camRight * h).normalized;
        float currentAccel = isGrounded ? acceleration : airAcceleration;

        // Durma (Deceleration) kontrolü
        if (targetDir.magnitude < 0.1f) currentAccel = deceleration;

        Vector3 targetVel = targetDir * moveSpeed;
        Vector3 currentHorizontalVel = new Vector3(myRb.linearVelocity.x, 0, myRb.linearVelocity.z);
        Vector3 newVel = Vector3.MoveTowards(currentHorizontalVel, targetVel, currentAccel * Time.fixedDeltaTime);

        myRb.linearVelocity = new Vector3(newVel.x, myRb.linearVelocity.y, newVel.z);

        if (targetDir.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
            currentNoiseRange = Input.GetKey(KeyCode.LeftShift) ? 20f : 10f;
        }
        else currentNoiseRange = 0f;
    }

    private void ApplyCustomGravity()
    {
        float gravity = Physics.gravity.y;
        if (myRb.linearVelocity.y < 0) gravity *= fallMultiplier;
        else if (myRb.linearVelocity.y > 0 && !Input.GetButton("Jump")) gravity *= lowJumpMultiplier;
        else if (Mathf.Abs(myRb.linearVelocity.y) < 1f) gravity *= apexGravityMultiplier;

        myRb.linearVelocity += Vector3.up * gravity * Time.fixedDeltaTime;
    }

    private void Jump()
    {
        myRb.linearVelocity = new Vector3(myRb.linearVelocity.x, jumpForce, myRb.linearVelocity.z);
        jumpBufferCounter = 0;
        coyoteTimeCounter = 0;
        currentNoiseRange = 30f;
    }

    // --- ETKİLEŞİM VE RPC'LER ---

    private void HandleAction()
    {
        if (currentlyHeldObject != null) ThrowObjectServerRpc();
        else TryPushPlayer();
    }

    private void TryPickup()
    {
        if (currentlyHeldObject != null || cameraTransform == null) return;
        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, 4f, ~LayerMask.GetMask("Player")))
        {
            if (hit.transform.TryGetComponent(out NetworkObject netObj)) RequestPickupServerRpc(netObj.NetworkObjectId);
        }
    }

    [ServerRpc]
    void RequestPickupServerRpc(ulong objectId, ServerRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            netObj.ChangeOwnership(rpcParams.Receive.SenderClientId);
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
            PerformDropLogic();
            if (rb != null)
            {
                Vector3 throwDir = (transform.forward + Vector3.up * 0.3f).normalized;
                rb.AddForce(throwDir * throwForce, ForceMode.Impulse);
            }
        }
    }

    private void PerformDropLogic()
    {
        if (currentlyHeldObject != null)
        {
            NetworkObject netObj = currentlyHeldObject.GetComponent<NetworkObject>();
            if (netObj != null) { netObj.TryRemoveParent(); TogglePhysicsClientRpc(netObj.NetworkObjectId, true); }
        }
        ClearHeldObjectClientRpc();
    }

    [ClientRpc] void ClearHeldObjectClientRpc() { currentlyHeldObject = null; currentGadget = null; }

    [ClientRpc]
    void TogglePhysicsClientRpc(ulong objectId, bool isPhysicsOn)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject netObj))
        {
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = !isPhysicsOn; rb.useGravity = isPhysicsOn; }
            foreach (var col in netObj.GetComponentsInChildren<Collider>()) col.enabled = isPhysicsOn;

            if (!isPhysicsOn)
            {
                currentlyHeldObject = netObj.transform;
                netObj.transform.SetParent(transform);
                netObj.transform.localPosition = Vector3.zero;
                netObj.transform.localRotation = Quaternion.identity;
                currentGadget = netObj.GetComponent<GadgetBase>();
                if (currentGadget != null) currentGadget.OnEquip(this);
            }
            else netObj.transform.SetParent(null);
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
            targetNetObj.GetComponent<PlayerController>()?.GetHitClientRpc(forceVector);
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
        if (currentlyHeldObject != null) PerformDropLogic();
        myRb.freezeRotation = false;
        myRb.constraints = RigidbodyConstraints.None;
        myRb.linearVelocity = Vector3.zero;
        myRb.AddForce(force + Vector3.up * 5f, ForceMode.Impulse);

        yield return new WaitForSeconds(ragdollDuration);

        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        myRb.freezeRotation = true;
        myRb.constraints = RigidbodyConstraints.FreezeRotation;
        myRb.linearVelocity = Vector3.zero;
        myRb.angularVelocity = Vector3.zero;
        isRagdolled = false;
    }
    // --- GİZMO (GÖRSELLEŞTİRME) ---
    private void OnDrawGizmos()
    {
        // Eğer oyun çalışmıyorsa veya karakter seçili değilse hata vermemesi için kontrol
        // (Raycast başlangıç pozisyonunu koddakiyle birebir aynı yapıyoruz)
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        Vector3 rayDirection = Vector3.down * rayLength;

        // Zemine değip değmediğine göre renk değiştir (Yeşil: Değiyor, Kırmızı: Havada)
        // Not: isGrounded sadece oyun çalışırken güncellenir.
        Gizmos.color = isGrounded ? Color.green : Color.red;

        // Çizgiyi çiz
        Gizmos.DrawRay(rayStart, rayDirection);

        // Işının bittiği noktaya küçük bir küre ekleyelim ki tam mesafeyi görebilelim
        Gizmos.DrawWireSphere(rayStart + rayDirection, 0.1f);
    }
}