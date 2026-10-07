using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class LobbyAccessGuard : MonoBehaviour
{
    [SerializeField] private string loginSceneName = "LoginMenu";
    [SerializeField] private float authenticationWaitSeconds = 8f;

    private bool _redirecting;

    private void Start()
    {
        StartCoroutine(ValidateAccess());
    }

    private IEnumerator ValidateAccess()
    {
        var deadline = Time.realtimeSinceStartup + authenticationWaitSeconds;
        while (AuthenticationService.Instance != null &&
               !AuthenticationService.Instance.IsAuthenticationReady &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        if (AuthenticationService.Instance == null ||
            !AuthenticationService.Instance.IsAuthenticationReady ||
            !AuthenticationService.Instance.IsAuthenticated)
        {
            RedirectToLogin();
        }
    }

    private void RedirectToLogin()
    {
        if (_redirecting || string.IsNullOrEmpty(loginSceneName))
            return;

        _redirecting = true;
        Debug.LogWarning("[LobbyAccessGuard] Unauthenticated lobby access denied. Returning to LoginMenu.");
        SceneManager.LoadScene(loginSceneName);
    }
}
