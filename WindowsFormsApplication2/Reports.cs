using System;
using System.Collections.Generic;
using WealthLab;

namespace WindowsFormsApplication
{
    class Reports
    {
        //отправляет портфель открытых позиций
        public static string Positions(List<Dictionary<string, object>> SP, IList<Bars> ib)
        {
            string returned_positions = "";

            //на всякий случай не запускаемся если сегодня неторговый день или мделок нет
            Bars bb = ib[ib.Count - 1];

            if ((SP.Count > 1) &
                (DateTime.Now.ToString("dd.MM.yy") == bb.Date[bb.Count - 1].ToString("dd.MM.yy")))
            {
                string positions = "Алгоритмический профиль fin.MOEX на " + DateTime.Now.ToString("dd.MM.yy")
                                    + " (акция, позиция, дата смены позиции, результат в %): "
                                    + System.Environment.NewLine + System.Environment.NewLine;

                foreach (Bars b in ib)
                {
                    string T = b.Symbol;
                    string LSO = "cash ";
                    DateTime d = new DateTime(1900, 01, 01);
                    string rez = "0";

                    for (int p = SP.Count - 1; p >= 0; p--)
                    {
                        if (T == Convert.ToString(SP[p]["symbol"]))
                        {
                            if (Convert.ToDateTime(SP[p]["exitdate"]) > DateTime.Now)
                            {
                                d = Convert.ToDateTime(SP[p]["entrydate"]);

                                if (Convert.ToString(SP[p]["type"]) == "long") { LSO = "long "; }
                                else if (Convert.ToString(SP[p]["type"]) == "short") { LSO = "short"; }
                            }
                            else { d = Convert.ToDateTime(SP[p]["exitdate"]); }

                            double _rez = Math.Round(Convert.ToDouble(SP[p]["profit_pct"]) * 100, 2);

                            rez = string.Format("{0:0.00}", (double)_rez);

                            if (_rez >= 0) { rez = "+" + rez; }

                            break;
                        }
                    }

                    positions = positions + Symbols.SymbolChange(T) + "    " + LSO + "    "
                                          + Convert.ToString(d.ToString("dd.MM.yy")) + "    "
                                          + rez + System.Environment.NewLine;
                }

                positions = positions + System.Environment.NewLine;

                returned_positions = positions;

                //пишем на всякий случай в файл
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\info\positions.txt");
                file.WriteLine(returned_positions);
                file.Close();
            }

            return returned_positions;
        }

        //собирает маркет профиль для отправки
        public static string MarketProfile(IList<Bars> ib)
        {
            string returned_profile = "";

            //на всякий случай не запускаемся, если данные по последней бумаге не сегодняшние
            Bars bb = ib[ib.Count - 1];

            if (DateTime.Now.ToString("dd.MM.yy") == bb.Date[bb.Count - 1].ToString("dd.MM.yy"))
            {
                IList<Bars> ib_i = new List<Bars>();

                ib_i.Add(DataLoad.PathLoad(@"C:\info\NEVLEZAIi\MICEX.txt"));
                ib_i.Add(DataLoad.PathLoad(@"C:\info\NEVLEZAIi\RTSI.txt"));
                ib_i.Add(DataLoad.PathLoad(@"C:\info\NEVLEZAIi\USDr.txt"));

                string profile = "Обзор MOEX по закрытию на " + DateTime.Now.ToString("dd.MM.yy")
                                    + " (изменения в % за день, неделю, месяц, 90 дней): "
                                    + System.Environment.NewLine + System.Environment.NewLine
                                    + Reports.Profile(ib_i) + System.Environment.NewLine
                                    + Reports.Profile(ib);


                returned_profile = profile;

                //пишем на всякий случай в файл
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\info\profile.txt");
                file.WriteLine(returned_profile);
                file.Close();
            }

            return returned_profile;
        }

