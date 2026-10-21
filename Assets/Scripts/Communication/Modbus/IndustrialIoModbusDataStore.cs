using System;
using IndustrialSim.IO;
using NModbus;

namespace IndustrialSim.Communication.Modbus
{
    /// <summary>
    /// Adapter entre a imagem de I/O industrial
    /// e as quatro áreas de memória do Modbus.
    ///
    /// Modbus:
    /// Coils             -> Digital Outputs
    /// Discrete Inputs   -> Digital Inputs
    /// Holding Registers -> Analog Outputs
    /// Input Registers   -> Analog Inputs
    /// </summary>
    public sealed class IndustrialIoModbusDataStore
        : ISlaveDataStore
    {
        public IPointSource<bool> CoilDiscretes
        {
            get;
        }

        public IPointSource<bool> CoilInputs
        {
            get;
        }

        public IPointSource<ushort> HoldingRegisters
        {
            get;
        }

        public IPointSource<ushort> InputRegisters
        {
            get;
        }

        public IndustrialIoModbusDataStore(
            IndustrialIoImage ioImage)
        {
            if (ioImage == null)
            {
                throw new ArgumentNullException(
                    nameof(ioImage));
            }

            CoilDiscretes =
                new IndustrialIoPointSource<bool>(
                    ioImage.ReadDigitalOutputs,
                    ioImage.WriteDigitalOutputs);

            CoilInputs =
                new IndustrialIoPointSource<bool>(
                    ioImage.ReadDigitalInputs,
                    ioImage.WriteDigitalInputs);

            HoldingRegisters =
                new IndustrialIoPointSource<ushort>(
                    ioImage.ReadAnalogOutputs,
                    ioImage.WriteAnalogOutputs);

            InputRegisters =
                new IndustrialIoPointSource<ushort>(
                    ioImage.ReadAnalogInputs,
                    ioImage.WriteAnalogInputs);
        }
    }
}