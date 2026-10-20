using System;
using System.Collections;
using System.Reflection;
using BepInEx.Configuration;
using WTTG2Mods;

// Compiles the production plugin against test doubles, without Unity, a game process or saves.
internal static class CurrencyOwnershipTests
{
    private static int assertions;
    private static readonly Type PluginType = typeof(RemoteVPNGreenPlugin);
    private static void Set(object instance, string name, object value)
    {
        PluginType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    }
    private static void Call(object instance, string name)
    {
        PluginType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null).Invoke(instance, null);
    }
    private static int Tracked(object instance)
    {
        return ((IDictionary)PluginType.GetField("originals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance)).Count;
    }
    private static void Check(bool condition, string name)
    {
        assertions++;
        if (!condition) throw new Exception(name);
    }
    private static bool Currency(RemoteVPNObject remote, float seconds, float amount)
    {
        return remote.Currency.GenerateTime == seconds && remote.Currency.GenerateDOSCoinValue == amount;
    }
    public static int Main()
    {
        try
        {
            var plugin = new RemoteVPNGreenPlugin();
            var enabled = new ConfigEntry<bool>(true);
            var seconds = new ConfigEntry<float>(120f);
            var amount = new ConfigEntry<float>(3f);
            Set(plugin, "maintainCurrency", enabled);
            Set(plugin, "generateTime", seconds);
            Set(plugin, "generateValue", amount);
            Set(plugin, "currencyField", typeof(RemoteVPNObject).GetField("myCurrency", BindingFlags.Instance | BindingFlags.NonPublic));
            var remote = new RemoteVPNObject();
            UnityEngine.Object.Objects = new UnityEngine.Object[] { remote };

            Call(plugin, "UpdateCurrency");
            Check(remote.Writes == 0 && Tracked(plugin) == 0, "unplaced pooled object must remain untouched");

            remote.Placed = true;
            remote.SetGameCurrency(600, 2);
            Call(plugin, "UpdateCurrency");
            Check(Currency(remote, 120, 3) && Tracked(plugin) == 1, "placed object receives override");
            int writes = remote.Writes;
            Call(plugin, "UpdateCurrency");
            Check(remote.Writes == writes, "unchanged tick must not call SetCurrency again");

            remote.Placed = false;
            Call(plugin, "UpdateCurrency");
            Check(Currency(remote, 600, 2) && Tracked(plugin) == 0, "pickup restores and releases previous placement baseline");

            remote.Placed = true;
            remote.SetGameCurrency(900, 4);
            Call(plugin, "UpdateCurrency");
            enabled.Value = false;
            Call(plugin, "UpdateCurrency");
            Check(Currency(remote, 900, 4) && Tracked(plugin) == 0, "replacement disable restores new placement instead of pooled zero");

            enabled.Value = true;
            Call(plugin, "UpdateCurrency");
            remote.SetGameCurrency(1000, 5);
            Call(plugin, "UpdateCurrency");
            Check(Currency(remote, 120, 3), "same-instance placement recalculation still receives override");
            Call(plugin, "RestoreCurrency");
            Check(Currency(remote, 1000, 5), "latest game recalculation becomes restore baseline");

            Call(plugin, "UpdateCurrency");
            seconds.Value = 60;
            amount.Value = 1;
            Call(plugin, "UpdateCurrency");
            Check(Currency(remote, 60, 1), "configuration switch applies new override");
            Call(plugin, "RestoreCurrency");
            Check(Currency(remote, 1000, 5), "configuration switch preserves actual game baseline");

            Call(plugin, "UpdateCurrency");
            remote.SetGameCurrency(2000, 6);
            Call(plugin, "RestoreCurrency");
            Check(Currency(remote, 2000, 6), "restore does not clobber a newer external change");

            seconds.Value = Single.NaN;
            amount.Value = Single.PositiveInfinity;
            Call(plugin, "UpdateCurrency");
            Check(Currency(remote, 120, 3), "non-finite configuration uses finite defaults");
            Call(plugin, "RestoreCurrency");
            Check(Currency(remote, 2000, 6), "finite fallback preserves game baseline");
            Console.WriteLine("PASS: " + assertions + " production currency ownership assertions (synthetic Unity/BepInEx doubles).");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.GetBaseException().Message); return 1; }
    }
}
