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

    [Header("Eğilme Ayarları")]
    public float crouchHeight = 1.0f;
    public float standingHeight = 2.0f;
    public float crouchTransitionSpeed = 10f;
    private CapsuleCollider myCollider;
    private Vector3 originalCameraPos;

    [Header("Ses / Gürültü Ayarları (YENİ)")]
    // Düşmanın okuyacağı değişken bu:
    public float currentNoiseRange = 0f;

    [Header("Mouse Ayarları")]
    public float mouseSensitivity = 100f;
    private float xRotation = 0f;

    [Header("Bağlantılar")]
    public Transform cameraTransform;
    public Transform handPosition;

    private bool isRagdolled = false;
    private Rigidbody myRb;
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
        // --- ZIPLAMA ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            float rayLength = 1.3f;
            // Zıplayınca ANLIK olarak çok ses çıkar (Gürültü: 30)
            currentNoiseRange = 30f;
            // Sesin hemen sönmesi için bir Coroutine başlatılabilir ama şimdilik Update sıfırlayacak.

            bool isTooHeavy = currentMoveSpeed < (baseMoveSpeed * 0.3f);
            if (!isTooHeavy && Physics.Raycast(transform.position, Vector3.down, rayLength))
            {
                GetComponent<Rigidbody>().AddForce(Vector3.up * 800f, ForceMode.Impulse);
            }
        }

        if (!IsOwner) return;
        if (isRagdolled) return;

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

        // --- HAREKET VE GÜRÜLTÜ HESABI ---

        float finalSpeed = currentMoveSpeed;
        bool isCrouching = Input.GetKey(KeyCode.LeftControl);
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        // HAREKET INPUTU VAR MI? (WASD'ye basılıyor mu?)
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        bool isMoving = (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f);

        // Gürültüyü Sıfırla
        currentNoiseRange = 0f;

        if (isCrouching)
        {
            finalSpeed *= crouchMultiplier;

            // Eğilme Fiziği
            myCollider.height = Mathf.Lerp(myCollider.height, crouchHeight, Time.deltaTime * crouchTransitionSpeed);
            myCollider.center = Vector3.Lerp(myCollider.center, new Vector3(0, -0.5f, 0), Time.deltaTime * crouchTransitionSpeed);
            Vector3 crouchCamPos = new Vector3(originalCameraPos.x, originalCameraPos.y - 0.5f, originalCameraPos.z);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, crouchCamPos, Time.deltaTime * crouchTransitionSpeed);

            // Eğilerek yürüyorsa ÇOK AZ ses çıkar
            if (isMoving) currentNoiseRange = 2f;
        }
        else
        {
            // Normal Duruş
            myCollider.height = Mathf.Lerp(myCollider.height, standingHeight, Time.deltaTime * crouchTransitionSpeed);
            myCollider.center = Vector3.Lerp(myCollider.center, Vector3.zero, Time.deltaTime * crouchTransitionSpeed);
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, originalCameraPos, Time.deltaTime * crouchTransitionSpeed);

            if (isRunning)
            {
                finalSpeed *= runMultiplier;
                // Koşuyorsa ÇOK ses çıkar
                if (isMoving) currentNoiseRange = 20f;
            }
            else
            {
                // Normal yürüyorsa ORTA ses çıkar
                if (isMoving) currentNoiseRange = 10f;
            }
        }

        // Hareketi Uygula
        Vector3 move = transform.right * x + transform.forward * z;
        transform.position += move * finalSpeed * Time.deltaTime;

        // --- ETKİLEŞİM ---
        if (Input.GetKeyDown(KeyCode.E)) TryPickup();
        if (Input.GetMouseButtonDown(0)) ThrowObjectServerRpc();
    }

    // --- BURADAN AŞAĞISI AYNI (RPC'ler) ---
    // (Kodun devamı öncekiyle aynı, yer kaplamasın diye kısalttım, sen eskisini silip bunu yapıştırınca düzelir)
    // Sadece yukarıdaki değişkenler ve Update kısmı önemli.

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

        // 1. RAGDOLL OLUNCA: Tüm kilitleri kaldır (Serbestçe yuvarlansın)
        myRb.constraints = RigidbodyConstraints.None;

        myRb.AddForce(force, ForceMode.Impulse);

        yield return new WaitForSeconds(3.0f);

        // --- TOPARLANMA ---
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        transform.position += Vector3.up * 1.0f;

        // 2. AYAĞA KALKINCA: X, Z ve Y'yi tekrar kilitle!
        // (Y'yi kilitlemezsek yine kendi kendine dönmeye başlar)
        myRb.constraints = RigidbodyConstraints.FreezeRotation;

        // Hızları sıfırla
        myRb.linearVelocity = Vector3.zero; // Unity 6 (Eski sürümse: myRb.velocity)
        myRb.angularVelocity = Vector3.zero; // Dönme hızını sıfırla (ÖNEMLİ)

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
        if (IsOwner)
        {
            currentlyHeldObject = null;
            currentMoveSpeed = baseMoveSpeed;
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