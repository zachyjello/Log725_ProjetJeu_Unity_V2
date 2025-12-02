using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkIdentity))]
public class ChatAI : NetworkBehaviour
{
    public float speed = 2f;          // vitesse de déplacement du Chat
    public float stopDistance = 1.5f; // distance minimale avant de s'arrêter
    public float rotationSpeed = 5f;  // vitesse de rotation du chat
    public float soundInterval = 3f;  // intervalle entre les sons (3 secondes)

    private Transform target;         // cible actuelle (Player Ombre)
    private float soundTimer = 0f;    // chronomètre pour le son
    private Rigidbody rb;             // composant Rigidbody
    
    [Header("Audio")]
    public AudioClip chatSound;
    public string audioEventName = "ChatClip";

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Empêcher le chat de tomber
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }

        Debug.Log($"[ChatAI] Start - isServer:{isServer} isClient:{isClient} isLocalPlayer:{isLocalPlayer}");
    }

    void OnTriggerEnter(Collider other)
    {
        // Autorité uniquement côté serveur
        if (!isServer) return;

        if (other.TryGetComponent<ShadowPlayer>(out var shadow))
        {
            target = other.transform;
            soundTimer = 0f;

            // Déclenche le son sur tous les clients
            CmdPlayChatSound();
            Debug.Log("[ChatAI] Cible détectée (server).");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!isServer) return;

        if (other.TryGetComponent<ShadowPlayer>(out _))
        {
            target = null;
            Debug.Log("[ChatAI] Cible perdue (server).");
        }
    }

    void Update()
    {
        // IMPORTANT: toute la logique d'IA s'exécute côté serveur uniquement
        if (!isServer) return;

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
                CmdPlayChatSound();
                soundTimer = 0f;
            }
        }
    }

    [Command(requiresAuthority = false)]
    void CmdPlayChatSound()
    {
        // Jouer le son sur tous les clients
        RpcPlaySound();
    }

    [ClientRpc]
    void RpcPlaySound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(chatSound);
        }
        else
        {
            Debug.LogWarning("[ChatAI] AudioManager non trouvé, aucun son joué.");
        }
    }
}