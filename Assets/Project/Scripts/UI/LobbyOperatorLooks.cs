using UnityEngine;

namespace Railbound.UI
{
    /// <summary>How an operator dresses the shared MIL-R5 model: a tint per gear group, and which accessories are worn.</summary>
    public struct OperatorLook
    {
        public Color suit, plate, pouches, pads, cap, headset;
        public bool wearCap, wearHeadset;
    }

    /// <summary>The pieces of an operator's look the player can change on the customization screen.</summary>
    public enum LookPart { Uniform, Vest, Headgear, Headset }

    /// <summary>
    /// Each operator's authored look, plus the choices the player has saved on top of it (kept in PlayerPrefs, per operator).
    /// The tints multiply the model's own olive textures, so values are chosen to move the hue, not replace it.
    /// </summary>
    public static class LobbyOperatorLooks
    {
        /// <summary>Choice value: keep the operator's authored colour for this part.</summary>
        public const int Default = -1;
        /// <summary>Choice value: do not wear this accessory (headgear / headset only).</summary>
        public const int None = -2;

        public struct Swatch { public string name; public Color tint; public Color display; }

        /// <summary><c>tint</c> is the multiplier applied to the model; <c>display</c> is the colour shown in the menu.</summary>
        public static readonly Swatch[] Swatches =
        {
            new Swatch { name = "OLIVE",    tint = new Color(1.00f, 1.00f, 1.00f), display = new Color(0.42f, 0.45f, 0.33f) },
            new Swatch { name = "STEEL",    tint = new Color(0.70f, 0.80f, 1.05f), display = new Color(0.45f, 0.55f, 0.70f) },
            new Swatch { name = "TEAL",     tint = new Color(0.55f, 0.82f, 0.78f), display = new Color(0.25f, 0.55f, 0.52f) },
            new Swatch { name = "SAND",     tint = new Color(1.55f, 1.25f, 0.80f), display = new Color(0.80f, 0.68f, 0.45f) },
            new Swatch { name = "CHARCOAL", tint = new Color(0.40f, 0.42f, 0.46f), display = new Color(0.20f, 0.20f, 0.22f) },
            new Swatch { name = "SNOW",     tint = new Color(1.70f, 1.75f, 1.80f), display = new Color(0.88f, 0.90f, 0.92f) },
            new Swatch { name = "RUST",     tint = new Color(1.50f, 0.85f, 0.65f), display = new Color(0.62f, 0.30f, 0.20f) },
            new Swatch { name = "CRIMSON",  tint = new Color(1.45f, 0.55f, 0.55f), display = new Color(0.60f, 0.16f, 0.18f) }
        };

        private static readonly OperatorLook[] Defaults =
        {
            // 0: MIL-R5 GHOST - the model as authored
            new OperatorLook { suit = Color.white, plate = Color.white, pouches = Color.white, pads = Color.white, cap = Color.white, headset = Color.white, wearCap = true, wearHeadset = true },
            // 1: KRUGER-9 VANGUARD - steel grey-blue, heavy dark plate, bare head
            new OperatorLook { suit = new Color(0.70f, 0.80f, 1.05f), plate = new Color(0.42f, 0.46f, 0.54f), pouches = new Color(0.60f, 0.66f, 0.78f), pads = new Color(0.38f, 0.40f, 0.46f), cap = Color.white, headset = new Color(0.55f, 0.60f, 0.70f), wearCap = false, wearHeadset = true },
            // 2: SPECTRE-X RECON - near-black teal night kit, cap and headset
            new OperatorLook { suit = new Color(0.55f, 0.82f, 0.78f), plate = new Color(0.30f, 0.40f, 0.40f), pouches = new Color(0.40f, 0.55f, 0.55f), pads = new Color(0.28f, 0.34f, 0.34f), cap = new Color(0.30f, 0.46f, 0.46f), headset = new Color(0.35f, 0.45f, 0.45f), wearCap = true, wearHeadset = true },
            // 3: VALKYRIE-7 MEDIC - desert sand with a rust vest, no headset
            new OperatorLook { suit = new Color(1.55f, 1.25f, 0.80f), plate = new Color(1.50f, 0.85f, 0.65f), pouches = new Color(1.35f, 1.10f, 0.75f), pads = new Color(1.10f, 0.90f, 0.70f), cap = new Color(1.50f, 1.20f, 0.80f), headset = Color.white, wearCap = true, wearHeadset = false }
        };

