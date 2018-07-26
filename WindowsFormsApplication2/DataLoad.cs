using System;
using WealthLab;
using System.IO;

namespace WindowsFormsApplication
{
    //загрузка котировок из файла
    class DataLoad
    {
        public static Bars PathLoad(string path)
        {
            string name = path.Substring(path.Length - 9, 5).Replace(@"\", "");

            Bars bars = new Bars(name, BarScale.Daily, 0);

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

            return bars;
        }
    }
}
