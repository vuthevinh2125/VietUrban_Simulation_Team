
using Firebase.Auth;
using Firebase.Extensions;
using Firebase;
using TMPro;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AccountMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenuContent;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text emailText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_InputField newPasswordInput;
    [SerializeField] private Image passwordBackground;
    [SerializeField] private TMP_Text passwordLabel;
    [SerializeField] private TMP_Text passwordPlaceholder;
    [SerializeField] private GameObject confirmPasswordButton;
    [SerializeField] private GameObject cancelPasswordButton;
    [SerializeField] private GameObject profileMessageRow;
    [SerializeField] private Image profileMessageBackground;
    [SerializeField] private Image profileMessageIcon;
    [SerializeField] private TMP_Text profileMessageText;
    [SerializeField] private Sprite inputFieldSprite;
    [SerializeField] private Sprite inputReadonlySprite;
    [SerializeField] private Sprite errorBarSprite;
    [SerializeField] private Sprite successBarSprite;
    [SerializeField] private Sprite alertIconSprite;
    [SerializeField] private Sprite checkIconSprite;
    [SerializeField] private string authSceneName = "AuthScene";
    private static readonly Color errorTextColor = new Color32(0x79, 0x1F, 0x1F, 255);
    private static readonly Color successTextColor = new Color32(0x27, 0x50, 0x0A, 255);
    private void Awake()
    {
        CloseProfile();
        if (statusText != null)
        {
            statusText.text = "";
        }
        ExitPasswordEditMode();
        HideProfileMessage();
    }
    public void OpenProfile()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(authSceneName);
            return;
        }
        PopulateProfile(user);
        HideProfileMessage();
        if (statusText != null)
        {
            statusText.text = "";
        }
        if (pauseMenuContent != null)
        {
            pauseMenuContent.SetActive(false);
        }
        if (profilePanel != null)
        {
            profilePanel.SetActive(true);
        }
    }
    public void PopulateProfile(FirebaseUser user)
    {
        if (user == null)
        {
            return;
        }
        if (displayNameText != null)
        {
            if (string.IsNullOrEmpty(user.DisplayName))
            {
                displayNameText.text = "";
            }
            else
            {
                displayNameText.text = user.DisplayName;
            }
        }
        if (emailText != null)
        {
            if (string.IsNullOrEmpty(user.Email))
            {
                emailText.text = "";
            }
            else
            {
                emailText.text = user.Email;
            }
        }
        if (profilePanel != null)
        {
            profilePanel.SetActive(true);
        }
    }
    public void CloseProfile()
    {
        ExitPasswordEditMode();
        HideProfileMessage();
        if (profilePanel != null)
        {
            profilePanel.SetActive(false);
        }
        if (pauseMenuContent != null)
        {
            pauseMenuContent.SetActive(true);
        }
    }
    public void EnterPasswordEditMode()
    {
        HideProfileMessage();
        if (newPasswordInput != null)
        {
            newPasswordInput.text = "";
            newPasswordInput.interactable = true;
            newPasswordInput.ActivateInputField();
        }
        if (passwordBackground != null)
        {
            passwordBackground.sprite = inputFieldSprite;
        }
        if (passwordLabel != null)
        {
            passwordLabel.text = "New password";
        }
        if (passwordPlaceholder != null)
        {
            passwordPlaceholder.text = "Enter new password";
        }
        if (confirmPasswordButton != null)
        {
            confirmPasswordButton.SetActive(true);
        }
        if (cancelPasswordButton != null)
        {
            cancelPasswordButton.SetActive(true);
        }
    }
    public void CancelPasswordEdit()
    {
        ExitPasswordEditMode();
        HideProfileMessage();
    }
    public void ConfirmPasswordChange()
    {
        HideProfileMessage();
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            ShowProfileMessage("You are not signed in", false);
            return;
        }
        string newPassword = "";
        if (newPasswordInput != null)
        {
            newPassword = newPasswordInput.text;
        }
        if (newPassword.Length < 6)
        {
            ShowProfileMessage("Password must have at least 6 characters", false);
            return;
        }
        user.UpdatePasswordAsync(newPassword).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                FirebaseException firebaseException = null;
                if (task.Exception != null)
                {
                    firebaseException = task.Exception.GetBaseException() as FirebaseException;
                }
                if (firebaseException != null &&
                    (AuthError)firebaseException.ErrorCode == AuthError.RequiresRecentLogin)
                {
                    ShowProfileMessage("Please log out and log in again, then change your password", false);
                }
                else
                {
                    ShowProfileMessage("Could not change password. Please try again", false);
                }
                return;
            }
            ExitPasswordEditMode();
            ShowProfileMessage("Password changed successfully", true);
        });
    }
    public void LogOut()
    {
        FirebaseUser user = FirebaseAuth.DefaultInstance.CurrentUser;
        if (user == null)
        {
            LoadAuthScene();
            return;
        }
        PlayerData currentData = GameSession.Current;
        SaveSystem.SaveLocalOnly(currentData);
        if (statusText != null)
        {
            statusText.text = "saving before logout...";
        }
        else
        {
            ShowProfileMessage("saving before logout...", true);
        }
        FirebaseCloudSaveService.SaveImmediately(currentData, delegate (bool saved)
        {
            if (saved == false)
            {
                if (statusText != null)
                {
                    statusText.text = "could not sync your save. check your connection and try again.";
                }
                else
                {
                    ShowProfileMessage("could not sync your save. check your connection and try again.", false);
                }
                Debug.Log("logout stopped because cloud save failed");
                return;
            }
            FirebaseAuth.DefaultInstance.SignOut();
            LoadAuthScene();
        });
    }
    private void LoadAuthScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(authSceneName);
    }
    private void ExitPasswordEditMode()
    {
        if (newPasswordInput != null)
        {
            newPasswordInput.text = "";
            newPasswordInput.interactable = false;
        }
        if (passwordBackground != null)
        {
            passwordBackground.sprite = inputReadonlySprite;
        }
        if (passwordLabel != null)
        {
            passwordLabel.text = "Password";
        }
        if (passwordPlaceholder != null)
        {
            passwordPlaceholder.text = "••••••••";
        }
        if (confirmPasswordButton != null)
        {
            confirmPasswordButton.SetActive(false);
        }
        if (cancelPasswordButton != null)
        {
            cancelPasswordButton.SetActive(false);
        }
    }
    public void ShowProfileMessage(string message, bool success)
    {
        if (profileMessageRow == null ||
            profileMessageBackground == null ||
            profileMessageIcon == null ||
            profileMessageText == null)
        {
            return;
        }
        if (success == true)
        {
            profileMessageBackground.sprite = successBarSprite;
            profileMessageIcon.sprite = checkIconSprite;
            profileMessageText.color = successTextColor;
        }
        else
        {
            profileMessageBackground.sprite = errorBarSprite;
            profileMessageIcon.sprite = alertIconSprite;
            profileMessageText.color = errorTextColor;
        }
        profileMessageText.text = message;
        profileMessageRow.SetActive(true);
    }
    public void HideProfileMessageFromAuth()
    {
        if (profileMessageRow != null)
        {
            profileMessageRow.SetActive(false);
        }
    }
    private void HideProfileMessage()
    {
        HideProfileMessageFromAuth();
    }
}
