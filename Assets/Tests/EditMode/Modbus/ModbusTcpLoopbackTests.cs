using System.Net;
using System.Net.Sockets;
using IndustrialSim.Communication.Modbus;
using IndustrialSim.IO;
using NModbus;
using NUnit.Framework;

public sealed class ModbusTcpLoopbackTests
{
    [Test]
    public void Client_ShouldWriteAndReadHoldingRegister()
    {
        int port =
            GetAvailableTcpPort();

        IndustrialIoImage image =
            new IndustrialIoImage();

        using ModbusTcpServer server =
            new ModbusTcpServer(
                image,
                IPAddress.Loopback,
                port,
                unitId: 1);

        server.Start();

        using TcpClient tcpClient =
            new TcpClient();

        tcpClient.Connect(
            IPAddress.Loopback,
            port);

        IModbusFactory factory =
            new ModbusFactory();

        IModbusMaster master =
            factory.CreateMaster(
                tcpClient);

        master.WriteSingleRegister(
            slaveAddress: 1,
            registerAddress: 10,
            value: 54321);

        ushort[] result =
            master.ReadHoldingRegisters(
                slaveAddress: 1,
                startAddress: 10,
                numberOfPoints: 1);

        Assert.That(
            result[0],
            Is.EqualTo(54321));

        Assert.That(
            image.ReadAnalogOutput(10),
            Is.EqualTo(54321));
    }

    [Test]
    public void Client_ShouldWriteAndReadCoil()
    {
        int port =
            GetAvailableTcpPort();

        IndustrialIoImage image =
            new IndustrialIoImage();

        using ModbusTcpServer server =
            new ModbusTcpServer(
                image,
                IPAddress.Loopback,
                port,
                unitId: 1);

        server.Start();

        using TcpClient tcpClient =
            new TcpClient();

        tcpClient.Connect(
            IPAddress.Loopback,
            port);

        IModbusFactory factory =
            new ModbusFactory();

        IModbusMaster master =
            factory.CreateMaster(
                tcpClient);

        master.WriteSingleCoil(
            slaveAddress: 1,
            coilAddress: 5,
            value: true);

        bool[] result =
            master.ReadCoils(
                slaveAddress: 1,
                startAddress: 5,
                numberOfPoints: 1);

        Assert.That(
            result[0],
            Is.True);

        Assert.That(
            image.ReadDigitalOutput(5),
            Is.True);
    }

    [Test]
    public void Client_ShouldReadInputRegisterWrittenBySimulation()
    {
        int port =
            GetAvailableTcpPort();

        IndustrialIoImage image =
            new IndustrialIoImage();

        image.WriteAnalogInput(
            address: 20,
            value: 32100);

        using ModbusTcpServer server =
            new ModbusTcpServer(
                image,
                IPAddress.Loopback,
                port,
                unitId: 1);

        server.Start();

        using TcpClient tcpClient =
            new TcpClient();

        tcpClient.Connect(
            IPAddress.Loopback,
            port);

        IModbusFactory factory =
            new ModbusFactory();

        IModbusMaster master =
            factory.CreateMaster(
                tcpClient);

        ushort[] result =
            master.ReadInputRegisters(
                slaveAddress: 1,
                startAddress: 20,
                numberOfPoints: 1);

        Assert.That(
            result[0],
            Is.EqualTo(32100));
    }


    // ================================================================
    // TEST HELPERS
    // ================================================================

    private static int GetAvailableTcpPort()
    {
        TcpListener listener =
            new TcpListener(
                IPAddress.Loopback,
                0);

        listener.Start();

        int port =
            ((IPEndPoint)listener.LocalEndpoint)
            .Port;

        listener.Stop();

        return port;
    }
}