using UnityEngine;
using Unity.Netcode;

public class DeliverableItem : NetworkBehaviour
{
    public ItemData itemData; // Yukarýda oluþturduðun veriyi buraya sürükle
    private bool isBroken = false;

    // Çarpýþma olduðunda çalýþýr (Sadece Server yönetir)
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return; // Kýrýlma kararý sunucunundur
        if (isBroken) return; // Zaten kýrýldýysa iþlem yapma
        if (!itemData.isFragile) return; // Eþya saðlam bir þeyse (Fýçý) kýrýlmaz

        // Çarpýþma þiddetini ölç (Hýzýn büyüklüðü)
        float impactForce = collision.relativeVelocity.magnitude;

        // Eðer eþya oyuncunun elindeyse ve sadece sürtünüyorsa kýrýlmasýn diye
        // sadece sert vuruþlarý kontrol ediyoruz.
        if (impactForce > itemData.breakForceThreshold)
        {
            BreakObject();
        }
    }

    private void BreakObject()
    {
        isBroken = true;
        Debug.Log($"{itemData.itemName} kýrýldý! Para yok.");

        // Kýrýlma efektini herkese göster (ClientRpc)
        SpawnBrokenEffectClientRpc();

        // Nesneyi yok et
        GetComponent<NetworkObject>().Despawn();
    }

    [ClientRpc]
    private void SpawnBrokenEffectClientRpc()
    {
        // Ses çalma veya parçacýk efekti burada yapýlabilir
        if (itemData.brokenPrefab != null)
        {
            Instantiate(itemData.brokenPrefab, transform.position, transform.rotation);
        }
    }
}