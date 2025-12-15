using UnityEngine;
using Unity.Netcode;

public class GameEconomy : NetworkBehaviour
{
    public static GameEconomy Instance;

    // Parayý að üzerinde senkronize tutuyoruz
    public NetworkVariable<int> totalMoney = new NetworkVariable<int>(0);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddMoney(int amount)
    {
        if (IsServer)
        {
            totalMoney.Value += amount;
            Debug.Log($"Kasa Güncellendi: {totalMoney.Value} Altýn");
        }
    }

    // UI'da göstermek istersen
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 190, 50));
        GUILayout.Box($"Kasa: {totalMoney.Value} Altýn");
        GUILayout.EndArea();
    }
}