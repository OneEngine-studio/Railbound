using System.Globalization;
using System.Text;
using Railbound.UI;
using UnityEngine;

namespace Project.Character
{
    /// <summary>
    /// Puts an operator's look (per-gear tints, cap and headset on or off) on a MIL-R5 model: the lobby's preview, the party line-up and the
    /// player's character in a match all use this, so what a player picks in the lobby is what everybody sees in the match. A look travels as
    /// a short code (<see cref="Encode"/>): six tints at two hex digits per channel plus two flags.
    /// </summary>
    public static class OperatorLookApplier
    {
        private enum GearGroup { Suit, Plate, Pouches, Pads, Cap, Headset, Skin }

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColor = Shader.PropertyToID("_Color");
        private const float TintRange = 2f;   // tints run from 0 to 2 (they multiply the model's own textures)

        private static GearGroup GroupOf(string rendererName)
        {
            if (rendererName.Contains("Eye") || rendererName.Contains("Head") && !rendererName.Contains("Headphones")) return GearGroup.Skin;
            if (rendererName.Contains("Headphones")) return GearGroup.Headset;
            if (rendererName.Contains("Cap")) return GearGroup.Cap;
            if (rendererName.Contains("Plate")) return GearGroup.Plate;
            if (rendererName.Contains("Puches")) return GearGroup.Pouches;
            if (rendererName.Contains("Pads")) return GearGroup.Pads;
            return GearGroup.Suit;
        }

        /// <summary>Tints every renderer of <paramref name="model"/> by its gear group; <paramref name="brightness"/> &lt; 1 dims it.</summary>
        public static void Apply(GameObject model, OperatorLook look, float brightness = 1f)
        {
            if (model == null) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                Color tint;
                switch (GroupOf(r.name))
                {
                    case GearGroup.Suit: tint = look.suit; break;
                    case GearGroup.Plate: tint = look.plate; break;
                    case GearGroup.Pouches: tint = look.pouches; break;
                    case GearGroup.Pads: tint = look.pads; break;
                    case GearGroup.Cap: tint = look.cap; r.enabled = look.wearCap; break;
                    case GearGroup.Headset: tint = look.headset; r.enabled = look.wearHeadset; break;
                    default: tint = Color.white; break;   // face and eyes keep their own colour
                }
                tint = new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, 1f);
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, tint);
                block.SetColor(LegacyColor, tint);
                r.SetPropertyBlock(block);
            }
        }

        /// <summary>Only renderers that belong to a weapon or a held item are left out: they are not part of the operator.</summary>
        public static void ApplyToCharacter(GameObject character, OperatorLook look)
        {
            if (character == null) return;
            var skin = character.transform.Find(CharacterSkinApplier.SkinChildName);
            Apply(skin != null ? skin.gameObject : character, look);
        }

        public static string Encode(OperatorLook look)
        {
            var sb = new StringBuilder(40);
            foreach (var c in new[] { look.suit, look.plate, look.pouches, look.pads, look.cap, look.headset })
            {
                sb.Append(Byte(c.r).ToString("x2")).Append(Byte(c.g).ToString("x2")).Append(Byte(c.b).ToString("x2"));
            }
            sb.Append(look.wearCap ? '1' : '0').Append(look.wearHeadset ? '1' : '0');
            return sb.ToString();
        }

        public static bool TryDecode(string code, out OperatorLook look)
        {
            look = default;
            if (string.IsNullOrEmpty(code) || code.Length != 38) return false;
            var colors = new Color[6];
            for (int i = 0; i < 6; i++)
            {
                if (!Channel(code, i * 6, out float r) || !Channel(code, i * 6 + 2, out float g) || !Channel(code, i * 6 + 4, out float b)) return false;
                colors[i] = new Color(r, g, b, 1f);
            }
            look.suit = colors[0]; look.plate = colors[1]; look.pouches = colors[2]; look.pads = colors[3]; look.cap = colors[4]; look.headset = colors[5];
            look.wearCap = code[36] == '1';
            look.wearHeadset = code[37] == '1';
            return true;
        }

        private static int Byte(float v) => Mathf.Clamp(Mathf.RoundToInt(v / TintRange * 255f), 0, 255);

        private static bool Channel(string s, int at, out float v)
        {
            v = 0f;
            if (!int.TryParse(s.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b)) return false;
            v = b / 255f * TintRange;
            return true;
        }
    }
}
