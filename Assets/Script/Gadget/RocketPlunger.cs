using UnityEngine;
using Unity.Netcode;

public class RocketPlunger : GadgetBase
{
    [Header("Roket Ayarlarý")]
    public float recoilForce = 50f; // Geri tepme gücü (Rocket Jump için yüksek olmalý)
    public GameObject rocketProjectilePrefab; // Fýrlatýlacak roket mermisi (Varsa)
    public Transform muzzlePoint; // Roketin çýkacaðý namlu ucu

    public override void OnUseStart()
    {
        if (!IsOwner) return;

        // 1. KAMERAYI AL
        Transform cam = ownerPlayer.cameraTransform;
        if (cam == null) return;

        // 2. GERÝ TEPME (RECOIL) - ASIL DÜZELTME BURADA
        // Karakterin yönünü deðil, KAMERANIN yönünü baz alýyoruz.
        // "-cam.forward" demek, kameranýn baktýðý yönün tam tersi demektir.
        Vector3 recoilDir = -cam.forward;

        // Rigidbody'yi bul ve güç uygula
        Rigidbody playerRb = ownerPlayer.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            // Havada daha iyi uçmak için mevcut dikey hýzý sýfýrlayabiliriz (Opsiyonel ama iyi hissettirir)
            // playerRb.linearVelocity = new Vector3(playerRb.linearVelocity.x, 0, playerRb.linearVelocity.z);

            playerRb.AddForce(recoilDir * recoilForce, ForceMode.Impulse);
        }

        // 3. ROKETÝ ATEÞLE (Server'a söyle)
        FireRocketServerRpc(cam.position, cam.forward);
    }

    public override void OnUseStop() { }

    [ServerRpc]
    void FireRocketServerRpc(Vector3 fireOrigin, Vector3 fireDirection)
    {
        // Burada roket mermisini oluþturabilirsin (Projectile Logic)
        // Þimdilik sadece debug mesajý verelim veya patlama efekti yapalým.

        // Eðer elinde bir roket mermisi prefabý varsa:
        /*
        GameObject rocket = Instantiate(rocketProjectilePrefab, fireOrigin + fireDirection * 1.5f, Quaternion.LookRotation(fireDirection));
        rocket.GetComponent<NetworkObject>().Spawn();
        rocket.GetComponent<Rigidbody>().linearVelocity = fireDirection * 20f; // Mermi hýzý
        */

        FireRocketClientRpc(fireOrigin, fireDirection);
    }

    [ClientRpc]
    void FireRocketClientRpc(Vector3 pos, Vector3 dir)
    {
        // Ses ve Efektler buraya (Herkes görsün)
        Debug.Log("BOOM! Roket ateþlendi.");
    }
}