using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Mirror;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private float maxGameTime = 300f; // durée totale fixe
    [SyncVar] private float gameTime;

    public enum GameState { Loading, Playing, GameOver }
    public GameOverUI gameOverUI; // Assign in Inspector
    [SyncVar(hook = nameof(OnKeysFoundChanged))]
    public int keysFound = 0;
    [SyncVar(hook = nameof(OnKeysToSpawnChanged))]
    public int keysToSpawn = 0;
    [SyncVar] public bool AllKeysFound = false;

    public GameState CurrentState { get; private set; } = GameState.Playing;
    public List<ShadowPlayer> players = new();
    public List<KeySpawnLocation> keySpawnLocations = new();

    public GameObject keyPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip bellsSound;
    [SerializeField] private AudioClip[] ambianceSounds; // Dossier de sons d'ambiance, à prendre random
    [SerializeField] private float minAmbianceInterval = 20f; // 20 secondes min d'intervalle pour pas overlap
    [SerializeField] private float maxAmbianceInterval = 50f;

    private bool bellsSoundPlayed = false;
    private float nextAmbianceSoundTime = 0f;
    private bool isInBellsZone = false; // Bloquer sons d'ambiance autour de bells
    public float GameProgress => (1f - (gameTime / maxGameTime)) * 100f;

    public override void OnStartServer()
    {
        base.OnStartServer();

        players = FindObjectsOfType<ShadowPlayer>().ToList();
        keySpawnLocations = FindObjectsOfType<KeySpawnLocation>().ToList();
        Debug.Log($"Amount of players: {NetworkServer.connections.Count}");
        StartCoroutine(WaitForPlayersAndInitialize());
    }

    private IEnumerator WaitForPlayersAndInitialize()
    {
        int totalPlayers = 0;
        // Wait until players are spawned
        while (totalPlayers != NetworkServer.connections.Count)
        {
            GameObject[] gameObjects = GameObject.FindGameObjectsWithTag("Player");
            totalPlayers = gameObjects.Length;
            players = FindObjectsOfType<ShadowPlayer>().ToList();
            yield return new WaitForSeconds(0.5f); // Check every half second
        }

        Debug.Log($"All {players.Count} players spawned, initializing game. {totalPlayers}");

        SpawnKeys();
    }

    private void Start()
    {
        gameTime = maxGameTime; // initialisation au début de la partie

        ScheduleNextAmbianceSound(); // Plannifier 1er son d'ambiance
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [Server]
    void SpawnKeys()
    {
        keySpawnLocations.AddRange(FindObjectsOfType<KeySpawnLocation>());

        // Vérifier si on a au moins 1 spawn location
        if (keySpawnLocations.Count == 0)
        {
            Debug.LogWarning("Aucune KeySpawnLocation trouvée ! Les clés ne seront pas spawnées.");
            return; // On arrête ici, sinon exception
        }

        // Nombre de clés à spawn = min(players + 1, nombre de spawn locations)
        keysToSpawn = Mathf.Min(players.Count + 1, keySpawnLocations.Count);

        for (int i = 0; i < keysToSpawn; i++)
        {
            int choice = Random.Range(0, keySpawnLocations.Count);
            Debug.Log($"Spawning Key at location {keySpawnLocations[choice].transform.position}");
            GameObject key = Instantiate(keyPrefab, keySpawnLocations[choice].transform.position, keySpawnLocations[choice].transform.rotation);
            NetworkServer.Spawn(key);
            keySpawnLocations.RemoveAt(choice); // retirer la location pour ne pas spawn dessus
        }

        GameUIManager ui = GameUIManager.Instance;
        ui.UpdateTotalKeys(keysToSpawn);
    }

    private void OnKeysToSpawnChanged(int oldValue, int newValue)
    {
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.UpdateTotalKeys(newValue);
        }
    }

    public void AddKey()
    {
        keysFound++;
        // GameUIManager.Instance.KeyFound();
        if (keysFound == keysToSpawn)
        {
            AllKeysFound = true;
        }
    }

    private void OnKeysFoundChanged(int oldValue, int newValue)
    {
        Debug.Log($"[GameManager] Keys found updated: {newValue}");
        if (GameUIManager.Instance != null)
        {
            GameUIManager.Instance.KeyFound();
        }
    }


    // Update is called once per frame
    void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            gameTime -= Time.deltaTime;
            gameTime = Mathf.Max(gameTime, 0f); // clamp à 0

            float progress = (1f - (gameTime / maxGameTime)) * 100f;

            if (GameUIManager.Instance != null)
            {
                GameUIManager.Instance.SetGameProgress(progress);
            }

            // Vérifier si on est dans la zone réservée pour les bells (45% à 55%)
            isInBellsZone = (progress >= 45f && progress <= 55f);

            // Son cloche à 50%
            if (progress >= 50f && !bellsSoundPlayed)
            {
                PlayBellsSound();
                bellsSoundPlayed = true;
            }

            // Son ambiance random
            if (Time.time >= nextAmbianceSoundTime && !isInBellsZone)
            {
                PlayRandomAmbianceSound();
                ScheduleNextAmbianceSound();
            }

            if (gameTime <= 0) EndGameShadowsWin(false);
        }
    }

    public void UpdatePlayerStatus()
    {
        // S'assurer que seule la logique serveur gère la fin de partie
        if (!isServer)
        {
            Debug.LogWarning("[GameManager] UpdatePlayerStatus appelé depuis un client (ignore).");
            return;
        }

        // Re-générer la liste des joueurs au cas où elle ne serait pas à jour
        players = FindObjectsOfType<ShadowPlayer>().ToList();

        // Calculer les comptes actuels
        int totalPlayers = players.Count;
        if (totalPlayers == 0)
        {
            Debug.LogWarning("[GameManager] UpdatePlayerStatus: aucun joueur trouvé");
            return;
        }

        int aliveCount = players.Count(p => p.playerStatus == PlayerStatus.Alive);
        int escapedCount = players.Count(p => p.playerStatus == PlayerStatus.Escaped);
        int deadCount = players.Count(p => p.playerStatus == PlayerStatus.Dead);

        Debug.Log($"[GameManager] États joueurs - total:{totalPlayers} alive:{aliveCount} escaped:{escapedCount} dead:{deadCount}");

        // Si tous les joueurs sont dans un état final (Dead ou Escaped) => fin de la partie
        if (deadCount + escapedCount == totalPlayers)
        {
            // Si au moins une ombre s'est échappée => les Ombres gagnent
            if (escapedCount > 0)
            {
                Debug.Log("[GameManager] Toutes les ombres sont arrivées à un état final et AU MOINS UNE s'est échappée -> Les Ombres gagnent");
                EndGameShadowsWin(true);
                return;
            }

            // Sinon (toutes sont mortes) => les Gardiens gagnent
            Debug.Log("[GameManager] Toutes les ombres sont arrivées à un état final et AUCUNE ne s'est échappée -> Les Gardiens gagnent");
            EndGameShadowsWin(false);
            return;
        }

        // Si le temps est écoulé, les Gardiens gagnent (déjà géré normalement dans Update()).
        // Si on arrive ici, la partie continue.
        Debug.Log("[GameManager] UpdatePlayerStatus : la partie continue.");
    }

    private void PlayBellsSound()
    {
        if (AudioManager.Instance != null && bellsSound != null)
        {
            AudioManager.Instance.PlaySFX(bellsSound);
        }
        else
        {
            Debug.LogWarning("[GameManager] AudioManager ou bellsSound manquant");
        }
    }

    private void PlayRandomAmbianceSound()
    {
        if (ambianceSounds == null || ambianceSounds.Length == 0)
        {
            Debug.LogWarning("[GameManager] Aucun son d'ambiance assigné");
            return;
        }

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[GameManager] AudioManager manquant");
            return;
        }

        // Choisir un son aléatoire
        int randomIndex = Random.Range(0, ambianceSounds.Length);
        AudioClip randomSound = ambianceSounds[randomIndex];

        if (randomSound != null)
        {
            AudioManager.Instance.PlaySFX(randomSound);
            Debug.Log($"[GameManager] Son d'ambiance joué : {randomSound.name}");
        }
    }

    private void ScheduleNextAmbianceSound()
    {
        float randomInterval = Random.Range(minAmbianceInterval, maxAmbianceInterval);
        nextAmbianceSoundTime = Time.time + randomInterval;

        Debug.Log($"[GameManager] Prochain son d'ambiance dans {randomInterval:F1} s");
    }


    private void EndGameShadowsWin(bool shadowsWin)
    {
        if (CurrentState == GameState.GameOver)
            return; // Prevent double triggers

        CurrentState = GameState.GameOver;

        // Stop all players
        foreach (var player in players)
        {
            player.enabled = false; // or disable their movement scripts only
            if (player.TryGetComponent(out Rigidbody rb))
                rb.velocity = Vector3.zero;
        }

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
        {
            GamePlayer gp = conn.identity.GetComponent<GamePlayer>();

            bool won = DeterminePlayerWin(gp, shadowsWin);

            string subtitle = shadowsWin ? "Les Ombres ont gagné !" : "Les Gardiens ont gagné !";

            TargetShowGameOver(conn, won, subtitle);
        }
    }

    private bool DeterminePlayerWin(GamePlayer gp, bool shadowsWin)
    {
        if (shadowsWin && gp.PlayerRole == Role.Ombre)
            return true;
        if (!shadowsWin && gp.PlayerRole == Role.Gardien)
            return true;
        return false;
    }

    [TargetRpc]
    public void TargetShowGameOver(NetworkConnectionToClient target, bool won, string subtitle)
    {
        DisableLocalPlayer();
        // This runs on the client
        PlayerPrefs.SetInt("GameOver_Win", won ? 1 : 0);
        PlayerPrefs.SetString("GameOver_Subtitle", subtitle);
        PlayerPrefs.Save();

        // Détruire l'UI de jeu pour éviter les overlays persistants
        if (GameUIManager.Instance != null)
        {
            Destroy(GameUIManager.Instance.gameObject);
        }

        SceneManager.LoadScene("GameOver");
    }

    private void DisableLocalPlayer()
    {
        var localPlayer = NetworkClient.localPlayer;
        if (localPlayer == null) return;

        // Disable movement, input, camera, etc.
        foreach (var comp in localPlayer.GetComponentsInChildren<MonoBehaviour>())
        {
            if (!(comp is NetworkBehaviour))
                comp.enabled = false;
        }
    }
}
