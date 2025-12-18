using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            // Oyuncunun son noktasýný güncelle
            pc.lastCheckpointPos = transform.position;
            Debug.Log("Checkpoint Alýndý!");

            // Buraya istersen "Bayrak Rengi Deðiþsin" veya "Ses Çýksýn" kodu eklersin
        }
    }
}