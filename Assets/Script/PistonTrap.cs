using UnityEngine;
using Unity.Netcode;

public class PistonTrap : NetworkBehaviour
{
    [Header("Hareket Ayarlarý")]
    public Vector3 moveDirection = Vector3.forward; // Hangi yöne çýkacak?
    public float distance = 3f;  // Ne kadar ileri gitsin?
    public float speed = 2f;     // Ne kadar hýzlý?
    public float startOffset = 0f; // Farklý pistonlar farklý zamanda çýksýn diye

    private Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void FixedUpdate()
    {
        if (!IsServer) return;

        // Sinüs dalgasý ile git-gel hareketi (PingPong)
        // startOffset ekleyerek hepsinin ayný anda çýkmasýný engelleriz
        float cycle = Mathf.Sin((Time.time + startOffset) * speed);

        // 0 ile 1 arasýna çekip mesafe ile çarpýyoruz (Basit matematik)
        // Sinüs -1 ile 1 arasýdýr, (cycle + 1) / 2 yaparak 0-1 yaparýz.
        float normalizedCycle = (cycle + 1f) / 2f;

        Vector3 targetPos = startPos + (moveDirection.normalized * distance * normalizedCycle);

        // Rigidbody ile taþý ki oyuncuyu içinden geçirmesin, ÝTSÝN.
        GetComponent<Rigidbody>().MovePosition(targetPos);
    }
}