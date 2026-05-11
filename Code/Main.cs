using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using NeoModLoader.General;
using System.Collections;
using System.Runtime.CompilerServices;
using NeoModLoader;

namespace NoKaijuNoMechs;

public class NoKaijuNoMechsMain : BasicMod<NoKaijuNoMechsMain>
{
    
    public static bool KaijuEnabled = true;
    public static bool SpaceEnabled = true;
    public static bool ErasEnabled = true;
    public static bool BombsEnabled = true;
    public static bool BiomesEnabled = true;

    public static bool EraFixEnabled = true;
    public static bool ProdFixEnabled = true;
    public static bool SpeedFixEnabled = true;
    public static bool TimeFixEnabled = true;
    public static bool ExplosionFixEnabled = true;
    public static bool LogoFixEnabled = true;

    public static bool DurabilityEnabled = true;
    public static bool TraitLockEnabled = true;
    public static bool FPSBoosterEnabled = true;

    public static bool NaturalKaijuSpawnEnabled = false;
    public static bool RadiationEnabled = true;

    
    private static float _eraUpdateTimer = 0f;
    private static float _radiationTimer = 0f;
    private static float _initWaitTimer = 0f;
    private static float _mbCheckTimer = 0f;
    private static bool _uiSetupDone = false;
    private static bool _mbPatched = false;
    private static string _detectedModernBoxID = null;
    private static Harmony _harmony;

    
    private static class MBReflect {
        public static FieldInfo f_currentEra;
        public static FieldInfo f_enableMedieval, f_enableRenaissance, f_enableModern, f_enableHyperfuture;
        public static FieldInfo f_currentEraDesc, f_statLabel2;
        public static FieldInfo f_eraLibAll;
        public static MethodInfo m_setEraCity, m_getEra, m_isWeaponAllowed;
        public static FieldInfo f_gunsAllowed, f_mirvsAllowed, f_mirvIds, f_weaponEras;

        public static void Init(Type smType) {
            f_currentEra = AccessTools.Field(smType, "currentEra");
            f_enableMedieval = AccessTools.Field(smType, "enableMedieval");
            f_enableRenaissance = AccessTools.Field(smType, "enableRenaissance");
            f_enableModern = AccessTools.Field(smType, "enableModern");
            f_enableHyperfuture = AccessTools.Field(smType, "enableHyperfuture");
            f_currentEraDesc = AccessTools.Field(smType, "currentEraDescription");
            f_statLabel2 = AccessTools.Field(smType, "statLabel2");

            Type eraLibType = AccessTools.TypeByName("ModernBox.EraLibrary");
            if (eraLibType != null) f_eraLibAll = AccessTools.Field(eraLibType, "All");

            Type traitsType = AccessTools.TypeByName("ModernBox.Traits");
            if (traitsType != null) m_setEraCity = AccessTools.Method(traitsType, "SetEraCity");

            Type clType = AccessTools.TypeByName("ModernBox.CustomItemsList");
            if (clType != null) {
                m_getEra = AccessTools.Method(clType, "GetEra");
                m_isWeaponAllowed = AccessTools.Method(clType, "IsWeaponAllowedForEra");
                f_gunsAllowed = AccessTools.Field(clType, "GunsAllowed");
                f_mirvsAllowed = AccessTools.Field(clType, "MirvsAllowed");
                f_mirvIds = AccessTools.Field(clType, "Kys");
                f_weaponEras = AccessTools.Field(clType, "WeaponEras");
            }
        }
    }

