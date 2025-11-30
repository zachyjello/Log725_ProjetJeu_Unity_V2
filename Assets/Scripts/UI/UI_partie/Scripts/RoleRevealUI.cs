using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class RoleRevealUI : MonoBehaviour
{
    [Header("UI Document")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Settings")]
    [SerializeField] private float ombreDisplayDuration = 5f;
    [SerializeField] private float gardienDisplayDuration = 15f;
    [SerializeField] private float fadeOutDuration = 1f;

    private VisualElement root;
    private VisualElement overlay;
    private Label roleTitle;
    private Label roleName;
    private Label roleDescription;
    private Label roleInfo;
    private Label roleTimer;

    private bool isShowing = false;
    private bool isInitialized = false;
    private Coroutine timerCoroutine;

    public static RoleRevealUI Instance { get; private set; }

    public System.Action OnRevealComplete;

    private void Awake(){
        if (Instance != null && Instance != this){
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        Debug.Log("[RoleRevealUI] Instance créée");
    }

    private void Start(){
        InitializeUI();
    }

    private void InitializeUI(){
        if (isInitialized) return;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (uiDocument == null){
            Debug.LogError("[RoleRevealUI] UIDocument introuvable");
            return;
        }

        root = uiDocument.rootVisualElement;
        
        if (root == null){
            Debug.LogError("[RoleRevealUI] RootVisualElement null");
            return;
        }

        InitializeElements();
        
        // Cacher par défaut
        HideImmediate();

        isInitialized = true;
    }

    private void InitializeElements(){
        overlay = root.Q<VisualElement>("role-reveal-overlay");
        roleTitle = root.Q<Label>("role-title");
        roleName = root.Q<Label>("role-name");
        roleDescription = root.Q<Label>("role-description");
        roleInfo = root.Q<Label>("role-info");
        roleTimer = root.Q<Label>("role-timer");

        if (overlay == null) {
            Debug.LogError("[RoleRevealUI] 'role-reveal-overlay' introuvable");
        }
        if (roleTitle == null) {
            Debug.LogError("[RoleRevealUI] 'role-title' introuvable");
        }
        if (roleName == null) {
            Debug.LogError("[RoleRevealUI] 'role-name' introuvable");
        }
        if (roleDescription == null){
            Debug.LogError("[RoleRevealUI] 'role-description' introuvable");
        }
        if (roleInfo == null) {
            Debug.LogError("[RoleRevealUI] 'role-info' introuvable");
        }
        if (roleTimer == null) {
            Debug.LogError("[RoleRevealUI] 'role-timer' introuvable");
        }
    }

    // Affiche le rôle du joueur avec animation
    public void ShowRole(Role playerRole){
        Debug.Log($"[RoleRevealUI] ShowRole appelé pour {playerRole}");

        // Initialiser si pas encore fait
        if (!isInitialized) {
            InitializeUI();
        }

        if (overlay == null || roleTitle == null || roleName == null || roleDescription == null){
            Debug.LogError("[RoleRevealUI] Éléments UI manquants, impossible d'afficher");
            return;
        }

        if (isShowing){
            Debug.LogWarning("[RoleRevealUI] Déjà en train d'afficher, ignoré");
            return;
        }

        // Config selon rôle
        string description = "";
        string roleClass = "";
        string infoText = "";
        float duration = 0f;

        switch (playerRole){
            case Role.Gardien:
                description = "Éliminez les Ombres avec votre lampe avant qu'elles ne trouvent les clés et s'enfuient !";
                roleClass = "role-name--gardien";
                infoText = "Les Ombres se cachent...";
                duration = gardienDisplayDuration;
                break;

            case Role.Ombre:
                description = "Collectez les clés et échappez-vous sans vous faire prendre par la lampe du Gardien !";
                roleClass = "role-name--ombre";
                infoText = "Vous avez 15 sec pour vous cacher...";
                duration = ombreDisplayDuration;
                break;
        }

        roleTitle.text = "Vous êtes";
        roleName.text = playerRole.ToString();
        roleDescription.text = description;

        if (roleInfo != null){
            if (string.IsNullOrEmpty(infoText)){
                roleInfo.style.display = DisplayStyle.None;
            }
            else{
                roleInfo.text = infoText;
                roleInfo.style.display = DisplayStyle.Flex;
            }
        }

        roleName.ClearClassList();
        roleName.AddToClassList("role-name");
        roleName.AddToClassList(roleClass);

        overlay.style.display = DisplayStyle.Flex;
        overlay.style.opacity = 1f;

        isShowing = true;

        Debug.Log($"[RoleRevealUI] Overlay affiché pour {playerRole}, durée: {duration}s");


        if (timerCoroutine != null)
            StopCoroutine(timerCoroutine);
        
        timerCoroutine = StartCoroutine(CountdownAndFadeOut(duration));
    }

    private IEnumerator CountdownAndFadeOut(float duration){
        float remainingTime = duration;

        while (remainingTime > 0) {
            if (roleTimer != null) {
                int seconds = Mathf.CeilToInt(remainingTime);
                roleTimer.text = $"Début dans {seconds}s";

                // Changer la couleur quand <3s
                if (seconds <= 3){
                    roleTimer.AddToClassList("role-timer--warning");
                }
                else{
                    roleTimer.RemoveFromClassList("role-timer--warning");
                }
            }

            remainingTime -= Time.deltaTime;
            yield return null;
        }

        if (roleTimer != null){
            roleTimer.text = "C'est parti";
        }

        Debug.Log("[RoleRevealUI] Countdown terminé");

        // Attendre avant fade out
        yield return new WaitForSeconds(0.5f);

        // Notifier que reveal terminé --> débloquer les contrôles
        OnRevealComplete?.Invoke();

        float elapsed = 0f;

        while (elapsed < fadeOutDuration) {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            overlay.style.opacity = alpha;
            yield return null;
        }

        overlay.style.opacity = 0f;
        overlay.style.display = DisplayStyle.None;
        isShowing = false;

        Debug.Log("[RoleRevealUI] Overlay caché");
    }

    // Cache l'overlay sans animation
    public void HideImmediate() {
        if (timerCoroutine != null){
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        if (overlay != null){
            overlay.style.display = DisplayStyle.None;
            overlay.style.opacity = 0f;
        }
        isShowing = false;
    }

    // Force le fade out 
    public void ForceFadeOut(){
        if (isShowing && timerCoroutine != null){
            StopCoroutine(timerCoroutine);
            timerCoroutine = StartCoroutine(FadeOutImmediate());
        }
    }

    private IEnumerator FadeOutImmediate(){
        float elapsed = 0f;

        while (elapsed < fadeOutDuration){
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            overlay.style.opacity = alpha;
            yield return null;
        }

        overlay.style.opacity = 0f;
        overlay.style.display = DisplayStyle.None;
        isShowing = false;
    }
}