using UnityEngine;
using Unity.Netcode;

public class PendulumTrap : NetworkBehaviour
{
    [Header("Sallanma Ayarlarý")]
    public float speed = 2.0f;      // Ne kadar hýzlý sallansýn?
    public float angleLimit = 75f;  // Kaç derece açýlsýn? (Saða 75, Sola 75)
    public Vector3 swingAxis = Vector3.forward; // Hangi eksende sallansýn? (Genelde Z ekseni: forward)

    [Header("Baþlangýç Ofseti")]
    public float timeOffset = 0f;   // Birden fazla sarkaç varsa hepsi ayný anda sallanmasýn diye

    private Quaternion startRotation;

    void Start()
    {
        startRotation = transform.rotation;
    }

    void Update()
    {
        // Hareketi sadece Sunucu hesaplar, NetworkTransform diðerlerine iletir.
        if (!IsServer) return;

        // Matematiksel Sarkaç Formülü: Sinüs Dalgasý
        // Time.time + offset diyerek zamaný kaydýrýyoruz
        float angle = angleLimit * Mathf.Sin((Time.time + timeOffset) * speed);

        // Hesaplanan açýyý baþlangýç rotasyonuna ekle
        transform.rotation = startRotation * Quaternion.AngleAxis(angle, swingAxis);
    }
}