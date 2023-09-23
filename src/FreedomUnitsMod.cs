using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FreedomUnits;

[HarmonyPatch]
public class FreedomUnitsMod : ModSystem {
    private static readonly Regex TEMPERATURE_REGEX = new(@"(-?\d+(?:\.|,)?\d*)( ?)(°C|deg)");

    private Harmony? harmony;

    public override bool ShouldLoad(EnumAppSide side) {
        return side.IsClient();
    }

    public override void StartClientSide(ICoreClientAPI api) {
        harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    public override void Dispose() {
        harmony?.UnpatchAll(Mod.Info.ModID);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TextDrawUtil), "Lineize", typeof(Context), typeof(string), typeof(EnumLinebreakBehavior), typeof(TextFlowPath[]), typeof(double), typeof(double), typeof(double), typeof(bool))]
    public static void PreLineize(ref string text) {
        string original = text;
        text = TEMPERATURE_REGEX.Replace(text, match => {
            if (match.Groups[3].Value.Normalize() is not ("°C" or "deg")) {
                return match.Value;
            }

            bool delta = original.Trim() switch {
                { } s when s.StartsWithFast("+") => true,
                { } s when s.StartsWithFast(Lang.Get("clothing-maxwarmth", "0.0").Split("0.0")[0]) => true,
                _ => false
            };

            try {
                return $"{float.Parse(match.Groups[1].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture) * 9F / 5F + (delta ? 0 : 32):0.#}°F";
            }
            catch (FormatException) {
                return match.Value;
            }
        });
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GuiDialogBlockEntityFirepit), "SetupDialog")]
    public static IEnumerable<CodeInstruction> TranspileSetupDialog(IEnumerable<CodeInstruction> instructions) {
        var codes = new List<CodeInstruction>(instructions);

        for (int i = 0; i < codes.Count; i++) {
            if (codes[i].opcode == OpCodes.Ldc_R8 && (codes[i].operand?.ToString()?.Equals("60") ?? false)) {
                codes[i] = new CodeInstruction(OpCodes.Ldc_R8, 90.0);
            }
        }

        return codes.AsEnumerable();
    }
}
