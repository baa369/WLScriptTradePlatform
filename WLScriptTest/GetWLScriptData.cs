//using Microsoft.VisualStudio.TestTools.UnitTesting;
using WealthLab;
using WindowsFormsApplication;
using System.Collections.Generic;
using Strategy;

namespace WLScriptTest
{
  //  [TestClass]
    public class GetWLScriptData
    {
        private TradingSystemExecutor executor;
        private StrategyCode script;

        public string ScriptName;

        public IList<Position> Positions;

        public List<Dictionary<string, object>> PositionsCreated = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> GetScriptPositions
        {
            get
            {
                return this.PositionsCreated;
            }
        }

        public List<Dictionary<string, object>> AlertsCreated = new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> GetScriptAlerts
        {
            get
            {
                return this.AlertsCreated;
            }
        }

       // [TestInitialize]
        public void Setup()
        {
            this.executor = new TradingSystemExecutor();
            this.script = new StrategyCode();
            this.ScriptName = this.script.Name;
        }

      //  [TestMethod]
        public void Execute_WealthScript(Bars bars)
        {
            this.executor.Execute(this.script, bars);
            this.Positions = this.script.PositionsCreated;
            this.AlertsCreated = this.script.AlertsCreated;

            foreach (Position p in this.Positions)
            {
                PositionsCreated.Add(MyPositions.PositionAdd(p));
            }
        }
    }
}
