using System;

namespace IndustrialSim.Core.Control
{
    public static class TransferFunctionFactory
    {
        /// <summary>
        /// Cria uma planta em espaço de estados a partir de uma função de transferência.
        ///
        /// Os coeficientes devem vir em ordem decrescente de potência de s.
        ///
        /// Exemplo:
        /// G(s) = 10 / (s + 1)
        /// numeratorDescending   = [10]
        /// denominatorDescending = [1, 1]
        ///
        /// Exemplo:
        /// G(s) = (2s + 5) / (s² + 3s + 2)
        /// numeratorDescending   = [2, 5]
        /// denominatorDescending = [1, 3, 2]
        /// </summary>
        public static StateSpacePlant FromTransferFunction(
            double[] numeratorDescending,
            double[] denominatorDescending)
        {
            if (numeratorDescending == null)
                throw new ArgumentNullException(nameof(numeratorDescending));

            if (denominatorDescending == null)
                throw new ArgumentNullException(nameof(denominatorDescending));

            if (numeratorDescending.Length == 0)
                throw new ArgumentException("Numerador vazio.");

            if (denominatorDescending.Length < 2)
                throw new ArgumentException("Denominador deve ter ordem pelo menos 1.");

            double leadingDenominator = denominatorDescending[0];

            if (System.Math.Abs(leadingDenominator) < 1e-12)
                throw new ArgumentException("Coeficiente líder do denominador não pode ser zero.");

            int denominatorOrder = denominatorDescending.Length - 1;
            int numeratorOrder = numeratorDescending.Length - 1;

            if (numeratorOrder > denominatorOrder)
                throw new ArgumentException("Função de transferência imprópria. O grau do numerador não pode ser maior que o do denominador.");

            int n = denominatorOrder;

            /*
             * Normaliza o denominador para:
             *
             * s^n + a[n-1]s^(n-1) + ... + a[1]s + a[0]
             *
             * denAsc[0] = a0
             * denAsc[1] = a1
             * ...
             * denAsc[n] = 1
             */
            double[] denAsc = new double[n + 1];

            for (int i = 0; i <= n; i++)
            {
                int descIndex = denominatorDescending.Length - 1 - i;
                denAsc[i] = denominatorDescending[descIndex] / leadingDenominator;
            }

            /*
             * Normaliza e coloca numerador em ordem ascendente:
             *
             * numAsc[0] = b0
             * numAsc[1] = b1
             * ...
             * numAsc[n] = b_n, se existir
             */
            double[] numAsc = new double[n + 1];

            for (int i = 0; i < numeratorDescending.Length; i++)
            {
                int power = numeratorOrder - i;
                numAsc[power] = numeratorDescending[i] / leadingDenominator;
            }

            /*
             * Se o numerador tiver mesmo grau do denominador, existe termo direto D.
             * Fazemos uma divisão polinomial simples:
             *
             * G(s) = D + resto(s)/den(s)
             */
            double d = numAsc[n];

            double[] remainderAsc = new double[n];

            for (int i = 0; i < n; i++)
                remainderAsc[i] = numAsc[i] - d * denAsc[i];

            /*
             * Forma canônica controlável:
             *
             * x' = A x + B u
             * y  = C x + D u
             *
             * A =
             * [ 0  1  0  ... 0       ]
             * [ 0  0  1  ... 0       ]
             * [ ...                  ]
             * [ -a0 -a1 -a2 ... -a_n-1]
             *
             * B = [0 0 ... 1]^T
             *
             * C = [b0 b1 ... b_n-1]
             */
            double[,] a = new double[n, n];

            for (int row = 0; row < n - 1; row++)
                a[row, row + 1] = 1.0;

            for (int col = 0; col < n; col++)
                a[n - 1, col] = -denAsc[col];

            double[] b = new double[n];
            b[n - 1] = 1.0;

            double[] c = new double[n];

            for (int i = 0; i < n; i++)
                c[i] = remainderAsc[i];

            return new StateSpacePlant(a, b, c, d);
        }
    }
}