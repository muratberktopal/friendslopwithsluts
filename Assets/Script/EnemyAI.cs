using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;

public class EnemyAI : NetworkBehaviour
{
    private NavMeshAgent agent;
    private Transform targetPlayer;

    [Header("Ayarlar")]
    public float detectRange = 20f;
    public float hitDistance = 2.0f;
    public float hitCooldown = 2.0f;

    // Vurma Gücü (Karakteri uçurmak için)
    public float hitForce = 15f;
    public float liftForce = 10f; // Havaya dikme gücü

    private bool canHit = true;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (!IsServer) return;

        FindClosestPlayer();

        if (targetPlayer != null)
        {
            agent.SetDestination(targetPlayer.position);

            float distance = Vector3.Distance(transform.position, targetPlayer.position);

            // Menzildeyse ve bekleme süresi bittiyse VUR
            if (distance <= hitDistance && canHit)
            {
                StartCoroutine(HitRoutine(targetPlayer.gameObject));
            }
        }
    }

    IEnumerator HitRoutine(GameObject victim)
    {
        canHit = false;
        agent.isStopped = true; // Vururken dur

        // Oyuncunun scriptine ulaş
        var playerScript = victim.GetComponent<PlayerController>();

        if (playerScript != null)
        {
            // Vuruş Yönünü Hesapla (Düşmandan Oyuncuya doğru)
            Vector3 direction = (victim.transform.position - transform.position).normalized;

            // Güç Vektörü: Biraz Geriye + Biraz Yukarıya
            Vector3 finalForce = (direction * hitForce) + (Vector3.up * liftForce);

            // OYUNCUYA EMİR VER: "Şamar yedin, ragdoll ol!"
            playerScript.GetHitClientRpc(finalForce);
        }

        // Bekleme süresi (Cooldown)
        yield return new WaitForSeconds(hitCooldown);

        agent.isStopped = false;
        canHit = true;
    }

    void FindClosestPlayer()
    {
        // "Player" tag'i olan herkesi bul
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0) return;

        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (GameObject player in players)
        {
            // Oyuncunun scriptine ulaşıp ne kadar gürültü yaptığını soralım
            var pController = player.GetComponent<PlayerController>();
            if (pController == null) continue; // Script yoksa geç

            // Aramızdaki mesafe kaç metre?
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

            // KRİTİK NOKTA: 
            // Eğer mesafe, oyuncunun çıkardığı gürültüden KÜÇÜKSE düşman duyar.
            // Örnek: Mesafe 15m. Oyuncu koşuyor (Gürültü 20m). 15 < 20 -> DUYDU!
            // Örnek: Mesafe 5m. Oyuncu eğiliyor (Gürültü 3m). 5 < 3 -> DUYMADI! (False)

            if (distanceToPlayer < pController.currentNoiseRange)
            {
                // Duyulanlar arasında en yakın olanı seç
                if (distanceToPlayer < closestDistance)
                {
                    closestDistance = distanceToPlayer;
                    bestTarget = player.transform;
                }
            }
        }

        // Eğer kimseyi duyamadıysam hedefimi boşalt (Veya devriye gezmeye devam et)
        targetPlayer = bestTarget;

        // Eğer hedef yoksa olduğu yerde dursun (veya rastgele gezsin)
        if (targetPlayer == null)
        {
            agent.isStopped = true;
        }
        else
        {
            agent.isStopped = false;
        }
    }
}