// Test-only API doubles. These are never included in the mod or release DLL.
using System;
namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)] public class BepInPlugin : Attribute { public BepInPlugin(string id, string name, string version) {} }
    public class BaseUnityPlugin { public Configuration.ConfigFile Config = new Configuration.ConfigFile(); public TestLogger Logger = new TestLogger(); }
    public class TestLogger { public void LogInfo(object message) {} public void LogWarning(object message) {} public void LogError(object message) {} public void LogDebug(object message) {} }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; public ConfigEntry(T value) { Value = value; } }
    public class ConfigFile { public ConfigEntry<T> Bind<T>(string section, string key, T value, string description) { return new ConfigEntry<T>(value); } public void Reload() {} }
}
namespace UnityEngine
{
    public class Object
    {
        public static Object[] Objects = new Object[0];
        public static Object[] FindObjectsOfType(Type type) { return Objects; }
        public static Object FindObjectOfType(Type type) { return null; }
    }
    public class Renderer : Object
    {
        public Material sharedMaterial;
        public void GetPropertyBlock(MaterialPropertyBlock block) {} public void SetPropertyBlock(MaterialPropertyBlock block) {}
    }
    public class Material { public bool HasProperty(string property) { return false; } }
    public class MaterialPropertyBlock { public void SetColor(string property, Color color) {} }
    public struct Color { public static Color green { get { return new Color(); } } }
    public enum KeyCode { F10 }
    public static class Application { public static bool isFocused; }
    public static class Input { public static bool GetKeyDown(KeyCode key) { return false; } }
    public static class Time { public static float unscaledTime; }
    public static class Mathf { public static float Clamp(float value, float min, float max) { return Math.Min(max, Math.Max(min, value)); } }
}
public struct VPNCurrencyData
{
    public float GenerateTime, GenerateDOSCoinValue;
    public VPNCurrencyData(float time, float value) { GenerateTime = time; GenerateDOSCoinValue = value; }
}
public class RemoteVPNObject : UnityEngine.Object
{
    private VPNCurrencyData myCurrency;
    public bool Placed { get; set; }
    public int Writes;
    public VPNCurrencyData Currency { get { return myCurrency; } }
    public void SetCurrency(VPNCurrencyData currency) { myCurrency = currency; Writes++; }
    public void SetGameCurrency(float time, float value) { myCurrency = new VPNCurrencyData(time, value); }
}
public class RemoteVPNPlacementBehaviour : UnityEngine.Object {}
