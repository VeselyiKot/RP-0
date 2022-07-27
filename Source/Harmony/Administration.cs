﻿using HarmonyLib;
using KSP.UI.Screens;
using KSP.UI;
using Strategies;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using RP0.Programs;
using UniLinq;
using KSP.Localization;

namespace RP0.Harmony
{
    [HarmonyPatch(typeof(Administration))]
    internal class PatchAdministration
    {
        [HarmonyPrefix]
        [HarmonyPatch("Start")]
        internal static void Prefix_Start(Administration __instance)
        {
            var adminExt = __instance.gameObject.GetComponent<AdminExtender>() ?? __instance.gameObject.AddComponent<AdminExtender>();
            adminExt.BindAndFixUI();
        }

        [HarmonyPostfix]
        [HarmonyPatch("Start")]
        internal static void Postfix_Start(Administration __instance, ref int ___activeStrategyCount, ref int ___maxActiveStrategies)
        {
            ___activeStrategyCount = ProgramHandler.Instance.ActivePrograms.Count;
            ___maxActiveStrategies = ProgramHandler.Instance.ActiveProgramLimit;
        }

        [HarmonyPrefix]
        [HarmonyPatch("CreateStrategiesList")]
        internal static void Prefix_CreateStrategiesList(Administration __instance, ref int ___activeStrategyCount, ref int ___maxActiveStrategies)
        {
            ___activeStrategyCount = ProgramHandler.Instance.ActivePrograms.Count;
            ___maxActiveStrategies = ProgramHandler.Instance.ActiveProgramLimit;

            Transform tSpacer1 = __instance.scrollListKerbals.transform.Find("DepartmentSpacer1");
            if (tSpacer1 != null)
                GameObject.Destroy(tSpacer1.gameObject);

            Transform tSpacer2 = __instance.scrollListKerbals.transform.Find("DepartmentSpacer2");
            if (tSpacer2 != null)
                GameObject.Destroy(tSpacer2.gameObject);
        }

        [HarmonyPostfix]
        [HarmonyPatch("CreateStrategiesList")]
        internal static void Postfix_CreateStrategiesList(Administration __instance)
        {
            __instance.scrollListStrategies.GetUilistItemAt(0).GetComponent<LayoutElement>().minWidth = 280f;
            var firstDep = __instance.scrollListKerbals.GetUilistItemAt(0);
            GameObject spacer = GameObject.Instantiate(firstDep.gameObject);
            spacer.name = "DepartmentSpacer1";
            for (int i = spacer.transform.childCount - 1; i >= 0; --i)
                GameObject.DestroyImmediate(spacer.transform.GetChild(i).gameObject);

            GameObject.DestroyImmediate(spacer.GetComponent<KerbalListItem>());
            GameObject.DestroyImmediate(spacer.GetComponent<Image>());
            GameObject.DestroyImmediate(spacer.GetComponent<UIListItem>());


            spacer.GetComponent<LayoutElement>().minWidth = 70f;
            spacer.transform.SetParent(firstDep.transform.parent, false);
            spacer.transform.SetAsFirstSibling();

            GameObject spacer2 = GameObject.Instantiate(spacer);
            spacer2.name = "DepartmentSpacer2";
            spacer2.transform.SetParent(spacer.transform.parent, false);
            spacer2.transform.SetSiblingIndex(firstDep.transform.GetSiblingIndex() + 1);
        }

        internal static List<Strategy> strategies = new List<Strategy>();

