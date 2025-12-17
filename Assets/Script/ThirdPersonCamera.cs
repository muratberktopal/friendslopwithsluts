using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Takip edilecek oyuncu (PlayerController atayacak)

    [Header("Ayarlar")]
    public float distance = 6.0f; // Arkadan mesafe
    public float height = 2.5f;   // Yerden yükseklik
    public float rotationSpeed = 2.0f; // Mouse dönüþ hýzý

    private float currentX = 0f;
    private float currentY = 0f;

    void Start()
    {
        // Baþlangýç açýsý
        Vector3 angles = transform.eulerAngles;
        currentX = angles.y;
        currentY = angles.x;
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Mouse girdisini al
        currentX += Input.GetAxis("Mouse X") * rotationSpeed;
        currentY -= Input.GetAxis("Mouse Y") * rotationSpeed;

        // Y eksenini sýnýrla (Kameranýn yerin altýna girmemesi veya takla atmamasý için)
        currentY = Mathf.Clamp(currentY, -30, 60);

        // Rotasyonu hesapla
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // Pozisyonu hesapla (Hedefin konumu + Rotasyon * Mesafe)
        // target.position + Vector3.up * 1.5f -> Karakterin ayaklarýna deðil sýrtýna odaklan
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + (target.position + Vector3.up * 1.5f);

        transform.rotation = rotation;
        transform.position = position;
    }
}