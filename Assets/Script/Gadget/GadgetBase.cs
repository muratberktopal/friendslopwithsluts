using UnityEngine;
using Unity.Netcode;

public abstract class GadgetBase : NetworkBehaviour
{
    protected PlayerController ownerPlayer;

    [Header("Elde Duruþ Ayarlarý")]
    // Silah eline tam otursun diye ince ayar yapacaðýn yerler
    public Vector3 holdPositionOffset = Vector3.zero; // Örn: (0.2, -0.1, 0.5)
    public Vector3 holdRotationOffset = Vector3.zero; // Örn: (0, 90, 0) -> 90 derece döndür

    public virtual void OnEquip(PlayerController player)
    {
        ownerPlayer = player;
    }

    public virtual void OnDrop()
    {
        ownerPlayer = null;
    }

    public abstract void OnUseStart();
    public abstract void OnUseStop();
}