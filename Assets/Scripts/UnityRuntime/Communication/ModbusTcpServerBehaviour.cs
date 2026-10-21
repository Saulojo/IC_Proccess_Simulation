using System;
using System.Net;
using IndustrialSim.Communication.Modbus;
using IndustrialSim.IO;
using UnityEngine;

namespace IndustrialSim.UnityRuntime.Communication
{
    /// <summary>
    /// Host Unity para o servidor Modbus TCP.
    ///
    /// Esta classe somente controla o ciclo de vida
    /// do servidor dentro da aplicação Unity.
    ///
    /// Nenhuma lógica de processo deve ser colocada aqui.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModbusTcpServerBehaviour
        : MonoBehaviour
    {
        // ============================================================
        // CONFIGURATION
        // ============================================================

        [Header("Modbus TCP Server")]

        [Tooltip(
            "0.0.0.0 escuta em todas as interfaces de rede.")]
        [SerializeField]
        private string bindAddress = "0.0.0.0";

        [Tooltip(
            "Porta de desenvolvimento. " +
            "Usaremos 1502 para evitar conflitos com a porta 502.")]
        [SerializeField]
        private int port = 1502;

        [Tooltip("Modbus Unit ID.")]
        [SerializeField, Range(1, 247)]
        private int unitId = 1;

        [SerializeField]
        private bool startOnAwake = true;


        // ============================================================
        // RUNTIME MONITORING
        // ============================================================

        [Header("Runtime Monitoring - Read Only")]

        [SerializeField]
        private bool serverRunning;

        [SerializeField]
        private string lastError;


        // ============================================================
        // SERVER
        // ============================================================

        private ModbusTcpServer server;

        public IndustrialIoImage IoImage
        {
            get;
            private set;
        }

        public ModbusTcpServer Server =>
            server;


        // ============================================================
        // UNITY LIFECYCLE
        // ============================================================

        private void Awake()
        {
            IoImage =
                new IndustrialIoImage();

            if (startOnAwake)
            {
                StartServer();
            }
        }

        private void Update()
        {
            serverRunning =
                server != null &&
                server.IsRunning;

            if (server != null &&
                server.BackgroundException != null)
            {
                lastError =
                    server.BackgroundException.ToString();
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        private void OnApplicationQuit()
        {
            StopServer();
        }

        private void OnValidate()
        {
            port =
                Mathf.Clamp(
                    port,
                    1,
                    65535);

            unitId =
                Mathf.Clamp(
                    unitId,
                    1,
                    247);
        }


        // ============================================================
        // SERVER CONTROL
        // ============================================================

        [ContextMenu("Start Modbus TCP Server")]
        public void StartServer()
        {
            if (server != null &&
                server.IsRunning)
            {
                return;
            }

            if (!IPAddress.TryParse(
                    bindAddress,
                    out IPAddress address))
            {
                lastError =
                    $"Endereço IP inválido: {bindAddress}";

                Debug.LogError(
                    lastError,
                    this);

                return;
            }

            try
            {
                if (IoImage == null)
                {
                    IoImage =
                        new IndustrialIoImage();
                }

                server?.Dispose();

                server =
                    new ModbusTcpServer(
                        IoImage,
                        address,
                        port,
                        (byte)unitId);

                server.Start();

                serverRunning = true;
                lastError = string.Empty;

                Debug.Log(
                    $"Modbus TCP Server iniciado em " +
                    $"{bindAddress}:{port}, Unit ID={unitId}.",
                    this);
            }
            catch (Exception exception)
            {
                serverRunning = false;
                lastError = exception.ToString();

                Debug.LogException(
                    exception,
                    this);
            }
        }

        [ContextMenu("Stop Modbus TCP Server")]
        public void StopServer()
        {
            if (server == null)
                return;

            server.Dispose();
            server = null;

            serverRunning = false;
        }
    }
}