using System;
using NModbus;

namespace IndustrialSim.Communication.Modbus
{
    internal sealed class IndustrialIoPointSource<T>
        : IPointSource<T>
    {
        private readonly Func<ushort, ushort, T[]> read;
        private readonly Action<ushort, T[]> write;

        public IndustrialIoPointSource(
            Func<ushort, ushort, T[]> read,
            Action<ushort, T[]> write)
        {
            this.read =
                read ??
                throw new ArgumentNullException(
                    nameof(read));

            this.write =
                write ??
                throw new ArgumentNullException(
                    nameof(write));
        }

        public T[] ReadPoints(
            ushort startAddress,
            ushort numberOfPoints)
        {
            return read(
                startAddress,
                numberOfPoints);
        }

        public void WritePoints(
            ushort startAddress,
            T[] points)
        {
            write(
                startAddress,
                points);
        }
    }
}