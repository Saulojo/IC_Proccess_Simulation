using IndustrialSim.Core.Components;
using IndustrialSim.UnityRuntime.Visualization.Boiler;
using UnityEngine;

namespace IndustrialSim.UnityRuntime.Simulation
{
    /// <summary>
    /// Ponte entre o modelo matemático do tanque e a cena da Unity.
    ///
    /// Responsabilidades:
    /// - executar WaterTankPlant em timestep fixo;
    /// - fornecer o setpoint ao controlador de nível;
    /// - comandar a válvula de saída para os ensaios;
    /// - expor variáveis do processo no Inspector;
    /// - enviar o nível normalizado para a visualização.
    ///
    /// Este componente NÃO contém a física do tanque.
    /// A física permanece em SimulationCore.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterTankSimulationController : MonoBehaviour
    {
        // ================================================================
        // REFERENCES
        // ================================================================

        [Header("References")]

        [SerializeField]
        private BoilerWaterPreviewController waterVisual;

        [Header("Visual Process Mapping")]

        [Tooltip(
            "Vazão de entrada que corresponde à agitação visual máxima.")]
        [SerializeField, Min(0.000001f)]
        private float inputFlowForMaximumAgitation = 0.02f;

        [Tooltip(
            "Velocidade com que a agitação visual acompanha a vazão.")]
        [SerializeField, Min(0.01f)]
        private float agitationResponseSpeed = 3.0f;

        [SerializeField, Range(0f, 1f)]
        private float hydraulicAgitation01;


        // ================================================================
        // LEVEL CONTROL
        // ================================================================

        [Header("Level Control")]

        [Tooltip("Habilita o PI/PID de nível da planta.")]
        [SerializeField]
        private bool enableLevelControl = true;

        [Tooltip("Setpoint normalizado do nível. 0 = vazio, 1 = nível máximo.")]
        [SerializeField, Range(0f, 1f)]
        private float levelSetpoint01 = 0.50f;

        [Tooltip(
            "Comando manual da válvula de entrada quando o controle automático " +
            "estiver desabilitado.")]
        [SerializeField, Range(0f, 1f)]
        private float manualInputValveOpening01 = 0f;


        // ================================================================
        // DISTURBANCE / OUTPUT
        // ================================================================

        [Header("Output Valve / Disturbance")]

        [Tooltip(
            "Abertura comandada da válvula de saída. " +
            "Ela funciona como carga/distúrbio para o controle de nível.")]
        [SerializeField, Range(0f, 1f)]
        private float outputValveOpening01 = 0.50f;


        // ================================================================
        // RUNTIME MONITORING
        // ================================================================

        [Header("Runtime Monitoring - Read Only")]

        [SerializeField]
        private float simulationTimeSeconds;

        [SerializeField]
        private float levelSetpointMeters;

        [SerializeField]
        private float actualLevelMeters;

        [SerializeField, Range(0f, 1f)]
        private float actualLevel01;

        [SerializeField, Range(0f, 1f)]
        private float controllerOutput01;

        [SerializeField, Range(0f, 1f)]
        private float inputValveOpening01;

        [SerializeField, Range(0f, 1f)]
        private float actualOutputValveOpening01;

        [SerializeField]
        private float inputFlow;

        [SerializeField]
        private float outputFlow;


        // ================================================================
        // MODEL
        // ================================================================

        private WaterTankPlant plant;


        // ================================================================
        // PUBLIC READ-ONLY API
        // ================================================================

        public WaterTankPlant Plant => plant;

        public float LevelSetpoint01 => levelSetpoint01;

        public float ActualLevel01 => actualLevel01;

        public float ActualLevelMeters => actualLevelMeters;

        public float InputValveOpening01 => inputValveOpening01;

        public float InputFlow => inputFlow;

        public float OutputFlow => outputFlow;


        // ================================================================
        // UNITY LIFECYCLE
        // ================================================================

        private void Awake()
        {
            InitializeSimulation();
        }

        private void Start()
        {
            UpdateRuntimeValues();
            PushVisualState();
        }

        private void FixedUpdate()
        {
            if (plant == null)
            {
                InitializeSimulation();
            }

            StepSimulation(Time.fixedDeltaTime);
        }

        private void OnValidate()
        {
            levelSetpoint01 =
                Mathf.Clamp01(levelSetpoint01);

            manualInputValveOpening01 =
                Mathf.Clamp01(manualInputValveOpening01);

            outputValveOpening01 =
                Mathf.Clamp01(outputValveOpening01);
        }

        private void UpdateVisualProcessState(float deltaTime)
        {
            if (waterVisual == null)
                return;

            float referenceFlow =
                Mathf.Max(
                    0.000001f,
                    inputFlowForMaximumAgitation);

            float targetAgitation =
                Mathf.Clamp01(
                    inputFlow / referenceFlow);

            hydraulicAgitation01 =
                Mathf.MoveTowards(
                    hydraulicAgitation01,
                    targetAgitation,
                    agitationResponseSpeed * deltaTime);

            waterVisual.SetCirculation01(
                hydraulicAgitation01);

            // Por enquanto não estamos simulando geração de vapor.
            waterVisual.SetBoilingIntensity01(0f);
        }


        // ================================================================
        // INITIALIZATION
        // ================================================================

        private void InitializeSimulation()
        {
            plant = new WaterTankPlant();

            simulationTimeSeconds = 0f;

            ApplyCommandsToPlant();
            UpdateRuntimeValues();
            PushVisualState();
        }


        // ================================================================
        // SIMULATION
        // ================================================================

        private void StepSimulation(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            ApplyCommandsToPlant();

            plant.Step(
                simulationTimeSeconds,
                deltaTime);

            simulationTimeSeconds += deltaTime;

            UpdateRuntimeValues();

            UpdateVisualProcessState(deltaTime);

            PushVisualState();
        }


        // ================================================================
        // COMMANDS
        // ================================================================

        private void ApplyCommandsToPlant()
        {
            if (plant == null)
                return;

            float tankHeight =
                Mathf.Max(
                    0.0001f,
                    (float)plant.Tank.Height);

            levelSetpointMeters =
                levelSetpoint01 *
                tankHeight;

            plant.EnableLevelControl =
                enableLevelControl;

            plant.LevelSetpoint =
                levelSetpointMeters;

            // A bomba permanece em máximo dentro de WaterTankPlant.
            // O controlador atua na válvula pneumática de entrada.

            if (!enableLevelControl)
            {
                plant.InputValve.Target =
                    manualInputValveOpening01;
            }

            // A saída representa a carga/distúrbio do processo.
            plant.OutputValve.Target =
                outputValveOpening01;
        }


        // ================================================================
        // MONITORING
        // ================================================================

        private void UpdateRuntimeValues()
        {
            if (plant == null)
                return;

            float tankHeight =
                Mathf.Max(
                    0.0001f,
                    (float)plant.Tank.Height);

            actualLevelMeters =
                (float)plant.Tank.Level;

            actualLevel01 =
                Mathf.Clamp01(
                    actualLevelMeters /
                    tankHeight);

            // Target = comando solicitado pelo controlador.
            controllerOutput01 =
                Mathf.Clamp01(
                    (float)plant.InputValve.Target);

            // Value = posição real do atuador após sua dinâmica.
            inputValveOpening01 =
                Mathf.Clamp01(
                    (float)plant.InputValve.Value);

            actualOutputValveOpening01 =
                Mathf.Clamp01(
                    (float)plant.OutputValve.Value);

            inputFlow =
                (float)plant.InputFlow;

            outputFlow =
                (float)plant.OutputFlow;
        }


        // ================================================================
        // VISUALIZATION
        // ================================================================

        private void PushVisualState()
        {
            if (waterVisual == null)
                return;

            waterVisual.SetWaterLevel01(
                actualLevel01);
        }


        // ================================================================
        // DEBUG / TEST
        // ================================================================

        [ContextMenu("Reset Simulation")]
        private void ResetSimulation()
        {
            InitializeSimulation();
        }
    }
}