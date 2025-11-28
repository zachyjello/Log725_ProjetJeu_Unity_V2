using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffsetFlashLight : NetworkBehaviour, ILightSource
{
    [Header("R�glages de la lampe")]
    //public GameObject FollowCam; // Camera qui suit le joueur
    [SerializeField] private float MoveSpeed = 13f; // Change vitesse de mouvement de la lampe
    [SerializeField] private float verticalRotationSpeed = 50f; // vitesse de rotation verticale
    [SerializeField] private float minVerticalAngle = -80f; // limite vers le bas
    [SerializeField] private float maxVerticalAngle = 80f;  // limite haut

    public Light FlashLight; // Flashlight component

    // Gestion de la batterie
    private FlashlightBattery batterySystem;

    // audio
    public AudioSource Source;
    public AudioClip FlashLightOnSound;
    public AudioClip FlashLightOffSound;

    private float currentVerticalAngle = 0f; // rotation verticale

    // Nouveau SyncVar pour synchroniser l'état de la lampe
    [SyncVar(hook = nameof(OnFlashlightStateChanged))]
    private bool flashlightEnabled = true; // État initial (allumé par défaut, ajustez si nécessaire)



    // Start appel� avant la premi�re frame update
    void Start()
    {
        batterySystem = GetComponent<FlashlightBattery>(); // R�cup�ration infos batterie

        // Appliquer l'état initial de la lampe
        FlashLight.enabled = flashlightEnabled;
    }

    // Update est appel� une fois par frame
    void Update()
    {
        // Multi : seul le joueur local peut contr�ler sa lampe
        if (!isLocalPlayer) return;

        HandleFlashlightToggle();
        HandleVerticalRotation();
    }

    private void HandleFlashlightToggle()
    {
        if (Input.GetKeyDown(KeyCode.F)) // Si appuie sur F
        {
            CmdToggleFlashlight(); // Appeler la commande pour synchroniser
        }
    }

    private void HandleVerticalRotation()
    {
        // Contr�le de la molette pour inclinaison verticale du faisceau
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            currentVerticalAngle -= scrollInput * verticalRotationSpeed;
            currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        // Application de la rotation sur la lampe
        Quaternion targetRotation = Quaternion.Euler(currentVerticalAngle - 90f, 0f, 0f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * MoveSpeed);
    }

    // Commande pour basculer l'état sur le serveur (nécessaire pour SyncVar)
    [Command]
    private void CmdToggleFlashlight()
    {
        // Vérifier la batterie : si éteint, vérifier si on peut allumer ; si allumé, toujours permettre d'éteindre
        if (!flashlightEnabled && (batterySystem == null || !batterySystem.CanTurnOnFlashlight()))
        {
            return; // Ne pas allumer si batterie vide
        }

        flashlightEnabled = !flashlightEnabled; // Basculer l'état
    }

    // Hook appelé sur tous les clients quand flashlightEnabled change
    private void OnFlashlightStateChanged(bool oldValue, bool newValue)
    {
        FlashLight.enabled = newValue; // Appliquer l'état visuel

        // Jouer le son approprié (seulement sur le client local pour éviter les sons multiples)
        if (isLocalPlayer && Source != null)
        {
            AudioClip soundToPlay = newValue ? FlashLightOnSound : FlashLightOffSound;
            if (soundToPlay != null)
            {
                Source.PlayOneShot(soundToPlay);
            }
        }
    }

    //public bool IsPlayerInLight(Vector3 playerPosition)
    //{
    //    if (FlashLight == null || !FlashLight.enabled)
    //        return false;

    //    float distance = Vector3.Distance(transform.position, playerPosition);

    //    if (distance > FlashLight.range)
    //        return false;

    //    Vector3 directionToPlayer = (playerPosition - transform.position).normalized;
    //    Vector3 lightDirection = transform.forward;

    //    float angle = Vector3.Angle(lightDirection, directionToPlayer);

    //    return angle < (FlashLight.spotAngle / 2f);
    //}

    // Modifie aussi IsPlayerInLight pour utiliser isFlashlightOn
    public bool IsPlayerInLight(Vector3 playerPosition)
    {
        // isFlashlightOn au lieu de FlashLight.enabled
        if (FlashLight == null || !flashlightEnabled)
            return false;

        float distance = Vector3.Distance(transform.position, playerPosition);
        if (distance > FlashLight.range)
            return false;

        Vector3 directionToPlayer = (playerPosition - transform.position).normalized;
        Vector3 lightDirection = transform.forward;
        float angle = Vector3.Angle(lightDirection, directionToPlayer);

        return angle < (FlashLight.spotAngle / 2f);
    }

    public Vector3 GetLightPosition()
    {
        return transform.position;
    }

    public bool IsGuardianLight()
    {
        return true; // Cette lampe blesse les Ombres
    }

    public bool IsFlashlightEnabled()
    {
        return flashlightEnabled;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ShadowPlayer[] shadows = FindObjectsOfType<ShadowPlayer>();
        foreach (var sh in shadows)
        {
            sh.RegisterLightSource(this);
        }
    }
}
