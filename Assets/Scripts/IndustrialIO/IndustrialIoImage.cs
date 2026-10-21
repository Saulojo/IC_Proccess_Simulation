using System;

namespace IndustrialSim.IO
{
    /// <summary>
    /// Imagem de processo/I/O compartilhada entre a simulação
    /// e os protocolos industriais.
    ///
    /// Esta classe não conhece Modbus, Unity, PLC ou qualquer
    /// equipamento específico.
    ///
    /// Mapeamento conceitual futuro:
    ///
    /// DigitalOutputs -> comandos digitais recebidos do controlador
    /// DigitalInputs  -> estados digitais publicados pela planta
    /// AnalogOutputs  -> comandos analógicos recebidos do controlador
    /// AnalogInputs   -> medições analógicas publicadas pela planta
    ///
    /// Todas as operações são protegidas para acesso concorrente.
    /// </summary>
    public sealed class IndustrialIoImage
    {
        public const int AddressSpaceSize = ushort.MaxValue + 1;

        private readonly object syncRoot = new object();

        private readonly bool[] digitalOutputs =
            new bool[AddressSpaceSize];

        private readonly bool[] digitalInputs =
            new bool[AddressSpaceSize];

        private readonly ushort[] analogOutputs =
            new ushort[AddressSpaceSize];

        private readonly ushort[] analogInputs =
            new ushort[AddressSpaceSize];


        // ============================================================
        // DIGITAL OUTPUTS
        // ============================================================

        public bool[] ReadDigitalOutputs(
            ushort startAddress,
            ushort count)
        {
            return ReadRange(
                digitalOutputs,
                startAddress,
                count);
        }

        public void WriteDigitalOutputs(
            ushort startAddress,
            bool[] values)
        {
            WriteRange(
                digitalOutputs,
                startAddress,
                values);
        }

        public bool ReadDigitalOutput(
            ushort address)
        {
            lock (syncRoot)
            {
                return digitalOutputs[address];
            }
        }

        public void WriteDigitalOutput(
            ushort address,
            bool value)
        {
            lock (syncRoot)
            {
                digitalOutputs[address] = value;
            }
        }


        // ============================================================
        // DIGITAL INPUTS
        // ============================================================

        public bool[] ReadDigitalInputs(
            ushort startAddress,
            ushort count)
        {
            return ReadRange(
                digitalInputs,
                startAddress,
                count);
        }

        public void WriteDigitalInputs(
            ushort startAddress,
            bool[] values)
        {
            WriteRange(
                digitalInputs,
                startAddress,
                values);
        }

        public bool ReadDigitalInput(
            ushort address)
        {
            lock (syncRoot)
            {
                return digitalInputs[address];
            }
        }

        public void WriteDigitalInput(
            ushort address,
            bool value)
        {
            lock (syncRoot)
            {
                digitalInputs[address] = value;
            }
        }


        // ============================================================
        // ANALOG OUTPUTS
        // ============================================================

        public ushort[] ReadAnalogOutputs(
            ushort startAddress,
            ushort count)
        {
            return ReadRange(
                analogOutputs,
                startAddress,
                count);
        }

        public void WriteAnalogOutputs(
            ushort startAddress,
            ushort[] values)
        {
            WriteRange(
                analogOutputs,
                startAddress,
                values);
        }

        public ushort ReadAnalogOutput(
            ushort address)
        {
            lock (syncRoot)
            {
                return analogOutputs[address];
            }
        }

        public void WriteAnalogOutput(
            ushort address,
            ushort value)
        {
            lock (syncRoot)
            {
                analogOutputs[address] = value;
            }
        }


        // ============================================================
        // ANALOG INPUTS
        // ============================================================

        public ushort[] ReadAnalogInputs(
            ushort startAddress,
            ushort count)
        {
            return ReadRange(
                analogInputs,
                startAddress,
                count);
        }

        public void WriteAnalogInputs(
            ushort startAddress,
            ushort[] values)
        {
            WriteRange(
                analogInputs,
                startAddress,
                values);
        }

        public ushort ReadAnalogInput(
            ushort address)
        {
            lock (syncRoot)
            {
                return analogInputs[address];
            }
        }

        public void WriteAnalogInput(
            ushort address,
            ushort value)
        {
            lock (syncRoot)
            {
                analogInputs[address] = value;
            }
        }


        // ============================================================
        // INTERNAL HELPERS
        // ============================================================

        private T[] ReadRange<T>(
            T[] source,
            ushort startAddress,
            ushort count)
        {
            if (count == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    "A quantidade deve ser maior que zero.");
            }

            int start = startAddress;
            int length = count;

            ValidateRange(
                source.Length,
                start,
                length);

            T[] result =
                new T[length];

            lock (syncRoot)
            {
                Array.Copy(
                    source,
                    start,
                    result,
                    0,
                    length);
            }

            return result;
        }

        private void WriteRange<T>(
            T[] destination,
            ushort startAddress,
            T[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(
                    nameof(values));
            }

            if (values.Length == 0)
            {
                throw new ArgumentException(
                    "O conjunto de valores não pode estar vazio.",
                    nameof(values));
            }

            int start = startAddress;

            ValidateRange(
                destination.Length,
                start,
                values.Length);

            lock (syncRoot)
            {
                Array.Copy(
                    values,
                    0,
                    destination,
                    start,
                    values.Length);
            }
        }

        private static void ValidateRange(
            int capacity,
            int start,
            int length)
        {
            if (start < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(start));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length));
            }

            if (start > capacity - length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(length),
                    "A faixa solicitada excede o espaço de I/O.");
            }
        }
    }
}