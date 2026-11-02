using System;
using System.Collections.Generic;
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

    public static async Task<PlayerProfile> LoadAsync()
    {
        var authentication = AuthenticationService.Instance;
        if (authentication == null || !authentication.IsAuthenticated)
            throw new InvalidOperationException("AuthenticationService is not authenticated.");

        var objectId = new StorageObjectId
        {
            Collection = Collection,
            Key = Key,
            UserId = authentication.CurrentUserId
        };

        var result = await authentication.Client.ReadStorageObjectsAsync(authentication.Session, new[] { objectId });
        foreach (var storageObject in result.Objects)
        {
            if (string.IsNullOrWhiteSpace(storageObject.Value))
                return null;

            try
            {
                var profile = UnityEngine.JsonUtility.FromJson<PlayerProfile>(storageObject.Value);
                if (profile == null)
                    return null;

                profile.username = profile.username?.Trim();
                if (string.IsNullOrWhiteSpace(profile.username))
                    return null;

                if (!profile.isComplete)
                    return null;

                profile.userId = authentication.CurrentUserId;
                return profile;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerProfileService] Failed to parse profile JSON: {ex.Message}");
                return null;
            }
        }

        return null;
    }

    public static async Task SaveAsync(string username, PlayerExperienceLevel experienceLevel, bool isComplete = true)
    {
        var authentication = AuthenticationService.Instance;
        if (authentication == null || !authentication.IsAuthenticated)
            throw new InvalidOperationException("AuthenticationService is not authenticated.");

        var cleanUsername = username?.Trim();
        if (string.IsNullOrWhiteSpace(cleanUsername))
            throw new InvalidOperationException("Username cannot be empty.");

        if (cleanUsername.Length < 3 || cleanUsername.Length > 16)
            throw new InvalidOperationException("Username must be between 3 and 16 characters.");

        var profile = new PlayerProfile
        {
            username = cleanUsername,
            experienceLevel = experienceLevel,
            userId = authentication.CurrentUserId,
            isComplete = isComplete,
            version = 1
        };

        await authentication.Client.UpdateAccountAsync(
            authentication.Session,
            username: cleanUsername,
            displayName: cleanUsername);

        var writeObject = new WriteStorageObject
        {
            Collection = Collection,
            Key = Key,
            Value = UnityEngine.JsonUtility.ToJson(profile),
            PermissionRead = 2,
            PermissionWrite = 1
        };

        await authentication.Client.WriteStorageObjectsAsync(authentication.Session, new[] { writeObject });
    }
}
