using System;
using IndustrialSim.Core.Math;

namespace IndustrialSim.Core.Components
{
    /// <summary>
    /// Modelo de tanque com entrada Qin e saída dependente do nível.
    ///
    /// Equação:
    /// dh/dt = (Qin - Qout) / A
    ///
    /// Qout = Kout * valveOpening * sqrt(h)
    /// </summary>
    public sealed class TankModel
    {
        public double Level { get; private set; }

        public double Radius { get; }
        public double Height { get; }

        public double InputFlow { get; set; }
        public double OutletCoefficient { get; set; }
        public double OutletValveOpening { get; set; }

        public double OutputFlow { get; private set; }

        public double Area => System.Math.PI * Radius * Radius;
        public double NormalizedLevel => Height <= 0.0 ? 0.0 : Level / Height;
        public double Volume => Area * Level;

        public TankModel(
            double radius,
            double height,
            double initialLevel,
            double outletCoefficient)
        {
            if (radius <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(radius));

            if (height <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(height));

            if (outletCoefficient < 0.0)
                throw new ArgumentOutOfRangeException(nameof(outletCoefficient));

            Radius = radius;
            Height = height;
            OutletCoefficient = outletCoefficient;

            Level = Clamp(initialLevel, 0.0, Height);
        }

        public void Step(double time, double dt)
        {
            Level = RungeKutta4.Step(
                Level,
                time,
                dt,
                Derivative);

            Level = Clamp(Level, 0.0, Height);

            OutputFlow = CalculateOutputFlow(Level);
        }

        private double Derivative(double time, double level)
        {
            level = System.Math.Max(0.0, level);

            double qOut = CalculateOutputFlow(level);

            return (InputFlow - qOut) / Area;
        }

        private double CalculateOutputFlow(double level)
        {
            double opening = Clamp(OutletValveOpening, 0.0, 1.0);

            if (level <= 0.0 || opening <= 0.0)
                return 0.0;

            return OutletCoefficient * opening * System.Math.Sqrt(level);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}