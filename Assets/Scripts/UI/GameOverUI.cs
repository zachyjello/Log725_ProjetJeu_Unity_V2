using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;

public class GameOverUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public Button quitGameButton;

    private void Awake()
    {
        // Setup button listeners
        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(OnQuitGameClicked);

        // Montrer le curseur pour l'UI
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Récupérer les infos de fin de partie
        bool won = PlayerPrefs.GetInt("GameOver_Win", 0) == 1;
        string subtitle = PlayerPrefs.GetString("GameOver_Subtitle", "");
        ShowGameOver(won, subtitle);
    }

    public void ShowGameOver(bool playerWon, string subtitle = "")
    {
        string title = playerWon ? "GAGNÉ !" : "PERDU !";
        if (titleText != null)
            titleText.text = title;
        if (subtitleText != null)
            subtitleText.text = subtitle;

        gameObject.SetActive(true);
    }

    public void HideInstant()
    {
        gameObject.SetActive(false);
    }

    public void ShowInstant()
    {
        gameObject.SetActive(true);
    }

    private void OnQuitGameClicked()
    {
        Debug.Log("Retour au menu de sélection (quitter la partie)");
        if (CustomNetworkRoomManager.Instance != null)
        {
            Debug.Log("CustomNetworkRoomManager trouvé, appel ReturnToGameSelection");
            CustomNetworkRoomManager.Instance.ReturnToGameSelection();
        }
        else
        {
            Debug.Log("CustomNetworkRoomManager non trouvé, chargement direct de GameSelectionMenu");
            SceneManager.LoadScene("GameSelectionMenu");
        }
    }

    private void OnDestroy()
    {
        if (quitGameButton != null)
            quitGameButton.onClick.RemoveListener(OnQuitGameClicked);
    }
}