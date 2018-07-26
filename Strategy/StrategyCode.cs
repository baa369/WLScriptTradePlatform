using System;
using System.Collections.Generic;
using WealthLab;
using WealthLab.Indicators;
using System.Globalization;
using Strategy;

namespace WindowsFormsApplication
{
    public class StrategyCode : WealthScript
    {
        public int Counter = 0;
        public int CyclesPerformed
        {
            get
            {
                return this.Counter;
            }
        }

        public string Name = "VP_ZRLS_CND_MBS";
        public string ScriptName
        {
            get
            {
                return this.Name;
            }
        }

        public List<Dictionary<string, object>> list_alerts = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> AlertsCreated
        {
            get
            {
                return list_alerts;
            }
        }

        public IList<Position> PositionsCreated
        {
            get
            {
                return Positions;
            }
        }

        public IList<Alert> AlertssCreated
        {
            get
            {
                return Alerts;
            }
        }

        //НАЧАЛО СКРИПТА WL - тестовая стратегия с двумя скользящими средними
        protected override void Execute()
        {
            for (int bar = 20; bar < Bars.Count; bar++)
            {
                if (IsLastPositionActive)
                {
                    if (Bars.Close[bar] < SMA.Value(bar, Bars.Low, 10))
                    {
                        SellAtLimit(bar + 1, LastActivePosition, Bars.Close[bar], "");

                        if (bar == Bars.Count - 1)
                        {
                            list_alerts.Add(MyAlerts.AlertAdd("sell", "limit", Bars.Close[bar], 0, "", "", bar, Bars));
                        }
                    }
                }
                else
                {
                    if (Bars.Close[bar] > SMA.Value(bar, Bars.High, 20))
                    {
                        RiskStopLevel = Bars.Close[bar] * 0.9;

                        BuyAtLimit(bar + 1, Bars.Close[bar], "");

                        if (bar == Bars.Count - 1)
                        {
                            list_alerts.Add(MyAlerts.AlertAdd("buy", "limit", Bars.Close[bar], RiskStopLevel, "VP", "", bar, Bars));
                        }
                    }
                }
            }
        }
    }
}