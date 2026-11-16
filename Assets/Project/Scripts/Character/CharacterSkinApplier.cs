using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// Puts a skin prefab on a gameplay character (a player or a bot): the soldier model that follows the character's invisible
    /// gameplay rig through <see cref="CharacterSkinRetargeter"/>.
    /// </summary>
    public static class CharacterSkinApplier
    {
        /// <summary>The visual child the retargeter looks for by name (the MIL-R5 model's name, kept for every skin).</summary>
        public const string SkinChildName = "Anim_Idle_MIL_R5";

        private static readonly string[] MannequinNames = { "Arms", "Body", "HeadGlass", "Torso" };

        /// <summary>The headless dedicated server has nothing to draw: it never builds visuals.</summary>
        public static bool NoVisuals => SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;

        /// <summary>Replaces the character's model with <paramref name="skinPrefab"/>. Returns false when nothing could be applied.</summary>
        public static bool Apply(GameObject character, GameObject skinPrefab)
        {
            if (character == null || skinPrefab == null || NoVisuals) return false;

            var retargeter = character.GetComponent<CharacterSkinRetargeter>();
            if (retargeter == null) retargeter = AddRetargeterFromTemplate(character);
            if (retargeter == null) return false;

            // the model that is there now
            var old = character.transform.Find(SkinChildName);
            if (old != null) { old.gameObject.name = "_old_skin"; Object.Destroy(old.gameObject); }

            var skin = Object.Instantiate(skinPrefab, character.transform);
            skin.name = SkinChildName;
            skin.transform.localPosition = Vector3.zero;
            skin.transform.localRotation = Quaternion.identity;
            skin.transform.localScale = Vector3.one;
            skin.SetActive(true);
            // the retargeter drives these bones; a second animator on the model would fight it
            var anim = skin.GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            // the grey mannequin of the gameplay rig must not show through
            foreach (var n in MannequinNames)
            {
                var m = character.transform.Find(n);
                if (m != null) m.gameObject.SetActive(false);
            }
            // With the mannequin hidden the gameplay Animator has no visible renderer of its own (the skin is a separate model the
            // retargeter drives), so "Cull Update Transforms" stopped writing its bones: the rig froze and the skin copied the frozen pose
            // while the body kept moving ("sliding statues"). The rig must always animate.
            KeepAnimating(character);

            ForgetBones(retargeter, includeSourceBones: false);
            retargeter.AutoRebindAllBones();
            retargeter.UpdateSkinPose();
            return true;
        }

        /// <summary>
        /// Makes the character's gameplay Animator update its bones even when none of its own renderers is visible: its look comes from a
        /// retargeted skin, and on a headless server nothing is ever visible (the bones still place the hands, the weapon and the hit areas).
        /// </summary>
        public static void KeepAnimating(GameObject character)
        {
            var anim = character != null ? character.GetComponent<Animator>() : null;
            if (anim != null && anim.cullingMode != AnimatorCullingMode.AlwaysAnimate) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        // A bot has no retargeter. Copy the one of the player prefab (its bone mapping and rotation offsets were authored for exactly
        // this gameplay rig, which the bots share), then forget every bone reference of it: they point into the player prefab.
        private static CharacterSkinRetargeter AddRetargeterFromTemplate(GameObject character)
        {
            CharacterSkinRetargeter template = null;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.NetworkConfig != null && nm.NetworkConfig.PlayerPrefab != null)
                template = nm.NetworkConfig.PlayerPrefab.GetComponent<CharacterSkinRetargeter>();
            if (template == null) return null;

            var copy = character.AddComponent<CharacterSkinRetargeter>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(template), copy);
            ForgetBones(copy, includeSourceBones: true);
            copy.CharacterController = null;
            return copy;
        }

        private static void ForgetBones(CharacterSkinRetargeter r, bool includeSourceBones)
        {
            foreach (var f in typeof(CharacterSkinRetargeter).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (f.FieldType != typeof(Transform)) continue;
                bool isSource = f.Name.StartsWith("Source");
                bool isTarget = f.Name.StartsWith("Target");
                if ((isTarget) || (isSource && includeSourceBones)) f.SetValue(r, null);
            }
            if (r.BoneMappings != null)
            {
                for (int i = 0; i < r.BoneMappings.Count; i++)
                {
                    var m = r.BoneMappings[i];
                    m.TargetBone = null;
                    if (includeSourceBones) m.SourceBone = null;
                    r.BoneMappings[i] = m;
                }
            }
        }
    }
}
