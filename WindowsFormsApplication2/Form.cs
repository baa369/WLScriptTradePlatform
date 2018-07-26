using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using WealthLab;
using WealthLab.Indicators;
using WindowsFormsApplication;
using WLScriptTest;
using TLSharp.Core;
using Telegram.Bot;
using NLog;

namespace Test
{
    public partial class MainForm : Form
    {
        //TelegramClient client;

        ////Telegram
        //int ipID = 118723;
        //string ipHash = "1";
        //string botToken = "1";
        //string chanel = "1";

        ////Facebook
        //string AccessToken = "1";

        //путь к файлу с котировками
        string QuikName = @"C:\";

        // Флаг основного потока программы при обмене сообщениями
        bool Run = true;

        // Форма для подробного отчёта 
        Form1 BigRepForm = new Form1();

        //статус, состояние программы, имя стратегии
        int ProgrammStatus;
        string StrName = "VP_ZRLS_CND_MBS";

        //еквити сделок, котировки, алерты, позиции, суммарную позицию
        DataSeries equity;
        IList<Bars> ibars = new List<Bars>();
        public List<Dictionary<string, object>> salerts = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> spositions = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> mpositions = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> opositions = new List<Dictionary<string, object>>();

        //сохранение настроек программы
        WindowsFormsApplication.Properties.Settings ps = new WindowsFormsApplication.Properties.Settings();
        
        // Выделенная именованная память
        public MemoryMappedFile MemoryCSharpCommand;
        StreamReader SR_CSharpCommand; // Объект для чтения из памяти
        StreamWriter SW_CSharpCommand; // Объект для записи в память

        // Создаст, или подключится к уже созданной памяти с таким именем
        public MemoryMappedFile MemoryQUIKCommand;
        StreamReader SR_QUIKCommand; // Создает поток для чтения
        StreamWriter SW_QUIKCommand; // Создает поток для записи

        //Логирование рабботы программы
        static Logger _Logger = LogManager.GetLogger("Logger");

        //Функция обмена командами C# и QLUA
        private void Commands()
        {
            string Msg = "";

            // Цикл работает пока Run == true
            while (Run == true)
            {
                // Встает в начало потока для чтения
                // Считывает данные из потока памяти, обрезая ненужные байты
                SR_CSharpCommand.BaseStream.Seek(0, SeekOrigin.Begin);
                Msg = SR_CSharpCommand.ReadToEnd().Trim('\0', '\r', '\n');

                // Если программа в статусе отсыла заявок и в потоке от LUA есть нужное сообщение
                if ((Msg != "") & (ProgrammStatus == 3))
                {
                    if (Msg == StrName) //запрос на ордера
                    {
                        BeginInvoke(new TB(AppText), "orders' request received");

                        if (CheckDates() == true)
                        {
                            //в потоке обновляет стратегию, отправляет ордера в LUA, отрисовывает применение стратегии
                            Invoke(new Action(() =>
                            {
                                Execute_Strategy();

                                Orders_ToQuik();

                                Show_Positions_Execute();

                                //если надо шлём позции в файл и соцсети
                                if (checkBox3.Checked == true) { Send_Positions(); };
                            }));
                        }
                    }

                    if (Msg == "PROFILE") //запрос на профиль рынка
                    {
                        BeginInvoke(new TB(AppText), "profile' request received");

                        Invoke(new Action(() =>
                        {
                            Execute_Strategy();

                            Show_Positions_Execute();

                            //шлём отчёт по дню
                            if (checkBox3.Checked == true) { Send_Profile(); }
                        }));
                    }

                    // Встает в начало потока для записи
                    SW_CSharpCommand.BaseStream.Seek(0, SeekOrigin.Begin);
                    // Очищает память, заполняя "нулевыми байтами"
                    for (int i = 0; i < 3000; i++) { SW_CSharpCommand.Write("\0"); }
                    // Очищает все буферы для SW_Memory и вызывает запись всех данных буфера в основной поток
                    SW_CSharpCommand.Flush();
                }

                //Пауза в 0.01 секунду
                Thread.Sleep(10);
            }

            // По завершению цикла, закрывает все потоки и освобождает именованную память
            SR_CSharpCommand.Close();
            SW_CSharpCommand.Close();
            MemoryCSharpCommand.Dispose();

            SR_QUIKCommand.Close(); 
            SW_QUIKCommand.Close();
            MemoryQUIKCommand.Dispose();
        }

