using System;

namespace IndustrialSim.Core.Math
{
    /// <summary>
    /// Runge-Kutta de 4ª ordem para integração numérica de EDOs.
    /// Este código é C# puro e não depende da Unity.
    /// </summary>
    public static class RungeKutta4
    {
        /// <summary>
        /// Integra uma única variável y' = f(t, y).
        /// </summary>
        public static double Step(
            double y,
            double t,
            double dt,
            Func<double, double, double> derivative)
        {
            if (dt <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(dt), "dt deve ser maior que zero.");

            if (derivative == null)
                throw new ArgumentNullException(nameof(derivative));

            double k1 = derivative(t, y);
            double k2 = derivative(t + 0.5 * dt, y + 0.5 * dt * k1);
            double k3 = derivative(t + 0.5 * dt, y + 0.5 * dt * k2);
            double k4 = derivative(t + dt, y + dt * k3);

            return y + (dt / 6.0) * (k1 + 2.0 * k2 + 2.0 * k3 + k4);
        }
    }
}