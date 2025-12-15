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
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        if (players.Length == 0) return;

        float closestDistance = Mathf.Infinity;
        Transform closestTransform = null;

        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance && distance < detectRange)
            {
                closestDistance = distance;
                closestTransform = player.transform;
            }
        }
        targetPlayer = closestTransform;
    }
}