        //Загрузка файлов с котировками
        private void IBars_Load(string[] fnames)
        {
            ibars.Clear();
            salerts.Clear();
            spositions.Clear();
            opositions.Clear();

            BigRepForm.Dispose();

            textBox3.Text =
            textBox4.Text =
            textBox5.Text =
            textBox6.Text =
            textBox7.Text =
            textBox8.Text =
            textBox9.Text = "";
            
            dataGridView1.Rows.Clear();
            dataGridView2.Rows.Clear();
            dataGridView3.Rows.Clear();

            chart1.Series[0].Points.Clear();

            foreach (String file in fnames)
            {
                string name = file.Substring(file.Length - 9, 5).Replace(@"\", "");

                Bars bars = new Bars(name, BarScale.Daily, 0);

                StreamReader sr = new StreamReader(file);
                try
                {
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
                        
                        //не пишем бары с нулевыми ценами
                        if ((Convert.ToDouble(open) > 0) & (Convert.ToDouble(high) > 0) & (Convert.ToDouble(low) > 0) & (Convert.ToDouble(close) > 0))
                        { bars.Add(Convert.ToDateTime(date), Convert.ToDouble(open), Convert.ToDouble(high), Convert.ToDouble(low), Convert.ToDouble(close), Convert.ToDouble(volume)); }
                    }
                    ibars.Add(bars);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Loading Symbols/Data file error", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                sr.Close();
            }

            //выводим лог
            AppText("bars loaded");
        }

        //Проверка активности кнопки применения стратегии
        private void Butt2_ON_bool()
        {
            if ((textBox10.ForeColor == Color.Black)
                & (maskedTextBox2.ForeColor == Color.Black)
                & (textBox11.ForeColor == Color.Black)
                & (textBox12.ForeColor == Color.Black)
                & (textBox13.ForeColor == Color.Black)
                & (maskedTextBox6.ForeColor == Color.Black)
                & (ibars.Count > 0))
            {
                button2.Enabled = true;
            }
        }
        
        //Проверка дат перед запуском стратегии
        private bool CheckDates()
        {
            DateTime leap_date;
            DateTime fpos_date;
            DateTime fb_date = new DateTime(9999, 12, 01);

            fb_date = ibars[0].Date[0]; 

            for (int bc = 0; bc < ibars.Count; bc++)
            {
                if (fb_date < ibars[bc].Date[0]) { fb_date = ibars[bc].Date[0]; }
            }

            if ((DateTime.TryParse(maskedTextBox2.Text, out leap_date) == false)
                || (DateTime.TryParse(maskedTextBox6.Text, out fpos_date) == false)
                || (leap_date <= fb_date) || (fpos_date < leap_date)
                || (leap_date > DateTime.Now) || (fpos_date > DateTime.Now))
            {
                maskedTextBox2.ForeColor = Color.Red;
                maskedTextBox6.ForeColor = Color.Red;
                return false;
            }
            else
            {
                maskedTextBox2.ForeColor = Color.Black;
                maskedTextBox6.ForeColor = Color.Black;
                return true;
            }
        }

        //Применение стратегии
        private void Execute_Strategy()
        {
            DateTime firstbar_date = Convert.ToDateTime(maskedTextBox2.Text);
            DateTime firstpos_date = Convert.ToDateTime(maskedTextBox6.Text);

            equity = new DataSeries("");

            //обновляем котировки
            if (ps.Symbols.Count() > 0)
            {
                string[] files = ps.Symbols.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Invoke(new Action(() => { IBars_Load(files); }));
            }

            //отрезаем нужные бары, корректируем для округления, применяем скрипт, достём позиции и алерты
            for (int bc = 0; bc < ibars.Count; bc++)
            {
                Bars ibars_cut = new Bars(ibars[bc].Symbol, BarScale.Daily, 0);

                for (int b = 0; b < ibars[bc].Count; b++)
                {
                    if (ibars[bc].Date[b] >= firstbar_date)
                    {
                        ibars_cut.Add(ibars[bc].Date[b], 
                                      ibars[bc].Open[b] * 1000, 
                                      ibars[bc].High[b] * 1000, 
                                      ibars[bc].Low[b] * 1000, 
                                      ibars[bc].Close[b] * 1000, 
                                      ibars[bc].Volume[b]);
                    }
                }

                GetWLScriptData DoScript = new GetWLScriptData();

                DoScript.Setup();
                DoScript.Execute_WealthScript(ibars_cut);

                foreach (Dictionary<string, object> a in DoScript.AlertsCreated) { salerts.Add(a); }
                foreach (Dictionary<string, object> p in DoScript.PositionsCreated)
                {
                    if (Convert.ToDateTime(p["entrydate"]) > firstpos_date) { spositions.Add(p); }
                }
            }

            //выводим лог сo временем
            AppText("script applied");

            //cоздаём еквити
            spositions = spositions.OrderBy(o => o["entrydate"]).ToList();

            DateTime[] equitydates = new DateTime[spositions.Count * 2];

            int d = 0;
            foreach (Dictionary<string, object> p in spositions)
            {
                equitydates[d] = Convert.ToDateTime(p["entrydate"]);

                if (Convert.ToBoolean(p["active"])) { equitydates[d + 1] = Convert.ToDateTime(p["entrydate"]); }
                else { equitydates[d + 1] = Convert.ToDateTime(p["exitdate"]); ; }

                d = d + 2;
            }

            Array.Sort(equitydates);
            equitydates = equitydates.Distinct().ToArray();

            foreach (DateTime dates in equitydates) { equity.Add(0, dates); }

            //считаем еквити, применяем мм к позициям
            if (equity.Count > 0)
            {
                for (int bar = 0; bar < equity.Count; bar++)
                {
                    double rez = 0;

                    if (bar == 0) { equity[0] = Convert.ToDouble(textBox10.Text); }
                    else { equity[bar] = equity[bar - 1]; }

                    foreach (Dictionary<string, object> p in spositions)
                    {
                        DateTime entrydate = Convert.ToDateTime(p["entrydate"]);
                        DateTime exitdate = Convert.ToDateTime(p["exitdate"]);
                        string positiontype = Convert.ToString(p["type"]);
                        string entrysignal = Convert.ToString(p["entrysignal"]);
                        double entryprice = Convert.ToDouble(p["entryprice"]);
                        double riskstop = Convert.ToDouble(p["risk"]);
                        double longcappart = equity[bar] / ibars.Count * Convert.ToDouble(textBox12.Text);
                        double shortcappart = equity[bar] / ibars.Count * Convert.ToDouble(textBox13.Text);
                        double positionrisk = Convert.ToDouble(textBox11.Text);
                        double positionprofit_pct = Convert.ToDouble(p["profit_pct"]);

                        if (entrydate == equity.Date[bar])
                        {
                            if (positiontype == "long")
                            { p["size"] = PositionSizeSet.RiskPerCap(entryprice, riskstop, longcappart, positionrisk, entrysignal); }
                            else { p["size"] = PositionSizeSet.RiskPerCap(entryprice, riskstop, shortcappart, positionrisk, entrysignal); }
                        }

                        p["profit_net"] = positionprofit_pct * entryprice * Convert.ToDouble(p["size"]);

                        if (exitdate == equity.Date[bar]) { rez = rez + Convert.ToDouble(p["profit_net"]); }
                    }

                    equity[bar] = equity[bar] + rez;
                }
            }

            //выводим лог сo временем
            AppText("equity created");

            //применяем мм к алертам, проверяем попадание цен ордеров в диапазоны
            foreach (Dictionary<string, object> a in salerts)
            {
                Bars bars = ibars[0];
                foreach (Bars b in ibars) 
                { 
                    if (b.Symbol == Convert.ToString(a["ticker"])) { bars = b; }
                }

                string alerttype = Convert.ToString(a["type"]);
                string alertorder = Convert.ToString(a["order"]);
                double alertlastprice = Convert.ToDouble(a["lastprice"]);
                double alertprice = Convert.ToDouble(a["price"]);

                if (alerttype == "close") { a["price"] = alertlastprice; }
                else if (alerttype == "market") { a["price"] = bars.Open[bars.Count - 1]; }
                else if (alerttype == "stop")
                {
                    if ((alertorder == "buy") || (alertorder == "cover"))
                    {
                        if (alertprice < bars.Open[bars.Count - 1])
                        {
                            a["price"] = bars.Open[bars.Count - 1];
                            a["type"] = "limit";
                        }
                    }
                    else if ((alertorder == "sell") || (alertorder == "short"))
                    {
                        if (alertprice > bars.Open[bars.Count - 1])
                        {
                            a["price"] = bars.Open[bars.Count - 1];
                            a["type"] = "limit";
                        }
                    }
                }
                else if (alerttype == "limit")
                {
                    if ((alertorder == "buy") || (alertorder == "cover"))
                    {
                        if (alertprice > bars.Open[bars.Count - 1]) { a["price"] = bars.Open[bars.Count - 1]; }
                    }
                    else if ((alertorder == "sell") || (alertorder == "short"))
                    {
                        if (alertprice < bars.Open[bars.Count - 1]) { a["price"] = bars.Open[bars.Count - 1]; }
                    }
                }

                double eq;
                if (equity.Count > 2) { eq = equity[equity.Count - 2]; }
                else  { eq = equity[0]; }

                double entryprice = Convert.ToDouble(a["price"]);
                double riskstop = Convert.ToDouble(a["risk"]);
                double longcappart = eq / ibars.Count * Convert.ToDouble(textBox12.Text);
                double shortcappart = eq / ibars.Count * Convert.ToDouble(textBox13.Text);
                double positionrisk = Convert.ToDouble(textBox11.Text);
                string entryname = Convert.ToString(a["name"]);

                if (alertorder == "buy")
                {
                    a["size"] = PositionSizeSet.RiskPerCap(entryprice, riskstop, longcappart, positionrisk, entryname);
                }
            
                else if (alertorder == "short")
                {
                    a["size"] = PositionSizeSet.RiskPerCap(entryprice, riskstop, shortcappart, positionrisk, entryname);
                }

                if ((alertorder == "sell") || (alertorder == "cover"))
                {
                    foreach (Dictionary<string, object> p in spositions)
                    {
                        bool positionactive = Convert.ToBoolean(p["active"]);
                        string positionsymbol = Convert.ToString(p["symbol"]);
                        string alertticker = Convert.ToString(a["ticker"]);
                        string positionentrysignal = Convert.ToString(p["entrysignal"]);
                        string alertposition = Convert.ToString(a["position"]);
                        DateTime positoinexitdate = Convert.ToDateTime(p["exitdate"]);

                        if ((positionactive 
                            & (positionsymbol == alertticker)
                            & (positionentrysignal == alertposition))
                            || ((positoinexitdate == bars.Date[bars.Count - 1])
                            & (positionsymbol == alertticker)
                            & (positionentrysignal == alertposition)))
                        { a["size"] = p["size"]; }
                    }
                }
            }

            //выводим лог сo временем
            AppText("strategy applied");
        }

        //Отрисовка рзультатов применения стратегии 
        private void Show_Positions_Execute()
        {
            List<Dictionary<string, object>> SA = salerts;
            List<Dictionary<string, object>> SP = spositions;
            DataSeries eq = equity;
            IList<Bars> ib = ibars;

            dataGridView1.Rows.Clear();
            dataGridView2.Rows.Clear();
            dataGridView3.Rows.Clear();

            BigRepForm.Dispose(); 

            textBox3.Text = 
            textBox4.Text = 
            textBox5.Text = 
            textBox6.Text = 
            textBox7.Text = 
            textBox8.Text = 
            textBox9.Text = "";

            chart1.Series[0].Points.Clear();
            
            double L = 0; double S = 0; double Ps = 0;

            //выводим позиции в таблицу
            if (SP.Count > 0)
            {
                opositions.Clear();

                foreach (Bars b in ib)
                {
                    Dictionary<string, object> pos = new Dictionary<string, object>();

                    pos["Symbol"] = b.Symbol;
                    pos["Position"] = 0;
                    pos["Price"] = 0;

                    opositions.Add(pos);
                }

                int c = 0;
                foreach (Dictionary<string, object> p in SP)
                {
                    dataGridView1.Rows.Add();
                    dataGridView1.Rows[c].Cells[0].Value = c + 1;
                    dataGridView1.Rows[c].Cells[1].Value = p["type"];
                    dataGridView1.Rows[c].Cells[2].Value = p["symbol"];
                    dataGridView1.Rows[c].Cells[3].Value = p["size"];
                    dataGridView1.Rows[c].Cells[4].Value = p["entrydate"];
                    dataGridView1.Rows[c].Cells[5].Value = Math.Round(Convert.ToDouble(p["entryprice"]), 5);

                    dataGridView1.Rows[c].Cells[6].Value = p["exitdate"];
                    dataGridView1.Rows[c].Cells[7].Value = Math.Round(Convert.ToDouble(p["exitprice"]), 5);
                    dataGridView1.Rows[c].Cells[8].Value = Math.Round(Convert.ToDouble(p["profit_pct"]) * 100, 2);
                    dataGridView1.Rows[c].Cells[9].Value = Math.Round(Convert.ToDouble(p["profit_net"]), 2);

                    dataGridView1.Rows[c].Cells[10].Value = p["entrysignal"];
                    dataGridView1.Rows[c].Cells[11].Value = p["exitsignal"];
                    dataGridView1.Rows[c].Cells[12].Value = Math.Round(Convert.ToDouble(p["risk"]), 5);

                    if (Convert.ToBoolean(p["active"]) == true)
                    {
                        double size = Convert.ToDouble(p["size"]);
                        double entryprice = Convert.ToDouble(p["entryprice"]);
                        double exitprice = Convert.ToDouble(p["exitprice"]);
                        double profit_pct = Convert.ToDouble(p["profit_pct"]);

                        if (Convert.ToString(p["type"]) == "long")
                        {
                            L = L + exitprice * size;

                            for(int o = 0; o < opositions.Count; o++)
                            {
                                if (Convert.ToString(opositions[o]["Symbol"]) == Convert.ToString(p["symbol"]))
                                {
                                    opositions[o]["Position"] = Convert.ToInt64(opositions[o]["Position"]) + size;
                                    opositions[o]["Price"] = exitprice;
                                }
                            }
                        }
                        else
                        {
                            S = S + exitprice * size;

                            for (int o = 0; o < opositions.Count; o++)
                            {

                                if (Convert.ToString(opositions[o]["Symbol"]) == Convert.ToString(p["symbol"]))
                                {
                                    opositions[o]["Position"] = Convert.ToUInt16(opositions[o]["Position"]) - size;
                                    opositions[o]["Price"] = exitprice;
                                }
                            }
                        }

                        Ps = Ps + profit_pct * entryprice * size;
                    }
                    c++;
                }

                dataGridView1.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                dataGridView1.CurrentCell = dataGridView1[0, c - 1];
            }

            //выводим суммарные позиции в твблицу
            if(opositions.Count > 0)
            {
                int c = 0;
                foreach(Dictionary<string, object> o in opositions)
                {
                    if (Convert.ToInt64(o["Position"]) != 0)
                    {
                        dataGridView3.Rows.Add();
                        dataGridView3.Rows[c].Cells[0].Value = o["Symbol"];
                        dataGridView3.Rows[c].Cells[1].Value = o["Position"];
                        dataGridView3.Rows[c].Cells[2].Value = Math.Round(Convert.ToDouble(o["Position"]) * Convert.ToDouble(o["Price"]), 0);
                        c++;
                    }
                }

                dataGridView3.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                dataGridView3.CurrentCell = dataGridView3[0, c - 1];
            }

            //выводим алерты в таблицу
            if (SA.Count > 0)
            {
                int c = 0;
                foreach (Dictionary<string, object> a in SA)
                {
                    dataGridView2.Rows.Add();
                    dataGridView2.Rows[c].Cells[0].Value = c + 1;
                    dataGridView2.Rows[c].Cells[1].Value = a["ticker"];
                    dataGridView2.Rows[c].Cells[2].Value = a["order"];
                    dataGridView2.Rows[c].Cells[3].Value = a["type"];
                    dataGridView2.Rows[c].Cells[4].Value = a["size"];
                    dataGridView2.Rows[c].Cells[5].Value = Math.Round(Convert.ToDouble(a["price"]), 5);
                    dataGridView2.Rows[c].Cells[6].Value = a["time"];
                    dataGridView2.Rows[c].Cells[7].Value = a["lastprice"];
                    dataGridView2.Rows[c].Cells[8].Value = a["name"];
                    dataGridView2.Rows[c].Cells[9].Value = a["position"];
                    c++;
                }
                
                dataGridView2.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                dataGridView2.CurrentCell = dataGridView2[0, c - 1];
            }

            //выводим статистику о сделках, график
            if (eq.Count > 0)
            {
                textBox3.Text = Convert.ToString(Math.Round(eq[eq.Count - 1] - eq[0], 2));
                textBox4.Text = Convert.ToString(Math.Round(Ps, 2));
                textBox5.Text = Convert.ToString(Math.Round(L + S, 2));
                textBox6.Text = Convert.ToString(Math.Round(L, 2));
                textBox7.Text = Convert.ToString(Math.Round(S, 2));

                chart1.Series["DD"].XValueType = System.Windows.Forms.DataVisualization.Charting.ChartValueType.Date;

                DataSeries dd = new DataSeries("");

                for (int bar = 0; bar < eq.Count; bar++)
                {
                    if ((eq[bar] == Highest.Value(bar, eq, bar)) || (bar == 0)) { dd.Add(0, eq.Date[bar]); }
                    else { dd.Add((eq[bar] - Highest.Value(bar, eq, bar)) / Highest.Value(bar, eq, bar) * 100, eq.Date[bar]); }

                    chart1.Series["DD"].Points.AddXY(dd.Date[bar], dd[bar]);
                }

                textBox8.Text = Convert.ToString(-Math.Round(dd[dd.Count - 1], 2));
                textBox9.Text = Convert.ToString(-Math.Round(Lowest.Value(dd.Count - 1, dd, dd.Count - 1), 2));
            }

            //если надо выводим подробный отчёт
            if (checkBox1.Checked == true) { Show_Big_Report(); };

            //выводим лог сo временем
            AppText("tables updated");
         }

        //вывод большого отчёта
        private void Show_Big_Report()
        {
            List<Dictionary<string, object>> SA = salerts;
            List<Dictionary<string, object>> SP = spositions;
            DataSeries eq = equity;
            IList<Bars> ib = ibars;

            BigRepForm = new Form1();
            BigRepForm.Show();

            if (eq.Count > 0)
            {
                //еквити, DD и дни в DD
                DataSeries EQL = new DataSeries(""); 
                DataSeries EQS = new DataSeries(""); 
                DataSeries BnH = new DataSeries(""); 
                DataSeries CSH = new DataSeries("");
                DataSeries dd = new DataSeries("");
                DataSeries ddl = new DataSeries(""); 
                DataSeries dds = new DataSeries(""); 
                DataSeries ddbh = new DataSeries("");
                DataSeries ddd = new DataSeries("");

                int flag = 0;
                DateTime d = eq.Date[0];
                
                BnH.Add(eq[0], eq.Date[0]);
                int[] b0 = new int[ib.Count];
                for (int i = 0; i < ibars.Count; i++) { b0[0] = 0; }

                for (int bar = 0; bar < eq.Count; bar++)
                {
                    //считаем еквити B&H 
                    double bhk = 0;

                    if (bar == 0)
                    {
                        for (int i = 0; i < ibars.Count; i++)
                            for (int b = 0; b < ib[i].Count; b++)
                                if (ib[i].Date[b] > eq.Date[bar]) { b0[i] = b; break; }
                    }
                    else
                    {
                        for (int i = 0; i < ibars.Count; i++)
                            for (int b = b0[i]; b < ib[i].Count; b++)
                                if ((eq.Date[bar - 1] < ib[i].Date[b]) & (eq.Date[bar] >= ib[i].Date[b]))
                                {
                                    bhk += (ib[i].Close[b] - ib[i].Close[b0[i]]) / ib[i].Close[b0[i]];
                                    break;
                                }
   
                        bhk /= ib.Count;

                        BnH.Add(eq[0] * (1 + bhk), eq.Date[bar]);
                    }

                    //считаем CASH, еквити long и short
                    double cash = 0;
                    double rezl = 0;
                    double rezs = 0;

                    if (bar == 0) { EQL.Add(eq[0], eq.Date[0]); EQS.Add(eq[0], eq.Date[0]); }
                    else { EQL.Add(EQL[bar - 1], eq.Date[bar - 1]); EQS.Add(EQS[bar - 1], eq.Date[bar - 1]); }

                    foreach (Dictionary<string, object> p in spositions)
                    {
                        DateTime entrydate = Convert.ToDateTime(p["entrydate"]);
                        DateTime exitdate = Convert.ToDateTime(p["exitdate"]);
                        double entryprice = Convert.ToDouble(p["entryprice"]);
                        double exitprice = Convert.ToDouble(p["exitprice"]);
                        double size = Convert.ToDouble(p["size"]);
                        string positiontype = Convert.ToString(p["type"]);

                        if ((eq.Date[bar] > entrydate) & (eq.Date[bar] <= exitdate))
                        { cash += entryprice * size; }

                        if (eq.Date[bar] == exitdate)
                            if (positiontype == "long") { rezl += (exitprice - entryprice) * size; }
                            else if (positiontype == "short") { rezs += (entryprice - exitprice) * size; }
                    }

                    CSH.Add(eq[bar] - cash, eq.Date[bar]);
                    
                    EQL[bar] += rezl; EQS[bar] += rezs;

                    //cчитаем DD и дни в DD основной еквити
                    if ((eq[bar] == Highest.Value(bar, eq, bar)) || (bar == 0))
                    {
                        dd.Add(0, eq.Date[bar]);
                        ddd.Add(0, eq.Date[bar]);

                        flag = 0;
                    }
                    else
                    {
                        if (flag == 0) { d = eq.Date[bar - 1]; }

                        dd.Add((eq[bar] - Highest.Value(bar, eq, bar)) / Highest.Value(bar, eq, bar) * 100, eq.Date[bar]);
                        ddd.Add((eq.Date[bar] - d).Days, eq.Date[bar]);

                        flag = 1;
                    }

                    //считаем DD B&H, DD long и DD short
                    if ((BnH[bar] == Highest.Value(bar, BnH, bar)) || (bar == 0)) { ddbh.Add(0, eq.Date[bar]); }
                    else { ddbh.Add((BnH[bar] - Highest.Value(bar, BnH, bar)) / Highest.Value(bar, BnH, bar) * 100, eq.Date[bar]); }

                    if ((EQL[bar] == Highest.Value(bar, EQL, bar)) || (bar == 0)) { ddl.Add(0, eq.Date[bar]); }
                    else { ddl.Add((EQL[bar] - Highest.Value(bar, EQL, bar)) / Highest.Value(bar, EQL, bar) * 100, eq.Date[bar]); }

                    if ((EQS[bar] == Highest.Value(bar, EQS, bar)) || (bar == 0)) { dds.Add(0, eq.Date[bar]); }
                    else { dds.Add((EQS[bar] - Highest.Value(bar, EQS, bar)) / Highest.Value(bar, EQS, bar) * 100, eq.Date[bar]); }

                    //рисуем графики
                    BigRepForm.chart1.Series["EQ"].Points.AddXY(eq.Date[bar], eq[bar]);
                    BigRepForm.chart1.Series["CSH"].Points.AddXY(CSH.Date[bar], CSH[bar]);
                    BigRepForm.chart1.Series["BNH"].Points.AddXY(BnH.Date[bar], BnH[bar]);
                    BigRepForm.chart2.Series["DD"].Points.AddXY(dd.Date[bar], dd[bar]);
                    BigRepForm.chart3.Series["DDD"].Points.AddXY(ddd.Date[bar], ddd[bar]);
                }

                //считаем статистику по сделкам, 0 - all, 1 - long, 2 - short, 3 - b&h
                double[] trades = new double[4]; 
                double[] win_trades = new double[4];
                double[] dd_ = new double[4];
                double[] pf_net = new double[4]; 
                double[] pf_pct = new double[4]; 
                double[] net_pf = new double[4]; 
                double[] an_gain = new double[4];
                double[] days_held = new double[4];
                double[] win_days_held = new double[4];
                double[] loss_days_held = new double[4];
                double[] trade_net = new double[4]; 
                double[] trade_pct = new double[4]; 
                double[] win_trade_net = new double[4]; 
                double[] win_trade_pct = new double[4]; 
                double[] loss_trade_net = new double[4]; 
                double[] loss_trade_pct = new double[4]; 

                int trade_count = 0;
                int plus_trade_count = 0;
                int L_plus_trade_count = 0;
                int L_minus_trade_count = 0;

                double bh = 0;
                double bh_plus = 0;
                double bh_minus = 0;
                double L_bh_plus = 0;
                double L_bh_minus = 0;
                double S_bh_plus = 0;
                double S_bh_minus = 0;

                double cum_profit_plus_pct = 0;
                double cum_profit_minus_pct = 0;
                double L_cum_profit_plus_pct = 0;
                double L_cum_profit_minus_pct = 0;
                double S_cum_profit_plus_pct = 0;
                double S_cum_profit_minus_pct = 0;

                double cum_profit_plus_net = 0;
                double cum_profit_minus_net = 0;
                double L_cum_profit_plus_net = 0;
                double L_cum_profit_minus_net = 0;
                double S_cum_profit_plus_net = 0;
                double S_cum_profit_minus_net = 0;

                foreach (Dictionary<string, object> p in spositions)
                {
                    string positiontype = Convert.ToString(p["type"]);
                    double profit_pct = Convert.ToDouble(p["profit_pct"]);
                    double profit_net = Convert.ToDouble(p["profit_net"]);
                    DateTime entrydate = Convert.ToDateTime(p["entrydate"]);
                    DateTime exitdate = Convert.ToDateTime(p["exitdate"]);
                    bool positionactive = Convert.ToBoolean(p["active"]);

                    trade_count++;

                    if (positionactive) { exitdate = eq.Date[eq.Count - 1]; }

                    bh += (exitdate - entrydate).Days;

                    if (profit_net > 0)
                    {
                        plus_trade_count++;
                        bh_plus += (exitdate - entrydate).Days;
                        cum_profit_plus_pct += profit_pct;
                        cum_profit_plus_net += profit_net;

                        if (positiontype == "long")
                        {
                            L_plus_trade_count++;
                            L_bh_plus += (exitdate - entrydate).Days;
                            L_cum_profit_plus_pct += profit_pct;
                            L_cum_profit_plus_net += profit_net;
                        }
                        else if (positiontype == "short")
                        {
                            S_bh_plus += (exitdate - entrydate).Days;
                            S_cum_profit_plus_pct += profit_pct;
                            S_cum_profit_plus_net += profit_net;
                        }
                    }
                    else
                    {
                        bh_minus += (exitdate - entrydate).Days;
                        cum_profit_minus_pct += profit_pct;
                        cum_profit_minus_net += profit_net; 

                        if (positiontype == "long")
                        {
                            L_minus_trade_count++;
                            L_bh_minus += (exitdate - entrydate).Days;
                            L_cum_profit_minus_pct += profit_pct;
                            L_cum_profit_minus_net += profit_net;
                        }
                        else if (positiontype == "short")
                        {
                            S_bh_minus += (exitdate - entrydate).Days;
                            S_cum_profit_minus_pct += profit_pct;
                            S_cum_profit_minus_net += profit_net;
                        }
                    }
                }

                //собираем и рисуем статистику по сделкам
                for (int r = 0; r < 21; r++) { BigRepForm.dataGridView2.Rows.Add(); }

                BigRepForm.dataGridView2.Rows[1].Cells[0].Value = "Net Profit, %";
                net_pf[0] = (eq[eq.Count - 1] - eq[0]) / eq[0] * 100;
                net_pf[1] = (EQL[EQL.Count - 1] - EQL[0]) / EQL[0] * 100;
                net_pf[2] = (EQS[EQS.Count - 1] - EQS[0]) / EQS[0] * 100;
                net_pf[3] = (BnH[BnH.Count - 1] - BnH[0]) / BnH[0] * 100;

                BigRepForm.dataGridView2.Rows[0].Cells[0].Value = "Annualized Gain, %";
                BigRepForm.dataGridView2.Rows[0].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                double k = Convert.ToDouble((eq.Date[eq.Count - 1] - eq.Date[0]).Days) / 365;
                an_gain[0] = net_pf[0] / k;
                an_gain[1] = net_pf[1] / k;
                an_gain[2] = net_pf[2] / k;
                an_gain[3] = net_pf[3] / k;

                BigRepForm.dataGridView2.Rows[2].Cells[0].Value = "Number of Trades";
                trades[0] = trade_count;
                trades[1] = L_plus_trade_count + L_minus_trade_count;
                trades[2] = trade_count - L_plus_trade_count - L_minus_trade_count;
                trades[3] = 1;

                BigRepForm.dataGridView2.Rows[3].Cells[0].Value = "Average Trade, %";
                BigRepForm.dataGridView2.Rows[3].DefaultCellStyle.BackColor = Color.LightGray;
                BigRepForm.dataGridView2.Rows[3].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                trade_pct[0] = (cum_profit_plus_pct + cum_profit_minus_pct) / trades[0] * 100;
                trade_pct[1] = (L_cum_profit_plus_pct + L_cum_profit_minus_pct) / trades[1] * 100;
                trade_pct[2] = (S_cum_profit_plus_pct + S_cum_profit_minus_pct) / trades[2] * 100;
                trade_pct[3] = (BnH[BnH.Count - 1] - BnH[0]) / BnH[0];

                BigRepForm.dataGridView2.Rows[4].Cells[0].Value = "Average Trade";
                trade_net[0] = (cum_profit_plus_net + cum_profit_minus_net) / trades[0];
                trade_net[1] = (L_cum_profit_plus_net + L_cum_profit_minus_net) / trades[1];
                trade_net[2] = (S_cum_profit_plus_net + S_cum_profit_minus_net) / trades[2];
                trade_net[3] = BnH[BnH.Count - 1] - BnH[0];

                BigRepForm.dataGridView2.Rows[5].Cells[0].Value = "Average Days Held";
                BigRepForm.dataGridView2.Rows[5].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                days_held[0] = bh / trades[0];
                days_held[1] = (L_bh_plus + L_bh_minus) / trades[1];
                days_held[2] = (S_bh_plus + S_bh_minus) / trades[2];
                days_held[3] = (BnH.Date[BnH.Count - 1] - BnH.Date[0]).Days;

                BigRepForm.dataGridView2.Rows[6].Cells[0].Value = "Winning Trades";
                win_trades[0] = plus_trade_count;
                win_trades[1] = L_plus_trade_count;
                win_trades[2] = plus_trade_count - L_plus_trade_count;
                if (BnH[0] > BnH[BnH.Count - 1]) { win_trades[3] = 0; }
                else { win_trades[3] = 1; }

                BigRepForm.dataGridView2.Rows[7].Cells[0].Value = "Win Rate, %";
                BigRepForm.dataGridView2.Rows[7].DefaultCellStyle.BackColor = Color.LightGray;
                BigRepForm.dataGridView2.Rows[7].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);

                BigRepForm.dataGridView2.Rows[8].Cells[0].Value = "Average Win";
                win_trade_net[0] = cum_profit_plus_net / win_trades[0];
                win_trade_net[1] = L_cum_profit_plus_net / win_trades[1];
                win_trade_net[2] = S_cum_profit_plus_net / win_trades[2];
                if (BnH[0] > BnH[BnH.Count - 1]) { win_trade_net[3] = 0; }
                else { win_trade_net[3] = BnH[BnH.Count - 1] - BnH[0]; }

                BigRepForm.dataGridView2.Rows[9].Cells[0].Value = "Average Win, %";
                BigRepForm.dataGridView2.Rows[9].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                win_trade_pct[0] = cum_profit_plus_pct / win_trades[0] * 100;
                win_trade_pct[1] = L_cum_profit_plus_pct / win_trades[1] * 100;
                win_trade_pct[2] = S_cum_profit_plus_pct / win_trades[2] * 100;
                if (BnH[0] > BnH[BnH.Count - 1]) { win_trade_pct[3] = 0; }
                else { win_trade_pct[3] = (BnH[BnH.Count - 1] - BnH[0]) / BnH[0]; }

                BigRepForm.dataGridView2.Rows[10].Cells[0].Value = "Average Win Days Held";
                win_days_held[0] = bh_plus / win_trades[0];
                win_days_held[1] = L_bh_plus / win_trades[1];
                win_days_held[2] = S_bh_plus  / win_trades[2];
                if (BnH[0] > BnH[BnH.Count - 1]) { win_days_held[3] = 0; }
                else { win_days_held[3] = days_held[3]; }

                BigRepForm.dataGridView2.Rows[11].Cells[0].Value = "Average Win / Average Loss";
                BigRepForm.dataGridView2.Rows[11].DefaultCellStyle.BackColor = Color.LightGray;
                BigRepForm.dataGridView2.Rows[11].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);

                BigRepForm.dataGridView2.Rows[12].Cells[0].Value = "Average Win, % / Average Loss, %";
                BigRepForm.dataGridView2.Rows[12].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);

