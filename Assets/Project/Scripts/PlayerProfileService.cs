using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

public enum PlayerExperienceLevel
{
    Inexpert,
    Accomplished,
    Master
}

[Serializable]
public sealed class PlayerProfile
{
    public string username;
    public PlayerExperienceLevel experienceLevel;
    public string userId;
    public bool isComplete;
    public int version = 1;
}

public static class PlayerProfileService
{
    private const string Collection = "player_profiles";
    private const string Key = "profile";
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 16;

    private static PlayerProfile _cached;

    private static AuthenticationService Auth
    {
        get
        {
            var auth = AuthenticationService.Instance;
            if (auth == null || !auth.IsAuthenticated)
                throw new InvalidOperationException("AuthenticationService is not authenticated.");
            return auth;
        }
    }

    public static async Task<PlayerProfile> LoadAsync()
    {
        var auth = Auth;
        var objectId = new StorageObjectId
        {
            Collection = Collection,
            Key = Key,
            UserId = auth.CurrentUserId
        };

        try
        {
            var result = await auth.Client.ReadStorageObjectsAsync(auth.Session, new[] { objectId });
            
            foreach (var storageObject in result.Objects)
            {
                if (string.IsNullOrWhiteSpace(storageObject.Value))
                    return null;

                var profile = JsonUtility.FromJson<PlayerProfile>(storageObject.Value);
                if (profile == null || !profile.isComplete)
                    return null;

                profile.username = profile.username?.Trim();
                if (string.IsNullOrWhiteSpace(profile.username))
                    return null;

                profile.userId = auth.CurrentUserId;
                _cached = profile;
                return profile;
            }
        }
        catch (ApiResponseException ex)
        {
            Debug.LogWarning($"[PlayerProfileService] Reading the profile failed (HTTP {ex.StatusCode}): {ex.Message}");
            if (_cached != null && _cached.userId == auth.CurrentUserId) 
                return _cached;
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[PlayerProfileService] Failed to parse profile JSON: {ex.Message}");
        }

        return null;
    }

    public static async Task SaveAsync(string username, PlayerExperienceLevel experienceLevel, bool isComplete = true)
    {
        var auth = Auth;
        var cleanUsername = username?.Trim();

        if (string.IsNullOrWhiteSpace(cleanUsername))
            throw new ArgumentException("Username cannot be empty.", nameof(username));

        if (cleanUsername.Length < MinUsernameLength || cleanUsername.Length > MaxUsernameLength)
            throw new ArgumentOutOfRangeException(nameof(username), $"Username must be between {MinUsernameLength} and {MaxUsernameLength} characters.");

        var profile = new PlayerProfile
        {
            username = cleanUsername,
            experienceLevel = experienceLevel,
            userId = auth.CurrentUserId,
            isComplete = isComplete,
            version = 1
        };

        await auth.Client.UpdateAccountAsync(auth.Session, username: cleanUsername, displayName: cleanUsername);

        var writeObject = new WriteStorageObject
        {
            Collection = Collection,
            Key = Key,
            Value = JsonUtility.ToJson(profile),
            PermissionRead = 2,
            PermissionWrite = 1
        };

        await auth.Client.WriteStorageObjectsAsync(auth.Session, new[] { writeObject });
    }
}