    protected override void OnModLoad()
    {
        LoadSettings();
        _harmony = new Harmony(GetDeclaration().UID);
        
        try {
            MethodInfo getItemToCraft = AccessTools.Method(typeof(ItemCrafting), "getItemAssetToCraft");
            if (getItemToCraft != null) _harmony.Patch(getItemToCraft, new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(GetItemToCraft_Prefix))));
        } catch {}

        SafeApplyPatch(_harmony, ApplyTraitLock, "Trait Lock");
        SafeApplyPatch(_harmony, ApplyFPSBooster, "FPS Booster");
        SafeApplyPatch(_harmony, ApplyTimeFix, "Time Fix");
        _harmony.PatchAll();
    }

    void Update()
    {
        if (_mbPatched || !Config.game_loaded) return;

        _mbCheckTimer += Time.deltaTime;
        if (_mbCheckTimer >= 1f) {
            _mbCheckTimer = 0f;
            try {
                Type sm = AccessTools.TypeByName("StatManager");
                if (sm != null) {
                    MBReflect.Init(sm);
                    _harmony.Patch(AccessTools.Method(sm, "Update"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(StatManager_Update_Prefix))));
                    ApplyCleanPatches(_harmony);
                    ApplyDurabilityFix(_harmony);
                    ApplySpeedFix(_harmony);
                    ApplyExplosionFix(_harmony);
                    ApplyBiomeFixes(_harmony);
                    _mbPatched = true;
                }
            } catch {}
        }
    }

    private void SafeApplyPatch(Harmony h, Action<Harmony> patchAction, string name)
    {
        try { patchAction(h); } catch {}
    }

    private void LoadSettings()
    {
        KaijuEnabled = PlayerPrefs.GetInt("MB_Fanpatch_KaijuEnabled", 1) == 1;
        SpaceEnabled = PlayerPrefs.GetInt("MB_Fanpatch_SpaceEnabled", 1) == 1;
        ErasEnabled = PlayerPrefs.GetInt("MB_Fanpatch_ErasEnabled", 1) == 1;
        BombsEnabled = PlayerPrefs.GetInt("MB_Fanpatch_BombsEnabled", 1) == 1;
        BiomesEnabled = PlayerPrefs.GetInt("MB_Fanpatch_BiomesEnabled", 1) == 1;
        EraFixEnabled = PlayerPrefs.GetInt("MB_Fanpatch_EraFixEnabled", 1) == 1;
        ProdFixEnabled = PlayerPrefs.GetInt("MB_Fanpatch_ProdFixEnabled", 1) == 1;
        SpeedFixEnabled = PlayerPrefs.GetInt("MB_Fanpatch_SpeedFixEnabled", 1) == 1;
        TimeFixEnabled = PlayerPrefs.GetInt("MB_Fanpatch_TimeFixEnabled", 1) == 1;
        ExplosionFixEnabled = PlayerPrefs.GetInt("MB_Fanpatch_ExplosionFixEnabled", 1) == 1;
        LogoFixEnabled = PlayerPrefs.GetInt("MB_Fanpatch_LogoFixEnabled", 1) == 1;
        DurabilityEnabled = PlayerPrefs.GetInt("MB_Fanpatch_DurabilityEnabled", 1) == 1;
        TraitLockEnabled = PlayerPrefs.GetInt("MB_Fanpatch_TraitLockEnabled", 1) == 1;
        FPSBoosterEnabled = PlayerPrefs.GetInt("MB_Fanpatch_FPSBoosterEnabled", 1) == 1;
        RadiationEnabled = PlayerPrefs.GetInt("MB_Fanpatch_RadiationEnabled", 1) == 1;
        NaturalKaijuSpawnEnabled = PlayerPrefs.GetInt("MB_Fanpatch_NaturalKaijuSpawnEnabled", 0) == 1;
    }

    public static void ResetAllSettings()
    {
        string[] keys = { "KaijuEnabled", "SpaceEnabled", "ErasEnabled", "BombsEnabled", "BiomesEnabled", "EraFixEnabled", "ProdFixEnabled", "SpeedFixEnabled", "TimeFixEnabled", "ExplosionFixEnabled", "LogoFixEnabled", "DurabilityEnabled", "TraitLockEnabled", "FPSBoosterEnabled", "RadiationEnabled" };
        foreach (var key in keys) PlayerPrefs.SetInt("MB_Fanpatch_" + key, 1);
        PlayerPrefs.SetInt("MB_Fanpatch_NaturalKaijuSpawnEnabled", 0);
        PlayerPrefs.SetInt("MB_Fanpatch_FirstBootDone_v1", 0);
        PlayerPrefs.Save();
    }

    public static void StatManager_Update_Prefix(object __instance)
    {
        if (__instance == null) return;
        if (!_uiSetupDone) {
            _initWaitTimer += Time.deltaTime;
            if (_initWaitTimer > 4.5f) { 
                try {
                    if (ErasEnabled) {
                        MBReflect.f_enableMedieval.SetValue(__instance, true);
                        MBReflect.f_enableModern.SetValue(__instance, true);
                    } else {
                        MBReflect.f_enableMedieval.SetValue(__instance, false);
                        MBReflect.f_enableRenaissance.SetValue(__instance, false);
                        MBReflect.f_enableModern.SetValue(__instance, false);
                        MBReflect.f_enableHyperfuture.SetValue(__instance, false);
                        MBReflect.f_currentEra.SetValue(__instance, "none");
                    }
                    InitializeFanpatchUI();
                    if (LogoFixEnabled) RestoreOriginalLogo();
                    if (PlayerPrefs.GetInt("MB_Fanpatch_FirstBootDone_v1", 0) == 0) {
                        GameObject warningGO = new GameObject("FanpatchWarningUI"); warningGO.AddComponent<FanpatchWarningUI>();
                        PlayerPrefs.SetInt("MB_Fanpatch_FirstBootDone_v1", 1); PlayerPrefs.Save();
                    }
                    _uiSetupDone = true;
                } catch {}
            }
        }
        _eraUpdateTimer += Time.deltaTime;
        _radiationTimer += Time.deltaTime;

        if (_eraUpdateTimer >= 3f) {
            _eraUpdateTimer = 0f;
            if (ErasEnabled && EraFixEnabled) {
                UpdateGlobalEra(__instance);
                if (KaijuEnabled && NaturalKaijuSpawnEnabled) TryNaturalKaijuSpawn(__instance);
            } else if (!ErasEnabled) {
                try {
                    MBReflect.f_currentEra.SetValue(__instance, "none");
                    var label2 = MBReflect.f_statLabel2.GetValue(__instance) as Text;
                    if (label2 != null && label2.gameObject.activeSelf) label2.gameObject.SetActive(false);
                } catch {}
            }
        }

        if (_radiationTimer >= 0.1f) { 
            _radiationTimer = 0f;
            if (RadiationEnabled) ProcessRadiationHazardStaggered();
        }
    }

    private static void ProcessRadiationHazardStaggered()
    {
        if (World.world == null || World.world.units == null) return;
        
        int frameStride = 60;
        int framePart = Time.frameCount % frameStride;
        
        foreach (Actor actor in World.world.units) {
            if (actor == null || (int)actor.id % frameStride != framePart || !actor.isAlive()) continue;
            WorldTile tile = actor.current_tile;
            if (tile == null || tile.top_type == null) continue;
            if (tile.top_type.wasteland) {
                if (!actor.hasTrait("immune") && !actor.hasTrait("acid_proof") && !actor.hasTrait("adaptation_wasteland")) {
                    actor.getHit(5f, true, AttackType.Other, null, true);
                }
            }
        }
    }

    private static string[] _spawningKaijus = { "spawnGojira", "spawnSachiel", "spawnZeruel", "spawnRamiel", "spawnSkullcrawler", "spawncrabzilord" };
    private static void TryNaturalKaijuSpawn(object statManager)
    {
        try {
            string currentEra = MBReflect.f_currentEra.GetValue(statManager) as string;
            if (currentEra == "Hyperfuture") {
                if (UnityEngine.Random.Range(0f, 1f) < 0.005f) {
                    WorldTile randomTile = World.world.tiles_list.GetRandom();
                    if (randomTile != null) {
                        string kaijuId = _spawningKaijus.GetRandom();
                        World.world.units.createNewUnit(kaijuId, randomTile);
                        WorldTip.showNow("A mysterious creature has been spotted!", false, "top", 6f);
                    }
                }
            }
        } catch {}
    }

    private static void RestoreOriginalLogo()
    {
        try {
            Sprite originalLogo = Resources.Load<Sprite>("ui/logo_loading") ?? Resources.Load<Sprite>("ui/worldbox_logo");
            if (originalLogo != null) {
                GameObject[] logos = ResourcesFinder.FindResources<GameObject>("Logo");
                foreach (GameObject logo in logos) { Image img = logo.GetComponent<Image>(); if (img != null) img.sprite = originalLogo; }
            }
        } catch {}
    }

    private static void InitializeFanpatchUI()
    {
        if (FindGameObject("fanpatch_config") != null) return;

        GameObject otherTab = FindGameObject("other");
        if (otherTab != null) {
            PowerButtonCreator.CreateSimpleButton("fanpatch_config", () => {
                GameObject uiGO = new GameObject("FanpatchUI_Overlay");
                uiGO.AddComponent<FanpatchUI>();
            }, Resources.Load<Sprite>("ui/icons/iconOptions"), otherTab.transform);
            
            TipButton tip = FindGameObject("fanpatch_config")?.GetComponent<TipButton>();
            if (tip != null) {
                tip.textOnClick = "ModernBox Fanpatch";
                tip.textOnClickDescription = "Configure Fanpatch settings.";
            }
        }
        PerformUI_Cleanup();
    }

    private static void PerformUI_Cleanup()
    {
        try {
            string[] tabs = { "Tab_kaiju", "ModernBoxSpace", "ModernBoxEras", "ModernBoxBombs" };
            bool[] states = { KaijuEnabled, SpaceEnabled, ErasEnabled, BombsEnabled };
            for (int i = 0; i < tabs.Length; i++) {
                if (!states[i]) {
                    string tid = tabs[i];
                    GameObject content = FindGameObject(tid); if (content != null) content.SetActive(false);
                    GameObject btn = FindGameObject(tid + "FuckYou"); if (btn != null) btn.SetActive(false);
                    if (AssetManager.power_tab_library != null) {
                        AssetManager.power_tab_library.dict.Remove(tid);
                        AssetManager.power_tab_library.list.RemoveAll(x => x.id == tid);
                    }
                }
            }
            if (!BiomesEnabled) { GameObject tab = FindGameObject("Tab_OceanBiomes"); if (tab != null) tab.SetActive(false); }
        } catch {}
    }

    private static GameObject FindGameObject(string name) { GameObject[] gos = Resources.FindObjectsOfTypeAll<GameObject>(); return gos.FirstOrDefault(g => g.name == name); }

    public static void GetItemToCraft_Prefix(ref List<EquipmentAsset> pItemList)
    {
        if (!ProdFixEnabled || pItemList == null || pItemList.Count == 0) return;
        try {
            if (MBReflect.f_gunsAllowed == null) return;
            bool gunsAllowed = (bool)MBReflect.f_gunsAllowed.GetValue(null);
            bool mirvsAllowed = (bool)MBReflect.f_mirvsAllowed.GetValue(null);
            HashSet<string> mirvIds = MBReflect.f_mirvIds.GetValue(null) as HashSet<string>;
            string currentEra = MBReflect.m_getEra?.Invoke(null, null) as string;
            var weaponEras = MBReflect.f_weaponEras.GetValue(null) as IDictionary<string, string>;
            for (int i = pItemList.Count - 1; i >= 0; i--) {
                EquipmentAsset item = pItemList[i];
                if (item == null) continue;
                if (!gunsAllowed && weaponEras != null && weaponEras.ContainsKey(item.id)) { pItemList.RemoveAt(i); continue; }
                if ((!BombsEnabled || !mirvsAllowed) && mirvIds != null && mirvIds.Contains(item.id)) { pItemList.RemoveAt(i); continue; }
                if (weaponEras != null && weaponEras.ContainsKey(item.id)) {
                    if (MBReflect.m_isWeaponAllowed != null && !(bool)MBReflect.m_isWeaponAllowed.Invoke(null, new object[] { item, currentEra })) pItemList.RemoveAt(i);
                }
            }
        } catch {}
    }

    private void ApplyCleanPatches(Harmony harmony)
    {
        if (!KaijuEnabled) {
            try {
                Type kaijuType = AccessTools.TypeByName("ModernBox.Kaiju");
                if (kaijuType != null) harmony.Patch(AccessTools.Method(kaijuType, "init"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(SkipPrefix))));
                Type kaijuUIType = AccessTools.TypeByName("ModernBox.KaijuUI");
                if (kaijuUIType != null) harmony.Patch(AccessTools.Method(kaijuUIType, "init"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(SkipPrefix))));
            } catch {}
        }
        if (!SpaceEnabled) {
            try {
                Type spaceWindowType = AccessTools.TypeByName("ModernBox.SpaceWindow");
                if (spaceWindowType != null) harmony.Patch(AccessTools.Method(spaceWindowType, "init"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(SkipPrefix))));
                Type spaceManagerType = AccessTools.TypeByName("ModernBox.SpaceManager");
                if (spaceManagerType != null) harmony.Patch(AccessTools.Method(spaceManagerType, "EnableSpace"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(SkipPrefix))));
            } catch {}
        }
        try {
            Type tabBuilderType = AccessTools.TypeByName("ModernBox.TabBuilder");
            if (tabBuilderType != null) harmony.Patch(AccessTools.Method(tabBuilderType, "Build"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(TabBuilder_Prefix))));
            Type buttonBuilderType = AccessTools.TypeByName("ButtonBuilder");
            if (buttonBuilderType != null) harmony.Patch(AccessTools.Method(buttonBuilderType, "Build"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(ButtonBuilder_Prefix))));
            Type unitTrackerType = AccessTools.TypeByName("UnitTracker");
            if (unitTrackerType != null) harmony.Patch(AccessTools.Method(unitTrackerType, "RegisterUnit"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(UnitTracker_Prefix))));
        } catch {}
    }

    public static bool SkipPrefix() => false;
    public static bool TabBuilder_Prefix(object __instance) {
        if (__instance == null) return true;
        string tid = (string)AccessTools.Field(__instance.GetType(), "tabID").GetValue(__instance);
        if (!KaijuEnabled && tid == "Tab_kaiju") return false;
        if (!SpaceEnabled && tid == "ModernBoxSpace") return false;
        if (!ErasEnabled && tid == "ModernBoxEras") return false;
        if (!BombsEnabled && tid == "ModernBoxBombs") return false;
        return true;
    }
    public static bool ButtonBuilder_Prefix(object __instance) {
        if (__instance == null) return true;
        string id = (string)AccessTools.Field(__instance.GetType(), "id").GetValue(__instance);
        if (id == "fanpatch_config") return true; 
        if (!KaijuEnabled && (blockedIDs_Kaiju.Contains(id) || (!string.IsNullOrEmpty(id) && (id.ToLower().Contains("kaiju") || id.ToLower().Contains("mecha"))))) return false;
        return true;
    }
    private static HashSet<string> blockedIDs_Kaiju = new HashSet<string> { "spawnRamiel", "spawnGaghiel", "spawnSachiel", "spawnZeruel", "spawnGojira", "spawnLonglegder", "spawnRodanix", "spawnInvaderax", "spawnPanKong", "spawnMegaGojira", "spawnSkullcrawler", "spawncrabzilord", "spawnmechacrabzilla", "mechagodzilla_goliath", "earthquaker", "spawn_atst", "spawn_atstsniper", "spawn_artilleryatst", "spawn_P9000", "spawn_Railgun", "spawn_AT9000", "spawn_MA9000", "spawn_dreadnaught", "spawn_dreadnaught_brrt", "spawn_HumanTitan", "spawn_HumanTitanElite", "spawn_EliteP9000", "spawn_OmegaRailgun", "spawn_eliteAT9000", "spawn_eliteMA9000", "spawn_crusaderdreadnaught", "spawn_xenotripod" };
    public static bool UnitTracker_Prefix(string id) {
        if (string.IsNullOrEmpty(id)) return true;
        if (!KaijuEnabled && (blockedIDs_Kaiju.Contains(id) || id.ToLower().Contains("kaiju") || id.ToLower().Contains("mecha"))) return false;
        return true;
    }

    private void ApplyDurabilityFix(Harmony harmony) {
        try {
            Type bt = AccessTools.TypeByName("ModernBox.Buildings");
            if (bt != null) harmony.Patch(AccessTools.Method(bt, "init"), null, new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(Buildings_Init_Postfix))));
        } catch {}
    }
    public static void Buildings_Init_Postfix() {
        if (!DurabilityEnabled) return;
        foreach (BuildingAsset asset in AssetManager.buildings.list) {
            if (asset.id.Contains("modern") || asset.id.Contains("future") || asset.id.Contains("watch_tower")) {
                asset.burnable = false;
                if (asset.base_stats != null) asset.base_stats["health"] *= 5f;
            }
        }
    }

    private void ApplyTraitLock(Harmony harmony) {
        try {
            harmony.Patch(AccessTools.Method(typeof(Actor), "addTrait"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(Actor_AddTrait_Prefix))));
        } catch {}
    }
    private static HashSet<string> _modernTraits = new HashSet<string> { "Dynastic", "Martial", "Peoplewoven", "Mercantile", "Chaosvolt" };
    public static bool Actor_AddTrait_Prefix(string pTraitID) {
        if (!TraitLockEnabled) return true;
        try {
            if (MBReflect.m_getEra == null) return true;
            string currentEra = MBReflect.m_getEra.Invoke(null, null) as string;
            if ((currentEra == "Medieval" || currentEra == "Renaissance") && _modernTraits.Contains(pTraitID)) {
                return false; 
            }
        } catch {}
        return true;
    }

    private void ApplyFPSBooster(Harmony harmony) {
        try {
            harmony.Patch(AccessTools.Method(typeof(Projectile), "update"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(Projectile_Update_Prefix))));
        } catch {}
    }
    private static ConditionalWeakTable<Projectile, ProjData> _pTimers = new ConditionalWeakTable<Projectile, ProjData>();
    private class ProjData { public float age = 0f; }
    public static void Projectile_Update_Prefix(Projectile __instance, float pElapsed) {
        if (!FPSBoosterEnabled || __instance.asset == null) return;
        string id = __instance.asset.id;
        if (id.Contains("shell") || id.Contains("missile") || id.Contains("rocket") || id.Contains("bullet")) {
            var data = _pTimers.GetOrCreateValue(__instance);
            data.age += pElapsed;
            if (data.age > 2.5f) __instance.setState(ProjectileState.ToRemove);
        }
    }

    private void ApplyTimeFix(Harmony harmony) {
        try {
            Type ms = typeof(MapStats);
            harmony.Patch(AccessTools.PropertySetter(ms, "year_obsolete"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(PrefixYear))));
            harmony.Patch(AccessTools.PropertySetter(ms, "month_obsolete"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(PrefixMonth))));
            harmony.Patch(AccessTools.PropertySetter(ms, "worldTime_obsolete"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(PrefixWorldTime))));
        } catch {}
    }
    public static bool PrefixYear(MapStats __instance, int value) { if (!TimeFixEnabled) return true; double val = (double)value * 60.0; if (__instance.world_time < val - 1.0) __instance.world_time = val; return false; }
    public static bool PrefixMonth(MapStats __instance, int value) { if (!TimeFixEnabled) return true; double val = (double)value * 5.0; if (__instance.world_time > 0 && Math.Abs(__instance.world_time % 60.0) < 0.1) __instance.world_time += val; else if (__instance.world_time < val - 1.0) __instance.world_time = val; return false; }
    public static bool PrefixWorldTime(MapStats __instance, double value) { if (!TimeFixEnabled) return true; if (__instance.world_time < value - 1.0) __instance.world_time = value; return false; }

    private void ApplySpeedFix(Harmony harmony) {
        try {
            Type prefsType = AccessTools.TypeByName("ModernBoxPrefs");
            if (prefsType != null) {
                MethodInfo scaleBalance = AccessTools.Method(prefsType, "ScaleBalance");
                if (scaleBalance != null) harmony.Patch(scaleBalance, new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(ScaleBalance_Prefix))));
            }
        } catch {}
    }
    public static bool ScaleBalance_Prefix(float value, ref float __result) { if (!SpeedFixEnabled) return true; __result = value; return false; }

    private void ApplyExplosionFix(Harmony harmony) {
        try {
            Type itz = AccessTools.TypeByName("ModernBox.Itemz");
            if (itz != null) harmony.Patch(AccessTools.Method(itz, "LoadItems"), null, new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(LoadItems_Postfix))));
        } catch {}
    }
    public static void LoadItems_Postfix() {
        if (!ExplosionFixEnabled) return;
        try {
            string b = "shotgun_bullet";
            EquipmentAsset m = AssetManager.items.get("M4A1"); if (m != null) m.projectile = b;
            EquipmentAsset g = AssetManager.items.get("greenheavyblaster"); if (g != null) g.projectile = b;
        } catch {}
    }

    private void ApplyBiomeFixes(Harmony harmony) {
        if (BiomesEnabled) return;
        try {
            Type bs = AccessTools.TypeByName("ModernBox.OceanBiomes.OceanBiomeBootstrap");
            if (bs != null) harmony.Patch(AccessTools.Method(bs, "Init"), new HarmonyMethod(AccessTools.Method(typeof(NoKaijuNoMechsMain), nameof(SkipPrefix))));
            
            string mbId = FindModernBoxHarmonyID();
            UnpatchMethod(typeof(MapAction), "setOcean", mbId);
            UnpatchMethod(typeof(MapGenerator), "prepare", mbId);
            UnpatchMethod(typeof(MapGenerator), "generateBiomes", mbId);
            UnpatchMethod(typeof(MapBox), "addLastStep", mbId);
        } catch {}
    }

    private string FindModernBoxHarmonyID() {
        if (!string.IsNullOrEmpty(_detectedModernBoxID)) return _detectedModernBoxID;
        foreach (var mod in WorldBoxMod.LoadedMods) {
            if (mod.GetDeclaration() != null && mod.GetDeclaration().Name == "ModernBox") {
                _detectedModernBoxID = mod.GetDeclaration().UID;
                return _detectedModernBoxID;
            }
        }
        return "com.Tuxxego.MX"; 
    }

    private static void UnpatchMethod(Type targetType, string methodName, string ownerId) {
        try {
            MethodInfo original = AccessTools.Method(targetType, methodName);
            if (original != null) {
                Harmony h = new Harmony("temp_unpatcher");
                h.Unpatch(original, HarmonyPatchType.All, ownerId);
            }
        } catch {}
    }

    public static void UpdateGlobalEra(object statManager) {
        try {
            if (MBReflect.f_enableMedieval == null || MBReflect.f_eraLibAll == null) return;
            bool m = (bool)MBReflect.f_enableMedieval.GetValue(statManager);
            bool mo = (bool)MBReflect.f_enableModern.GetValue(statManager);
            var allEras = MBReflect.f_eraLibAll.GetValue(null) as System.Collections.IEnumerable;
            if (allEras == null) return;
            List<object> enabledEras = new List<object>();
            foreach (object era in allEras) {
                string key = AccessTools.Field(era.GetType(), "key")?.GetValue(era) as string;
                if (key == "medieval" && m) enabledEras.Add(era);
                else if (key == "renaissance" && (bool)MBReflect.f_enableRenaissance.GetValue(statManager)) enabledEras.Add(era);
                else if (key == "modern" && mo) enabledEras.Add(era);
                else if (key == "hyperfuture" && (bool)MBReflect.f_enableHyperfuture.GetValue(statManager)) enabledEras.Add(era);
            }
            if (enabledEras.Count == 0) return;
            int maxLevel = 0;
            foreach (City city in World.world.cities.list) {
                Building bonfire = city.getBuildingOfType("type_bonfire");
                if (bonfire != null && bonfire.asset != null && bonfire.asset.upgrade_level > maxLevel) maxLevel = bonfire.asset.upgrade_level;
            }
            int index = Mathf.Clamp(maxLevel, 0, enabledEras.Count - 1);
            object targetEra = enabledEras[index];
            string targetKey = AccessTools.Field(targetEra.GetType(), "key")?.GetValue(targetEra) as string;
            string targetName = AccessTools.Field(targetEra.GetType(), "name")?.GetValue(targetEra) as string;
            string targetDesc = AccessTools.Field(targetEra.GetType(), "description")?.GetValue(targetEra) as string;
            if ((MBReflect.f_currentEra.GetValue(statManager) as string) != targetName) {
                MBReflect.f_currentEra.SetValue(statManager, targetName);
                MBReflect.f_currentEraDesc.SetValue(statManager, targetDesc);
                if (MBReflect.m_setEraCity != null) {
                    string[] civGroups = { "alliance", "harden", "gaia", "horde" };
                    foreach (var group in civGroups) MBReflect.m_setEraCity.Invoke(null, new object[] { targetKey, group });
                }
            }
        } catch {}
    }
}


