using IndustrialSim.Core.Components;
using UnityEngine;

public class WaterTankUnityRunner : MonoBehaviour
{
    [Header("Simulation")]
    [SerializeField] private double simulationStep = 0.01;

    [Header("Control")]
    [SerializeField] private bool enableLevelControl = true;
    [SerializeField] private double levelSetpoint = 1.0;

    [Header("Manual Commands")]
    [Range(0f, 1f)]
    [SerializeField] private float manualPumpCommand = 1.0f;

    [Range(0f, 1f)]
    [SerializeField] private float inputValveCommand = 1.0f;

    [Range(0f, 1f)]
    [SerializeField] private float outputValveCommand = 0.4f;

    [Header("Debug")]
    [SerializeField] private double simulationTime;
    [SerializeField] private double tankLevel;
    [SerializeField] private double pumpValue;
    [SerializeField] private double InputValve;
    [SerializeField] private double inputFlowLitersPerMinute;
    [SerializeField] private double outputFlowLitersPerMinute;

    public WaterTankPlant Plant { get; private set; }

    private double accumulator;

    private void Awake()
    {
        Plant = new WaterTankPlant();
    }

    private void Update()
    {
        ApplyInspectorInputs();

        accumulator += Time.deltaTime;

        while (accumulator >= simulationStep)
        {
            Plant.Step(simulationTime, simulationStep);

            simulationTime += simulationStep;
            accumulator -= simulationStep;
        }

        UpdateDebugValues();
    }

    private void ApplyInspectorInputs()
    {
        Plant.EnableLevelControl = enableLevelControl;
        Plant.LevelSetpoint = levelSetpoint;

        Plant.InputValve.Target = inputValveCommand;
        Plant.OutputValve.Target = outputValveCommand;

        if (!enableLevelControl)
            Plant.Pump.Target = manualPumpCommand;
    }

    private void UpdateDebugValues()
    {
        tankLevel = Plant.Tank.Level;
        pumpValue = Plant.Pump.Value;
        InputValve = Plant.InputValve.Value;
        inputFlowLitersPerMinute = Plant.InputFlow * 60000.0;
        outputFlowLitersPerMinute = Plant.OutputFlow * 60000.0;
    }
}