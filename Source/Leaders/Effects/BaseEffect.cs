﻿using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
    public abstract class BaseEffect : StrategyEffect
    {
        [Persistent]
        protected string effectDescription = string.Empty;

        [Persistent]
        protected string locStringOverride = string.Empty;

        [Persistent]
        protected string effectTitle = string.Empty;

        [Persistent]
        protected double multiplier = 1d;

        [Persistent]
        protected bool flipPositive = false;

        protected const string _GoodColor = "#caff00";
        protected const string _BadColor = "#feb200";

        public BaseEffect(Strategy parent)
            : base(parent)
        {
        }

        protected virtual bool IsPositive => (multiplier > 1d) ^ flipPositive;

        protected abstract string DescriptionString();

        protected override string GetDescription()
        {
            string retStr = $"<color={(IsPositive ? _GoodColor : _BadColor)}>{DescriptionString()}</color>";

            if (Parent is StrategyRP0 sr && sr.ShowExtendedInfo && !string.IsNullOrEmpty(effectTitle))
                retStr = effectTitle + "\n  " + retStr;

            return retStr;
        }

        protected override void OnLoadFromConfig(ConfigNode node)
        {
            ConfigNode.LoadObjectFromConfig(this, node);
        }
    }
}
