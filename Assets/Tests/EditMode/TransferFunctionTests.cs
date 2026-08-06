using System;
using IndustrialSim.Core.Control;
using NUnit.Framework;

public class TransferFunctionTests
{
    [Test]
    public void FirstOrderTransferFunction_ShouldFollowExpectedStepResponse()
    {
        StateSpacePlant plant = TransferFunctionFactory.FromTransferFunction(
            numeratorDescending: new double[] { 1.0 },
            denominatorDescending: new double[] { 5.0, 1.0 }
        );

        double input = 1.0;
        double output = 0.0;

        double time = 0.0;
        double dt = 0.01;
        double duration = 10.0;

        int steps = (int)(duration / dt);

        for (int i = 0; i < steps; i++)
        {
            output = plant.Step(input, time, dt);
            time += dt;
        }

        double expected = 1.0 - Math.Exp(-duration / 5.0);

        Assert.That(output, Is.EqualTo(expected).Within(1e-4));
    }

    [Test]
    public void TransferFunction_ShouldReachGain_ForFirstOrderStablePlant()
    {
        StateSpacePlant plant = TransferFunctionFactory.FromTransferFunction(
            numeratorDescending: new double[] { 10.0 },
            denominatorDescending: new double[] { 1.0, 1.0 }
        );

        double input = 1.0;
        double output = 0.0;

        double time = 0.0;
        double dt = 0.01;
        double duration = 10.0;

        int steps = (int)(duration / dt);

        for (int i = 0; i < steps; i++)
        {
            output = plant.Step(input, time, dt);
            time += dt;
        }

        Assert.That(output, Is.EqualTo(10.0).Within(0.01));
    }

    [Test]
    public void TransferFunctionFactory_ShouldRejectImproperTransferFunction()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            TransferFunctionFactory.FromTransferFunction(
                numeratorDescending: new double[] { 1.0, 0.0, 1.0 },
                denominatorDescending: new double[] { 1.0, 1.0 }
            );
        });
    }
}