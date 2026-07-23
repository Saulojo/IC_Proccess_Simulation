using System;
using IndustrialSim.Core.Math;

namespace IndustrialSim.Core.Control
{
    public sealed class StateSpacePlant
    {
        public double[,] A { get; }
        public double[] B { get; }
        public double[] C { get; }
        public double D { get; }

        public double[] State { get; private set; }

        public double Output { get; private set; }

        private double _inputDuringStep;

        public StateSpacePlant(
            double[,] a,
            double[] b,
            double[] c,
            double d)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (c == null) throw new ArgumentNullException(nameof(c));

            int n = b.Length;

            if (a.GetLength(0) != n || a.GetLength(1) != n)
                throw new ArgumentException("A deve ser uma matriz n x n.");

            if (c.Length != n)
                throw new ArgumentException("C deve ter o mesmo tamanho de B.");

            A = a;
            B = b;
            C = c;
            D = d;

            State = new double[n];
        }

        public double Step(double input, double time, double dt)
        {
            _inputDuringStep = input;

            State = RungeKutta4Vector.Step(
                State,
                time,
                dt,
                Derivative);

            Output = CalculateOutput(input);

            return Output;
        }

        public void Reset()
        {
            for (int i = 0; i < State.Length; i++)
                State[i] = 0.0;

            Output = 0.0;
        }

        private double[] Derivative(double time, double[] x)
        {
            int n = x.Length;
            double[] dx = new double[n];

            for (int row = 0; row < n; row++)
            {
                double sum = 0.0;

                for (int col = 0; col < n; col++)
                    sum += A[row, col] * x[col];

                sum += B[row] * _inputDuringStep;

                dx[row] = sum;
            }

            return dx;
        }

        private double CalculateOutput(double input)
        {
            double y = D * input;

            for (int i = 0; i < State.Length; i++)
                y += C[i] * State[i];

            return y;
        }
    }
}