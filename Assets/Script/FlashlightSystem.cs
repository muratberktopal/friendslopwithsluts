using UnityEngine;
using Unity.Netcode;

public class FlashlightSystem : NetworkBehaviour
{
    [Header("Baðlantýlar")]
    public GameObject torchObject; // 'TorchHolder' objesini buraya sürükle
    public AudioSource toggleSound; // Çakmak/Yanma sesi (Varsa)

    private NetworkVariable<bool> isTorchOn = new NetworkVariable<bool>(true);

    public override void OnNetworkSpawn()
    {
        UpdateTorchState(isTorchOn.Value);
        isTorchOn.OnValueChanged += OnLightStateChanged;
    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            ToggleTorchServerRpc();
        }
    }

    [ServerRpc]
    void ToggleTorchServerRpc()
    {
        isTorchOn.Value = !isTorchOn.Value;
    }

    void OnLightStateChanged(bool previousValue, bool newValue)
    {
        UpdateTorchState(newValue);
    }

    void UpdateTorchState(bool state)
    {
        if (torchObject != null)
        {
            // Sadece ýþýðý deðil, sopayý da gizle/göster
            torchObject.SetActive(state);

            if (toggleSound != null && state) toggleSound.Play();
        }
    }
}