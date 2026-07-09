using System;
using IndustrialSim.Core.Math;

namespace IndustrialSim.Core.Components
{
    /// <summary>
    /// Modelo genérico de primeira ordem.
    /// Serve para bomba, válvula, sensor, atuador pneumático, etc.
    /// </summary>
    public sealed class FirstOrderActuator
    {
        public double Value { get; private set; }
        public double Target { get; set; }
        public double TimeConstant { get; set; } = 0.5;

        public double MinValue { get; set; } = 0.0;
        public double MaxValue { get; set; } = 1.0;

        public FirstOrderActuator(
            double initialValue = 0.0,
            double timeConstant = 0.5,
            double minValue = 0.0,
            double maxValue = 1.0)
        {
            if (timeConstant <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(timeConstant));

            if (maxValue <= minValue)
                throw new ArgumentException("MaxValue deve ser maior que MinValue.");

            Value = initialValue;
            Target = initialValue;
            TimeConstant = timeConstant;
            MinValue = minValue;
            MaxValue = maxValue;

            Value = Clamp(Value, MinValue, MaxValue);
            Target = Clamp(Target, MinValue, MaxValue);
        }

        public void Step(double time, double dt)
        {
            Target = Clamp(Target, MinValue, MaxValue);

            Value = RungeKutta4.Step(
                Value,
                time,
                dt,
                Derivative);

            Value = Clamp(Value, MinValue, MaxValue);
        }

        private double Derivative(double time, double value)
        {
            return (Target - value) / TimeConstant;
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