                BigRepForm.dataGridView2.Rows[13].Cells[0].Value = "Losing Trades";

                BigRepForm.dataGridView2.Rows[14].Cells[0].Value = "Loss Rate";

                BigRepForm.dataGridView2.Rows[15].Cells[0].Value = "Average Loss";
                loss_trade_net[0] = cum_profit_minus_net / (trades[0] - win_trades[0]);
                loss_trade_net[1] = L_cum_profit_minus_net / (trades[1] - win_trades[1]);
                loss_trade_net[2] = S_cum_profit_minus_net / (trades[2] - win_trades[2]);
                if (BnH[0] > BnH[BnH.Count - 1]) { loss_trade_net[3] = BnH[BnH.Count - 1] - BnH[0]; }
                else { loss_trade_net[3] = 0; }

                BigRepForm.dataGridView2.Rows[16].Cells[0].Value = "Average Loss, %";
                BigRepForm.dataGridView2.Rows[16].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                loss_trade_pct[0] = cum_profit_minus_pct / (trades[0] - win_trades[0]) * 100;
                loss_trade_pct[1] = L_cum_profit_minus_pct / (trades[1] - win_trades[1]) * 100;
                loss_trade_pct[2] = S_cum_profit_minus_pct / (trades[2] - win_trades[2]) * 100;
                if (BnH[0] > BnH[BnH.Count - 1]) { loss_trade_pct[3] = (BnH[BnH.Count - 1] - BnH[0]) / BnH[0]; }
                else { loss_trade_pct[3] = 0; }

