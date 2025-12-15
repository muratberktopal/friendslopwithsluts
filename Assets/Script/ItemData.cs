using UnityEngine;

[CreateAssetMenu(fileName = "New Item Data", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public int value = 10; // Kaç para edecek?

    [Header("Fragility Settings")]
    public bool isFragile; // Kýrýlabilir mi? (Viski: Evet, Fýçý: Hayýr)
    public float breakForceThreshold = 5f; // Ne kadar sert çarparsa kýrýlsýn?
    public GameObject brokenPrefab; // Kýrýlýnca çýkacak efekt/parçalar
}