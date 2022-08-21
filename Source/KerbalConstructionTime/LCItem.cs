﻿using System;
using System.Collections.Generic;
using UniLinq;
using UnityEngine;
using RP0.DataTypes;

namespace KerbalConstructionTime
{
    public class LCItem : IConfigNode
    {
        public string Name;
        protected Guid _id;
        public Guid ID => _id;
        protected Guid _modID;
        public Guid ModID => _modID;
        public KCTObservableList<BuildListVessel> BuildList = new KCTObservableList<BuildListVessel>();
        public KCTObservableList<BuildListVessel> Warehouse = new KCTObservableList<BuildListVessel>();
        public KCTObservableList<PadConstruction> PadConstructions = new KCTObservableList<PadConstruction>();
        public PersistentList<ReconRollout> Recon_Rollout = new PersistentList<ReconRollout>();
        public PersistentList<AirlaunchPrep> Airlaunch_Prep = new PersistentList<AirlaunchPrep>();

        private LCData _lcData = new LCData();
        public LCData Stats => _lcData;

        private double _rate;
        private double _rateHRCapped;
        public double Rate => _rate;
        public double RateHRCapped => _rateHRCapped;

        public const int MinEngineersConst = 1;
        public const int EngineersPerPacket = 10;
        
        public int Engineers = 0;
        private static double RawMaxEngineers(float massMax, Vector3 sizeMax) =>
            massMax != float.MaxValue ? Math.Pow(massMax, 0.75d) : sizeMax.sqrMagnitude * 0.01d;
        public static int MaxEngineersCalc(float massMax, Vector3 sizeMax, bool isHuman) => 
            Math.Max(MinEngineersConst, (int)Math.Ceiling(RawMaxEngineers(massMax, sizeMax) * (isHuman ? 1.5d : 1d) * EngineersPerPacket));

        private double _RawMaxEngineers => RawMaxEngineers(MassMax, SizeMax);
        public int MaxEngineers => MaxEngineersCalc(MassMax, SizeMax, IsHumanRated);
        public int MaxEngineersNonHR => Math.Max(MinEngineersConst, (int)Math.Ceiling(_RawMaxEngineers * EngineersPerPacket));
        public int MaxEngineersFor(double mass, double bp, bool humanRated)
        {
            if (LCType == LaunchComplexType.Pad)
                return IsHumanRated && !humanRated ? MaxEngineersNonHR : MaxEngineers;

            double tngMax = RawMaxEngineers((float)mass, Vector3.zero);
            if (IsHumanRated && humanRated)
                tngMax *= 1.5d;
            double bpMax = Math.Pow(bp * 0.000015d, 0.75d);
            return Math.Max(MinEngineersConst, (int)Math.Ceiling((tngMax * 0.25d + bpMax * 0.75d) * EngineersPerPacket));
        }
        public int MaxEngineersFor(BuildListVessel blv) => blv == null ? MaxEngineers : MaxEngineersFor(blv.GetTotalMass(), blv.buildPoints + blv.integrationPoints, blv.humanRated);

        protected double _strategyRateMultiplier = 1d;
        public double StrategyRateMultiplier => _strategyRateMultiplier;

        private LCEfficiency _efficiencySource = null;
        public LCEfficiency EfficiencySource
        {
            get
            {
                if (LCType == LaunchComplexType.Hangar)
                    return null;

                if (_efficiencySource == null)
                    _efficiencySource = LCEfficiency.GetOrCreateEfficiencyForLC(this, true);

                return _efficiencySource;
            }
        }
        public double Efficiency
        {
            get
            {
                if (LCType == LaunchComplexType.Hangar)
                    return LCEfficiency.MaxEfficiency;

                return EfficiencySource.Efficiency;
            }
        }

        public bool IsRushing;
        public double RushRate => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushRateMult : 1d;
        public double RushSalary => IsRushing ? PresetManager.Instance.ActivePreset.GeneralSettings.RushSalaryMult : 1d;

        public bool IsOperational = false;