                BigRepForm.dataGridView2.Rows[17].Cells[0].Value = "Average Loss Days Held";
                loss_days_held[0] = bh_minus / (trades[0] - win_trades[0]);
                loss_days_held[1] = L_bh_minus / (trades[1] - win_trades[1]);
                loss_days_held[2] = S_bh_minus / (trades[2] - win_trades[2]);
                if (BnH[0] > BnH[BnH.Count - 1]) { loss_days_held[3] = days_held[3]; }
                else { loss_days_held[3] = 0; }

                BigRepForm.dataGridView2.Rows[18].Cells[0].Value = "Maximum Drawdown, %";
                BigRepForm.dataGridView2.Rows[18].DefaultCellStyle.BackColor = Color.LightGray;
                BigRepForm.dataGridView2.Rows[18].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                dd_[0] = Lowest.Value(dd.Count - 1, dd, dd.Count - 1);
                dd_[1] = Lowest.Value(ddl.Count - 1, ddl, ddl.Count - 1);
                dd_[2] = Lowest.Value(dds.Count - 1, dds, dds.Count - 1);
                dd_[3] = Lowest.Value(ddbh.Count - 1, ddbh, ddbh.Count - 1);

                BigRepForm.dataGridView2.Rows[19].Cells[0].Value = "Ann. Gain / Max. DD";
                BigRepForm.dataGridView2.Rows[19].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);

