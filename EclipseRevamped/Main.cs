using BepInEx;
using System.Diagnostics;
using R2API;
using MonoMod.Cil;
using System;
using RoR2;
using R2API.Utils;
using MonoMod.RuntimeDetour;
using System.Reflection;
using BepInEx.Configuration;
using static RoR2.CombatDirector;
using UnityEngine.Networking;
using UnityEngine;

namespace EclipseRevamped
{
  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  public class Main : BaseUnityPlugin
  {
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Nuxlar";
    public const string PluginName = "EclipseRevamped";
    public const string PluginVersion = "1.1.4";

    internal static Main Instance { get; private set; }
    public static string PluginDirectory { get; private set; }

    public static ConfigEntry<bool> shouldChangeE1;
    public static ConfigEntry<bool> shouldChangeE2;
    public static ConfigEntry<bool> shouldChangeE3;
    public static ConfigEntry<bool> shouldChangeE4;
    public static ConfigEntry<bool> shouldChangeE5;
    public static ConfigEntry<bool> shouldChangeE6;
    public static ConfigEntry<bool> shouldChangeE7;

    private static ConfigFile ERConfig { get; set; }

    public void Awake()
    {
      Instance = this;

      Stopwatch stopwatch = Stopwatch.StartNew();

      Log.Init(Logger);

      ERConfig = new ConfigFile(Paths.ConfigPath + "\\com.Nuxlar.EclipseRevamped.cfg", true);
      shouldChangeE1 = ERConfig.Bind<bool>("General", "Enable E1 Changes", true, "Rework this Eclipse level");
      shouldChangeE2 = ERConfig.Bind<bool>("General", "Enable E2 Changes", true, "Rework this Eclipse level");
      shouldChangeE3 = ERConfig.Bind<bool>("General", "Enable E3 Changes", true, "Rework this Eclipse level");
      shouldChangeE4 = ERConfig.Bind<bool>("General", "Enable E4 Changes", true, "Tweak this Eclipse level");
      shouldChangeE5 = ERConfig.Bind<bool>("General", "Enable E5 Changes", true, "Rework this Eclipse level");
      shouldChangeE6 = ERConfig.Bind<bool>("General", "Enable E6 Changes", true, "Rework this Eclipse level");
      shouldChangeE7 = ERConfig.Bind<bool>("General", "Enable E7 Changes", true, "Tweak this Eclipse level");

      ChangeDescriptions();

      if (shouldChangeE1.Value)
      {
        IL.RoR2.CharacterMaster.OnBodyStart += RemoveVanillaE1;
        IL.RoR2.TeleporterInteraction.ChargingState.OnEnter += NewE1Logic;
        On.RoR2.TeleporterInteraction.ChargingState.OnEnter += AddNewE1;
      }
      if (shouldChangeE2.Value)
      {
        IL.RoR2.HoldoutZoneController.DoUpdate += RemoveVanillaE2;
        MethodInfo target = typeof(DirectorCard).GetPropertyGetter(nameof(DirectorCard.cost));
        Hook hook = new Hook(target, AddNewE2);
      }
      if (shouldChangeE3.Value)
      {
        IL.RoR2.GlobalEventManager.OnCharacterHitGroundServer += RemoveVanillaE3;
        RecalculateStatsAPI.GetStatCoefficients += AddNewE3;
      }
      if (shouldChangeE4.Value)
      {
        IL.RoR2.CharacterBody.RecalculateStats += TweakE4;
      }
      if (shouldChangeE5.Value)
      {
        IL.RoR2.HealthComponent.Heal += RemoveVanillaE5;
        On.RoR2.Run.Start += AddNewE5;
      }
      if (shouldChangeE6.Value)
      {
        IL.RoR2.DeathRewards.OnKilledServer += RemoveVanillaE6;
        On.RoR2.CombatDirector.Init += AddNewE6;
      }
      if (shouldChangeE7.Value)
      {
        IL.RoR2.CharacterBody.RecalculateStats += TweakE7;
      }

      stopwatch.Stop();
      Log.Info_NoCallerPrefix($"Initialized in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }
    private void AddNewE5(On.RoR2.Run.orig_Start orig, Run self)
    {
      orig(self);
      if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5)
      {
        EliteTierDef t1Tier = EliteAPI.VanillaEliteTiers[1];
        t1Tier.costMultiplier = 4.5f;
        EliteTierDef t1GildedTier = EliteAPI.VanillaEliteTiers[4];
        t1GildedTier.costMultiplier = 4.5f;

        EliteTierDef t2Tier = EliteAPI.VanillaEliteTiers[5];
        t2Tier.costMultiplier = 25f;
        foreach (EliteDef def in t2Tier.eliteTypes)
        {
          if (def != null)
          {
            def.damageBoostCoefficient = 4f;
            def.healthBoostCoefficient = 12f;
          }
        }
      }
      else
      {
        EliteTierDef t1Tier = EliteAPI.VanillaEliteTiers[1];
        t1Tier.costMultiplier = 6f;
        EliteTierDef t1GildedTier = EliteAPI.VanillaEliteTiers[4];
        t1GildedTier.costMultiplier = 6f;
        EliteTierDef t2Tier = EliteAPI.VanillaEliteTiers[5];
        t2Tier.costMultiplier = 36f;
        foreach (EliteDef def in t2Tier.eliteTypes)
        {
          if (def != null)
          {
            def.damageBoostCoefficient = 6f;
            def.healthBoostCoefficient = 18f;
          }
        }
      }

    }

    private void AddNewE6(On.RoR2.CombatDirector.orig_Init orig)
    {
      orig();

      EliteTierDef t2Tier = EliteAPI.VanillaEliteTiers[5];
      t2Tier.isAvailable = (rules) =>
     {
       if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5)
       {
         return Run.instance && Run.instance.stageClearCount >= 3;
       }
       else
         return Run.instance && Run.instance.loopClearCount > 0 && rules == SpawnCard.EliteRules.Default;
     };
    }

    private void AddNewE3(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
    {
      if (!(bool)Run.instance || Run.instance.selectedDifficulty < DifficultyIndex.Eclipse3 || !(bool)sender || !(bool)sender.teamComponent || sender.teamComponent.teamIndex == TeamIndex.Player)
        return;
      args.attackSpeedMultAdd += 0.25f;
    }

    public int AddNewE2(Func<DirectorCard, int> orig, DirectorCard self)
    {
      if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse2)
      {
        SpawnCard spawnCard = self.GetSpawnCard();
        if (spawnCard && spawnCard.hullSize == HullClassification.Golem)
        {
          int reducedCost = (int)Math.Round(spawnCard.directorCreditCost * 0.80f, 0, MidpointRounding.AwayFromZero);
          return reducedCost;
        }
        else return spawnCard.directorCreditCost;
      }
      else return orig(self);
    }

    private void NewE1Logic(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.After,
        x => x.MatchCall<TeleporterInteraction.ChargingState>("get_bossDirector")
      ))
      {
        c.EmitDelegate<Func<CombatDirector, CombatDirector>>(useless => null);
      }
    }

    private void AddNewE1(On.RoR2.TeleporterInteraction.ChargingState.orig_OnEnter orig, TeleporterInteraction.ChargingState self)
    {
      orig(self);
      if (NetworkServer.active)
      {
        if ((bool)self.bossDirector)
        {
          float creditMultiplier = 0f;
          if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse1)
            creditMultiplier = 0.5f;

          self.bossDirector.enabled = true;
          self.bossDirector.monsterCredit += (float)(600.0 * Mathf.Pow(Run.instance.compensatedDifficultyCoefficient, 0.5f) * (1 + self.teleporterInteraction.shrineBonusStacks + creditMultiplier));
          self.bossDirector.currentSpawnTarget = self.gameObject;
          self.bossDirector.SetNextSpawnAsBoss();
        }
      }
    }

    private void TweakE7(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.After,
      x => x.MatchCallvirt(typeof(Run), "get_selectedDifficulty"),
      x => x.MatchLdcI4(9)
      ))
      {
        if (c.TryGotoNext(MoveType.Before,
        x => x.MatchLdcR4(0.5f)))
        {
          c.Next.Operand = 0.75f;
        }
      }
      else
        Log.Error("EclipseRevamped: Failed to tweak vanilla E7");
    }

    private void TweakE4(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.Before,
      x => x.MatchCallvirt(typeof(Run), "get_selectedDifficulty")
      ))
      {
        c.GotoNext(MoveType.Before, x => x.MatchLdcR4(0.4f));
        c.Next.Operand = 0.5f;
      }
      else
        Log.Error("EclipseRevamped: Failed to tweak vanilla E4");
    }

    private void RemoveVanillaE1(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.Before,
      x => x.MatchLdcR4(0.5f)
      ))
        c.Next.Operand = 1f;
      else
        Log.Error("EclipseRevamped: Failed to remove vanilla E1");
    }

    private void RemoveVanillaE2(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.Before,
      x => x.MatchLdcR4(0.5f))
      )
        c.Next.Operand = 1f;
      else
        Log.Error("EclipseRevamped: Failed to remove vanilla E2");
    }

    private void RemoveVanillaE3(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.Before, x => x.MatchLdcI4(5))
      )
      {
        ++c.Index;
        c.EmitDelegate<Func<int, int>>(useless => int.MaxValue);
      }
      else
        Log.Error("EclipseRevamped: Failed to remove vanilla E3");
    }

    private void RemoveVanillaE5(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.Before,
      x => x.MatchLdcR4(2f),
      x => x.MatchDiv()
      ))
        c.Next.Operand = 1f;
      else
        Log.Error("EclipseRevamped: Failed to tweak vanilla E5");
    }

    private void RemoveVanillaE6(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.Before,
     x => x.MatchLdcR4(0.8f)
      ))
        c.Next.Operand = 1f;
      else
        Log.Error("EclipseRevamped: Failed to remove vanilla E6");
    }

    private void ChangeDescriptions()
    {
      string str1 = "Starts at baseline Monsoon difficulty.\n";
      string str2 = shouldChangeE1.Value ? "\n<mspace=0.5em>(1)</mspace> Teleporter Enemies: <style=cIsHealth>+50%</style></style>" : "\n<mspace=0.5em>(1)</mspace> Ally Starting Health: <style=cIsHealth>-50%</style></style>";
      string str3 = shouldChangeE2.Value ? "\n<mspace=0.5em>(2)</mspace> Larger Enemies <style=cIsHealth>appear more often</style></style>" : "\n<mspace=0.5em>(2)</mspace> Teleporter Radius: <style=cIsHealth>-50%</style></style>";
      string str4 = shouldChangeE3.Value ? "\n<mspace=0.5em>(3)</mspace> Enemy Attack Speed: <style=cIsHealth>+25%</style></style>" : "\n<mspace=0.5em>(3)</mspace> Ally Fall Damage: <style=cIsHealth>+100% and lethal</style></style>";
      string str5 = shouldChangeE4.Value ? "\n<mspace=0.5em>(4)</mspace> Enemies: <style=cIsHealth>+50% Faster</style></style>" : "\n<mspace=0.5em>(4)</mspace> Enemies: <style=cIsHealth>+40% Faster</style></style>";
      string str6 = shouldChangeE5.Value ? "\n<mspace=0.5em>(5)</mspace> Enemy Elites: <style=cIsHealth>+25%</style></style>" : "\n<mspace=0.5em>(5)</mspace> Ally Healing: <style=cIsHealth>-50%</style></style>";
      string str7 = shouldChangeE6.Value ? "\n<mspace=0.5em>(6)</mspace> Tier 2 Elites <style=cIsHealth>appear earlier</style></style>" : "\n<mspace=0.5em>(6)</mspace> Enemy Gold Drops: <style=cIsHealth>-20%</style></style>";
      string str8 = shouldChangeE7.Value ? "\n<mspace=0.5em>(7)</mspace> Enemy Cooldowns: <style=cIsHealth>-25%</style></style>" : "\n<mspace=0.5em>(7)</mspace> Enemy Cooldowns: <style=cIsHealth>-50%</style></style>";
      string str9 = "\n<mspace=0.5em>(8)</mspace> Allies recieve <style=cIsHealth>permanent damage</style></style>";
      string str10 = "\"You only celebrate in the light... because I allow it.\" \n\n";
      LanguageAPI.Add("ECLIPSE_1_DESCRIPTION", str1 + str2);
      LanguageAPI.Add("ECLIPSE_2_DESCRIPTION", str1 + str2 + str3);
      LanguageAPI.Add("ECLIPSE_3_DESCRIPTION", str1 + str2 + str3 + str4);
      LanguageAPI.Add("ECLIPSE_4_DESCRIPTION", str1 + str2 + str3 + str4 + str5);
      LanguageAPI.Add("ECLIPSE_5_DESCRIPTION", str1 + str2 + str3 + str4 + str5 + str6);
      LanguageAPI.Add("ECLIPSE_6_DESCRIPTION", str1 + str2 + str3 + str4 + str5 + str6 + str7);
      LanguageAPI.Add("ECLIPSE_7_DESCRIPTION", str1 + str2 + str3 + str4 + str5 + str6 + str7 + str8);
      LanguageAPI.Add("ECLIPSE_8_DESCRIPTION", str10 + str1 + str2 + str3 + str4 + str5 + str6 + str7 + str8 + str9);
    }

  }
}