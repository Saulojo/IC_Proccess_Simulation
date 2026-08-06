using System;

namespace IndustrialSim.Core.Math
{
    public static class RungeKutta4Vector
    {
        public static double[] Step(
            double[] state,
            double time,
            double dt,
            Func<double, double[], double[]> derivative)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            if (derivative == null)
                throw new ArgumentNullException(nameof(derivative));

            if (dt <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(dt));

            double[] k1 = derivative(time, state);
            double[] k2 = derivative(time + dt * 0.5, AddScaled(state, k1, dt * 0.5));
            double[] k3 = derivative(time + dt * 0.5, AddScaled(state, k2, dt * 0.5));
            double[] k4 = derivative(time + dt, AddScaled(state, k3, dt));

            double[] result = new double[state.Length];

            for (int i = 0; i < state.Length; i++)
            {
                result[i] = state[i] + dt / 6.0 *
                    (k1[i] + 2.0 * k2[i] + 2.0 * k3[i] + k4[i]);
            }

            return result;
        }

        private static double[] AddScaled(double[] vector, double[] increment, double scale)
        {
            if (vector.Length != increment.Length)
                throw new ArgumentException("Vetores com tamanhos diferentes.");

            double[] result = new double[vector.Length];

            for (int i = 0; i < vector.Length; i++)
                result[i] = vector[i] + increment[i] * scale;

            return result;
        }
    }
}