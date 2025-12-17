using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic; // Sýralama için gerekli

public class BalloonGun : GadgetBase
{
    [Header("Silah Ayarlarý")]
    public GameObject balloonPrefab;
    public float range = 50f;

    public override void OnUseStart()
    {
        if (!IsOwner) return;

        Transform cam = ownerPlayer.cameraTransform;
        if (cam == null) return;

        // ESKÝ KOD: int layerMask = ~LayerMask.GetMask("Player"); 
        // Bu kod yüzünden düþmanlarý (Player layer) vuramýyordun.

        // --- YENÝ YÖNTEM: RAYCAST ALL (HER ÞEYÝ GÖR) ---
        Ray ray = new Ray(cam.position, cam.forward);

        // Yolumuzdaki her þeyi buluyoruz (Biz, Duvar, Düþman...)
        RaycastHit[] hits = Physics.RaycastAll(ray, range);

        // Mesafeye göre sýrala (Önce en yakýndakine bakalým)
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        foreach (var hit in hits)
        {
            // 1. KENDÝMÝZE ÇARPARSAK GEÇ
            // Iþýn kameradan çýktýðý için önce kendi sýrtýmýza çarpabilir. Bunu atlýyoruz.
            if (hit.transform.root == ownerPlayer.transform.root) continue;

            // 2. HEDEF BÝR OYUNCU VEYA KUTU MU?
            if (hit.rigidbody != null && hit.transform.TryGetComponent(out NetworkObject targetNetObj))
            {
                // Düþmaný bulduk! Server'a söyle.
                SpawnBalloonServerRpc(targetNetObj.NetworkObjectId, hit.point);
                return; // Tek mermi tek isabet, döngüden çýk.
            }

            // 3. DUVARA MI ÇARPTIK?
            // Rigidbody'si olmayan bir þeye (Duvar/Zemin) çarparsak mermi orada durmalý.
            // Yoksa duvarýn arkasýndaki adamý vururuz (Wallhack olur).
            if (hit.rigidbody == null)
            {
                return; // Mermi duvarda öldü.
            }
        }
    }

    public override void OnUseStop() { }

    [ServerRpc]
    void SpawnBalloonServerRpc(ulong targetId, Vector3 hitPoint)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObj))
        {
            GameObject balloon = Instantiate(balloonPrefab, hitPoint, Quaternion.identity);
            var balloonNetObj = balloon.GetComponent<NetworkObject>();
            balloonNetObj.Spawn();

            // Balonu hedefe yapýþtýr
            balloonNetObj.TrySetParent(targetObj.transform, true);

            var logic = balloon.GetComponent<BalloonLogic>();
            if (logic != null)
            {
                logic.AttachTo(targetObj.GetComponent<Rigidbody>());
            }
        }
    }
}