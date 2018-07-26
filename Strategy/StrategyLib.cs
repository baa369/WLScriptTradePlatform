using System;
using System.Collections.Generic;
using System.IO;
using WealthLab;
using WealthLab.Indicators;
using WindowsFormsApplication;

namespace Strategy
{
    public class MyAlerts
    {
        public static Dictionary<string, object> AlertAdd(string order, string type, double price, double risk, string name, string position_entry_signal, int bar, Bars Bars)
        {
            Dictionary<string, object> alert = new Dictionary<string, object>();

            alert["order"] = order;
            alert["type"] = type;
            alert["price"] = price / 1000;
            alert["risk"] = risk / 1000;
            alert["name"] = name;
            alert["position"] = position_entry_signal;

            alert["size"] = 0;
            alert["ticker"] = Bars.Symbol;
            alert["time"] = Bars.Date[bar];
            alert["lastprice"] = Bars.Close[bar] / 1000;

            return alert;
        }
    }

    public class MyPositions
    {
        public static Dictionary<string, object> PositionAdd(Position p)
        {
            Dictionary<string, object> position = new Dictionary<string, object>();

            position["symbol"] = p.Bars.Symbol;
            position["size"] = 0;
            position["entrydate"] = p.EntryDate;
            position["entryprice"] = p.EntryPrice / 1000;
            position["exitdate"] = p.ExitDate;
            position["exitprice"] = p.ExitPrice / 1000;
            position["entrysignal"] = p.EntrySignal;
            position["exitsignal"] = p.ExitSignal;
            position["risk"] = p.RiskStopLevel / 1000;
            position["profit_net"] = 0;

            if (p.Active)
            {
                position["active"] = true;
                position["exitprice"] = p.Bars.Close[p.Bars.Count - 1] / 1000;
                DateTime d = new DateTime(3456, 02, 01);
                position["exitdate"] = d;
            }
            else { position["active"] = false; }

            if (p.PositionType == PositionType.Long)
            {
                position["type"] = "long";
                position["profit_pct"] = (Convert.ToDouble(position["exitprice"]) - Convert.ToDouble(position["entryprice"])) / Convert.ToDouble(position["entryprice"]);
            }
            else
            {
                position["type"] = "short";
                position["profit_pct"] = (Convert.ToDouble(position["entryprice"]) - Convert.ToDouble(position["exitprice"])) / Convert.ToDouble(position["entryprice"]);
            }

            return position;           
        }
    }

    public class MyFilter
    {
        public static DataSeries SMAFilter(string path, int len)
        {
            DataSeries f = new DataSeries("");

            Bars bars = new Bars("", BarScale.Daily, 0);

            StreamReader sr = new StreamReader(path);
            while (sr.Peek() >= 0)
            {
                string next_line = sr.ReadLine();
                string[] temp_ar = next_line.Split('	');
                string date = temp_ar[0].Trim();
                string open = temp_ar[1].Trim();
                string high = temp_ar[2].Trim();
                string low = temp_ar[3].Trim();
                string close = temp_ar[4].Trim();
                string volume = temp_ar[5].Trim();

                if ((Convert.ToDouble(open) > 0) & (Convert.ToDouble(high) > 0) & (Convert.ToDouble(low) > 0) & (Convert.ToDouble(close) > 0))
                { bars.Add(Convert.ToDateTime(date), Convert.ToDouble(open), Convert.ToDouble(high), Convert.ToDouble(low), Convert.ToDouble(close), Convert.ToDouble(volume)); }
            }

            sr.Close();

            for (int b = len; b < bars.Count; b++)
            {
                if (bars.Close[b] > SMA.Value(b, bars.Close, len)) { f.Add(0, bars.Date[b]); }
                else { f.Add(1, bars.Date[b]); }
            }

            return f;
        }
    }

    public class MyMM
    {
        public static double ChangeCap(double c, string name)
        {
            //if ((name == "VP") || (name == "L1") || (name == "L2")) { c = c / 2; }
  
            return c;
        }

        public static double ChangeRisk(double r, string name)
        {
            //if (name == "S2") { r = r * 2; }

            return r;
        }
    }
}
