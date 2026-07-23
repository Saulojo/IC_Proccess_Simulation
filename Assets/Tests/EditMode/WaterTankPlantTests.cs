using System;
using IndustrialSim.Core.Components;
using NUnit.Framework;

public class WaterTankPlantTests
{
    [Test]
    public void TankLevel_ShouldIncrease_WhenPumpIsOnAndOutletIsClosed()
    {
        WaterTankPlant plant = new WaterTankPlant();

        plant.EnableLevelControl = false;
        plant.PumpMaxFlow = 0.01;

        plant.Pump.Target = 1.0;
        plant.InputValve.Target = 1.0;
        plant.OutputValve.Target = 0.0;

        double initialLevel = plant.Tank.Level;

        Simulate(plant, duration: 60.0, dt: 0.01);

        Assert.Greater(plant.Tank.Level, initialLevel);
    }

    [Test]
    public void TankLevel_ShouldDecrease_WhenPumpIsOffAndOutletIsOpen()
    {
        WaterTankPlant plant = new WaterTankPlant();

        plant.EnableLevelControl = false;
        plant.PumpMaxFlow = 0.01;

        plant.Pump.Target = 0.0;
        plant.InputValve.Target = 1.0;
        plant.OutputValve.Target = 1.0;

        double initialLevel = plant.Tank.Level;

        Simulate(plant, duration: 60.0, dt: 0.01);

        Assert.Less(plant.Tank.Level, initialLevel);
    }

    [Test]
    public void TankLevel_ShouldNeverBecomeNegative()
    {
        WaterTankPlant plant = new WaterTankPlant();

        plant.EnableLevelControl = false;
        plant.Pump.Target = 0.0;
        plant.InputValve.Target = 0.0;
        plant.OutputValve.Target = 1.0;

        Simulate(plant, duration: 600.0, dt: 0.01);

        Assert.GreaterOrEqual(plant.Tank.Level, 0.0);
    }

    [Test]
    public void TankLevel_ShouldNeverExceedTankHeight()
    {
        WaterTankPlant plant = new WaterTankPlant();

        plant.EnableLevelControl = false;
        plant.PumpMaxFlow = 0.1;

        plant.Pump.Target = 1.0;
        plant.InputValve.Target = 1.0;
        plant.OutputValve.Target = 0.0;

        Simulate(plant, duration: 600.0, dt: 0.01);

        Assert.LessOrEqual(plant.Tank.Level, plant.Tank.Height);
    }

    [Test]
    public void PID_ShouldReduceLevelError()
    {
        WaterTankPlant plant = new WaterTankPlant();

        plant.EnableLevelControl = true;
        plant.LevelSetpoint = 1.0;

        plant.PumpMaxFlow = 0.01;

        plant.InputValve.Target = 1.0;
        plant.OutputValve.Target = 0.4;

        plant.LevelController.Kp = 2.0;
        plant.LevelController.Ki = 0.1;
        plant.LevelController.Kd = 0.0;

        double initialError = Math.Abs(plant.LevelSetpoint - plant.Tank.Level);

        Simulate(plant, duration: 300.0, dt: 0.01);

        double finalError = Math.Abs(plant.LevelSetpoint - plant.Tank.Level);

        Assert.Less(finalError, initialError);
    }

    [Test]
    public void PID_ShouldDriveTankLevelNearSetpoint()
    {
        WaterTankPlant plant = new WaterTankPlant();

        plant.EnableLevelControl = true;
        plant.LevelSetpoint = 1.0;

        plant.PumpMaxFlow = 0.01;

        plant.InputValve.Target = 1.0;
        plant.OutputValve.Target = 0.4;

        plant.LevelController.Kp = 2.0;
        plant.LevelController.Ki = 0.1;
        plant.LevelController.Kd = 0.0;

        Simulate(plant, duration: 300.0, dt: 0.01);

        Assert.That(plant.Tank.Level, Is.InRange(0.8, 1.2));
    }

    private static void Simulate(WaterTankPlant plant, double duration, double dt)
    {
        double time = 0.0;
        int steps = (int)(duration / dt);

        for (int i = 0; i < steps; i++)
        {
            plant.Step(time, dt);
            time += dt;
        }
    }
}

/* 
VALIDA
1. Bomba ligada e saída fechada → nível sobe.
2. Bomba desligada e saída aberta → nível desce.
3. Nível nunca fica negativo.
4. Nível nunca passa da altura máxima.
5. PID reduz o erro.
6. PID leva o tanque perto do setpoint. 
*/