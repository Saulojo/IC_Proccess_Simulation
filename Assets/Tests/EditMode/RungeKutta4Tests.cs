using System;
using IndustrialSim.Core.Math;
using NUnit.Framework;

public class RungeKutta4Tests
{
    [Test]
    public void RK4_ShouldApproximateExponentialDecay()
    {
        double y = 1.0;
        double time = 0.0;
        double dt = 0.01;
        double duration = 1.0;

        int steps = (int)(duration / dt);

        for (int i = 0; i < steps; i++)
        {
            y = RungeKutta4.Step(y, time, dt, Derivative);
            time += dt;
        }

        double expected = Math.Exp(-1.0);

        Assert.That(y, Is.EqualTo(expected).Within(1e-8));
    }

    [Test]
    public void RK4_ShouldThrow_WhenDtIsZeroOrNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            RungeKutta4.Step(
                y: 1.0,
                t: 0.0,
                dt: 0.0,
                derivative: Derivative);
        });
    }

    private static double Derivative(double time, double y)
    {
        return -y;
    }
}

/* 
VALIDA:
dy/dt = -y
y(t) = e^(-t) 
*/