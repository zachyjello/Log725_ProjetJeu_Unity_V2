using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class FantomePath : NetworkBehaviour
{
    public Transform[] waypoints;   // liste des points à suivre
    public float speed = 2f;        // vitesse du fantôme
    private int currentIndex = 0;   // index du waypoint actuel

    [Header("Lamp interaction")]
    public float lampInteractDistance = 1.5f;
    public float toggleCooldown = 2f; // secondes entre deux toggles
    private float lastToggleTime = -Mathf.Infinity;

    void Update()
    {
        // Ne faire tourner la logique que sur le serveur
        if (!IsServer) return;

        if (waypoints.Length == 0) return;

        // Déplacement vers le waypoint courant
        Transform target = waypoints[currentIndex];

        transform.position = Vector3.MoveTowards(
            transform.position,
            target.position,
            speed * Time.deltaTime
        );

        // Vérifie si le fantôme est arrivé au waypoint
        if (Vector3.Distance(transform.position, target.position) < 0.1f)
        {
            currentIndex = Random.Range(0, waypoints.Length); // passe au waypoint suivant de manière aléatoire
            if (currentIndex >= waypoints.Length)
            {
                currentIndex = 0; // recommence au début (boucle)
            }

            Debug.Log("FantomeAI est arrivé au point : " + currentIndex);
        }

        // Essayer d'interagir avec une lampe proche (seulement sur le serveur)
        TryToggleNearbyLamp();
    }

    void TryToggleNearbyLamp()
    {
        // La logique de changement doit se faire sur le serveur
        if (!IsServer) return;

        if (Time.time - lastToggleTime < toggleCooldown) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, lampInteractDistance);
        foreach (var col in hits)
        {
            var lamp = col.GetComponentInParent<NetworkedLampInteraction>();
            if (lamp != null)
            {
                // Appel d'une méthode serveur sur la lampe.
                // Adaptez le nom si votre NetworkedLampInteraction utilise un ServerRpc différent.
                lamp.ServerToggleLamp();
                lastToggleTime = Time.time;
                Debug.Log($"Lampe intéragis par fantome : {lamp.gameObject.name}");
                break;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, lampInteractDistance);
    }
}