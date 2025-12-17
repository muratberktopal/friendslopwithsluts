using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Takip Ayarlarý")]
    public Transform target; // Takip edilecek oyuncu (Otomatik bulunacak)
    public Vector3 offset = new Vector3(0, 1.5f, -3.5f); // Kameranýn duracaðý yer (Sað, Yukarý, Geri)

    [Header("Hassasiyet")]
    public float sensitivityX = 200f;
    public float sensitivityY = 150f;
    public float minY = -40f; // Aþaðý bakma limiti
    public float maxY = 80f;  // Yukarý bakma limiti

    [Header("Duvar Çarpýþmasý (Collision)")]
    public LayerMask collisionLayers; // Kamera hangi objelere çarpýp öne gelsin?
    public float collisionRadius = 0.2f; // Kameranýn kapsadýðý alan
    public float collisionOffset = 0.2f; // Duvardan ne kadar uzak dursun

    private float currentX = 0f;
    private float currentY = 0f;

    void Start()
    {
        // Mouse'u kilitle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Varsayýlan açý
        currentY = 20f;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. Mouse Input
        currentX += Input.GetAxis("Mouse X") * sensitivityX * Time.deltaTime;
        currentY -= Input.GetAxis("Mouse Y") * sensitivityY * Time.deltaTime;
        currentY = Mathf.Clamp(currentY, minY, maxY);

        // 2. Dönüþü Hesapla
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // 3. Hedef Pozisyonu Hesapla (Duvar kontrolü olmadan)
        // Target'ýn pozisyonuna offset ekliyoruz ama rotasyona göre
        Vector3 desiredPosition = target.position + rotation * offset;

        // 4. Duvar Çarpýþmasý (Wall Clip Prevention)
        // Karakterden kameraya bir ýþýn atýyoruz, arada duvar var mý?
        Vector3 direction = desiredPosition - (target.position + Vector3.up * 1.5f); // Karakterin boynundan hesapla
        float distance = direction.magnitude;

        RaycastHit hit;
        // Eðer arada "collisionLayers" katmanýnda bir engel varsa
        if (Physics.SphereCast(target.position + Vector3.up * 1.5f, collisionRadius, direction.normalized, out hit, distance, collisionLayers))
        {
            // Kamerayý duvarýn hemen önüne çek
            float hitDistance = hit.distance - collisionOffset;
            if (hitDistance < 0) hitDistance = 0;

            desiredPosition = (target.position + Vector3.up * 1.5f) + direction.normalized * hitDistance;
        }

        // 5. Uygula
        transform.position = desiredPosition;
        transform.LookAt(target.position + Vector3.up * 1.5f); // Karakterin kafasýna bak
    }

    // PlayerController bu fonksiyonu çaðýrýp "Beni takip et" diyecek
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}