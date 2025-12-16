using UnityEngine;

public class ItemWeight : MonoBehaviour
{
    [Header("Aðýrlýk Ayarý (0.0 - 0.9 Arasý)")]
    [Tooltip("0.1 = %10 Yavaþlatýr, 0.5 = %50 Yavaþlatýr")]
    [Range(0f, 0.9f)]
    public float slowdownPercentage = 0.5f;
}