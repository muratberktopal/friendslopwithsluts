using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent] // Ayný objeye iki kere eklenmesin
public class ClientNetworkTransform : NetworkTransform
{
    // Otoriteyi Server'dan alýp Client'a veriyoruz
    protected override bool OnIsServerAuthoritative()
    {
        return false; // FALSE = Client kendi hareket edebilir
    }
}