        public LaunchComplexType LCType => _lcData.lcType;
        public bool IsHumanRated => _lcData.isHumanRated;
        public float MassMax => _lcData.massMax;
        public float MassOrig => _lcData.massOrig;
        public float MassMin => _lcData.MassMin;
        public Vector3 SizeMax => _lcData.sizeMax;
        public PersistentDictionaryValueTypes<string, double> ResourcesHandled => _lcData.resourcesHandled;

        public List<KCT_LaunchPad> LaunchPads = new List<KCT_LaunchPad>();
        public int ActiveLaunchPadIndex = 0;

        public static string SupportedMassAsPrettyTextCalc(float mass) => mass == float.MaxValue ? "unlimited" : $"{LCData.CalcMassMin(mass):N0}-{mass:N0}t";
        public string SupportedMassAsPrettyText => SupportedMassAsPrettyTextCalc(MassMax);

        public static string SupportedSizeAsPrettyTextCalc(Vector3 size) => size.y == float.MaxValue ? "unlimited" : $"{size.z:N0}x{size.x:N0}x{size.y:N0}m";
        public string SupportedSizeAsPrettyText => SupportedSizeAsPrettyTextCalc(SizeMax);

        private KSCItem _ksc = null;

        public KSCItem KSC => _ksc;

        #region Observable funcs
        void added(int idx, ConstructionBuildItem pc) { _ksc.Constructions.Add(pc); }
        void removed(int idx, ConstructionBuildItem pc) { _ksc.Constructions.Remove(pc); }
        void updated() { RP0.MaintenanceHandler.Instance?.ScheduleMaintenanceUpdate(); }

        void AddListeners()
        {
            PadConstructions.Added += added;
            PadConstructions.Removed += removed;
            PadConstructions.Updated += updated;

            BuildList.Updated += updated;
            Warehouse.Updated += updated;
        }
        #endregion

        public LCItem(KSCItem ksc)
        {
            _ksc = ksc;
            AddListeners();
        }

        public LCItem(LCData lcData, KSCItem ksc)
        {
            _ksc = ksc;
            _id = Guid.NewGuid();
            _modID = _id;
            _lcData.SetFrom(lcData);
            Name = _lcData.Name;

            if (_lcData.lcType == LaunchComplexType.Pad)
            {
                float fracLevel = _lcData.GetPadFracLevel();
                var pad = new KCT_LaunchPad(Guid.NewGuid(), Name, fracLevel);
                pad.isOperational = true;
                LaunchPads.Add(pad);
            }

            AddListeners();
        }

        public void Modify(LCData data, Guid modId)
        {
            _modID = modId;
            _lcData.SetFrom(data);

            if (_lcData.lcType == LaunchComplexType.Pad)
            {
                float fracLevel = _lcData.GetPadFracLevel();

                foreach (var pad in LaunchPads)
                {
                    pad.fractionalLevel = fracLevel;
                    pad.level = (int)fracLevel;
                }
            }

            // will create a new one if needed (it probably will be needed)
            // If it does, it will remove us from the old one, and then clear it if it's empty.
            if (LCType != LaunchComplexType.Hangar)
                _efficiencySource = LCEfficiency.GetOrCreateEfficiencyForLC(this, false);

            RecalculateBuildRates();
        }

        public KCT_LaunchPad ActiveLPInstance => LaunchPads.Count > ActiveLaunchPadIndex && ActiveLaunchPadIndex >= 0 ? LaunchPads[ActiveLaunchPadIndex] : null;

        public int LaunchPadCount
        {
            get
            {
                int count = 0;
                foreach (KCT_LaunchPad lp in LaunchPads)
                    if (lp.isOperational) count++;
                return count;
            }
        }

        public bool IsEmpty => LCType == LaunchComplexType.Hangar && BuildList.Count == 0 && Warehouse.Count == 0 && Airlaunch_Prep.Count == 0 && Engineers == 0 && LCData.StartingHangar.Compare(this);

