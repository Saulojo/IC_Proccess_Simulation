using IndustrialSim.Core.Control;

namespace IndustrialSim.Core.Components
{
    /// <summary>
    /// Planta didática:
    ///
    /// Bomba -> válvula de entrada -> tanque -> válvula de saída.
    ///
    /// O PID controla a bomba para manter o nível do tanque.
    /// </summary>
    public sealed class WaterTankPlant
    {
        public TankModel Tank { get; }

        public FirstOrderActuator Pump { get; }
        public FirstOrderActuator InputValve { get; }
        public FirstOrderActuator OutputValve { get; }

        public PIDController LevelController { get; }

        public bool EnableLevelControl { get; set; } = true;

        public double LevelSetpoint { get; set; } = 1.0;

        public double PumpMaxFlow { get; set; } = 0.002; // m³/s

        public double InputFlow { get; private set; }
        public double OutputFlow => Tank.OutputFlow;

        public WaterTankPlant()
        {
            Tank = new TankModel(
                radius: 0.5,
                height: 2.0,
                initialLevel: 0.2,
                outletCoefficient: 0.001);

            Pump = new FirstOrderActuator(
                initialValue: 0.0,
                timeConstant: 0.4,
                minValue: 0.0,
                maxValue: 1.0);

            InputValve = new FirstOrderActuator(
                initialValue: 1.0,
                timeConstant: 0.3,
                minValue: 0.0,
                maxValue: 1.0);

            OutputValve = new FirstOrderActuator(
                initialValue: 0.4,
                timeConstant: 0.3,
                minValue: 0.0,
                maxValue: 1.0);

            LevelController = new PIDController(
                kp: 2.0,
                ki: 0.3,
                kd: 0.0,
                outputMin: 0.0,
                outputMax: 1.0);
        }

        public void Step(double time, double dt)
        {
            if (EnableLevelControl)
            {
                double pumpCommand = LevelController.Update(
                    LevelSetpoint,
                    Tank.Level,
                    dt);

                Pump.Target = pumpCommand;
            }

            Pump.Step(time, dt);
            InputValve.Step(time, dt);
            OutputValve.Step(time, dt);

            InputFlow = PumpMaxFlow * Pump.Value * InputValve.Value;

            Tank.InputFlow = InputFlow;
            Tank.OutletValveOpening = OutputValve.Value;

            Tank.Step(time, dt);
        }
    }
}