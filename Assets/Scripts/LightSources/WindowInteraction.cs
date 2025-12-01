using Mirror;
using UnityEngine;

//public class WindowInteraction : MonoBehaviour
//{
//    [Header("R�f�rences")]
//    public GameObject windowGlass; // La vitre/fen�tre
//    public GameObject interactionUI; // Le texte "E"

//    [Header("Param�tres")]
//    public float interactionDistance = 3f;
//    public KeyCode interactionKey = KeyCode.E;

//    [Header("Sons")]
//    public AudioClip openSound; // Son d'ouverture
//    public AudioClip closeSound; // Son de fermeture
//    [Range(0f, 1f)]
//    public float soundVolume = 0.5f;

//    [Header("Textes d'interaction")]
//    public string openText = "E - Ouvrir la fen�tre";
//    public string closedText = "E - Fermer la fen�tre";

//    private Transform localPlayer;
//    private bool isPlayerNear = false;
//    private bool isWindowOpen = false; // false = ferm�e (vitre visible)
//    private TMPro.TextMeshProUGUI interactionText;
//    private AudioSource audioSource;

//    void Start()
//    {
//        // Cacher le UI au d�but
//        if (interactionUI != null)
//        {
//            interactionUI.SetActive(false);
//            interactionText = interactionUI.GetComponentInChildren<TMPro.TextMeshProUGUI>();
//        }

//        // V�rifier que la vitre existe
//        if (windowGlass == null)
//        {
//            Debug.LogError("WindowInteraction: Vitre non assign�e sur " + gameObject.name);
//        }

//        // Cr�er un AudioSource pour les sons
//        audioSource = gameObject.AddComponent<AudioSource>();
//        audioSource.playOnAwake = false;
//        audioSource.spatialBlend = 1f; // Son 3D
//        audioSource.minDistance = 1f;
//        audioSource.maxDistance = 10f;
//        audioSource.volume = soundVolume;

//        // �tat initial : fen�tre ferm�e (vitre visible)
//        if (windowGlass != null)
//        {
//            windowGlass.SetActive(!isWindowOpen);
//        }

//        UpdateInteractionText();
//    }

//    void Update()
//    {
//        // Chercher le joueur
//        if (localPlayer == null)
//        {
//            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
//            if (playerObj != null)
//            {
//                localPlayer = playerObj.transform;
//            }

//            if (localPlayer == null)
//            {
//                GamePlayer player = FindObjectOfType<GamePlayer>();
//                if (player != null)
//                {
//                    localPlayer = player.transform;
//                }
//            }

//            if (localPlayer == null)
//                return;
//        }

//        // Calculer la distance
//        float distance = Vector3.Distance(localPlayer.position, transform.position);

//        // V�rif si le joueur est � port�e
//        if (distance <= interactionDistance)
//        {
//            if (!isPlayerNear)
//            {
//                isPlayerNear = true;
//                if (interactionUI != null)
//                {
//                    interactionUI.SetActive(true);
//                }
//            }

//            // D�tecter appui E
//            if (Input.GetKeyDown(interactionKey))
//            {
//                ToggleWindow();
//            }
//        }
//        else
//        {
//            if (isPlayerNear)
//            {
//                isPlayerNear = false;
//                if (interactionUI != null)
//                {
//                    interactionUI.SetActive(false);
//                }
//            }
//        }
//    }

//    void ToggleWindow()
//    {
//        isWindowOpen = !isWindowOpen;

//        // Activer/d�sactiver vitre
//        if (windowGlass != null)
//        {
//            windowGlass.SetActive(!isWindowOpen); // Si ouverte = pas de vitre
//        }

//        // Jouer le son qu'il faut (ouvrir ou fermer)
//        if (audioSource != null)
//        {
//            AudioClip soundToPlay = isWindowOpen ? openSound : closeSound;
//            if (soundToPlay != null)
//            {
//                audioSource.PlayOneShot(soundToPlay);
//            }
//        }

//        UpdateInteractionText();

//        Debug.Log($"Fen�tre {(isWindowOpen ? "ouverte (vitre cach�e)" : "ferm�e (vitre visible)")}");
//    }

//    void UpdateInteractionText()
//    {
//        if (interactionText != null)
//        {
//            interactionText.text = isWindowOpen ? closedText : openText;
//        }
//    }

//    void OnDrawGizmosSelected()
//    {
//        Gizmos.color = Color.cyan;
//        Gizmos.DrawWireSphere(transform.position, interactionDistance);
//    }
//}



///////////////////////// VERSION ONLINE
///

public class WindowInteraction : NetworkBehaviour
{
    [Header("R�f�rences")]
    public GameObject windowGlass;
    public GameObject interactionUI;
    private GameObject windowLight;

    [Header("Param�tres")]
    public float interactionDistance = 4f;
    public KeyCode interactionKey = KeyCode.E;

    [Header("Sons")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Range(0f, 1f)]
    public float soundVolume = 0.7f;
    public float minHearDistance = 1f;      // Distance o� le son est � volume max
    public float maxHearDistance = 15f;     // Distance o� le son devient inaudible

    [SyncVar(hook = nameof(OnWindowStateChanged))]
    private bool isWindowOpen = false;

