using Mirror;
using StarterAssets;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ShadowPlayer : NetworkBehaviour
{
    public float shadowMoveSpeed = 7.0f;
    public PlayerStatus playerStatus = PlayerStatus.Alive;
    private List<ILightSource> _lightSources = new();
    private bool wasInLight = false;

    [SyncVar]
    public bool inLightSource = false;

    [SyncVar]
    public bool inEnemyLightSource = false;

    [SyncVar(hook = nameof(OnShadowFormChangedSync))]
    public bool inShadowForm = false;

    private ThirdPersonController _controller;
    private ParticleSystem _particleSystem;
    private Animator _animator;
    public float diveDuration = 0.2f;
    private bool inDiving = false;
    public float height = 2.0f;

    private CharacterController _characterController;
    private float originalMoveSpeed;
    private float originalSprintSpeed;
    private float originalJumpHeight;
    private Transform _visualRoot;
    private Transform _modelTransform;
    private float shadowCircleGroundOffset = 1.0f;

    private GameObject _shadowCircle;
    public float shadowCircleRadius = 0.5f;

    private float _lastLightCheck = 0f;
    private const float LIGHT_CHECK_INTERVAL = 0.1f;

    //private float _originalControllerHeight;
    //private Vector3 _originalControllerCenter;

    public float maxHealth = 20.0f;

    [SyncVar(hook = nameof(OnHealthChangedSync))]
    private float _health;
    public float health
    {
        get => _health;
        set
        {
            if (!isServer) return; // Seulement le serveur peut changer la santé
            float newHealth = Mathf.Clamp(value, 0f, maxHealth);
            _health = newHealth;
            if (_health <= 0 && playerStatus == PlayerStatus.Alive)
            {
                OnDeath();
            }
        }
    }
    public event Action<float> OnHealthChanged;

    private void OnHealthChangedSync(float oldHealth, float newHealth)
    {
        // Hook appelé sur tous les clients quand la santé change
        OnHealthChanged?.Invoke(newHealth);

        if (newHealth <= 0 && playerStatus == PlayerStatus.Alive)
        {
            OnDeath();
        }
    }


    private void OnShadowFormChangedSync(bool oldVal, bool newVal)
    {
        if (newVal && !inDiving)
            StartCoroutine(EnterShadowRoutine());
        else if (!newVal && !inDiving)
        {
            StopAllCoroutines();
            StartCoroutine(ExitShadowRoutine());
        }
    }

    public float healthRegenCooldown = 2.0f;
    public float healthRegenState = 0f;
    public float healthRegenRate = 2.0f;
    public float enemyDamageMult = 8.0f;

    [SerializeField] private LayerMask blockingLayers;

    public GameObject ghostPrefab; // Référence au prefab fantôme
    private GameObject spawnedGhost;
    private GameObject mainCamera;

    // Added flag to prevent double-transform
    private bool _hasTransformedToGhost = false;

    // Start is called before the first frame update
    void Start()
    {
        // Initialiser la santé sur le serveur
        if (isServer)
        {
            health = maxHealth;
        }

        gameObject.SetActive(true);
        //_lightSources.AddRange(FindObjectsOfType<MonoBehaviour>().OfType<ILightSource>());

        _animator = GetComponentInChildren<Animator>();
        _controller = GetComponent<ThirdPersonController>();
        _characterController = GetComponent<CharacterController>();
        _particleSystem = GetComponent<ParticleSystem>();

        if (_particleSystem != null)
            _particleSystem.Pause();

        if (_controller != null)
        {
            originalMoveSpeed = _controller.MoveSpeed;
            originalSprintSpeed = _controller.SprintSpeed;
            originalJumpHeight = _controller.JumpHeight;
        }


        _visualRoot = _animator != null ? _animator.transform.parent ?? _animator.transform : transform;

        _modelTransform = transform.Find("Model");

        CreateShadowCircle();

        if (Camera.main != null)
            mainCamera = Camera.main.gameObject;

        if (isLocalPlayer && GameUIManager.Instance != null)
        {
            GameUIManager.Instance.RegisterLocalShadowPlayer(this);
            GameUIManager.Instance.SetPlayerRole(true); // true = Ombre
            Debug.Log("[ShadowPlayer] Enregistré auprès de GameUIManager");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Wait a frame for all network objects to be ready
        StartCoroutine(InitializeLightSources());
        Debug.Log("[ShadowPlayer] Light sources count = " + _lightSources.Count);
    }

    IEnumerator InitializeLightSources()
    {
        // Wait for end of frame to ensure all spawned objects are registered
        yield return new WaitForEndOfFrame();

        _lightSources.Clear();
        _lightSources.AddRange(FindObjectsOfType<MonoBehaviour>().OfType<ILightSource>());
    }
    void CreateShadowCircle()
    {
        _shadowCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _shadowCircle.name = "ShadowCircle";
        _shadowCircle.transform.parent = transform;

        float groundY = -_characterController.height / 2f + shadowCircleGroundOffset;
        _shadowCircle.transform.localPosition = new Vector3(0, groundY, 0);
        _shadowCircle.transform.localScale = new Vector3(shadowCircleRadius * 2, 0.01f, shadowCircleRadius * 2);

        Destroy(_shadowCircle.GetComponent<Collider>());

        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("HDRP/Unlit");

        Material shadowMat = new Material(shader);
        shadowMat.color = new Color(0, 0, 0, 0.5f);

        shadowMat.SetInt("_Surface", 1);
        shadowMat.SetOverrideTag("RenderType", "Transparent");
        shadowMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Renderer renderer = _shadowCircle.GetComponent<Renderer>();
        renderer.material = shadowMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _shadowCircle.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        // Détection de lumière sur le serveur ET le joueur local
        if (isServer || isLocalPlayer)
        {
            InLightCheck();
        }

        // Gestion de la santé UNIQUEMENT sur le serveur
        if (isServer)
        {
            HandleHealth();
        }

        // Input UNIQUEMENT pour le joueur local
        if (!isLocalPlayer) return;

        if (Input.GetKeyDown(KeyCode.Q) && !inShadowForm && !inLightSource && !inDiving)
        {
            CmdTryEnterShadow();
        }
        else if (inShadowForm && (Input.GetKeyUp(KeyCode.Q) || inLightSource) && !inDiving)
        {
            CmdExitShadow();
        }
    }

    [Command]
    void CmdTryEnterShadow()
    {
        if (!inShadowForm && !inLightSource && !inDiving)
        {
            inShadowForm = true;
            RpcEnterShadow();
        }
    }

    [Command]
    void CmdExitShadow()
    {
        if (inShadowForm && !inDiving)
        {
            inShadowForm = false;
            RpcExitShadow();
        }
    }

    [ClientRpc]
    void RpcEnterShadow()
    {
        if (!inDiving)
            StartCoroutine(EnterShadowRoutine());
    }

    [ClientRpc]
    void RpcExitShadow()
    {
        if (!inDiving)
        {
            StopAllCoroutines();
            StartCoroutine(ExitShadowRoutine());
        }
    }

    void TryEnterShadow()
    {
        inDiving = true;

        if (!inShadowForm)
            StartCoroutine(EnterShadowRoutine());
    }

    IEnumerator EnterShadowRoutine()
    {
        inDiving = true;

        if (_particleSystem != null)
            _particleSystem.Play();

        if (_modelTransform != null)
            _modelTransform.gameObject.SetActive(false);

        if (_shadowCircle != null)
            _shadowCircle.SetActive(true);

        if (_animator) _animator.SetTrigger("Dive");

        float elapsed = 0f;

        while (elapsed < diveDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_controller != null)
        {
            _controller.MoveSpeed = shadowMoveSpeed;
            _controller.SprintSpeed = shadowMoveSpeed;
            _controller.JumpHeight = 0;
        }

        inDiving = false;
    }

    //void ExitShadow()
    //{
    //    inDiving = true;

    //    StopAllCoroutines();
    //    StartCoroutine(ExitShadowRoutine());
    //}

    IEnumerator ExitShadowRoutine()
    {
        inDiving = true;

        if (_shadowCircle != null)
            _shadowCircle.SetActive(false);

        if (_particleSystem != null)
            _particleSystem.Stop();

        if (_animator) _animator.SetTrigger("Emerge");

        if (_modelTransform != null)
            _modelTransform.gameObject.SetActive(true);

        float elapsed = 0;

        while (elapsed < diveDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_controller != null)
        {
            _controller.MoveSpeed = originalMoveSpeed;
            _controller.SprintSpeed = originalSprintSpeed;
            _controller.JumpHeight = originalJumpHeight;
        }

        inDiving = false;
    }

    private void InLightCheck()
    {
        //Debug.Log($"[ShadowPlayer] Démarrage inlightcheck");
        bool inLight = false;
        bool inEnemyLight = false;

        if (Time.time - _lastLightCheck < LIGHT_CHECK_INTERVAL)
            return;

        _lastLightCheck = Time.time;

        // Définit des points d'échantillonnage verticalement sur le joueur (pieds / centre / tête)
        Vector3 centerWorld;
        float halfHeight;
        if (_characterController != null)
        {
            centerWorld = transform.position + _characterController.center;
            halfHeight = _characterController.height * 0.5f;
        }
        else
        {
            centerWorld = transform.position;
            halfHeight = 1.0f; // fallback si pas de CharacterController
        }

        Vector3 sampleBottom = centerWorld - Vector3.up * halfHeight; // pieds approximés
        Vector3 sampleMiddle = centerWorld;                            // torse/centre
        Vector3 sampleTop = centerWorld + Vector3.up * halfHeight;    // tête approximée

        Vector3[] samplePoints = new Vector3[] { sampleBottom, sampleMiddle, sampleTop };

        foreach (var lightSource in _lightSources)
        {
            if (lightSource == null) continue;

            // Cast vers OffsetFlashLight pour accéder à isFlashlightOn
            OffsetFlashLight flashlight = lightSource as OffsetFlashLight;
            if (flashlight != null)
            {
                // Vérifier si la lampe est allumée (propriété synchro)
                if (!flashlight.IsFlashlightEnabled())
                    continue;
            }

            // Tester si l'une des positions échantillonnées est dans la lumière
            bool playerInThisLight = false;
            foreach (var samplePoint in samplePoints)
            {
                if (lightSource.IsPlayerInLight(samplePoint))
                {
                    // Vérifier qu'il n'y a pas d'obstacle entre la lumière et ce point du joueur
                    Vector3 directionToPlayer = (samplePoint - lightSource.GetLightPosition()).normalized;
                    float distance = Vector3.Distance(samplePoint, lightSource.GetLightPosition());
                    if (!Physics.Raycast(lightSource.GetLightPosition(), directionToPlayer, distance, blockingLayers))
                    {
                        playerInThisLight = true;
                        break; // un seul point suffisant pour considérer le joueur dans la lumière
                    }
                }
            }

            if (playerInThisLight)
            {
                inLight = true;

                if (lightSource.IsGuardianLight())
                {
                    inEnemyLight = true;
                    // Une lumière ennemie suffit, pas besoin de tester d'autres lumières
                    break;
                }
            }
        }

        // Mettre à jour les SyncVars sur le serveur
        if (isServer)
        {
            inEnemyLightSource = inEnemyLight;
            inLightSource = inLight;
        }
        // Ou envoyer une commande au serveur si on est le client
        else if (isLocalPlayer)
        {
            CmdUpdateLightStatus(inLight, inEnemyLight);
        }

        if (inLight)
            OnEnterLight();
        else
            OnExitLight();
    }

    [Command]
    void CmdCollectKey(GameObject keyObject)
    {
        if (keyObject == null || !keyObject.CompareTag("Key")) return;

        NetworkServer.Destroy(keyObject);
        GameManager.Instance.AddKey();
    }

    [Command]
    void CmdEscape()
    {
        if (!GameManager.Instance.AllKeysFound) return;

        playerStatus = PlayerStatus.Escaped;

        if (GameManager.Instance != null)
            GameManager.Instance.UpdatePlayerStatus();

        gameObject.SetActive(false);
    }

    [Command]
    void CmdUpdateLightStatus(bool inLight, bool inEnemyLight)
    {
        inLightSource = inLight;
        inEnemyLightSource = inEnemyLight;
    }

    private void OnEnterLight()
    {
        if (!wasInLight)
            wasInLight = true;
    }

    private void OnExitLight()
    {
        if (wasInLight)
            wasInLight = false;
    }

    private void OnDeath()
    {
        playerStatus = PlayerStatus.Dead;

        if (GameManager.Instance != null)
            GameManager.Instance.UpdatePlayerStatus();

        if (isServer && !_hasTransformedToGhost)
        {
            _hasTransformedToGhost = true;

            if (ghostPrefab == null)
            {
                Debug.LogWarning("[ShadowPlayer] ghostPrefab is null — can't transform to ghost.");
            }
            else
            {
                var identity = ghostPrefab.GetComponent<NetworkIdentity>();
                if (identity == null)
                {
                    Debug.LogError("[ShadowPlayer] ghostPrefab has no NetworkIdentity! Add a NetworkIdentity to the prefab and register it in the NetworkManager spawnable prefabs.");
                }
                else
                {
                    GameObject ghostObj = Instantiate(ghostPrefab, transform.position, transform.rotation);

                    if (connectionToClient != null)
                    {
                        NetworkServer.ReplacePlayerForConnection(connectionToClient, ghostObj, true);
                        Debug.Log($"[ShadowPlayer] Replaced player for connection with Ghost: {ghostObj.name}");
                    }
                    else
                    {
                        NetworkServer.Spawn(ghostObj);
                        NetworkServer.Destroy(this.gameObject);
                        Debug.Log($"[ShadowPlayer] Spawned ghost as NPC: {ghostObj.name}");
                    }
                }
            }
        }

        // Désactiver le mesh pour tous les clients
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = false;
        }

        // Désactiver les composants de contrôle
        if (_controller != null) _controller.enabled = false;
        if (_characterController != null) _characterController.enabled = false;
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (!isLocalPlayer) return;

        if (collision.gameObject.CompareTag("Key"))
        {
            CmdCollectKey(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("ExitDoor") && GameManager.Instance.AllKeysFound)
        {
            CmdEscape();
        }
    }


    private void HandleHealth()
    {
        if (!isServer) return; // CRITIQUE : Santé gérée juste par le serveur

        if (!inShadowForm)
            healthRegenState = 0;
        else
            healthRegenState += Time.deltaTime;

        if (inEnemyLightSource)
        {
            //health -= Time.deltaTime * enemyDamageMult;
            float damage = Time.deltaTime * enemyDamageMult;
            health -= damage;
            // Debug à chaque seconde
            if (Time.frameCount % 60 == 0)
            {
                Debug.LogWarning($"[ShadowPlayer] OMBRE DEAD : Damage={damage:F2}, Health={health:F1}/{maxHealth}");
            }
        }
        else if (healthRegenState >= healthRegenCooldown && health < maxHealth)
        {
            health += Time.deltaTime * healthRegenRate;
        }
    }

    public void RegisterLightSource(ILightSource src)
    {
        if (!_lightSources.Contains(src))
            _lightSources.Add(src);
    }
}