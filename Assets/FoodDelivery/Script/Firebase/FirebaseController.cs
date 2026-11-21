
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using System.Collections;
using System.Net.Mail;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FirebaseController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject signUpPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject resetPasswordPanel;
    [SerializeField] private AccountMenuController accountMenuController;
    [Header("Login / Sign up")]
    [SerializeField] private GameObject loginError;
    [SerializeField] private GameObject signUpError;
    [SerializeField] private TMP_Text loginErrorText;
    [SerializeField] private TMP_Text signUpErrorText;
    [SerializeField] private TMP_InputField loginEmail;
    [SerializeField] private TMP_InputField loginPassword;
    [SerializeField] private TMP_InputField signUpEmail;
    [SerializeField] private TMP_InputField signUpUsername;
    [SerializeField] private TMP_InputField signUpPassword;
    [SerializeField] private TMP_InputField signUpCPassword;
    [Header("Reset password panel")]
    [SerializeField] private TMP_InputField resetPEmail;
    [SerializeField] private Image resetEmailBg;
    [SerializeField] private GameObject resetMessageRow;
    [SerializeField] private Image resetMessageBg;
    [SerializeField] private Image resetMessageIcon;
    [SerializeField] private TMP_Text resetMessageText;
    [SerializeField] private Button resetSendButton;
    [SerializeField] private TMP_Text resetSendButtonText;
    [Header("Scene flow")]
    [SerializeField] private string gameSceneName = "SampleScene";
    [Header("Sprites")]
    [SerializeField] private Sprite inputFieldSprite;
    [SerializeField] private Sprite inputReadonlySprite;
    [SerializeField] private Sprite errorBarSprite;
    [SerializeField] private Sprite successBarSprite;
    [SerializeField] private Sprite alertIconSprite;
    [SerializeField] private Sprite checkIconSprite;
    private static readonly Color errorTextColor = new Color32(0x79, 0x1F, 0x1F, 255);
    private static readonly Color successTextColor = new Color32(0x27, 0x50, 0x0A, 255);
    private const int ResendSeconds = 30;
    private FirebaseAuth auth;
    private Coroutine resetCooldown;
    private bool accountSaveReady;
    private void Awake()
    {
        if (accountMenuController == null)
        {
            accountMenuController = GetComponent<AccountMenuController>();
        }
        loginError.SetActive(false);
        signUpError.SetActive(false);
        resetMessageRow.SetActive(false);
        HideProfileMessage();
        loginPanel.SetActive(true);
        signUpPanel.SetActive(false);
        profilePanel.SetActive(false);
        resetPasswordPanel.SetActive(false);
        ExitPasswordEditMode();
        ResetResetPanel();
    }
    private void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                auth = FirebaseAuth.DefaultInstance;
            }
            else
            {
                Debug.LogError("Could not resolve all Firebase dependencies: " + task.Result);
            }
        });
    }
    private void ShowPanel(GameObject target)
    {
        HideErrors();
        ResetResetPanel();
        ExitPasswordEditMode();
        HideProfileMessage();
        loginPanel.SetActive(target == loginPanel);
        signUpPanel.SetActive(target == signUpPanel);
        profilePanel.SetActive(target == profilePanel);
        resetPasswordPanel.SetActive(target == resetPasswordPanel);
    }
    public void OpenLoginPage()
    {
        ShowPanel(loginPanel);
    }
    public void OpenSignUpPage()
    {
        ShowPanel(signUpPanel);
    }
    public void OpenResetPasswordPage()
    {
        ShowPanel(resetPasswordPanel);
    }
    public void OpenProfilePage()
    {
        if (auth == null || auth.CurrentUser == null)
        {
            ShowPanel(loginPanel);
            ShowLoginError("Please log in to view your profile");
            return;
        }
        if (accountMenuController == null)
        {
            Debug.Log("account menu controller is not assigned");
            return;
        }
        accountMenuController.PopulateProfile(auth.CurrentUser);
        ShowPanel(profilePanel);
    }
    public void CloseProfile()
    {
        ExitPasswordEditMode();
        HideProfileMessage();
        if (auth != null && auth.CurrentUser != null)
        {
            if (accountSaveReady)
            {
                if (accountMenuController != null) accountMenuController.CloseProfile();
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                ShowProfileMessage("Could not load your cloud save. Check your connection and try again", false);
            }
        }
        else
        {
            OpenLoginPage();
        }
    }
    public void Login()
    {
        HideErrors();
        if (auth == null)
        {
            ShowLoginError("Connecting to server, please try again");
            return;
        }
        if (string.IsNullOrEmpty(loginEmail.text) || string.IsNullOrEmpty(loginPassword.text))
        {
            ShowLoginError("Please fill in email and password");
            return;
        }
        SignInUser(loginEmail.text.Trim(), loginPassword.text);
    }
    public void SignUp()
    {
        HideErrors();
        if (auth == null)
        {
            ShowSignUpError("Connecting to server, please try again");
            return;
        }
        string mail = signUpEmail.text.Trim();
        string name = signUpUsername.text.Trim();
        if (string.IsNullOrEmpty(mail) ||
            string.IsNullOrEmpty(name) ||
            string.IsNullOrEmpty(signUpPassword.text) ||
            string.IsNullOrEmpty(signUpCPassword.text))
        {
            ShowSignUpError("Please fill in all fields");
            return;
        }
        if (!IsValidEmail(mail))
        {
            ShowSignUpError("Invalid email");
            return;
        }
        if (signUpPassword.text.Length < 6)
        {
            ShowSignUpError("Password must have at least 6 characters");
            return;
        }
        if (signUpPassword.text != signUpCPassword.text)
        {
            ShowSignUpError("Confirm password does not match");
            return;
        }
        CreateUser(mail, signUpPassword.text, name);
    }
    public void LogOut()
    {
        accountSaveReady = false;
        if (accountMenuController != null)
        {
            accountMenuController.LogOut();
        }
        ClearAuthFields();
    }
    private void CreateUser(string mail, string pass, string name)
    {
        auth.CreateUserWithEmailAndPasswordAsync(mail, pass).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Sign up failed: " + task.Exception);
                ShowSignUpError("Sign up failed. Email may already exist");
                return;
            }
            UpdateUserProfile(task.Result.User, name);
        });
    }
    private void UpdateUserProfile(FirebaseUser newUser, string name)
    {
        UserProfile profile = new UserProfile();
        profile.DisplayName = name;
        newUser.UpdateUserProfileAsync(profile).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Update profile failed: " + task.Exception);
                LoadAccountAndShowProfile(newUser, true);
                return;
            }
            LoadAccountAndShowProfile(newUser, true);
        });
    }
    private void SignInUser(string mail, string pass)
    {
        auth.SignInWithEmailAndPasswordAsync(mail, pass).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("Sign in failed: " + task.Exception);
                ShowLoginError("Wrong email or password");
                return;
            }
            FirebaseUser signedIn = task.Result.User;
            LoadAccountAndShowProfile(signedIn, false);
        });
    }
    private void LoadAccountAndShowProfile(FirebaseUser signedIn, bool startFreshIfCloudSaveMissing)
    {
        accountSaveReady = false;
        FirebaseCloudSaveService.LoadForUser(signedIn, delegate(PlayerData data)
        {
            if (data != null)
            {
                GameSession.SetCurrent(data);
                accountSaveReady = true;
            }
            ShowProfile();
            if (!accountSaveReady)
            {
                ShowProfileMessage("Could not load your cloud save. Check your connection and try again", false);
            }
        }, startFreshIfCloudSaveMissing);
    }
    public void ForgetPassword()
    {
        HideResetMessage();
        if (auth == null)
        {
            ShowResetMessage("Connecting to server, please try again", false);
            return;
        }
        string mail = resetPEmail.text.Trim();
        if (string.IsNullOrEmpty(mail))
        {
            ShowResetMessage("Please enter your email", false);
            return;
        }
        if (!IsValidEmail(mail))
        {
            ShowResetMessage("Invalid email", false);
            return;
        }
        resetSendButton.interactable = false;
        auth.SendPasswordResetEmailAsync(mail).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                FirebaseException firebaseException = null;
                if (task.Exception != null)
                {
                    firebaseException = task.Exception.GetBaseException() as FirebaseException;
                }
                if (firebaseException != null &&
                    (AuthError)firebaseException.ErrorCode == AuthError.UserNotFound)
                {
                    ShowResetSuccess(mail);
                    return;
                }
                Debug.Log("password reset request failed");
                resetSendButton.interactable = true;
                resetSendButtonText.text = "Send link";
                ShowResetMessage("Could not send the email. Check your connection and try again", false);
                return;
            }
            ShowResetSuccess(mail);
        });
    }
    private void ShowResetSuccess(string mail)
    {
        ShowResetMessage("If this email is registered, a reset link has been sent", true);
        resetPEmail.text = mail;
        resetPEmail.interactable = false;
        resetEmailBg.sprite = inputReadonlySprite;
        StartResetCooldown();
    }
    private void StartResetCooldown()
    {
        if (resetCooldown != null)
        {
            StopCoroutine(resetCooldown);
        }
        resetCooldown = StartCoroutine(ResetCooldownRoutine());
    }
    private IEnumerator ResetCooldownRoutine()
    {
        resetSendButton.interactable = false;
        for (int s = ResendSeconds; s > 0; s--)
        {
            resetSendButtonText.text = "Resend in " + s + "s";
            yield return new WaitForSeconds(1f);
        }
        resetSendButton.interactable = true;
        resetSendButtonText.text = "Resend";
        resetCooldown = null;
    }
    private void ResetResetPanel()
    {
        if (resetCooldown != null)
        {
            StopCoroutine(resetCooldown);
            resetCooldown = null;
        }
        if (resetPEmail != null)
        {
            resetPEmail.text = "";
        }
        if (resetPEmail != null) resetPEmail.interactable = true;
        if (resetEmailBg != null) resetEmailBg.sprite = inputFieldSprite;
        if (resetSendButton != null) resetSendButton.interactable = true;
        if (resetSendButtonText != null) resetSendButtonText.text = "Send link";
        if (resetMessageRow != null) HideResetMessage();
    }
    public void EnterPasswordEditMode()
    {
        if (accountMenuController != null)
        {
            accountMenuController.EnterPasswordEditMode();
        }
    }
    public void CancelPasswordEdit()
    {
        if (accountMenuController != null) accountMenuController.CancelPasswordEdit();
    }
    public void ConfirmPasswordChange()
    {
        if (accountMenuController != null) accountMenuController.ConfirmPasswordChange();
    }
    private void ExitPasswordEditMode()
    {
        if (accountMenuController != null) accountMenuController.CancelPasswordEdit();
    }
    private void ShowProfile()
    {
        if (accountMenuController == null)
        {
            Debug.Log("account menu controller is not assigned");
            return;
        }
        accountMenuController.PopulateProfile(auth.CurrentUser);
        ClearAuthFields();
        ShowPanel(profilePanel);
    }
    private void ClearAuthFields()
    {
        loginEmail.text = "";
        loginPassword.text = "";
        signUpEmail.text = "";
        signUpUsername.text = "";
        signUpPassword.text = "";
        signUpCPassword.text = "";
    }
    private bool IsValidEmail(string value)
    {
        try
        {
            MailAddress address = new MailAddress(value);
            return address.Address == value;
        }
        catch
        {
            return false;
        }
    }
    private void ShowLoginError(string message)
    {
        loginErrorText.text = message;
        loginError.SetActive(true);
    }
    private void ShowSignUpError(string message)
    {
        signUpErrorText.text = message;
        signUpError.SetActive(true);
    }
    private void HideErrors()
    {
        loginError.SetActive(false);
        signUpError.SetActive(false);
    }
    private void ShowResetMessage(string message, bool success)
    {
        SetMessage(resetMessageRow, resetMessageBg, resetMessageIcon, resetMessageText, message, success);
    }
    private void HideResetMessage()
    {
        resetMessageRow.SetActive(false);
    }
    private void ShowProfileMessage(string message, bool success)
    {
        if (accountMenuController != null) accountMenuController.ShowProfileMessage(message, success);
    }
    private void HideProfileMessage()
    {
        if (accountMenuController != null) accountMenuController.HideProfileMessageFromAuth();
    }
    private void SetMessage(GameObject row, Image bg, Image icon, TMP_Text text, string message, bool success)
    {
        if (success)
        {
            bg.sprite = successBarSprite;
            icon.sprite = checkIconSprite;
            text.color = successTextColor;
        }
        else
        {
            bg.sprite = errorBarSprite;
            icon.sprite = alertIconSprite;
            text.color = errorTextColor;
        }
        text.text = message;
        row.SetActive(true);
    }
}
