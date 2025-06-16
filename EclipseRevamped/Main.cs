using BepInEx;
using System.Diagnostics;
using System.IO;
using R2API;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using RoR2;
using R2API.Utils;
using MonoMod.RuntimeDetour;
using System.Reflection;
using EntityStates.AI.Walker;

namespace EclipseRevamped
{
  [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
  public class Main : BaseUnityPlugin
  {
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "Nuxlar";
    public const string PluginName = "EclipseRevamped";
    public const string PluginVersion = "1.0.0";

    internal static Main Instance { get; private set; }
    public static string PluginDirectory { get; private set; }

    public void Awake()
    {
      Instance = this;

      Stopwatch stopwatch = Stopwatch.StartNew();

      Log.Init(Logger);

      ChangeDescriptions();
      /*
    ideas
        E1 Teleporter Bosses +100%
        E2 
        E3 Enemy Attack Speed: +25%
        E4 Enemies +50% faster
        E5 
        E6 
        E7 Enemy Cooldowns -25%
        E8 Permanent Damage (except self-damage)
        More elite spawns?
        TP events more dangerous? mountain shrine but no reward
        Tier 2 elites appear pre loop

        Vanilla E2
        E4
      */
      IL.RoR2.CharacterMaster.OnBodyStart += RemoveVanillaE1;
      IL.RoR2.HoldoutZoneController.DoUpdate += RemoveVanillaE2;
      IL.RoR2.GlobalEventManager.OnCharacterHitGroundServer += RemoveVanillaE3;
      IL.RoR2.HealthComponent.Heal += RemoveVanillaE5;
      IL.RoR2.DeathRewards.OnKilledServer += RemoveVanillaE6;
      IL.RoR2.CharacterBody.RecalculateStats += TweakE4;
      IL.RoR2.CombatDirector.CalcHighestEliteCostMultiplier += NewE5Hook1;
      IL.RoR2.CombatDirector.PrepareNewMonsterWave += NewE5Hook3;
      IL.RoR2.CombatDirector.AttemptSpawnOnTarget += NewE5Hook4;
      IL.RoR2.CharacterBody.RecalculateStats += TweakE7;
      // get_selectedDifficulty
      On.RoR2.TeleporterInteraction.Awake += AddNewE1;
      MethodInfo target = typeof(DirectorCard).GetPropertyGetter(nameof(DirectorCard.cost));
      Hook hook = new Hook(target, AddNewE2);
      MethodInfo target2 = typeof(CombatDirector).GetPropertyGetter(nameof(CombatDirector.lowestEliteCostMultiplier));
      Hook hook2 = new Hook(target2, NewE5Hook2);
      RecalculateStatsAPI.GetStatCoefficients += AddNewE3;

      stopwatch.Stop();
      Log.Info_NoCallerPrefix($"Initialized in {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }
    private void NewE5Hook4(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.After,
      x => x.MatchLdfld(typeof(CombatDirector.EliteTierDef), "costMultiplier")
      ))
      {
        c.EmitDelegate<Func<float, float>>((mult) =>
        {
          if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5 && mult != 1)
          {
            float newMult = mult * 0.75f;
            return newMult;
          }
          else return mult;
        });
        if (c.TryGotoNext(MoveType.After, x => x.MatchLdfld(typeof(CombatDirector.EliteTierDef), "costMultiplier")))
        {
          c.EmitDelegate<Func<float, float>>((mult) =>
        {
          if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5 && mult != 1)
          {
            float newMult = mult * 0.75f;
            return newMult;
          }
          else return mult;
        });
        }
      }
      else
        Log.Error("EclipseRevamped: Failed to add NewE5Hook4");
    }

    private void NewE5Hook3(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.After,
      x => x.MatchLdfld(typeof(CombatDirector.EliteTierDef), "costMultiplier")
      ))
      {
        c.EmitDelegate<Func<float, float>>((mult) =>
        {
          if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5)
          {
            float newMult = mult * 0.75f;
            return newMult;
          }
          else return mult;
        });
      }
      else
        Log.Error("EclipseRevamped: Failed to add NewE5Hook3");
    }

    public float NewE5Hook2(Func<float> orig)
    {
      if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5)
      {
        float newMult = CombatDirector.eliteTiers[1].costMultiplier * 0.75f;
        return newMult;
      }
      else
        return orig();
    }

    private void NewE5Hook1(ILContext il)
    {
      ILCursor c = new ILCursor(il);
      if (c.TryGotoNext(MoveType.After,
      x => x.MatchLdfld(typeof(CombatDirector.EliteTierDef), "costMultiplier")
      ))
      {
        c.EmitDelegate<Func<float, float>>((mult) =>
        {
          if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse5)
          {
            float newMult = mult * 0.75f;
            return newMult;
          }
          else return mult;
        });
      }
      else
        Log.Error("EclipseRevamped: Failed to add NewE5Hook1");
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
          int reducedCost = (int)Math.Round(spawnCard.directorCreditCost * 0.75f, 0, MidpointRounding.AwayFromZero);
          return reducedCost;
        }
        else return spawnCard.directorCreditCost;
      }
      else return orig(self);
    }

    private void AddNewE1(On.RoR2.TeleporterInteraction.orig_Awake orig, TeleporterInteraction self)
    {
      orig(self);
      if (Run.instance && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse1)
      {
        self.shrineBonusStacks += 1;
        self.gameObject.transform.GetChild(0).GetChild(7).gameObject.SetActive(true);
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
      string str2 = "\n<mspace=0.5em>(1)</mspace> Teleporter Bosses: <style=cIsHealth>+100%</style></style>";
      string str3 = "\n<mspace=0.5em>(2)</mspace> Larger Enemies <style=cIsHealth>appear more often</style></style>";
      string str4 = "\n<mspace=0.5em>(3)</mspace> Enemy Attack Speed: <style=cIsHealth>+25%</style></style>";
      string str5 = "\n<mspace=0.5em>(4)</mspace> Enemies: <style=cIsHealth>+50% Faster</style></style>";
      string str6 = "\n<mspace=0.5em>(5)</mspace> Enemy Elites: <style=cIsHealth>+25%</style></style>";
      string str7 = "\n<mspace=0.5em>(6)</mspace> </style></style>";
      string str8 = "\n<mspace=0.5em>(7)</mspace> Enemy Cooldowns: <style=cIsHealth>-25%</style></style>";
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