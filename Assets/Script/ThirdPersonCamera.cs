using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target; // Takip edilecek karakter (PlayerController atayacak)

    [Header("Ayarlar")]
    public float distance = 5.0f; // Karakterden ne kadar uzakta?
    public float sensitivity = 2.0f; // Mouse hýzý
    public Vector2 pitchLimits = new Vector2(-40, 85); // Aþaðý/Yukarý bakma sýnýrý

    private float currentX = 0f;
    private float currentY = 0f;

    void LateUpdate()
    {
        if (target == null) return;

        // Mouse girdisini al
        currentX += Input.GetAxis("Mouse X") * sensitivity;
        currentY -= Input.GetAxis("Mouse Y") * sensitivity;

        // Y eksenini (yukarý/aþaðý) sýnýrla ki takla atmasýn
        currentY = Mathf.Clamp(currentY, pitchLimits.x, pitchLimits.y);

        // Mesafeyi ve dönüþü hesapla
        Vector3 dir = new Vector3(0, 0, -distance);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);

        // Kamerayý konumlandýr: Hedefin pozisyonu + Rotasyon * Mesafe
        // Vector3.up * 1.5f eklememizin sebebi ayaklarýna deðil, sýrtýna/kafasýna odaklanmasý
        transform.position = target.position + (Vector3.up * 1.5f) + rotation * dir;

        // Kamerayý hedefe çevir
        transform.LookAt(target.position + Vector3.up * 1.5f);
    }
}