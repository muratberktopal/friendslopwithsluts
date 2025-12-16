using UnityEngine;

public class TorchFlicker : MonoBehaviour
{
    public float minIntensity = 1.5f; // En sönük hali
    public float maxIntensity = 3.0f; // En parlak hali
    public float flickerSpeed = 10f;  // Ne kadar hýzlý titresin?

    private Light fireLight;
    private float targetIntensity;

    void Start()
    {
        fireLight = GetComponent<Light>();
    }

    void Update()
    {
        // Rastgele bir parlaklýk seç ve ona doðru yumuþakça geçiþ yap
        targetIntensity = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * (maxIntensity - minIntensity) + minIntensity;
        fireLight.intensity = targetIntensity;
    }
}