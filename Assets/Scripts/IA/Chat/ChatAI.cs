using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class ChatAI : NetworkBehaviour
{
    public float speed = 2f;          // vitesse de déplacement du Chat
    public float stopDistance = 1.5f; // distance minimale avant de s'arrêter
    public float rotationSpeed = 5f;  // vitesse de rotation du chat
    private Transform target;         // cible actuelle (Player Ombre)
    private float soundTimer = 0f;    // chronomètre pour le son
    private float soundInterval = 3f; // intervalle entre les sons (3 secondes)
    private Rigidbody rb;             // composant Rigidbody

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Empêcher le chat de tomber (local)
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Ne traiter les triggers que sur le serveur
        if (!IsServer) return;

        ShadowPlayer shadow = other.GetComponent<ShadowPlayer>();
        if (shadow != null)
        {
            target = other.transform;

            // Demander aux clients de jouer le son
            PlaySoundClientRpc();

            // Réinitialiser le chronomètre
            soundTimer = 0f;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        if (other.GetComponent<ShadowPlayer>() != null)
        {
            target = null;
        }
    }

    void Update()
    {
        // Toute la logique de mouvement/son est exécutée côté serveur
        if (!IsServer) return;

        if (target != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            // Vérifier si le chat n'est pas trop proche
            if (distanceToTarget > stopDistance)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target.position,
                    speed * Time.deltaTime
                );

                // Faire tourner le chat vers la cible
                Vector3 directionToTarget = (target.position - transform.position).normalized;
                if (directionToTarget.sqrMagnitude > 0f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                }
            }

            // Gérer le son toutes les 3 secondes
            soundTimer += Time.deltaTime;
            if (soundTimer >= soundInterval)
            {
                PlaySoundClientRpc();
                soundTimer = 0f;
            }

            Debug.Log("Chat (serveur) poursuit l'ombre. Position actuelle : " + transform.position);
        }
    }

    [ClientRpc]
    private void PlaySoundClientRpc()
    {
        GetComponent<AudioSource>()?.Play();
    }
}