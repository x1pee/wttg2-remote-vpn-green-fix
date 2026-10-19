using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WTTG2Mods
{
    [BepInPlugin("com.wttg2.remotevpn.greenfix", "Remote VPN Green Fix", "1.1.0")]
    public sealed class RemoteVPNGreenPlugin : BaseUnityPlugin
    {
        private ConfigEntry<bool> pluginEnabled, greenPreview, maintainCurrency, debugLogging;
        private ConfigEntry<float> interval, generateTime, generateValue;
        private ConfigEntry<KeyCode> reloadKey;
        private FieldInfo rendererField, currencyField;
        private RemoteVPNPlacementBehaviour placement;
        private Renderer tintedRenderer;
        private MaterialPropertyBlock originalBlock, workingBlock;
        private readonly Dictionary<RemoteVPNObject, VPNCurrencyData> originals = new Dictionary<RemoteVPNObject, VPNCurrencyData>();
        private readonly List<RemoteVPNObject> stale = new List<RemoteVPNObject>();
        private float nextTick;
        private bool failed, colorFailed, currencyFailed;
        private float lastAppliedTime = 120f, lastAppliedValue = 3f;

        private void Awake()
        {
            pluginEnabled = Config.Bind("General", "Enabled", true, "Enable this plugin. F10 reloads configuration. Disabling restores this session's previous values when still owned by the plugin.");
            reloadKey = Config.Bind("General", "ReloadKey", KeyCode.F10, "Reload configuration while the game has focus.");
            interval = Config.Bind("General", "UpdateIntervalSeconds", 1f, "Object discovery interval in real seconds, clamped to 0.25-10. No scene-wide object search runs every frame.");
            greenPreview = Config.Bind("Appearance", "GreenPlacementPreview", true, "Keep the existing mod's green preview, using MaterialPropertyBlock without creating material instances.");
            maintainCurrency = Config.Bind("ExistingBehavior", "MaintainCurrencyOverride", true, "Preserve the old plugin's SetCurrency(120,3) behavior. Disable to use normal game values. This changes generation parameters, not placement validity.");
            generateTime = Config.Bind("ExistingBehavior", "GenerateTimeSeconds", 120f, "Existing plugin value: 120. Valid range 0.1-86400 seconds.");
            generateValue = Config.Bind("ExistingBehavior", "GenerateDOSCoinValue", 3f, "Existing plugin value: 3. Valid range 0-100000.");
            debugLogging = Config.Bind("Diagnostics", "DebugLogging", false, "Log counts and feature status, never credentials.");
            rendererField = typeof(RemoteVPNPlacementBehaviour).GetField("remoteVPNObjectMeshRenderer", BindingFlags.Instance | BindingFlags.NonPublic);
            currencyField = typeof(RemoteVPNObject).GetField("myCurrency", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rendererField == null) { colorFailed = true; Logger.LogWarning("Green preview unavailable: expected renderer field was not found. Other features remain available."); }
            if (currencyField == null || currencyField.FieldType != typeof(VPNCurrencyData)) { currencyFailed = true; Logger.LogWarning("Currency override unavailable: expected data field was not found. Other features remain available."); }
            Logger.LogInfo("Remote VPN Green Fix 1.1.0 ready. Cached discovery; no Harmony patches; F10 reloads config. Existing 120/3 override is configurable.");
        }

        private void LateUpdate()
        {
            if (failed) return;
            try
            {
                if (Application.isFocused && Input.GetKeyDown(reloadKey.Value))
                {
                    Config.Reload();
                    nextTick = 0f;
                    Logger.LogInfo("Remote VPN configuration reloaded.");
                }
                if (!pluginEnabled.Value) { RestoreAll(); return; }
                if (Time.unscaledTime < nextTick) return;
                nextTick = Time.unscaledTime + Valid(interval.Value, 1f, 0.25f, 10f);
                UpdatePreview();
                UpdateCurrency();
            }
            catch (Exception ex)
            {
                failed = true;
                RestoreAll();
                Logger.LogError("Remote VPN plugin stopped safely after " + ex.GetType().Name + ". Restart to retry; game launch handlers were not patched.");
                Logger.LogDebug(ex.StackTrace);
            }
        }

        private void UpdatePreview()
        {
            if (!greenPreview.Value || colorFailed) { RestorePreview(); return; }
            try
            {
                if (placement == null) placement = UnityEngine.Object.FindObjectOfType(typeof(RemoteVPNPlacementBehaviour)) as RemoteVPNPlacementBehaviour;
                Renderer found = placement == null ? null : rendererField.GetValue(placement) as Renderer;
                if (found == tintedRenderer && found != null) return;
                RestorePreview();
                if (found == null || found.sharedMaterial == null) return;
                Material material = found.sharedMaterial;
                originalBlock = new MaterialPropertyBlock();
                workingBlock = new MaterialPropertyBlock();
                found.GetPropertyBlock(originalBlock);
                found.GetPropertyBlock(workingBlock);
                if (material.HasProperty("_Color")) workingBlock.SetColor("_Color", Color.green);
                if (material.HasProperty("_TintColor")) workingBlock.SetColor("_TintColor", Color.green);
                if (material.HasProperty("_EmissionColor")) workingBlock.SetColor("_EmissionColor", Color.green);
                tintedRenderer = found;
                found.SetPropertyBlock(workingBlock);
                if (debugLogging.Value) Logger.LogInfo("Green preview bound with a renderer property block.");
            }
            catch (Exception ex)
            {
                colorFailed = true;
                RestorePreview();
                Logger.LogWarning("Green preview disabled after " + ex.GetType().Name + "; original renderer state restored when available.");
            }
        }

        private void UpdateCurrency()
        {
            if (!maintainCurrency.Value || currencyFailed) { RestoreCurrency(); return; }
            try
            {
                float seconds = Valid(generateTime.Value, 120f, 0.1f, 86400f);
                float value = Valid(generateValue.Value, 3f, 0f, 100000f);
                // Restore the old setting before switching to another configured override.
                if (seconds != lastAppliedTime || value != lastAppliedValue) RestoreCurrency();
                lastAppliedTime = seconds; lastAppliedValue = value;
                UnityEngine.Object[] found = UnityEngine.Object.FindObjectsOfType(typeof(RemoteVPNObject));
                foreach (UnityEngine.Object item in found)
                {
                    RemoteVPNObject remote = item as RemoteVPNObject;
                    if (remote == null) continue;
                    if (!originals.ContainsKey(remote)) originals.Add(remote, (VPNCurrencyData)currencyField.GetValue(remote));
                    remote.SetCurrency(new VPNCurrencyData(seconds, value));
                }
                stale.Clear();
                foreach (RemoteVPNObject remote in originals.Keys) if (remote == null) stale.Add(remote);
                foreach (RemoteVPNObject remote in stale) originals.Remove(remote);
                stale.Clear();
            }
            catch (Exception ex)
            {
                currencyFailed = true;
                RestoreCurrency();
                Logger.LogWarning("Currency override disabled after " + ex.GetType().Name + "; original session values restored when available.");
            }
        }

        private static float Valid(float value, float fallback, float min, float max)
        {
            if (Single.IsNaN(value) || Single.IsInfinity(value)) return fallback;
            return Mathf.Clamp(value, min, max);
        }

        private void RestorePreview()
        {
            try { if (tintedRenderer != null) tintedRenderer.SetPropertyBlock(originalBlock); }
            catch (Exception ex) { Logger.LogWarning("Could not restore an unavailable preview renderer: " + ex.GetType().Name); }
            tintedRenderer = null;
            originalBlock = null; workingBlock = null;
        }

        private void RestoreCurrency()
        {
            foreach (KeyValuePair<RemoteVPNObject, VPNCurrencyData> entry in originals)
            {
                if (entry.Key == null) continue;
                try
                {
                    VPNCurrencyData current = (VPNCurrencyData)currencyField.GetValue(entry.Key);
                    // Do not replace a later change made by the game or another mod.
                    if (current.GenerateTime == lastAppliedTime && current.GenerateDOSCoinValue == lastAppliedValue)
                        entry.Key.SetCurrency(entry.Value);
                }
                catch (Exception ex) { Logger.LogWarning("Could not restore an unavailable VPN object: " + ex.GetType().Name); }
            }
            originals.Clear(); stale.Clear();
        }

        private void RestoreAll() { RestorePreview(); RestoreCurrency(); placement = null; }
        private void OnDisable() { RestoreAll(); }
        private void OnDestroy() { RestoreAll(); }
    }
}
