using System;

namespace IndustrialSim.Core.Control
{
    /// <summary>
    /// PID discreto com saturação e anti-windup simples.
    /// C# puro, sem dependência da Unity.
    /// </summary>
    public sealed class PIDController
    {
        public double Kp { get; set; }
        public double Ki { get; set; }
        public double Kd { get; set; }

        public double OutputMin { get; set; } = 0.0;
        public double OutputMax { get; set; } = 1.0;

        public double Integral { get; private set; }
        public double PreviousError { get; private set; }

        private bool _hasPreviousError;

        public PIDController(
            double kp,
            double ki,
            double kd,
            double outputMin = 0.0,
            double outputMax = 1.0)
        {
            if (outputMax <= outputMin)
                throw new ArgumentException("OutputMax deve ser maior que OutputMin.");

            Kp = kp;
            Ki = ki;
            Kd = kd;
            OutputMin = outputMin;
            OutputMax = outputMax;
        }

        public double Update(double setpoint, double processVariable, double dt)
        {
            if (dt <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(dt));

            double error = setpoint - processVariable;

            double derivative = 0.0;

            if (_hasPreviousError)
                derivative = (error - PreviousError) / dt;

            double candidateIntegral = Integral + error * dt;

            double unsaturatedOutput =
                Kp * error +
                Ki * candidateIntegral +
                Kd * derivative;

            double output = Clamp(unsaturatedOutput, OutputMin, OutputMax);

            /*
             * Anti-windup simples:
             * Só aceita atualizar a integral se a saída não estiver saturada
             * ou se o erro estiver tentando sair da saturação.
             */
            bool isSaturatedHigh = unsaturatedOutput > OutputMax;
            bool isSaturatedLow = unsaturatedOutput < OutputMin;

            bool allowIntegral =
                (!isSaturatedHigh && !isSaturatedLow) ||
                (isSaturatedHigh && error < 0.0) ||
                (isSaturatedLow && error > 0.0);

            if (allowIntegral)
                Integral = candidateIntegral;

            PreviousError = error;
            _hasPreviousError = true;

            return output;
        }

        public void Reset()
        {
            Integral = 0.0;
            PreviousError = 0.0;
            _hasPreviousError = false;
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