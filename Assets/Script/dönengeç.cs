using UnityEngine;
using Unity.Netcode;

public class dönengeç : NetworkBehaviour
{
    public Vector3 rotationSpeed = new Vector3(0, 0, 100); // Dönüş hızı

    void FixedUpdate()
    {
        // Sadece Server fiziği yönetir
        if (!IsServer) return;

        // Rigidbody ile döndür ki üzerindeki oyuncuyu da etkilesin
        Quaternion deltaRotation = Quaternion.Euler(rotationSpeed * Time.fixedDeltaTime);
        GetComponent<Rigidbody>().MoveRotation(GetComponent<Rigidbody>().rotation * deltaRotation);
    }
}