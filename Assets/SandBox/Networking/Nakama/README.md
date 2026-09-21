# Nakama Network Integration for Railbound

This integration provides Nakama multiplayer/backend connectivity for Railbound game using the HeroicLabs Nakama Unity SDK.

## 📁 Files Created

### 1. `NakamaConnection.cs`
A unified connection manager that handles:
- Device-based authentication (guest login)
- Username/password authentication (email auth)
- Custom ID authentication
- Session management and auto-refresh
- Socket connections for real-time multiplayer
- Privacy policy management
- Matchmaking functionality

### 2. `LoginManager.cs`
UI controller for the login menu that integrates Nakama authentication:
- Username and password login
- Guest login
- Register new accounts
- Privacy policy popup with explanation
- Auto-connect on app start if session exists
- Cross-references with existing `LoginMenuController.cs`

## 🔧 Setup Instructions

### Step 1: Configure Nakama Server
Make sure you have a running Nakama server:
```
docker-compose up -d nakama
```

Default settings:
- Scheme: `https` (or `http`)
- Host: `127.0.0.1` (or your server IP)
- Port: `7350` (HTTP) or `7359` (HTTPS)
- Server Key: Generate in your Nakama dashboard

### Step 2: Update NakamaConnection ScriptableObject
1. Find `Assets/SandBox/Networking/NakamaConnection.unity` in your project
2. Select it and update the `NakamaConnection` component settings:
   - **Scheme**: HTTP or HTTPS
   - **Host**: Your Nakama server IP
   - **Port**: Nakama server port
   - **Server Key**: Your server key
   - **Session Storage Key**: Default: `nakama.session.token`
   - **Device Identifier Storage Key**: Default: `nakama.device.id`

### Step 3: Add LoginManager to Login Menu

In your `Assets/Project/LoginMenu.unity`:

1. Add a `CanvasGroup` named `LoginMenuCanvasGroup` that contains the login UI
2. Attach the `LoginManager` script to the LoginMenu object
3. Assign references:
   - **Nakama Connection**: Assign your `NakamaConnection` ScriptableObject
   - **Username Input**: Username input field
   - **Password Input**: Password input field
   - **Login Button**: Login button reference
   - **Register Button**: Register button reference
   - **Guest Button**: Guest login button reference
   - **More Button**: Reference to the MoreButton from LoginMenuController
   - **Account Login Popup**: CanvasGroup from the account login popup
   - **Login Status Text**: Text element for status messages
   - **Privacy Policy Popup**: The privacy policy popup GameObject

### Step 4: UI Hierarchy Example

```
LoginMenu
├── CanvasGroup (LoginMenuCanvasGroup)
│   ├── Background
│   ├── LoginPanel
│   │   ├── Title
│   │   ├── UsernameInputField
│   │   ├── PasswordInputField
│   │   ├── LoginStatusText
│   │   ├── LoginButton
│   │   ├── RegisterButton
│   │   └── GuestButton
│   └── MoreButton (Button)
├── AccountLoginPopup (CanvasGroup)
│   └── AccountLoginCard
│       └── CloseButton
└── PrivacyPolicyPopup (GameObject)
    ├── Background
    ├── Container
    │   ├── Title
    │   ├── Content (TextMeshPro)
    │   ├── AcceptButton
    │   └── QuitButton
```

## 🎮 Usage

### LoginManager API

```csharp
// Get the LoginManager
var loginManager = FindObjectOfType<LoginManager>();

// Guest Login
await loginManager.AuthenticateDeviceAsync();

// Username/Password Login
loginManager.usernameInput.text = "player1";
loginManager.passwordInput.text = "password";
loginManager.OnLoginClicked();

// Register New Account
loginManager.usernameInput.text = "newplayer";
loginManager.passwordInput.text = "newpassword";
loginManager.OnRegisterClicked();

// Log out
loginManager.Logout();

// Check privacy policy
bool accepted = loginManager.nakamaConnection.HasAcceptedPrivacyPolicy();

// Get current session
var session = loginManager.nakamaConnection.Session;
if (session != null) {
    Debug.Log($"User: {session.Username}, ID: {session.UserId}");
}
```