                BigRepForm.dataGridView2.Rows[20].Cells[0].Value = "Profit Factor";
                BigRepForm.dataGridView2.Rows[20].DefaultCellStyle.BackColor = Color.LightGray;
                BigRepForm.dataGridView2.Rows[20].DefaultCellStyle.Font = new Font(this.Font, FontStyle.Bold);
                pf_net[0] = -cum_profit_plus_net / cum_profit_minus_net;
                pf_net[1] = -L_cum_profit_plus_net / L_cum_profit_minus_net;
                pf_net[2] = -S_cum_profit_plus_net / S_cum_profit_minus_net;
                pf_net[3] = 0;

                BigRepForm.dataGridView2.Rows[21].Cells[0].Value = "Profit Factor, %";
                pf_pct[0] = -cum_profit_plus_pct / cum_profit_minus_pct;
                pf_pct[1] = -L_cum_profit_plus_pct / L_cum_profit_minus_pct;
                pf_pct[2] = -S_cum_profit_plus_pct / S_cum_profit_minus_pct;
                pf_net[3] = 0;

                for (int i = 0; i < 4; i++)
                {
                    BigRepForm.dataGridView2.Rows[0].Cells[i + 1].Value = Convert.ToString(Math.Round(an_gain[i], 2));
                    BigRepForm.dataGridView2.Rows[1].Cells[i + 1].Value = Convert.ToString(Math.Round(net_pf[i], 2));
                    BigRepForm.dataGridView2.Rows[2].Cells[i + 1].Value = Convert.ToString(trades[i]);
                    BigRepForm.dataGridView2.Rows[3].Cells[i + 1].Value = Convert.ToString(Math.Round(trade_pct[i], 2));
                    BigRepForm.dataGridView2.Rows[4].Cells[i + 1].Value = Convert.ToString(Math.Round(trade_net[i], 2));
                    BigRepForm.dataGridView2.Rows[5].Cells[i + 1].Value = Convert.ToString(Math.Round(days_held[i], 2));
                    BigRepForm.dataGridView2.Rows[6].Cells[i + 1].Value = Convert.ToString(win_trades[i]);
                    BigRepForm.dataGridView2.Rows[7].Cells[i + 1].Value = Convert.ToString(Math.Round(win_trades[i] / trades[i] * 100, 2));
                    BigRepForm.dataGridView2.Rows[8].Cells[i + 1].Value = Convert.ToString(Math.Round(win_trade_net[i], 2));
                    BigRepForm.dataGridView2.Rows[9].Cells[i + 1].Value = Convert.ToString(Math.Round(win_trade_pct[i], 2));
                    BigRepForm.dataGridView2.Rows[10].Cells[i + 1].Value = Convert.ToString(Math.Round(win_days_held[i], 2));
                    BigRepForm.dataGridView2.Rows[13].Cells[i + 1].Value = Convert.ToString(trades[i] - win_trades[i]);
                    BigRepForm.dataGridView2.Rows[14].Cells[i + 1].Value = Convert.ToString(Math.Round((trades[i] - win_trades[i]) / trades[i] * 100, 2));
                    BigRepForm.dataGridView2.Rows[15].Cells[i + 1].Value = Convert.ToString(Math.Round(loss_trade_net[i], 2));
                    BigRepForm.dataGridView2.Rows[16].Cells[i + 1].Value = Convert.ToString(Math.Round(loss_trade_pct[i], 2));
                    BigRepForm.dataGridView2.Rows[11].Cells[i + 1].Value = Convert.ToString(Math.Round(- win_trade_net[i] / loss_trade_net[i], 2));
                    BigRepForm.dataGridView2.Rows[12].Cells[i + 1].Value = Convert.ToString(Math.Round(- win_trade_pct[i] / loss_trade_pct[i], 2));
                    BigRepForm.dataGridView2.Rows[17].Cells[i + 1].Value = Convert.ToString(Math.Round(loss_days_held[i], 2));
                    BigRepForm.dataGridView2.Rows[18].Cells[i + 1].Value = Convert.ToString(Math.Round(dd_[i], 2));
                    BigRepForm.dataGridView2.Rows[19].Cells[i + 1].Value = Convert.ToString(Math.Round(- an_gain[i] / dd_[i], 2));
                    BigRepForm.dataGridView2.Rows[20].Cells[i + 1].Value = Convert.ToString(Math.Round(pf_net[i], 2));
                    BigRepForm.dataGridView2.Rows[21].Cells[i + 1].Value = Convert.ToString(Math.Round(pf_pct[i], 2));
                }
            }

            //выводим лог сo временем
            AppText("big report created");
        }

        //Отсылка ордеров в Quik
        private void Orders_ToQuik()
        {
            string command = "orders: " + ";";

            int c = 0;
            
            if (salerts.Count > 0)
            {
                //собираем ордера для отправки без нулевого сайза
                foreach (Dictionary<string, object> a in salerts)
                {
                    if (Convert.ToInt32(a["size"]) > 0)
                    {
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

                        command += Convert.ToString(a["ticker"]) + ","
                                + order + "," + type + "," 
                                + Convert.ToString(a["size"]) + "," 
                                + Convert.ToString(a["price"]) + ","
                                + ";";
                        c++;
                    }
                }
            }

            //отправляем ордера
            SetQUIKCommandData();
            SetQUIKCommandData(command);
            
            //выводим лог сo временем
             AppText("orders sent ("+ Convert.ToString(c) + ")");
        }

        //Отправка позиций и периодических отчётов  
        private void Send_Positions()
        {
            string positions = Reports.Quick_Orders(salerts);

            //var bot = new TelegramBotClient(botToken);
            //var s = bot.SendTextMessageAsync(chanel, positions);

            //выводим лог сo временем
            AppText("positions sent");
        }

        //Отправка профиля рынка
        private void Send_Profile()
        {
            string positions = Reports.Executed_Trades(spositions) + Environment.NewLine + Reports.Opened_Positions(opositions);

            //var bot = new TelegramBotClient(botToken);
            //var s = bot.SendTextMessageAsync(chanel, positions);

            //выводим лог сo временем
            AppText("profile sent");
        }

        //Функция отправки команды в QUIK, или очистки памяти, при вызове без параметров
        private void SetQUIKCommandData(string Data = "")
        {
            if (Data != "") //Если нужно отправить команду
            {
                for (int i = Data.Length; i < 3000; i++) { Data += "\0"; } //Дополняет строку команды "нулевыми байтами" до нужной длины
            }
            else //Если нужно очистить память
            {
                for (int i = 0; i < 3000; i++) { Data += "\0"; } //Заполняет строку для записи "нулевыми байтами"
            }

            SW_QUIKCommand.BaseStream.Seek(0, SeekOrigin.Begin); //Встает в начало
            SW_QUIKCommand.Write(Data); //Записывает строку
            SW_QUIKCommand.Flush(); //Сохраняет изменения в памяти
        }

        // Делегат отрисовки лога
        private delegate void TB(string Msg);
        private void AppText(string Msg)
        {
            textBox1.AppendText(CTime() + " " + Msg + Environment.NewLine);
            _Logger.Info(CTime() + " " + Msg);
        }

        //Вывод времени для логов
        private static string CTime()
        {
            string ct = Convert.ToString(DateTime.Now.Day) + "."
                      + Convert.ToString(DateTime.Now.Month) + "."
                      + Convert.ToString(DateTime.Now.Year) + "  "
                      + Convert.ToString(DateTime.Now.Hour) + ":"
                      + Convert.ToString(DateTime.Now.Minute) + ":"
                      + Convert.ToString(DateTime.Now.Second) + "."
                      + Convert.ToString(DateTime.Now.Millisecond) + "  ";
            
            return ct;
        }

        //Установка статуса программы 
        public void Programm_Status_Set(int x)
        {
            string s = "[" + StrName + "] ";

            if (x == 0)
            {
                ProgrammStatus = 0;
                Text = s + "Programm Status = Symbols/Data not loaded | Strategy not executed | Auto-Trading OFF";
            }
            if (x == 1)
            {
                ProgrammStatus = 1;
                Text = s + "Programm Status = Symbols/Data loaded | Strategy not executed | Auto-Trading OFF";
            }
            if (x == 2)
            {
                ProgrammStatus = 2;
                Text = s + "Programm Status = Symbols/Data loaded | Strategy executed | Auto-Trading OFF";
            }
            if (x == 3)
            {
                ProgrammStatus = 3;
                Text = s + "Programm Status = Symbols/Data loaded | Strategy executed | Auto-Trading ON";

                // Встает в начало потока для записи. Очищает память, заполняя "нулевыми байтами"
                // Очищает все буферы для SW_Memory и вызывает запись всех данных буфера в основной поток
                SW_CSharpCommand.BaseStream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < 3000; i++) { SW_CSharpCommand.Write("\0"); }
                SW_CSharpCommand.Flush();
            }
        }

        //Функции интерфейса
        public MainForm()
        {
            InitializeComponent();

            //сворачивание в трей
            this.notifyIcon1.MouseDoubleClick += new MouseEventHandler(notifyIcon1_MouseDoubleClick);
            this.Resize += new System.EventHandler(this.Form1_Resize);

            //Создаст, или подключится к уже созданной памяти с таким именем, создает потоки для чтения/записи
            MemoryCSharpCommand = MemoryMappedFile.CreateOrOpen("CSharpCommand2", 3000, MemoryMappedFileAccess.ReadWrite);
            SR_CSharpCommand = new StreamReader(MemoryCSharpCommand.CreateViewStream(), System.Text.Encoding.Default);
            SW_CSharpCommand = new StreamWriter(MemoryCSharpCommand.CreateViewStream(), System.Text.Encoding.Default);

            //Выделит именованную память размером 3000 байт для отправки КОМАНД в QUIK, создает потоки чтения/записи
            MemoryQUIKCommand = MemoryMappedFile.CreateOrOpen("QUIKCommand2", 3000, MemoryMappedFileAccess.ReadWrite);
            SR_QUIKCommand = new StreamReader(MemoryQUIKCommand.CreateViewStream());
            SW_QUIKCommand = new StreamWriter(MemoryQUIKCommand.CreateViewStream());
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //коннектим Telegram
            //client = new TelegramClient(ipID, ipHash);
            //client.ConnectAsync();

            this.MaximizeBox = false;

            GetWLScriptData StrategyScript = new GetWLScriptData();

            StrategyScript.Setup();
            StrName = StrategyScript.ScriptName;

            this.notifyIcon1.Text = StrName;

            Programm_Status_Set(0);

            //загрузка заведённых в программу параметров/настроек
            textBox10.Text = Convert.ToString(ps.StCap); 
            textBox11.Text = Convert.ToString(ps.Risk); 
            textBox12.Text = Convert.ToString(ps.LShld); 
            textBox13.Text = Convert.ToString(ps.SShld);
            maskedTextBox2.Text = Convert.ToString(ps.StDate); 
            maskedTextBox6.Text = Convert.ToString(ps.PDate);
            if (ps.BigRep == true) { checkBox1.Checked = true; }
            else { checkBox1.Checked = false; }
            if (ps.SendP == true) { checkBox3.Checked = true; }
            else { checkBox3.Checked = false; }

            if (ps.Symbols.Count() > 0)
            {
                string[] files = ps.Symbols.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                IBars_Load(files);

                string symbols = "";
                for (int i = 0; i < ibars.Count; i++) { symbols = symbols + ibars[i].Symbol + " "; }
                textBox2.Text = symbols;

                Programm_Status_Set(1);

                Butt2_ON_bool();
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            // Запускает функцию обмена командами в отдельном потоке, чтобы форма отвечала на действия пользователя
            new Thread(() => { Commands(); }).Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Выключает флаг
            Run = false;

            //сохранение заведённых в программу параметров/настроек
            if (textBox10.ForeColor == Color.Black) { ps.StCap = Convert.ToDouble(textBox10.Text); }
            if (textBox11.ForeColor == Color.Black) { ps.Risk = Convert.ToDouble(textBox11.Text); }
            if (textBox12.ForeColor == Color.Black) { ps.LShld = Convert.ToDouble(textBox12.Text); }
            if (textBox13.ForeColor == Color.Black) { ps.SShld = Convert.ToDouble(textBox13.Text); }
            if (maskedTextBox2.ForeColor == Color.Black) { ps.StDate = Convert.ToDateTime(maskedTextBox2.Text); }
            if (maskedTextBox6.ForeColor == Color.Black) { ps.PDate = Convert.ToDateTime(maskedTextBox6.Text); }
            if (checkBox1.Checked == true) { ps.BigRep = true; }
            else { ps.BigRep = false; }
            if (checkBox3.Checked == true) { ps.SendP = true; }
            else { ps.SendP = false; }

            ps.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            openFileDialog1.InitialDirectory = QuikName;
            openFileDialog1.Filter = "txt files (*.txt)|*.txt|All Files (*.*)| *.*";
            openFileDialog1.Multiselect = true;
            openFileDialog1.RestoreDirectory = true;
            
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                IBars_Load(openFileDialog1.FileNames);
                
                string files = "";
                foreach (string fn in openFileDialog1.FileNames) { files = files + " " + fn; }
                ps.Symbols = files;
                ps.Save();

                string symbols = "";
                for(int i = 0; i < ibars.Count; i++) { symbols = symbols + " " + ibars[i].Symbol; }
                textBox2.Text = symbols;

                Programm_Status_Set(1);
                
                Butt2_ON_bool();

                button3.Enabled = false;
                button3.Text = "Auto-Trading ON";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (CheckDates() == true)
            {
                Execute_Strategy();

                if (checkBox2.Checked == true)
                {
                    Orders_ToQuik();
                    checkBox2.Checked = false;
                }

                Show_Positions_Execute();

                if (checkBox3.Checked == true)
                {
                    Send_Positions();
                    //Send_Profile();
                };

                if (button3.Enabled == false)
                {
                    Programm_Status_Set(2);
                    button3.Enabled = true;
                }
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (button3.Text == "Auto-Trading ON") 
            {
                button3.Text = "Auto-Trading OFF";

                Programm_Status_Set(3);
            }
            else if (button3.Text == "Auto-Trading OFF") 
            {
                button3.Text = "Auto-Trading ON";

                Programm_Status_Set(2);

                Butt2_ON_bool();
            }
        }

        private void textBox10_TextChanged(object sender, EventArgs e)
        {
            double result;

            if ((Double.TryParse(textBox10.Text, out result) == false) || (result < 1000))
            {
                textBox10.ForeColor = Color.Red;
            }
            else
            {
                textBox10.ForeColor = Color.Black;
                Butt2_ON_bool();
            }
        }

        private void textBox11_TextChanged(object sender, EventArgs e)
        {
            double result;

            if ((double.TryParse(textBox11.Text, out result) == false)
                || (result < 0.01) || (result > 100))
            {
                textBox11.ForeColor = Color.Red;
            }
            else
            {
                textBox11.ForeColor = Color.Black;
                Butt2_ON_bool();
            }
        }

        private void textBox12_TextChanged(object sender, EventArgs e)
        {
            double result;

            if ((Double.TryParse(textBox12.Text, out result) == false)
                || (result < 0.1) || (result > 99.9))
            {
                textBox12.ForeColor = Color.Red;
            }
            else
            {
                textBox12.ForeColor = Color.Black;
                Butt2_ON_bool();
            }
        }

        private void textBox13_TextChanged(object sender, EventArgs e)
        {
            double result;

            if ((Double.TryParse(textBox13.Text, out result) == false)
                || (result < 0.1) || (result > 99.9))
            {
                textBox13.ForeColor = Color.Red;
            }
            else
            {
                textBox13.ForeColor = Color.Black;
                Butt2_ON_bool();
            }
        }

        private void maskedTextBox2_TextChanged(object sender, EventArgs e)
        {
            DateTime date;

            if ((DateTime.TryParse(maskedTextBox2.Text, out date) == false)
                || (date > DateTime.Today) || (maskedTextBox2.MaskCompleted == false))
            {
                maskedTextBox2.ForeColor = Color.Red;
            }
            else
            {
                maskedTextBox2.ForeColor = Color.Black;
                Butt2_ON_bool();
            }
        }

        private void maskedTextBox6_TextChanged(object sender, EventArgs e)
        {
            DateTime date;

            if ((DateTime.TryParse(maskedTextBox6.Text, out date) == false)
               || (date > DateTime.Today) || (maskedTextBox6.MaskCompleted == false))
            {
                maskedTextBox6.ForeColor = Color.Red;
            }
            else
            {
                maskedTextBox6.ForeColor = Color.Black;
                Butt2_ON_bool();
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIcon1.Visible = true;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            notifyIcon1.Visible = false;
            WindowState = FormWindowState.Normal;
        }
    }
}