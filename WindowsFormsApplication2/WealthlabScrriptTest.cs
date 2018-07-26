using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WealthLab;

namespace WindowsFormsApplication
{
    //[TestClass]
    public class WealthlabScrriptTest
    {
        private TradingSystemExecutor executor;
        private StrategyCode script;

        public string x;

        //[TestInitialize]
        public void Setup()
        {
            this.executor = new TradingSystemExecutor();
            this.script = new StrategyCode();
        }

        //[TestMethod]
        public void Execute_WealthScript()
        {
            WindowsFormsApplication.MyLib.Read r = new WindowsFormsApplication.MyLib.Read();

            Bars bars = r.ReadBars("GAZRf", @"C:\Info\FORTS.cd\");

            this.executor.Execute(this.script, bars);

            x = Convert.ToString("x");//this.script.Bars.Date[bars.Count - 1]);
        }

    }
}