        public bool IsActive => BuildList.Count > 0 || Recon_Rollout.Count > 0 || Airlaunch_Prep.Count > 0;
        public bool CanModify => BuildList.Count == 0 && Warehouse.Count == 0 && !Recon_Rollout.Any(r => r.RRType != ReconRollout.RolloutReconType.Reconditioning) && Airlaunch_Prep.Count == 0;
        public bool IsIdle => !IsActive;

        public ReconRollout GetReconditioning(string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => r.launchPadID == launchSite && ((IKCTBuildItem)r).GetItemName() == "LaunchPad Reconditioning");

        public ReconRollout GetReconRollout(ReconRollout.RolloutReconType type, string launchSite = "LaunchPad") =>
            Recon_Rollout.FirstOrDefault(r => (type == ReconRollout.RolloutReconType.None ||  r.RRType == type) && r.launchPadID == launchSite);

        public void RecalculateBuildRates()
        {
            _strategyRateMultiplier = RP0.CurrencyUtils.Rate(LCType == LaunchComplexType.Pad ? RP0.TransactionReasonsRP0.RateIntegrationVAB : RP0.TransactionReasonsRP0.RateIntegrationSPH);
            _rate = Utilities.GetBuildRate(0, this, IsHumanRated, true);
            _rateHRCapped = Utilities.GetBuildRate(0, this, false, true);
            foreach (var blv in BuildList)
                blv.UpdateBuildRate();

            foreach (var rr in Recon_Rollout)
                rr.UpdateBuildRate();

            foreach (var al in Airlaunch_Prep)
                al.UpdateBuildRate();

            KCTDebug.Log($"Build rate for {Name} = {_rate:N3}, capped {_rateHRCapped:N3}");
        }

        public void SwitchToPrevLaunchPad() => SwitchLaunchPad(false);
        public void SwitchToNextLaunchPad() => SwitchLaunchPad(true);

        public void SwitchLaunchPad(bool forwardDirection)
        {
            if (LaunchPadCount < 2)
            {
                ActiveLaunchPadIndex = 0;
                return;
            }

            int idx = ActiveLaunchPadIndex;
            KCT_LaunchPad pad;
            int count = LaunchPads.Count;
            do
            {
                if (forwardDirection)
                {
                    ++idx;
                    if (idx == count)
                        idx = 0;
                }
                else
                {
                    if (idx == 0)
                        idx = count;
                    --idx;
                }
                pad = LaunchPads[idx];
            } while (!pad.isOperational);

            SwitchLaunchPad(idx);
        }

        public void Rename(string newName)
        {
            Name = newName;
            _lcData.Name = newName;
        }

        public void SwitchLaunchPad(int LP_ID = -1, bool updateDestrNode = true)
        {
            if (LP_ID >= 0)
            {
                if (ActiveLaunchPadIndex == LP_ID && ActiveLPInstance != null && ActiveLPInstance.isOperational)
                    return;

                ActiveLaunchPadIndex = LP_ID;
            }

            if (ActiveLPInstance == null)
            {
                for (ActiveLaunchPadIndex = 0; ActiveLaunchPadIndex < LaunchPads.Count; ++ActiveLaunchPadIndex)
                {
                    if (LaunchPads[ActiveLaunchPadIndex].isOperational)
                    {
                        break;
                    }
                }
                // failed to find
                if (ActiveLaunchPadIndex == LaunchPads.Count)
                {
                    ActiveLaunchPadIndex = 0;
                    return;
                }
            }

            //set the active LP's new state
            //activate new pad

            if (updateDestrNode)
                ActiveLPInstance?.RefreshDestructionNode();

            ActiveLPInstance?.SetActive();
        }

        public LaunchPadState GetBestLaunchPadState()
        {
            LaunchPadState state = LaunchPadState.None;
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                var padState = lp.State;
                if (padState > state)
                    state = padState;
            }

            return state;
        }

        public KCT_LaunchPad FindFreeLaunchPad()
        {
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                if (lp.State == LaunchPadState.Free)
                    return lp;
            }