        [HarmonyPrefix]
        [HarmonyPatch("CreateActiveStratList")]
        internal static bool Prefix_CreateActiveStratList(Administration __instance, ref int ___activeStrategyCount, ref int ___maxActiveStrategies)
        {
            __instance.scrollListActive.Clear(true);
            Administration.StrategyWrapper wrapper = null;

            if (AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Leaders)
            {
                foreach (var s in StrategySystem.Instance.Strategies)
                    if (s.IsActive && !(s is ProgramStrategy))
                        strategies.Add(s);
            }
            else
            {
                List<Program> programs = AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Active ? ProgramHandler.Instance.ActivePrograms : ProgramHandler.Instance.CompletedPrograms;
                foreach (Program p in programs)
                {
                    Strategy strategy = StrategySystem.Instance.Strategies.Find(s => s.Config.Name == p.name);
                    if (strategy == null)
                        continue;

                    // Just in case. This should never happen unless you use the debugging UI...
                    if (AdminExtender.Instance.ActiveTabView == AdministrationActiveTabView.Active && !strategy.IsActive)
                    {
                        strategy.Activate();
                    }

                    if (strategy is ProgramStrategy ps)
                    {
                        if (ps.Program != p)
                            Debug.LogError($"[RP-0] ProgramStrategy binding mismatch for completed program! Strat {strategy.Config.Name} is bound to program that is not the same. Null? {ps.Program == null}");
                    }

                    strategies.Add(strategy);
                }
            }
            foreach (Strategy strategy in strategies)
            {
                UIListItem item = UnityEngine.Object.Instantiate(__instance.prefabActiveStrat);
                ActiveStrategyListItem stratItem = item.GetComponent<ActiveStrategyListItem>();
                UIRadioButton button = item.GetComponent<UIRadioButton>();
                wrapper = new Administration.StrategyWrapper(strategy, button);
                button.Data = wrapper;
                button.onTrue.AddListener(wrapper.OnTrue);
                button.onFalse.AddListener(wrapper.OnFalse);
                Texture icon = wrapper.strategy.Config.IconImage;
                if (icon == null)
                {
                    icon = __instance.defaultIcon;
                }
                stratItem.Setup("<b><color=" + XKCDColors.HexFormat.KSPBadassGreen + ">" + strategy.Title + "</color></b>", strategy.Effect, icon as Texture2D);

                __instance.scrollListActive.AddItem(item);
            }
            strategies.Clear();

            ___activeStrategyCount = ProgramHandler.Instance.ActivePrograms.Count;
            ___maxActiveStrategies = ProgramHandler.Instance.ActiveProgramLimit;
            __instance.activeStratCount.text = Localizer.Format("#autoLOC_439627", ProgramHandler.Instance.ActivePrograms.Count, ProgramHandler.Instance.ActiveProgramLimit);

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("SetSelectedStrategy")]
        internal static void Prefix_SetSelectedStrategy(Administration __instance, ref Administration.StrategyWrapper wrapper)
        {
            if (wrapper.strategy is ProgramStrategy ps)
            {
                ps.NextTextIsShowSelected = true; // pass through we're about to print the long-form description.

                // Set best speed before we get description
                if (!ps.Program.IsComplete && !ps.Program.IsActive && !AdminExtender.Instance.PressedSpeedButton)
                    ps.Program.SetBestAllowableSpeed();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("SetSelectedStrategy")]
        internal static void Postfix_SetSelectedStrategy(Administration __instance, ref Administration.StrategyWrapper wrapper)
        {
            var tooltip = __instance.btnAcceptCancel.GetComponent<KSP.UI.TooltipTypes.UIStateButtonTooltip>();

            // If it's a Program, we always want to show the green checkmark
            // not the red x.
            if (wrapper.strategy is ProgramStrategy ps)
            {
                if (ps.Program.IsComplete)
                    __instance.btnAcceptCancel.gameObject.SetActive(false);
                else if (__instance.btnAcceptCancel.currentState == "accept")
                {
                    float cost = ps.Program.ConfidenceCost;
                    var stateAccept = tooltip.tooltipStates.First(s => s.name == "accept");
                    if (cost > 0)
                        stateAccept.tooltipText = Localizer.Format("#rp0AcceptProgramWithCost", cost.ToString("N0"));
                    else
                        stateAccept.tooltipText = Localizer.GetStringByTag("#autoLOC_900259");
                }
                else
                {
                    // Use the "deactivate" tooltip for the accept button
                    tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = Localizer.GetStringByTag("#autoLOC_900260");
                    __instance.btnAcceptCancel.SetState("accept");
                }

                AdminExtender.Instance.SetSpeedButtonsActive(!ps.Program.IsActive && !ps.Program.IsComplete ? ps.Program : null);
            }
            else
            {
                tooltip.tooltipStates.First(s => s.name == "accept").tooltipText = Localizer.GetStringByTag("#rp0AppointLeader");
                tooltip.tooltipStates.First(s => s.name == "cancel").tooltipText = Localizer.GetStringByTag("#rp0RemoveLeader");
                AdminExtender.Instance.SetSpeedButtonsActive(null);
            }
            AdminExtender.Instance.BtnSpacer.gameObject.SetActive(!Administration.Instance.btnAcceptCancel.gameObject.activeSelf);
        }

        [HarmonyPostfix]
        [HarmonyPatch("UnselectStrategy")]
        internal static void Postfix_UnselectStrategy()
        {
            AdminExtender.Instance.SetSpeedButtonsActive(null);
        }

        internal static void OnPopupDismiss()
        {
            // Stock does nothing here. Leaving this in case we need it.
        }

        internal static void OnCompleteProgramConfirm()
        {
            OnPopupDismiss();

            if (!Administration.Instance.SelectedWrapper.strategy.Deactivate())
                return;

            Administration.Instance.UnselectStrategy();
            Administration.Instance.RedrawPanels();
        }

        internal static MethodInfo addActive = typeof(Administration).GetMethod("AddActiveStratItem", AccessTools.all);
        internal static MethodInfo createStratList = typeof(Administration).GetMethod("CreateStrategiesList", AccessTools.all);

        internal static void OnActivateProgramConfirm()
        {
            OnPopupDismiss();
            if (Administration.Instance.SelectedWrapper.strategy.Activate())
            {
                var newActiveStrat = Administration.Instance.SelectedWrapper.strategy;
                AdminExtender.Instance.SetTabView(AdministrationActiveTabView.Active);
                Administration.StrategyWrapper selectedStrategy = addActive.Invoke(Administration.Instance, new object[] { Administration.Instance.SelectedWrapper.strategy }) as Administration.StrategyWrapper;
                Administration.Instance.SetSelectedStrategy(selectedStrategy);
                // Reset program speeds
                foreach (var strat in StrategySystem.Instance.Strategies)
                {
                    if (!strat.IsActive && strat != newActiveStrat && strat is ProgramStrategy ps && !ps.IsActive)
                        ps.Program.SetBestAllowableSpeed();
                }
                createStratList.Invoke(Administration.Instance, new object[] { StrategySystem.Instance.SystemConfig.Departments });
                Administration.Instance.SelectedWrapper.ButtonInUse.Value = true;
            }
            StrategySystem.Instance.StartCoroutine(CallbackUtil.DelayedCallback(2, delegate
            {
                if (!Administration.Instance.SelectedWrapper.strategy.IsActive)
                {

                    Administration.Instance.UnselectStrategy();
                    return;
                }
            }));
        }

        [HarmonyPrefix]
        [HarmonyPatch("BtnInputAccept")]
        internal static bool Prefix_BtnInputAccept(Administration __instance, ref string state)
        {
            if (state != "accept" && state != "cancel")
                return false;

            if (__instance.SelectedWrapper.strategy is ProgramStrategy ps)
            {
                if (ps.IsActive)
                {
                    if (!ps.CanBeDeactivated(out _))
                        return false;

                    var dlg = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog("StrategyConfirmation",
                            Localizer.GetStringByTag("#rp0ProgramCompleteConfirm"),
                            Localizer.GetStringByTag("#autoLOC_464288"),
                            HighLogic.UISkin,
                            new DialogGUIButton(Localizer.GetStringByTag("#autoLOC_439855"), OnCompleteProgramConfirm),
                            new DialogGUIButton(Localizer.GetStringByTag("#autoLOC_439856"), OnPopupDismiss)),
                        persistAcrossScenes: false, HighLogic.UISkin);
                    dlg.OnDismiss = OnPopupDismiss;
                }
                else
                {
                    if (ps.Program.IsComplete)
                        return false;

                    double cost = ps.Program.ConfidenceCost;
                    string message = cost > 0 ? Localizer.Format("#rp0ProrgamActivateConfirmWithCost", cost.ToString("N0")) : Localizer.GetStringByTag("#rp0ProrgamActivateConfirm");

                    var dlg = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog("StrategyConfirmation",
                        message,
                        Localizer.Format("#autoLOC_464288"),
                        HighLogic.UISkin,
                        new DialogGUIButton(Localizer.Format("#autoLOC_439839"), OnActivateProgramConfirm),
                        new DialogGUIButton(Localizer.Format("#autoLOC_439840"), OnPopupDismiss)), persistAcrossScenes: false, HighLogic.UISkin);
                    dlg.OnDismiss = OnPopupDismiss;
                }
            }
            //else if( is LeaderStrategy...

            return false;
        }
    }
}