    private Transform localPlayer;
    private bool isPlayerNear = false;
    private AudioSource audioSource;
    private bool playerSearchLogged = false;

    void Start()
    {
        Debug.Log($"[Window {gameObject.name}] Start - IsServer: {isServer}, IsClient: {isClient}");

        if (interactionUI != null)
        {
            interactionUI.SetActive(false);
        }

        Transform childTransform = transform.Find("WindowLight"); // Works even if inactive
        if (childTransform != null)
        {
            windowLight = childTransform.gameObject;
        }

        // Cr�er AudioSource avec spatialisation 3D
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;              // 1 = son 3D complet (spatialisation)
        audioSource.volume = soundVolume;
        audioSource.minDistance = minHearDistance;  // Distance min pour att�nuation
        audioSource.maxDistance = maxHearDistance;  // Distance max audible
        audioSource.rolloffMode = AudioRolloffMode.Linear; // Att�nuation lin�aire
        audioSource.dopplerLevel = 0f;              // Pas d'effet Doppler pour fen�tres

        // Appliquer �tat initial (sans son au d�marrage)
        if (windowGlass != null)
        {
            windowGlass.SetActive(!isWindowOpen);
        }
    }

    void Update()
    {
        // Chercher le joueur local de plusieurs fa�ons
        if (localPlayer == null)
        {
            // M�thode 1 : Via GamePlayer
            GamePlayer[] allPlayers = FindObjectsOfType<GamePlayer>();

            if (!playerSearchLogged)
            {
                Debug.Log($"[Window] Recherche joueur... {allPlayers.Length} GamePlayer(s) trouv�(s)");
            }

            foreach (GamePlayer player in allPlayers)
            {
                if (!playerSearchLogged)
                {
                    Debug.Log($"[Window] GamePlayer: {player.PlayerName}, isLocalPlayer: {player.isLocalPlayer}");
                }

                if (player.isLocalPlayer)
                {
                    localPlayer = player.transform;
                    Debug.Log($"[Window] Joueur local trouv� via GamePlayer: {player.PlayerName}");
                    playerSearchLogged = true;
                    break;
                }
            }

            // M�thode 2 : Via Tag "Player" (fallback)
            if (localPlayer == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    NetworkIdentity netId = playerObj.GetComponent<NetworkIdentity>();
                    if (netId != null && netId.isLocalPlayer)
                    {
                        localPlayer = playerObj.transform;
                        Debug.Log($"[Window] Joueur local trouv� via Tag");
                        playerSearchLogged = true;
                    }
                }
            }

            // M�thode 3 : Via Camera (last resort)
            if (localPlayer == null && !playerSearchLogged)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    // Chercher le joueur parent de la cam�ra
                    Transform parent = mainCam.transform.parent;
                    while (parent != null)
                    {
                        NetworkIdentity netId = parent.GetComponent<NetworkIdentity>();
                        if (netId != null && netId.isLocalPlayer)
                        {
                            localPlayer = parent;
                            Debug.Log($"[Window] Joueur local trouv� via Camera: {parent.name}");
                            playerSearchLogged = true;
                            break;
                        }
                        parent = parent.parent;
                    }
                }
            }

            if (localPlayer == null)
            {
                playerSearchLogged = true; // Pour ne pas spammer les logs
                return;
            }
        }

        // Calculer la distance
        float distance = Vector3.Distance(localPlayer.position, transform.position);

        // V�rifier si le joueur est � port�e
        if (distance <= interactionDistance)
        {
            if (!isPlayerNear)
            {
                isPlayerNear = true;
                Debug.Log($"[Window {gameObject.name}] Joueur proche. Distance: {distance:F2}m");

                if (interactionUI != null)
                {
                    interactionUI.SetActive(true);
                }
            }

            if (Input.GetKeyDown(interactionKey))
            {
                Debug.Log($"[Window {gameObject.name}] Touche E press�e");
                CmdToggleWindow();
            }
        }
        else
        {
            if (isPlayerNear)
            {
                isPlayerNear = false;
                if (interactionUI != null)
                {
                    interactionUI.SetActive(false);
                }
            }
        }
    }

    [Command(requiresAuthority = false)]
    void CmdToggleWindow()
    {
        Debug.Log($"[Window {gameObject.name}] Command re�ue sur serveur");
        isWindowOpen = !isWindowOpen;

        Debug.Log($"[Window Server] Fen�tre {(isWindowOpen ? "OUVERTE" : "FERM�E")}");
    }

    void OnWindowStateChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"[Window {gameObject.name}] Hook: {oldValue} -> {newValue}");

        if (windowGlass != null)
        {
            windowGlass.SetActive(!newValue);
        }
        if (windowLight != null){
            windowLight.SetActive(newValue);
        }

        // Jouer le son localement sur CHAQUE client quand l'�tat change
        if (audioSource != null && oldValue != newValue) // V�rif qu'il y a vraiment un changement
        {
            AudioClip soundToPlay = newValue ? openSound : closeSound;
            if (soundToPlay != null)
            {
                audioSource.PlayOneShot(soundToPlay);
                Debug.Log($"[Window {gameObject.name}] Son jou�: {(newValue ? "ouverture" : "fermeture")}");
            }
        }
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}