            return null;
        }

        public void OnRemove()
        {
            if (_efficiencySource == null)
                KerbalConstructionTimeData.Instance.LCToEfficiency.TryGetValue(this, out _efficiencySource);
            if (_efficiencySource != null)
                _efficiencySource.RemoveLC(this);
            else
                LCEfficiency.ClearEmpty();
        }

        public ConfigNode AsConfigNode()
        {
            KCTDebug.Log("Saving LC " + Name);
            var node = new ConfigNode("LaunchComplex");
            node.AddValue("LCName", Name);
            node.AddValue("ActiveLPID", ActiveLaunchPadIndex);
            node.AddValue("operational", IsOperational);
            node.AddValue("id", _id);
            node.AddValue("modID", _modID);
            node.AddValue("Engineers", Engineers);
            node.AddValue("IsRushing", IsRushing);

            var statsNode = new ConfigNode("Stats");
            ConfigNode.CreateConfigFromObject(_lcData, statsNode);
            node.AddNode(statsNode);

            var cnBuildl = new ConfigNode("BuildList");
            BuildList.Save(cnBuildl);
            node.AddNode(cnBuildl);

            var cnWh = new ConfigNode("Warehouse");
            Warehouse.Save(cnWh);
            node.AddNode(cnWh);

            var cnPadConstructions = new ConfigNode("PadConstructions");
            PadConstructions.Save(cnPadConstructions);
            node.AddNode(cnPadConstructions);

            var cnRR = new ConfigNode("Recon_Rollout");
            Recon_Rollout.Save(cnRR);
            node.AddNode(cnRR);

            var cnAP = new ConfigNode("Airlaunch_Prep");
            Airlaunch_Prep.Save(cnAP);
            node.AddNode(cnAP);

            var cnLPs = new ConfigNode("LaunchPads");
            foreach (KCT_LaunchPad lp in LaunchPads)
            {
                ConfigNode lpCN = lp.AsConfigNode();
                lpCN.AddNode(lp.DestructionNode);
                cnLPs.AddNode(lpCN);
            }
            node.AddNode(cnLPs);

            return node;
        }

        public LCItem FromConfigNode(ConfigNode node)
        {
            BuildList.Clear();
            Warehouse.Clear();
            PadConstructions.Clear();
            Recon_Rollout.Clear();
            Airlaunch_Prep.Clear();
            LaunchPads.Clear();
            _rate = 0;
            _rateHRCapped = 0;
            Engineers = 0;
            IsRushing = false;

            Name = node.GetValue("LCName");
            ActiveLaunchPadIndex = 0;
            node.TryGetValue("ActiveLPID", ref ActiveLaunchPadIndex);
            if (ActiveLaunchPadIndex < 0)
                ActiveLaunchPadIndex = 0;
            node.TryGetValue("operational", ref IsOperational);
            node.TryGetValue("id", ref _id);
            if (!node.TryGetValue("modID", ref _modID) || _modID == (new Guid()) )
                _modID = Guid.NewGuid();
            node.TryGetValue("Engineers", ref Engineers);
            node.TryGetValue("IsRushing", ref IsRushing);

            ConfigNode tmp = node.GetNode("Stats");
            if (tmp != null)
                ConfigNode.LoadObjectFromConfig(_lcData, tmp);

            BuildList.Load(node.GetNode("BuildList"));
            foreach (var blv in BuildList)
                blv.LinkToLC(this);

            Warehouse.Load(node.GetNode("Warehouse"));
            foreach (var blv in Warehouse)
                blv.LinkToLC(this);

            tmp = node.GetNode("Recon_Rollout");
            Recon_Rollout.Load(tmp);

            if (node.TryGetNode("Airlaunch_Prep", ref tmp))
            {
                Airlaunch_Prep.Load(tmp);
            }

            tmp = node.GetNode("LaunchPads");
            if (tmp != null)
            {
                foreach (ConfigNode cn in tmp.GetNodes("KCT_LaunchPad"))
                {
                    var tempLP = new KCT_LaunchPad("LP0");
                    ConfigNode.LoadObjectFromConfig(tempLP, cn);
                    if (!cn.TryGetValue(nameof(KCT_LaunchPad.id), ref tempLP.id) || tempLP.id == Guid.Empty)
                    {
                        tempLP.id = Guid.NewGuid();
                    }
                    tempLP.DestructionNode = cn.GetNode("DestructionState");
                    if (tempLP.fractionalLevel == -1) tempLP.MigrateFromOldState();
                    LaunchPads.Add(tempLP);
                }
            }

            tmp = node.GetNode("PadConstructions");
            if (tmp != null)
            {
                PadConstructions.Load(tmp);
            }

            if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < KCTGameStates.VERSION)
            {
                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 1)
                {
                    Engineers *= 2;
                }

                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 6 && LCType != LaunchComplexType.Hangar)
                {
                    double oldEffic = 0.5d;
                    node.TryGetValue("EfficiencyEngineers", ref oldEffic);

                    // we can't use the dict yet
                    bool createEffic = true;
                    foreach (var e in KerbalConstructionTimeData.Instance.LCEfficiencies)
                    {
                        if (e.Contains(_id) && e.Efficiency < oldEffic)
                        {
                            e.IncreaseEfficiency(oldEffic - e.Efficiency, false);
                            createEffic = false;
                            break;
                        }
                    }
                    if (createEffic)
                    {
                        LCEfficiency closest = LCEfficiency.FindClosest(this, out double closeness);
                        if (closeness == 1d && closest.Efficiency < oldEffic)
                        {
                            closest.IncreaseEfficiency(oldEffic - closest.Efficiency, false);
                            createEffic = false;
                        }
                    }
                    if (createEffic)
                    {
                        var e = LCEfficiency.GetOrCreateEfficiencyForLC(this, true);
                        if (e.Efficiency < oldEffic)
                            e.IncreaseEfficiency(oldEffic - e.Efficiency, false);
                    }
                }
                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 7)
                {
                    node.TryGetEnum<LaunchComplexType>("lcType", ref _lcData.lcType, LaunchComplexType.Pad);
                    node.TryGetValue("massMax", ref _lcData.massMax);
                    node.TryGetValue("massOrig", ref _lcData.massOrig);
                    node.TryGetValue("sizeMax", ref _lcData.sizeMax);
                    node.TryGetValue("IsHumanRated", ref _lcData.isHumanRated);
                }
                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 8)
                {
                    if (_id == Guid.Empty)
                        _id = Guid.NewGuid();
                    if (_modID == Guid.Empty)
                        _modID = Guid.NewGuid();

                    // check if we're the hangar
                    if (_ksc.LaunchComplexes.Count == 0) // KSC loader hasn't added us yet
                    {
                        if (_lcData.lcType != LaunchComplexType.Hangar || _lcData.massMax != float.MaxValue)
                            _lcData.SetFrom(LCData.StartingHangar);
                    }
                }
                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 12)
                {
                    _lcData.Name = Name;
                }
                if (KerbalConstructionTimeData.Instance.LoadedSaveVersion < 13)
                {
                    tmp = node.GetNode("Plans");

                    if (tmp != null)
                    {
                        foreach (ConfigNode cnV in tmp.GetNodes("KCTVessel"))
                        {
                            var blv = new BuildListVessel();
                            blv.Load(cnV);
                            blv.LCID = Guid.Empty;
                            KCTGameStates.Plans.Remove(blv.shipName);
                            KCTGameStates.Plans.Add(blv.shipName, blv);
                        }
                    }
                }
            }

            return this;
        }

        public void Load(ConfigNode node)
        {
            throw new NotImplementedException();
        }

        public void Save(ConfigNode node)
        {
            throw new NotImplementedException();
        }
    }
}

/*
    KerbalConstructionTime (c) by Michael Marvin, Zachary Eck

    KerbalConstructionTime is licensed under a
    Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International License.

    You should have received a copy of the license along with this
    work. If not, see <http://creativecommons.org/licenses/by-nc-sa/4.0/>.
*/
