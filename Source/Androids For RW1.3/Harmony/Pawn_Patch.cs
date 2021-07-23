﻿using Verse;
using Verse.AI;
using Verse.AI.Group;
using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld.Planet;

namespace MOARANDROIDS
{

    internal class Pawn_Patch
    {
        [HarmonyPatch(typeof(Pawn), "SetFaction")]
        public class SetFaction_Patch
        {
            [HarmonyPostfix]
            public static void Listener(Faction newFaction, Pawn recruiter, Pawn __instance)
            {
                try
                {
                    if (__instance == null)
                        return;

                    //Si surrogate on le deconnecte et on clear le controlleur (SI pas faisant suite à un piratage)
                    CompAndroidState cas = Utils.getCachedCAS(__instance);
                    if (cas != null && cas.isSurrogate && cas.externalController != null && newFaction != null && newFaction.IsPlayer && !(Find.DesignatorManager.SelectedDesignator != null && Find.DesignatorManager.SelectedDesignator is Designator_SurrogateToHack))
                    {
                        if (cas.surrogateController != null)
                        {
                            //On affiche une notif
                            Find.LetterStack.ReceiveLetter("ATPP_LetterTraitorOffline".Translate(), "ATPP_LetterTraitorOfflineDesc".Translate(__instance.LabelShortCap), LetterDefOf.NegativeEvent);

                            CompSurrogateOwner cso = Utils.getCachedCSO(cas.surrogateController);
                            //Le cas echeant on le deconnecte
                            if (cso != null)
                                cso.disconnectControlledSurrogate(null);
                        }

                        //On vire l'external controller
                        cas.externalController = null;
                    }
                }
                catch(Exception e)
                {
                    Log.Message("[ATPP] Pawn.SetFaction " + e.Message + " " + e.StackTrace);
                }
            }
        }


        [HarmonyPatch(typeof(Pawn), "Kill")]
        public class Kill
        {
            [HarmonyPrefix]
            public static bool Listener(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit = null)
            {
                try
                {
                    if (__instance.IsSurrogateAndroid())
                    {
                        Utils.insideKillFuncSurrogate = true;

                        //Si c'est un surrogate controllé temporaire alors on le restitue a sa faction
                        CompSkyMind csm = Utils.getCachedCSM(__instance);
                        if(csm != null)
                        {
                            //Log.Message("Restitution surrogate a sa faction");
                            csm.tempHackingEnding();
                        }
                    }
                    //disconnect killed user
                    Utils.GCATPP.disconnectUser(__instance);
                    //Log.Message("YOU KILLED "+__instance.LabelCap);
                    //Is surrogate android used ?
                    if (__instance.IsSurrogateAndroid(true))
                    {
                        //Obtention controlleur
                        CompAndroidState cas = Utils.getCachedCAS(__instance);
                        if (cas == null)
                            return true;

                        //Arret du mode de control chez le controller
                        CompSurrogateOwner cso = Utils.getCachedCSO(cas.surrogateController);
                        cso.stopControlledSurrogate(__instance,false, false, true);

                        //On reset les données pour une potentiel futur resurection
                        cas.resetInternalState();

                    }

                    
                    //Log.Message("YOU KILLED END");
                    Utils.insideKillFuncSurrogate = false;
                    return true;
                }
                catch (Exception e)
                {
                    Log.Message("[ATPP] Pawn.Kill(Error) : " + e.Message + " - " + e.StackTrace);

                    if (__instance.IsSurrogateAndroid())
                        Utils.insideKillFuncSurrogate = false;
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Pawn), "PreKidnapped")]
        public class PreKidnapped_Patch
        {
            [HarmonyPostfix]
            public static void Listener(Pawn __instance, Pawn kidnapper)
            {
                try
                {
                    if ((__instance.IsAndroidTier() || __instance.VXChipPresent() || __instance.IsSurrogateAndroid()))
                    {
                        //On deconnecte l'user de force le cas echeant
                        Utils.GCATPP.disconnectUser(__instance);
                    }
                }
                catch(Exception e)
                {
                    Log.Message("[ATPP] Pawn.PreKidnapped(Error) : " + e.Message + " - " + e.StackTrace);
                }
            }
        }

        [HarmonyPatch(typeof(Pawn), "ButcherProducts")]
        public class ButcherProducts_Patch
        {
            [HarmonyPostfix]
            public static void Listener(Pawn butcher, float efficiency, Pawn __instance)
            {
                if (__instance.IsAndroidTier())
                    Utils.lastButcheredPawnIsAndroid = true;
                else
                    Utils.lastButcheredPawnIsAndroid = false;
            }
        }

        // Patch used to deregister from the mapPawns surrogates (only if the related setting is enabled) And register surrogate in the listerSurrogates
        [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
        public class SpawnSetup_Patch
        {
            [HarmonyPostfix]
            public static void Listener(Map map, bool respawningAfterLoad, Pawn __instance)
            {
                if (__instance.IsSurrogateAndroid())
                {
                    CompAndroidState cas = Utils.getCachedCAS(__instance);
                    if (cas != null)
                    {
                        if(__instance.Downed)
                            Utils.addDownedSurrogateToLister(__instance);

                        if (Settings.hideInactiveSurrogates)
                        {
                            //Remove surrogate from main lists only if inactive surrogate
                            if (cas.surrogateController == null)
                            {
                                //hide only surrogate on player's map
                                if (map != null && map.IsPlayerHome)
                                    map.mapPawns.DeRegisterPawn(__instance);
                            }
                        }
                    }
                }
            }
        }

        

        [HarmonyPatch(typeof(Pawn), "GetGizmos")]
        public class GetGizmos_Patch
        {
            [HarmonyPostfix]
            public static void Listener(Pawn __instance, ref IEnumerable<Gizmo> __result)
            {
                try
                {
                    CompSkyMind csm = Utils.getCachedCSM(__instance);

                    //Si prisonnier et possede une VX2 on va obtenir les GIZMOS associés OU virusé
                    if ((__instance.IsPrisoner || __instance.IsSlave) || (csm != null && csm.Hacked == 1))
                    {
                        IEnumerable<Gizmo> tmp;
                        //Si posseseur d'une VX2

                        if (__instance.VXChipPresent())
                        {
                            CompSurrogateOwner cso = Utils.getCachedCSO(__instance);
                            if (cso != null)
                            {
                                tmp = cso.CompGetGizmosExtra();
                                if (tmp != null)
                                    __result = __result.Concat(tmp);
                            }
                        }

                        //Si android prisonier ou virusé
                        if (__instance.IsAndroidTier())
                        {
                            CompAndroidState cas = Utils.getCachedCAS(__instance);

                            if (cas != null)
                            {
                                tmp = cas.CompGetGizmosExtra();
                                if (tmp != null)
                                    __result = __result.Concat(tmp);
                            }
                        }

                        if (csm != null && csm.Hacked == -1)
                        {
                            tmp = csm.CompGetGizmosExtra();
                            if (tmp != null)
                                __result = __result.Concat(tmp);
                        }
                    }

                    //Si animal posséder par player
                    if (__instance.IsPoweredAnimalAndroids())
                    {
                        CompAndroidState cas = null;
                        cas = Utils.getCachedCAS(__instance);
                        if (cas != null)
                        {
                            IEnumerable<Gizmo> tmp = cas.CompGetGizmosExtra();
                            if (tmp != null)
                                __result = __result.Concat(tmp);
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.Message("[ATPP] Pawn.GetGizmos " + e.Message + " " + e.StackTrace);
                }
            }
        }
    }
}