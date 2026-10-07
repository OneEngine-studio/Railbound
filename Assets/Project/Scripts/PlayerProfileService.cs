using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nakama;

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

            var profile = UnityEngine.JsonUtility.FromJson<PlayerProfile>(storageObject.Value);
            if (profile == null || !profile.isComplete || string.IsNullOrWhiteSpace(profile.username))
                return null;

            profile.userId = authentication.CurrentUserId;
            return profile;
        }

        return null;
    }

    public static async Task SaveAsync(string username, PlayerExperienceLevel experienceLevel, bool isComplete = true)
    {
        var authentication = AuthenticationService.Instance;
        if (authentication == null || !authentication.IsAuthenticated)
            throw new InvalidOperationException("AuthenticationService is not authenticated.");

        var profile = new PlayerProfile
        {
            username = username,
            experienceLevel = experienceLevel,
            userId = authentication.CurrentUserId,
            isComplete = isComplete,
            version = 1
        };

        await authentication.Client.UpdateAccountAsync(
            authentication.Session,
            username: username,
            displayName: username);

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
