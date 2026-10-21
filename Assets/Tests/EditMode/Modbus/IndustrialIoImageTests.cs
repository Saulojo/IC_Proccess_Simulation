using IndustrialSim.IO;
using NUnit.Framework;

public sealed class IndustrialIoImageTests
{
    [Test]
    public void AnalogOutput_ShouldRoundTrip()
    {
        IndustrialIoImage image =
            new IndustrialIoImage();

        image.WriteAnalogOutput(
            address: 10,
            value: 12345);

        ushort value =
            image.ReadAnalogOutput(
                address: 10);

        Assert.That(
            value,
            Is.EqualTo(12345));
    }

    [Test]
    public void DigitalOutput_ShouldRoundTrip()
    {
        IndustrialIoImage image =
            new IndustrialIoImage();

        image.WriteDigitalOutput(
            address: 7,
            value: true);

        bool value =
            image.ReadDigitalOutput(
                address: 7);

        Assert.That(
            value,
            Is.True);
    }

    [Test]
    public void InputsAndOutputs_ShouldRemainIndependent()
    {
        IndustrialIoImage image =
            new IndustrialIoImage();

        image.WriteAnalogOutput(
            address: 5,
            value: 1000);

        image.WriteAnalogInput(
            address: 5,
            value: 9000);

        Assert.That(
            image.ReadAnalogOutput(5),
            Is.EqualTo(1000));

        Assert.That(
            image.ReadAnalogInput(5),
            Is.EqualTo(9000));
    }

    [Test]
    public void BlockWriteAndRead_ShouldPreserveValues()
    {
        IndustrialIoImage image =
            new IndustrialIoImage();

        ushort[] values =
        {
            100,
            200,
            300,
            400
        };

        image.WriteAnalogOutputs(
            startAddress: 20,
            values: values);

        ushort[] result =
            image.ReadAnalogOutputs(
                startAddress: 20,
                count: 4);

        Assert.That(
            result,
            Is.EqualTo(values));
    }
}