        private static readonly int PartCount = System.Enum.GetValues(typeof(LookPart)).Length;
        private static int[,] choices;      // [operator, part], loaded from PlayerPrefs on first use

        /// <summary>Raised when a saved choice changed (the cloud copy listens).</summary>
        public static event System.Action Changed;
        public static int OperatorCount => Defaults.Length;

        /// <summary>All choices as one flat list: index = operator * parts + part.</summary>
        public static int[] Export()
        {
            Load();
            var flat = new int[Defaults.Length * PartCount];
            for (int o = 0; o < Defaults.Length; o++)
                for (int p = 0; p < PartCount; p++) flat[o * PartCount + p] = choices[o, p];
            return flat;
        }

        /// <summary>Replaces every saved choice (from the cloud copy). Does not raise <see cref="Changed"/>.</summary>
        public static void Import(int[] flat)
        {
            if (flat == null || flat.Length != Defaults.Length * PartCount) return;
            Load();
            for (int o = 0; o < Defaults.Length; o++)
                for (int p = 0; p < PartCount; p++)
                {
                    choices[o, p] = flat[o * PartCount + p];
                    if (flat[o * PartCount + p] == Default) PlayerPrefs.DeleteKey(Key(o, (LookPart)p));
                    else PlayerPrefs.SetInt(Key(o, (LookPart)p), flat[o * PartCount + p]);
                }
            PlayerPrefs.Save();
        }

        private static int Wrap(int op) { return Mathf.Abs(op) % Defaults.Length; }
        private static string Key(int op, LookPart part) { return "railbound.look." + op + "." + (int)part; }

        private static void Load()
        {
            if (choices != null) return;
            choices = new int[Defaults.Length, PartCount];
            for (int o = 0; o < Defaults.Length; o++)
                for (int p = 0; p < PartCount; p++)
                    choices[o, p] = PlayerPrefs.GetInt(Key(o, (LookPart)p), Default);
        }

        public static int GetChoice(int op, LookPart part)
        {
            Load();
            return choices[Wrap(op), (int)part];
        }

        public static void SetChoice(int op, LookPart part, int choice)
        {
            Load();
            op = Wrap(op);
            choices[op, (int)part] = choice;
            PlayerPrefs.SetInt(Key(op, part), choice);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        /// <summary>Drops every saved choice for this operator, back to its authored look.</summary>
        public static void Reset(int op)
        {
            Load();
            op = Wrap(op);
            for (int p = 0; p < PartCount; p++)
            {
                choices[op, p] = Default;
                PlayerPrefs.DeleteKey(Key(op, (LookPart)p));
            }
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        /// <summary>The look to put on a model: the authored one, plus the player's saved choices when <paramref name="applyChoices"/> is set.</summary>
        public static OperatorLook Resolve(int op, bool applyChoices)
        {
            op = Wrap(op);
            var look = Defaults[op];
            if (!applyChoices) return look;
            Load();

            int u = choices[op, (int)LookPart.Uniform];
            if (u >= 0 && u < Swatches.Length)
            {
                look.suit = Swatches[u].tint;
                look.pads = Swatches[u].tint * 0.7f;
            }

            int v = choices[op, (int)LookPart.Vest];
            if (v >= 0 && v < Swatches.Length)
            {
                look.plate = Swatches[v].tint * 0.8f;
                look.pouches = Swatches[v].tint;
            }

            int h = choices[op, (int)LookPart.Headgear];
            if (h == None) look.wearCap = false;
            else if (h >= 0 && h < Swatches.Length) { look.wearCap = true; look.cap = Swatches[h].tint; }

            int s = choices[op, (int)LookPart.Headset];
            if (s == None) look.wearHeadset = false;
            else if (s >= 0 && s < Swatches.Length) { look.wearHeadset = true; look.headset = Swatches[s].tint; }

            return look;
        }
    }
}
