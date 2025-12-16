using UnityEngine;
using Unity.Netcode;

public class Rotator : NetworkBehaviour
{
    public float rotationSpeed = 100f; // Dönme hýzý
    public Vector3 rotationAxis = Vector3.up; // Hangi yöne dönecek? (Y = Etrafýnda)

    void Update()
    {
        // Sadece SERVER döndürür, diðerleri izler (Senkronizasyon için)
        if (!IsServer) return;

        // Objeyi kendi ekseninde döndür
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime);
    }
}