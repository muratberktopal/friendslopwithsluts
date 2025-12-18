using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class FinishZone : NetworkBehaviour
{
    private bool levelFinished = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || levelFinished) return;

        if (other.CompareTag("Player"))
        {
            levelFinished = true;
            ulong winnerId = other.GetComponent<NetworkObject>().OwnerClientId;

            // Kazananý duyur
            AnnounceWinnerClientRpc(winnerId);
        }
    }

    [ClientRpc]
    void AnnounceWinnerClientRpc(ulong winnerId)
    {
        Debug.Log($"BÖLÜM BÝTTÝ! KAZANAN: {winnerId}");

        // Ekrana yazý yazdýrabilirsin (UI)
        // Kaybedenleri havaya uçurabilirsin
    }
}