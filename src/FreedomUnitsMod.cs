using System;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

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
        text = TEMPERATURE_REGEX.Replace(text, match => {
            if (match.Groups[3].Value.Normalize() is not ("°C" or "deg")) {
                return match.Value;
            }

            try {
                return $"{float.Parse(match.Groups[1].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture) * 9F / 5F + 32:0}°F";
            }
            catch (FormatException) {
                return match.Value;
            }
        });
    }
}
