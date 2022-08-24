﻿using Strategies;
using UnityEngine;
using System.Collections.Generic;
using RP0.DataTypes;
using KerbalConstructionTime;

namespace RP0.Leaders
{
    public class CurrencyModifier : BaseEffect
    {
        [Persistent]
        private PersistentListValueType<TransactionReasonsRP0> transactionReasons = new PersistentListValueType<TransactionReasonsRP0>();

        private TransactionReasonsRP0 listeningReasons = TransactionReasonsRP0.None;

        [Persistent]
        private CurrencyRP0 currency = CurrencyRP0.Invalid;

        [Persistent]
        private bool invertIfNegative = false;

        public CurrencyModifier(Strategy parent)
            : base(parent)
        {
        }

        protected override string DescriptionString()
        {
            return KSP.Localization.Localizer.Format(string.IsNullOrEmpty(locStringOverride) ? "#rp0_Leaders_Effect_CurrencyModifier" : locStringOverride,
                LocalizationHandler.FormatRatioAsPercent(multiplier),
                currency.displayDescription(),
                effectDescription);
        }

        public override void OnLoadFromConfig(ConfigNode node)
        {
            base.OnLoadFromConfig(node);

            if (currency == CurrencyRP0.Invalid)
                return;

            listeningReasons = TransactionReasonsRP0.None;
            foreach (var r in transactionReasons)
                listeningReasons |= r;
        }

        public override void OnRegister()
        {
            GameEvents.Modifiers.OnCurrencyModifierQuery.Add(OnEffectQuery);
        }

        public override void OnUnregister()
        {
            GameEvents.Modifiers.OnCurrencyModifierQuery.Remove(OnEffectQuery);
        }

        protected void OnEffectQuery(CurrencyModifierQuery qry)
        {
            double multToUse = multiplier;
            if (invertIfNegative && qry.GetInput(currency.Stock()) < 0d)
            {
                multToUse = 2d - multToUse;
            }

            if (qry is CurrencyModifierQueryRP0 qryRP0)
            {
                if ((listeningReasons & qryRP0.reasonRP0) != 0)
                    qryRP0.Multiply(currency, multToUse);
            }
            else
            {
                if (currency <= CurrencyRP0.Reputation && (listeningReasons & qry.reason.RP0()) != 0)
                {
                    qry.AddDelta(currency.Stock(), (float)(qry.GetInput(currency.Stock()) * (multiplier - 1d)));
                }
            }
                
        }
    }
}