        //делает отчёт по изменениям цен на актив
        private static string Profile(IList<Bars> ib)
        {
            string profile = "";
            foreach (Bars b in ib)
            {
                string ch_d = "N/A";
                string ch_w = "N/A";
                string ch_m = "N/A";
                string ch_q = "N/A";

                DateTime dw = b.Date[b.Count - 1].AddDays(-7);
                DateTime dm = b.Date[b.Count - 1].AddMonths(-1);
                DateTime dq = b.Date[b.Count - 1].AddMonths(-3);

                double p_w = 0;
                double p_m = 0;
                double p_q = 0;

                for (int bar = b.Count - 2; bar > 0; bar--)
                {
                    if ((b.Date[bar] <= dw) & (b.Date[bar + 1] > dw))
                    {
                        p_w = b.Close[bar];
                    }

                    if ((b.Date[bar] <= dm) & (b.Date[bar + 1] > dm))
                    {
                        p_m = b.Close[bar];
                    }

                    if ((b.Date[bar] <= dq) & (b.Date[bar + 1] > dq))
                    {
                        p_q = b.Close[bar];
                        break;
                    }
                }

                double pd = Math.Round((b.Close[b.Count - 1] - b.Close[b.Count - 2]) / b.Close[b.Count - 2], 4) * 100;
                double pw = Math.Round((b.Close[b.Count - 1] - p_w) / p_w, 4) * 100;
                double pm = Math.Round((b.Close[b.Count - 1] - p_m) / p_m, 4) * 100;
                double pq = Math.Round((b.Close[b.Count - 1] - p_q) / p_q, 4) * 100;

                ch_d = string.Format("{0:0.00}", (double)pd);
                ch_w = string.Format("{0:0.00}", (double)pw);
                ch_m = string.Format("{0:0.00}", (double)pm);
                ch_q = string.Format("{0:0.00}", (double)pq);

                if (pd >= 0) { ch_d = "+" + ch_d; }
                if (pw >= 0) { ch_w = "+" + ch_w; }
                if (pm >= 0) { ch_m = "+" + ch_m; }
                if (pq >= 0) { ch_q = "+" + ch_q; }

                profile = profile + Symbols.SymbolChange(b.Symbol) + "   " 
                            + ch_d + "   "
                            + ch_w + "   "
                            + ch_m + "   " 
                            + ch_q + System.Environment.NewLine;
            }

            return profile;
        }

        //подсчет статистики сделок за период
        public static string Period(int mns, DateTime last_date, List<Dictionary<string, object>> SP, IList<Bars> ib)
        {
            string returned_report = "";

            //не делаем отчёт если нет сделок или если он не квартальный и не месячный или сегодня нет торгов
            Bars bb = ib[ib.Count - 1];

            if ((SP.Count > 1) & 
                ((mns == 1) || (mns == 3)) &
                (DateTime.Now.ToString("dd.MM.yy") == bb.Date[bb.Count - 1].ToString("dd.MM.yy")))
            {
                string report = "";
                double av_rez = 0;
                string string_av_rez = "";
                string period_name = "";

                DateTime first_date = new DateTime(last_date.Year, last_date.Month, 01).AddMonths(-mns + 1);

                if (mns == 1) { period_name = "#месяц - " + Convert.ToString(last_date.ToString("MMMM yyyy")) + "го"; }
                else if (mns == 3) { period_name = "#квартал - "+ Convert.ToString(first_date.ToString("MMMM")) + "-"
                                                    + Convert.ToString(last_date.ToString("MMMM yyyy")) + "го"; }

                report = "Алгоритмический профиль fin.MOEX за " + period_name
                           + " (акция, закрыто позиций, результат в %): " + System.Environment.NewLine + System.Environment.NewLine;

                foreach (Bars b in ib)
                {
                    string T = b.Symbol;
                    int count = 0;
                    string string_count = "";
                    double rez = 0;
                    string string_rez = "";

                    for (int p = SP.Count - 1; p >= 0; p--)
                    {
                        if (T == Convert.ToString(SP[p]["symbol"]))
                        {
                            if ((Convert.ToDateTime(SP[p]["exitdate"]) >= first_date) & (Convert.ToDateTime(SP[p]["exitdate"]) <= last_date))
                            {
                                rez = rez + Math.Round(Convert.ToDouble(SP[p]["profit_pct"]) * 100, 2);
                                count = count + 1;
                            }
                        }
                    }

                    string_rez = string.Format("{0:0.00}", (double)rez);

                    string_count = Convert.ToString(count);

                    if (rez >= 0) { string_rez = "+" + string_rez; }

                    av_rez = av_rez + rez;

                    report = report + Symbols.SymbolChange(T) + "     " 
                            + string_count + "     " + string_rez + System.Environment.NewLine;
                }

                av_rez = Math.Round(av_rez / ib.Count, 2);

                string_av_rez = string.Format("{0:0.00}", (double)av_rez);

                if (av_rez >= 0) { string_av_rez = "+" + string_av_rez; }

                report = report + System.Environment.NewLine + "Общий результат по закрытым позициям: " + string_av_rez + "%";

                returned_report = report;

                //пишем на всякий случай в файл
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\info\period" + period_name + ".txt");
                file.WriteLine(returned_report);
                file.Close();
            }

            return returned_report;
        }

        //подсчёт биржевых позиций
        public static string Opened_Positions(List<Dictionary<string, object>> OP)
        {
            string op = "0";

            if (OP.Count > 0)
            {
                op = "позиции на " + DateTime.Now.ToString("dd.MM.yy") + ":" + Environment.NewLine + Environment.NewLine;

                foreach(Dictionary<string, object> O in OP)
                {
                    if(Convert.ToInt64(O["Position"]) != 0)
                    {
                        op = op + Convert.ToString(O["Symbol"]) + "  " + Convert.ToString(O["Position"]) + Environment.NewLine;
                    }
                }

                //пишем на всякий случай в файл
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\info\opositions.txt");
                file.WriteLine(op);
                file.Close();
            }
            else
            {
                op = "открытых позиций на " + DateTime.Now.ToString("dd.MM.yy") + "нет!";
            }

            return op;
        }

