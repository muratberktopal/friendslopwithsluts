using UnityEngine;

public class ConveyorBelt : MonoBehaviour
{
    public float speed = 3.0f;       // Bandýn hýzý
    public Vector3 direction = Vector3.back; // Hangi yöne kaydýrsýn?

    // Rigidbody'si olan herhangi bir þey (Oyuncu, Kutu, Kaya) dokunursa taþýr
    void OnCollisionStay(Collision collision)
    {
        // Server/Client fark etmez, fizik her yerde çalýþmalý (Client-side prediction için)

        Rigidbody targetRb = collision.gameObject.GetComponent<Rigidbody>();
        if (targetRb != null && !targetRb.isKinematic)
        {
            // Oyuncunun pozisyonunu bandýn yönüne doðru kaydýr
            // MovePosition kullanmýyoruz çünkü oyuncunun kendi hareketini bozabilir.
            // Bunun yerine pozisyonuna minik eklemeler yapýyoruz.

            // YÖNTEM 1: Transform Kaydýrma (Daha pürüzsüz)
            collision.transform.position += direction.normalized * speed * Time.deltaTime;
        }
    }
}