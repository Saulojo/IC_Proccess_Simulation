using System;

namespace IndustrialSim.Core.Runtime
{
    /// <summary>
    /// Executa simulação em passo fixo.
    /// Não depende da Unity.
    /// </summary>
    public sealed class FixedStepSimulation
    {
        public double StepSize { get; }
        public double Time { get; private set; }

        private double _accumulator;

        public FixedStepSimulation(double stepSize)
        {
            if (stepSize <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(stepSize));

            StepSize = stepSize;
        }

        public void Advance(double elapsedTime, Action<double, double> simulateStep)
        {
            if (elapsedTime < 0.0)
                throw new ArgumentOutOfRangeException(nameof(elapsedTime));

            if (simulateStep == null)
                throw new ArgumentNullException(nameof(simulateStep));

            _accumulator += elapsedTime;

            while (_accumulator >= StepSize)
            {
                simulateStep(Time, StepSize);

                Time += StepSize;
                _accumulator -= StepSize;
            }
        }

        public void Reset()
        {
            Time = 0.0;
            _accumulator = 0.0;
        }
    }
}