        //подсчёт ордеров
        public static string Quick_Orders(List<Dictionary<string, object>> A)
        {
            string command = "";

            int c = 0;

            if (A.Count > 0)
            {
                //собираем ордера для отправки без нулевого сайза
                foreach (Dictionary<string, object> a in A)
                {
                    if (Convert.ToInt32(a["size"]) > 0)
                    {
                        c++;

                        string order = "";
                        string type = "";

                        if ((Convert.ToString(a["order"]) == "buy") || (Convert.ToString(a["order"]) == "cover")) { order = "B"; }
                        else if ((Convert.ToString(a["order"]) == "short") || (Convert.ToString(a["order"]) == "sell")) { order = "S"; }
                        else { order = "!!!"; }

                        if (Convert.ToString(a["type"]) == "market") { type = "M"; }
                        else if (Convert.ToString(a["type"]) == "stop") { type = "S"; }
                        else if (Convert.ToString(a["type"]) == "limit") { type = "L"; }
                        else if (Convert.ToString(a["type"]) == "close") { type = "C"; }
                        else { type = "!!!"; }

                        command += Convert.ToString(c) + "  " 
                                + Convert.ToString(a["ticker"]) + "  "
                                + order + "  " + type + "  "
                                + Convert.ToString(a["size"]) + "  "
                                + Convert.ToString(Math.Round(Convert.ToDouble(a["price"]), 0)) + "  "
                                + Environment.NewLine;
                    }
                }

                //пишем на всякий случай в файл
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\info\orders.txt");
                file.WriteLine(command);
                file.Close();
            }

            return "ордера (" + Convert.ToString(c) + ") на " + DateTime.Now.ToString("dd.MM.yy") + ":" + Environment.NewLine + Environment.NewLine + command;
        }

        //подсчёт сделок за день
        public static string Executed_Trades(List<Dictionary<string, object>> P)
        {
            string trades = "";

            int c = 0;

            if(P.Count > 0)
            {
                foreach(Dictionary<string, object> p in P)
                {
                    DateTime e = Convert.ToDateTime(p["entrydate"]);
                    DateTime x = Convert.ToDateTime(p["exitdate"]);

                    if ((e.Day == DateTime.Now.Day) & (e.Month == DateTime.Now.Month) & (e.Year == DateTime.Now.Year))
                    {
                        c++;

                        trades += Convert.ToString(c) + "  " + Convert.ToString(p["symbol"]) + "  ";

                        if (Convert.ToString(p["type"]) == "long")
                        {
                            trades += "B" + "  ";
                        }
                        else if (Convert.ToString(p["type"]) == "short")
                        {
                            trades += "S" + "  ";
                        }

                        trades += Convert.ToString(Math.Round(Convert.ToDouble(p["entryprice"]), 2)) + Environment.NewLine;

                    }

                    if ((x.Day == DateTime.Now.Day) & (x.Month == DateTime.Now.Month) & (x.Year == DateTime.Now.Year))
                    {
                        c++;

                        trades += Convert.ToString(c) + "  " + Convert.ToString(p["symbol"]) + "  ";

                        if (Convert.ToString(p["type"]) == "long")
                        {
                            trades += "S" + "  ";
                        }
                        else if (Convert.ToString(p["type"]) == "short")
                        {
                            trades += "B" + "  ";
                        }

                        trades += Convert.ToString(Math.Round(Convert.ToDouble(p["exitprice"]), 2)) + Environment.NewLine;
                    }
                }

                //пишем на всякий случай в файл
                System.IO.StreamWriter file = new System.IO.StreamWriter(@"C:\info\trades.txt");
                file.WriteLine(trades);
                file.Close();
            }

            trades = "сделки (" + Convert.ToString(c) + ") за " + DateTime.Now.ToString("dd.MM.yy") + ":" + Environment.NewLine + Environment.NewLine + trades;

            return trades;
        }
    }

    class Symbols
    {
        //замена названий бумаг для отчётов
        public static string SymbolChange(string s)
        {
            string _s = s;

            if (_s == "SBERP") { _s = "SBRP"; }
            if (_s == "SNGSP") { _s = "SNGP"; }
            if (_s == "TRNFP") { _s = "TRNP"; }
            if (_s == "MICEX") { _s = "MEXi"; }
            if (_s == "RTSI") { _s = "RTSi"; }

            return _s;
        }
    }
}
