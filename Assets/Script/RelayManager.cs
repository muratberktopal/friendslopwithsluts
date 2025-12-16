using UnityEngine;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models; // Burasý kalsa da aþaðýda tam adýný yazacaðýz
using Unity.Netcode.Transports.UTP;
using TMPro;

public class RelayManager : MonoBehaviour
{
    [Header("UI Elemanlarý")]
    public TMP_InputField joinCodeInput;
    public TextMeshProUGUI statusText;
    public GameObject buttonsPanel;

    async void Start()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    public async void CreateRelay()
    {
        try
        {
            // --- DÜZELTME BURADA ---
            // Sadece "Allocation" yazarsak karýþýyor.
            // Baþýna "Unity.Services.Relay.Models." ekleyerek "Relay'in Allocation'ý" dedik.
            Unity.Services.Relay.Models.Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            statusText.text = "Oda Kodu: " + joinCode;
            Debug.Log("Oda Kodu: " + joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );

            NetworkManager.Singleton.StartHost();
            buttonsPanel.SetActive(false);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            statusText.text = "Hata: Relay Kurulamadý.";
        }
    }

    public async void JoinRelay()
    {
        string joinCode = joinCodeInput.text;

        if (string.IsNullOrEmpty(joinCode)) return;

        try
        {
            Debug.Log("Odaya Katýlýnýyor: " + joinCode);

            // --- DÜZELTME BURADA ---
            // Sadece "JoinAllocation" yazarsak karýþýyor.
            // Baþýna tam adresini ekledik.
            Unity.Services.Relay.Models.JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );

            NetworkManager.Singleton.StartClient();
            buttonsPanel.SetActive(false);
            statusText.text = "Baðlandý!";
        }
        catch (RelayServiceException e)
        {
            Debug.LogError(e);
            statusText.text = "Hata: Kod Yanlýþ.";
        }
    }
}