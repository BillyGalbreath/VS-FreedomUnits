using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Cairo;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace FreedomUnits;

[HarmonyPatch]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public partial class FreedomUnitsMod : ModSystem {
    [GeneratedRegex("([+-]?\\d+(?:[.,])?\\d*)( ?)(°C|degree|deg)")]
    private static partial Regex TemperatureRegex();

    private static GuiDialog? _hudDebugScreen;
    private static ICoreAPI? _api;

    private Harmony? _harmony;

    public override bool ShouldLoad(EnumAppSide side) {
        return side.IsClient();
    }

    public override void StartClientSide(ICoreClientAPI api) {
        _api = api;

        _harmony = new Harmony(Mod.Info.ModID);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    private static bool IsDebugHugText(string text) {
        _hudDebugScreen ??= ((ClientMain)_api?.World!).GetField<List<GuiDialog>>("LoadedGuis")!.FirstOrDefault(dlg => dlg is HudDebugScreen);
        return (_hudDebugScreen?.IsOpened() ?? false) && text.Contains("Yaw: ") && text.Contains("Facing: ");
    }

    public override void Dispose() {
        _harmony?.UnpatchAll(Mod.Info.ModID);

        _hudDebugScreen = null;
        _api = null;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(TextDrawUtil), "Lineize", typeof(Context), typeof(string), typeof(EnumLinebreakBehavior), typeof(TextFlowPath[]), typeof(double), typeof(double), typeof(double), typeof(bool))]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static void PreLineize(ref string text) {
        string original = text;
        text = TemperatureRegex().Replace(text, match => {
            if (match.Groups[3].Value.Normalize() is not ("°C" or "degree" or "deg") || IsDebugHugText(original)) {
                return match.Value;
            }

            bool prefixed = match.Value.Normalize().StartsWithFast("+");

            bool delta = prefixed || original.Normalize() switch {
                { } s when s.StartsWithFast(Lang.Get("clothing-maxwarmth", "0.0").Split("0.0")[0]) => true,
                { } s when s.Contains(Lang.Get("xskills:abilitydesc-heatinghits", "0.0").Split("0.0")[0]) => true,
                _ => false
            };

            float temp = float.Parse(match.Groups[1].Value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);

            try {
                return $"{(prefixed ? "+" : "")}{temp * 9F / 5F + (delta ? 0 : 32):0.#}°F";
            } catch (FormatException) {
                return match.Value;
            }
        });
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(GuiDialogBlockEntityFirepit), "SetupDialog")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public static IEnumerable<CodeInstruction> TranspileSetupDialog(IEnumerable<CodeInstruction> instructions) {
        List<CodeInstruction> codes = new(instructions);

        for (int i = 0; i < codes.Count; i++) {
            if (codes[i].opcode == OpCodes.Ldc_R8 && (codes[i].operand?.ToString()?.Equals("60") ?? false)) {
                codes[i] = new CodeInstruction(OpCodes.Ldc_R8, 90.0);
            }
        }

        return codes.AsEnumerable();
    }
}

public static class Extensions {
    private const BindingFlags _flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static T? GetField<T>(this object obj, string name) {
        return (T?)obj.GetType().GetField(name, _flags)?.GetValue(obj);
    }
}
