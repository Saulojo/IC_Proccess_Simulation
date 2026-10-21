using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using IndustrialSim.IO;
using NModbus;

namespace IndustrialSim.Communication.Modbus
{
    /// <summary>
    /// Servidor Modbus TCP independente da Unity.
    ///
    /// Responsabilidades:
    /// - abrir o listener TCP;
    /// - criar a rede Modbus;
    /// - criar a unidade Modbus;
    /// - conectar o protocolo à IndustrialIoImage;
    /// - iniciar e encerrar o listener.
    ///
    /// Nenhuma API da Unity é utilizada nesta classe.
    /// </summary>
    public sealed class ModbusTcpServer
        : IDisposable
    {
        private readonly IndustrialIoImage ioImage;
        private readonly IPAddress bindAddress;
        private readonly int port;
        private readonly byte unitId;

        private TcpListener tcpListener;
        private IModbusSlaveNetwork slaveNetwork;

        private CancellationTokenSource cancellationTokenSource;
        private Task listenTask;

        private volatile bool isRunning;
        private Exception backgroundException;

        public bool IsRunning =>
            isRunning;

        public int Port =>
            port;

        public byte UnitId =>
            unitId;

        public IPAddress BindAddress =>
            bindAddress;

        public IndustrialIoImage IoImage =>
            ioImage;

        public Exception BackgroundException =>
            backgroundException;


        public ModbusTcpServer(
            IndustrialIoImage ioImage,
            IPAddress bindAddress,
            int port,
            byte unitId)
        {
            this.ioImage =
                ioImage ??
                throw new ArgumentNullException(
                    nameof(ioImage));

            this.bindAddress =
                bindAddress ??
                throw new ArgumentNullException(
                    nameof(bindAddress));

            if (port < 1 || port > 65535)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(port),
                    "A porta TCP deve estar entre 1 e 65535.");
            }

            if (unitId < 1 || unitId > 247)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unitId),
                    "O Unit ID deve estar entre 1 e 247.");
            }

            this.port = port;
            this.unitId = unitId;
        }


        // ============================================================
        // START
        // ============================================================

        public void Start()
        {
            if (isRunning)
                return;

            backgroundException = null;

            tcpListener =
                new TcpListener(
                    bindAddress,
                    port);

            tcpListener.Start();

            cancellationTokenSource =
                new CancellationTokenSource();

            IModbusFactory factory =
                new ModbusFactory();

            IndustrialIoModbusDataStore dataStore =
                new IndustrialIoModbusDataStore(
                    ioImage);

            IModbusSlave slave =
                factory.CreateSlave(
                    unitId,
                    dataStore);

            slaveNetwork =
                factory.CreateSlaveNetwork(
                    tcpListener);

            slaveNetwork.AddSlave(
                slave);

            isRunning = true;

            listenTask =
                slaveNetwork.ListenAsync(
                    cancellationTokenSource.Token);

            _ = listenTask.ContinueWith(
                HandleListenTaskCompletion,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }


        // ============================================================
        // STOP
        // ============================================================

        public void Stop()
        {
            if (!isRunning &&
                tcpListener == null &&
                slaveNetwork == null)
            {
                return;
            }

            isRunning = false;

            try
            {
                cancellationTokenSource?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Já encerrado.
            }

            try
            {
                tcpListener?.Stop();
            }
            catch (SocketException)
            {
                // Socket já encerrado.
            }
            catch (ObjectDisposedException)
            {
                // Já encerrado.
            }

            try
            {
                slaveNetwork?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Já encerrado.
            }

            if (listenTask != null)
            {
                try
                {
                    listenTask.Wait(
                        millisecondsTimeout: 1000);
                }
                catch (AggregateException exception)
                {
                    Exception baseException =
                        exception.GetBaseException();

                    if (!(baseException is OperationCanceledException) &&
                        !(baseException is ObjectDisposedException) &&
                        !(baseException is SocketException))
                    {
                        backgroundException =
                            baseException;
                    }
                }
            }

            cancellationTokenSource?.Dispose();

            cancellationTokenSource = null;
            listenTask = null;
            slaveNetwork = null;
            tcpListener = null;
        }


        // ============================================================
        // TASK COMPLETION
        // ============================================================

        private void HandleListenTaskCompletion(
            Task completedTask)
        {
            if (completedTask == null)
                return;

            if (completedTask.IsFaulted &&
                completedTask.Exception != null)
            {
                Exception exception =
                    completedTask.Exception
                        .GetBaseException();

                if (!(exception is OperationCanceledException) &&
                    !(exception is ObjectDisposedException) &&
                    !(exception is SocketException))
                {
                    backgroundException =
                        exception;
                }
            }

            if (!completedTask.IsCanceled)
            {
                isRunning = false;
            }
        }


        // ============================================================
        // DISPOSE
        // ============================================================

        public void Dispose()
        {
            Stop();
        }
    }
}