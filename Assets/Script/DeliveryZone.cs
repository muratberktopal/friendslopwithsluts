using UnityEngine;
using Unity.Netcode;

public class DeliveryZone : NetworkBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; // Para iþlemlerini sadece sunucu yapar

        // Giren þey bir teslimat eþyasý mý?
        DeliverableItem item = other.GetComponent<DeliverableItem>();

        if (item != null)
        {
            // Eþyanýn deðerini al
            int value = item.itemData.value;

            // Parayý kasaya ekle
            if (GameEconomy.Instance != null)
            {
                GameEconomy.Instance.AddMoney(value);
            }

            // Eþyayý baþarýlý þekilde yok et (Despawn)
            // Not: Kýrýlma efekti olmadan sakince yok ediyoruz
            item.GetComponent<NetworkObject>().Despawn();

            Debug.Log($"{item.itemData.itemName} teslim edildi! +{value} Altýn");
        }
    }
}