### NakamaConnection API

```csharp
var nakamaConn = FindObjectOfType<NakamaConnection>();

// Auto-connect with existing session
await nakamaConn.ConnectAsync();

// Check connection state
if (nakamaConn.ConnectionState == ConnectionState.Connected) {
    Debug.Log("Connected to Nakama!");
}

// Guest authentication
await nakamaConn.AuthenticateDeviceAsync();

// Username/password authentication
await nakamaConn.AuthenticateUsernamePasswordAsync("username", "password", true);

// Check privacy policy
if (nakamaConn.HasAcceptedPrivacyPolicy()) {
    // User accepted
} else {
    // Show privacy policy
    loginManager.ShowPrivacyPolicyPopup();
}
```

## 🔐 Privacy Policy Features

The integration includes a complete privacy policy popup that:
- Displays at startup if not accepted
- Shows appropriate legal text about data collection and usage
- Tracks acceptance status in PlayerPrefs
- Accept button saves the preference and closes the popup
- Can trigger on demand via `LoginManager.ShowPrivacyPolicyPopup()`

## 🎯 Authentication Methods

### 1. Device Auth (Guest)
- Uses unique device identifier
- No username/password required
- Good for quick play or mobile apps

### 2. Email Auth (Username/Password)
- Standard account creation
- More secure and persistent
- Allows user-selectable usernames

### 3. Custom ID Auth
- Alternative to device auth
- Uses custom tokens from your backend

## 📡 Socket Connection

The connection manager automatically creates a socket connection for real-time features:

```csharp
// Get socket for multiplayer
var socket = nakamaConn.Socket;
if (socket != null) {
    // Socket is connected and ready for events
}
```

## 🔧 Debug Logging

All connection activities are logged with `[NakamaConnection]` and `[LoginManager]` prefixes:

```
[NakamaConnection] Initialized with https://127.0.0.1:7359
[NakamaConnection] Session restored from storage
[NakamaConnection] Authenticated successfully! User ID: abc123
[NakamaConnection] Socket connected successfully
[LoginManager] Authentication successful: abc123
```

## ⚠️ Important Notes

1. **Server Key Security**: Never commit your Nakama server key to version control
2. **HTTPS Recommended**: Use HTTPS for production connections
3. **Session Persistence**: Sessions are stored in PlayerPrefs (local device storage)
4. **Network Errors**: Connection failures are logged with detailed error messages
5. **Auto-Retry**: On startup, tries to restore existing session before prompting for login

## 🚀 Next Steps

After setting up the login system, you can add:

1. **Matchmaking UI** - Connect to multiplayer games
2. **Leaderboards** - Display and submit scores
3. **Friend System** - Add/Unfriend other players
4. **Social Feeds** - Player messages and status
5. **Storage System** - Save player progress on the server

## 📝 Example Usage in Scene

```csharp
using UnityEngine;

public class GameLauncher : MonoBehaviour
{
    private LoginManager loginManager;

    private void Start()
    {
        loginManager = FindObjectOfType<LoginManager>();
        if (loginManager == null)
        {
            Debug.LogError("LoginManager not found!");
            return;
        }

        // Listen for login success
        loginManager.OnLoginSuccess += OnLoggedIn;

        // Listen for logout
        loginManager.OnLogout += OnLoggedOut;
    }

    private void OnLoggedIn(GameObject obj)
    {
        // Transition to main menu
        Debug.Log("Player logged in!");
        // Load main menu scene...
    }

    private void OnLoggedOut()
    {
        // Return to login screen
        Debug.Log("Player logged out!");
        // Return to login scene...
    }
}
```

## 📚 Links

- [Nakama Documentation](https://heroiclabs.com/docs/)
- [Nakama Unity SDK](https://github.com/heroiclabs/nakama-unity)
- [Nakama Docker Setup](https://heroiclabs.com/docs/setup-docker/)
