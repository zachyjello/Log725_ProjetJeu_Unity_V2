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
    [SyncVar] public int keyCount = 0; 

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


        // CHECK ICI VIEILLE VERSION
        //players.AddRange(FindObjectsOfType<MonoBehaviour>().OfType<ShadowPlayer>());
        //keySpawnLocations.AddRange(FindObjectsOfType<MonoBehaviour>().OfType<KeySpawnLocation>());

        //if (keySpawnLocations.Count < players.Count) throw new Exception("Not enough spawn locations");

        //if (Instance != null && Instance != this)
        //{
        //    Destroy(gameObject);
        //    return;
        //}

        //for (int i = 0; i < players.Count + 1; i++)
        //{
        //    int choice = Random.Range(0, keySpawnLocations.Count);
        //    Instantiate(keyPrefab, keySpawnLocations[choice].transform.position, keySpawnLocations[choice].transform.rotation);
        //    keySpawnLocations.Remove(keySpawnLocations[choice]);
        //}
        // Debug : afficher combien de joueurs et de spawn locations

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
        int keysToSpawn = Mathf.Min(players.Count + 1, keySpawnLocations.Count);

        for (int i = 0; i < keysToSpawn; i++)
        {
            int choice = Random.Range(0, keySpawnLocations.Count);
            Debug.Log($"Spawning Key at location {keySpawnLocations[choice].transform.position}");
            Instantiate(keyPrefab, keySpawnLocations[choice].transform.position, keySpawnLocations[choice].transform.rotation);
            keySpawnLocations.RemoveAt(choice); // retirer la location pour ne pas spawn dessus
            keyCount++;
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
        bool alive = false;
        bool escaped = false;

        foreach (var player in players)
        {
            if (player.playerStatus == PlayerStatus.Alive)
                alive = true;
            else if (player.playerStatus == PlayerStatus.Escaped)
                escaped = true;
        }

        if (!alive && !escaped)
            EndGameShadowsWin(false);
        if (!alive && escaped)
            EndGameShadowsWin(true);
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

        // Optionally freeze world time
        Time.timeScale = 0f;

        // Determine if local player won
        bool localPlayerWon = DetermineLocalPlayerWin(shadowsWin);

        PlayerPrefs.SetInt("GameOver_Win", localPlayerWon ? 1 : 0);
        PlayerPrefs.SetString("GameOver_Subtitle", shadowsWin ? "Les Ombres ont gagné !" : "Les Chercheurs ont gagné !");
        PlayerPrefs.Save();

        SceneManager.LoadScene("GameOver");
    }

    private bool DetermineLocalPlayerWin(bool shadowsWin)
    {
        // Find all GamePlayers and get the local one
        GamePlayer[] gamePlayers = FindObjectsOfType<GamePlayer>();
        foreach (GamePlayer gp in gamePlayers)
        {
            if (gp.isLocalPlayer)
            {
                // If shadows win, and local player is Shadow, then won
                if (shadowsWin && gp.PlayerRole == Role.Ombre)
                    return true;
                // If seekers win, and local player is Gardien, then won
                if (!shadowsWin && gp.PlayerRole == Role.Gardien)
                    return true;
                return false;
            }
        }
        // If no local player found, assume lost
        return false;
    }
}