public enum FanpatchMenuState { Main, Features, Patches, Balance, Extra }

public class FanpatchUI : MonoBehaviour
{
    Canvas _canvas; GameObject _mainPanel; Font _font; bool _isClosing = false;
    FanpatchMenuState _state = FanpatchMenuState.Main;
    void Awake() { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); BuildUI(); }
    void ClearUI() { foreach (Transform child in _mainPanel.transform) Destroy(child.gameObject); }
    void BuildUI() {
        if (_canvas == null) {
            GameObject canvasGO = new GameObject("FanpatchCanvas");
            _canvas = canvasGO.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 30000;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; canvasGO.AddComponent<GraphicRaycaster>();
            GameObject bgGO = new GameObject("BG_Overlay"); bgGO.transform.SetParent(_canvas.transform, false);
            Image bgImg = bgGO.AddComponent<Image>(); bgImg.color = new Color(0.03f, 0.02f, 0.02f, 1f); bgImg.raycastTarget = true; Stretch(bgGO.GetComponent<RectTransform>());
            _mainPanel = new GameObject("MainPanel"); _mainPanel.transform.SetParent(_canvas.transform, false);
            _mainPanel.AddComponent<Image>().color = new Color(0.06f, 0.03f, 0.03f, 0.98f);
        }
        ClearUI();
        _mainPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(460, 440);
        if (_state == FanpatchMenuState.Main) BuildMainMenu();
        else if (_state == FanpatchMenuState.Features) BuildFeaturesMenu();
        else if (_state == FanpatchMenuState.Patches) BuildPatchesMenu();
        else if (_state == FanpatchMenuState.Balance) BuildBalanceMenu();
        else if (_state == FanpatchMenuState.Extra) BuildExtraMenu();
    }
    void BuildMainMenu() {
        MakeText(_mainPanel, "FANPATCH CONFIG", 22, true, new Vector2(0, 180), new Vector2(400, 40)); Decor_HLine(_mainPanel, new Vector2(0, 160), 250);
        MakePrimaryButton(_mainPanel, "CONFIGURE FEATURES", new Vector2(0, 95), () => { _state = FanpatchMenuState.Features; BuildUI(); });
        MakePrimaryButton(_mainPanel, "CONFIGURE PATCHES", new Vector2(0, 55), () => { _state = FanpatchMenuState.Patches; BuildUI(); });
        MakePrimaryButton(_mainPanel, "CONFIGURE BALANCE", new Vector2(0, 15), () => { _state = FanpatchMenuState.Balance; BuildUI(); });
        MakePrimaryButton(_mainPanel, "CONFIGURE EXTRAS", new Vector2(0, -25), () => { _state = FanpatchMenuState.Extra; BuildUI(); });
        MakePrimaryButton(_mainPanel, "RESET TO DEFAULT", new Vector2(0, -75), ResetToDefault);
        MakePrimaryButton(_mainPanel, "BACK TO GAME", new Vector2(0, -185), CloseUI);
    }
    void ResetToDefault() { NoKaijuNoMechsMain.ResetAllSettings(); ShowRestartScreen(); }
    void BuildFeaturesMenu() {
        MakeText(_mainPanel, "CONFIGURE FEATURES", 20, true, new Vector2(0, 185), new Vector2(400, 35)); Decor_HLine(_mainPanel, new Vector2(0, 165), 250);
        float startY = 135f; float spacingY = -65f;
        CreateToggleCard("KAIJU BOX", "Godzilla, Mechs, Monsters.", new Vector2(0, startY), NoKaijuNoMechsMain.KaijuEnabled, (val) => { NoKaijuNoMechsMain.KaijuEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_KaijuEnabled", val ? 1 : 0); });
        CreateToggleCard("SPACE MODE", "Biome, Star Map, Camera.", new Vector2(0, startY + spacingY), NoKaijuNoMechsMain.SpaceEnabled, (val) => { NoKaijuNoMechsMain.SpaceEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_SpaceEnabled", val ? 1 : 0); });
        CreateToggleCard("ERA SYSTEM", "Building unlocks and Era fix.", new Vector2(0, startY + spacingY * 2), NoKaijuNoMechsMain.ErasEnabled, (val) => { NoKaijuNoMechsMain.ErasEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_ErasEnabled", val ? 1 : 0); });
        CreateToggleCard("BOMB SYSTEMS", "Nukes and custom Explosives.", new Vector2(0, startY + spacingY * 3), NoKaijuNoMechsMain.BombsEnabled, (val) => { NoKaijuNoMechsMain.BombsEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_BombsEnabled", val ? 1 : 0); });
        CreateToggleCard("CUSTOM BIOMES", "Disable custom aquatic biomes.", new Vector2(0, startY + spacingY * 4), NoKaijuNoMechsMain.BiomesEnabled, (val) => { NoKaijuNoMechsMain.BiomesEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_BiomesEnabled", val ? 1 : 0); });
        MakePrimaryButton(_mainPanel, "SAVE & APPLY", new Vector2(0, -180), () => { PlayerPrefs.Save(); ShowRestartScreen(); });
        MakePrimaryButton(_mainPanel, "BACK", new Vector2(0, -220), () => { _state = FanpatchMenuState.Main; BuildUI(); });
    }
    void BuildPatchesMenu() {
        MakeText(_mainPanel, "CONFIGURE PATCHES", 20, true, new Vector2(0, 185), new Vector2(400, 35)); Decor_HLine(_mainPanel, new Vector2(0, 165), 250);
        float startY = 140f; float spacingY = -50f;
        CreateToggleCard("ERA AUTO-FIX", "Force automatic era progression.", new Vector2(0, startY), NoKaijuNoMechsMain.EraFixEnabled, (val) => { NoKaijuNoMechsMain.EraFixEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_EraFixEnabled", val ? 1 : 0); });
        CreateToggleCard("PRODUCTION LOCK", "Ensure weapons respect eras.", new Vector2(0, startY + spacingY), NoKaijuNoMechsMain.ProdFixEnabled, (val) => { NoKaijuNoMechsMain.ProdFixEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_ProdFixEnabled", val ? 1 : 0); });
        CreateToggleCard("SPEED SCALING FIX", "Fix projectiles being too slow.", new Vector2(0, startY + spacingY * 2), NoKaijuNoMechsMain.SpeedFixEnabled, (val) => { NoKaijuNoMechsMain.SpeedFixEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_SpeedFixEnabled", val ? 1 : 0); });
        CreateToggleCard("OLD AGE FIX", "Fix the 3000-year loading jump.", new Vector2(0, startY + spacingY * 3), NoKaijuNoMechsMain.TimeFixEnabled, (val) => { NoKaijuNoMechsMain.TimeFixEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_TimeFixEnabled", val ? 1 : 0); });
        CreateToggleCard("RESTORE LOGO", "Revert custom MB loading logo.", new Vector2(0, startY + spacingY * 4), NoKaijuNoMechsMain.LogoFixEnabled, (val) => { NoKaijuNoMechsMain.LogoFixEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_LogoFixEnabled", val ? 1 : 0); });
        MakePrimaryButton(_mainPanel, "SAVE & APPLY", new Vector2(0, -150), () => { PlayerPrefs.Save(); ShowRestartScreen(); });
        MakePrimaryButton(_mainPanel, "BACK", new Vector2(0, -190), () => { _state = FanpatchMenuState.Main; BuildUI(); });
    }
    void BuildBalanceMenu() {
        MakeText(_mainPanel, "CONFIGURE BALANCE", 20, true, new Vector2(0, 185), new Vector2(400, 35)); Decor_HLine(_mainPanel, new Vector2(0, 165), 250);
        float startY = 140f; float spacingY = -50f;
        CreateToggleCard("MODERN DURABILITY", "Buff modern buildings/fireproof.", new Vector2(0, startY), NoKaijuNoMechsMain.DurabilityEnabled, (val) => { NoKaijuNoMechsMain.DurabilityEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_DurabilityEnabled", val ? 1 : 0); });
        CreateToggleCard("ERA-LOCKED TRAITS", "Block modern traits in old eras.", new Vector2(0, startY + spacingY), NoKaijuNoMechsMain.TraitLockEnabled, (val) => { NoKaijuNoMechsMain.TraitLockEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_TraitLockEnabled", val ? 1 : 0); });
        CreateToggleCard("FPS BOOSTER", "Clean custom projectiles (2.5s).", new Vector2(0, startY + spacingY * 2), NoKaijuNoMechsMain.FPSBoosterEnabled, (val) => { NoKaijuNoMechsMain.FPSBoosterEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_FPSBoosterEnabled", val ? 1 : 0); });
        MakePrimaryButton(_mainPanel, "SAVE & APPLY", new Vector2(0, -150), () => { PlayerPrefs.Save(); ShowRestartScreen(); });
        MakePrimaryButton(_mainPanel, "BACK", new Vector2(0, -190), () => { _state = FanpatchMenuState.Main; BuildUI(); });
    }
    void BuildExtraMenu() {
        MakeText(_mainPanel, "CONFIGURE EXTRAS", 20, true, new Vector2(0, 185), new Vector2(400, 35)); Decor_HLine(_mainPanel, new Vector2(0, 165), 250);
        float startY = 140f; float spacingY = -50f;
        CreateToggleCard("NATURAL KAIJU SPAWN", "Rare spawns in Hyperfuture.", new Vector2(0, startY), NoKaijuNoMechsMain.NaturalKaijuSpawnEnabled, (val) => { NoKaijuNoMechsMain.NaturalKaijuSpawnEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_NaturalKaijuSpawnEnabled", val ? 1 : 0); });
        CreateToggleCard("RADIATION HAZARD", "Wastelands deal damage to units.", new Vector2(0, startY + spacingY), NoKaijuNoMechsMain.RadiationEnabled, (val) => { NoKaijuNoMechsMain.RadiationEnabled = val; PlayerPrefs.SetInt("MB_Fanpatch_RadiationEnabled", val ? 1 : 0); });
        MakePrimaryButton(_mainPanel, "SAVE & APPLY", new Vector2(0, -150), () => { PlayerPrefs.Save(); ShowRestartScreen(); });
        MakePrimaryButton(_mainPanel, "BACK", new Vector2(0, -190), () => { _state = FanpatchMenuState.Main; BuildUI(); });
    }
    void CreateToggleCard(string title, string desc, Vector2 pos, bool initial, Action<bool> onToggle) {
        var card = MakeOptionCard(_mainPanel, title, desc, pos, initial);
        card.GetComponent<Button>().onClick.AddListener(() => { initial = !initial; card.GetComponent<Image>().color = initial ? new Color(0.60f, 0.05f, 0.05f, 1f) : new Color(0.10f, 0.06f, 0.06f, 1f); onToggle(initial); });
    }
    void ShowRestartScreen() { ClearUI(); MakeText(_mainPanel, "RESTART REQUIRED", 24, true, new Vector2(0, 100), new Vector2(400, 45)).color = new Color(1.00f, 0.18f, 0.12f, 1f); MakeText(_mainPanel, "Settings saved. Restart game to apply.", 12, false, new Vector2(0, 40), new Vector2(350, 70)); MakePrimaryButton(_mainPanel, "EXIT GAME", new Vector2(0, -80), Application.Quit, true); MakePrimaryButton(_mainPanel, "BACK TO GAME", new Vector2(0, -130), CloseUI); }
    void CloseUI() { if (_isClosing) return; _isClosing = true; Destroy(_canvas.gameObject); Destroy(this.gameObject); }
    GameObject MakeOptionCard(GameObject parent, string title, string desc, Vector2 pos, bool active) {
        GameObject go = new GameObject("Card"); go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(360, 46); rt.anchoredPosition = pos;
        Image img = go.AddComponent<Image>(); img.color = active ? new Color(0.60f, 0.05f, 0.05f, 1f) : new Color(0.10f, 0.06f, 0.06f, 1f);
        go.AddComponent<Button>(); MakeText(go, title, 11, true, new Vector2(0, 8), new Vector2(340, 20));
        MakeText(go, desc, 8, false, new Vector2(0, -8), new Vector2(340, 18)).color = new Color(0.40f, 0.32f, 0.32f, 1f); return go;
    }
    GameObject MakePrimaryButton(GameObject parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, bool danger = false) {
        GameObject go = new GameObject("Btn"); go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(200, 36); rt.anchoredPosition = pos;
        Image img = go.AddComponent<Image>(); img.color = danger ? new Color(0.5f, 0, 0) : new Color(0.82f, 0.07f, 0.07f, 1f);
        go.AddComponent<Button>().onClick.AddListener(onClick); MakeText(go, label, 14, true, Vector2.zero, new Vector2(190, 36)); return go;
    }
    void Decor_HLine(GameObject parent, Vector2 pos, float width) {
        GameObject go = new GameObject("Line"); go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(width, 2);
        go.AddComponent<Image>().color = new Color(0.38f, 0.04f, 0.04f, 1f);
    }
    Text MakeText(GameObject parent, string text, int size, bool bold, Vector2 pos, Vector2 area) {
        GameObject go = new GameObject("Txt"); go.transform.SetParent(parent.transform, false);
        Text t = go.AddComponent<Text>(); t.text = text; t.font = _font; t.fontSize = size; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        RectTransform rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = area; return t;
    }
    void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero; }
}

public class FanpatchWarningUI : MonoBehaviour
{
    Canvas _canvas; GameObject _mainPanel; Font _font;
    void Awake() { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); BuildUI(); }
    void BuildUI() {
        GameObject canvasGO = new GameObject("WarningCanvas");
        _canvas = canvasGO.AddComponent<Canvas>(); _canvas.renderMode = RenderMode.ScreenSpaceOverlay; _canvas.sortingOrder = 40000;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; canvasGO.AddComponent<GraphicRaycaster>();
        GameObject bgGO = new GameObject("BG"); bgGO.transform.SetParent(_canvas.transform, false);
        bgGO.AddComponent<Image>().color = new Color(0.03f, 0.02f, 0.02f, 1f); Stretch(bgGO.GetComponent<RectTransform>());
        _mainPanel = new GameObject("MainPanel"); _mainPanel.transform.SetParent(_canvas.transform, false);
        _mainPanel.AddComponent<Image>().color = new Color(0.06f, 0.03f, 0.03f, 0.98f); _mainPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(500, 400);
        MakeText(_mainPanel, "MODERNBOX WARNING", 24, true, new Vector2(0, 140), new Vector2(400, 50)).color = new Color(1.00f, 0.18f, 0.12f, 1f);
        MakeText(_mainPanel, "The Steam version of ModernBox is currently OUTDATED.\n\nPlease use the GameBanana version for the best experience\nand to ensure all Fanpatch fixes work correctly.", 14, false, new Vector2(0, 30), new Vector2(450, 150));
        MakePrimaryButton(_mainPanel, "DOWNLOAD LATEST", new Vector2(0, -60), () => Application.OpenURL("https://gamebanana.com/mods/462076"));
        MakePrimaryButton(_mainPanel, "I UNDERSTAND", new Vector2(0, -110), () => { Destroy(_canvas.gameObject); Destroy(this.gameObject); }, true);
    }
    GameObject MakePrimaryButton(GameObject parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick, bool grey = false) {
        GameObject go = new GameObject("Btn"); go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>(); rt.sizeDelta = new Vector2(220, 36); rt.anchoredPosition = pos;
        go.AddComponent<Image>().color = grey ? new Color(0.2f, 0.2f, 0.2f, 1f) : new Color(0.82f, 0.07f, 0.07f, 1f);
        go.AddComponent<Button>().onClick.AddListener(onClick);
        MakeText(go, label, 14, true, Vector2.zero, new Vector2(210, 36)); return go;
    }
    Text MakeText(GameObject parent, string text, int size, bool bold, Vector2 pos, Vector2 area) {
        GameObject go = new GameObject("Txt"); go.transform.SetParent(parent.transform, false);
        Text t = go.AddComponent<Text>(); t.text = text; t.font = _font; t.fontSize = size; t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        RectTransform rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = area; return t;
    }
    void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero; }
}
