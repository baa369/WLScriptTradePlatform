using System;
using Strategy;

namespace WindowsFormsApplication
{
    public class PositionSizeSet
    {
        public static int RiskPerCap(double price, double stop, double cap, double risk, string signal)
        {
            int Size = 0;

            cap = MyMM.ChangeCap(cap, signal);
            risk = MyMM.ChangeRisk(risk, signal);

            double delta = Math.Abs(price - stop);

            if ((cap > 0) & (risk > 0) & (delta > 0))
            {
                Size = (int)Math.Floor((cap * risk / 100) / delta);

                if (Size * price > cap) { Size = (int)Math.Floor(cap / price); }
            }

            return Size;
        }

        public static int PerOfCap(double price, double cap, double per)
        {
            int Size = 0;

            Size = (int)(per / 100 * cap / price);

            return Size;
        }
    }
}
