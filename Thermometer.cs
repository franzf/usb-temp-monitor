using System;
using System.IO.Ports;
using System.Threading;

namespace UsbTempMonitor;

public class Thermometer : IDisposable
{
    private SerialPort? _port;
    private readonly string _portName;

    public Thermometer(string portName)
    {
        _portName = portName;
    }

    public void Open()
    {
        _port = new SerialPort(_portName, 115200, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
        _port.Open();
    }

    public void Close()
    {
        _port?.Close();
        _port?.Dispose();
        _port = null;
    }

    public void Dispose() => Close();

    public void Detect() => OwReset();

    public double Temperature()
    {
        OwReset();
        OwWrite(0xCC); // Skip ROM
        OwWrite(0x44); // Convert T
        Thread.Sleep(1000);

        OwReset();
        OwWrite(0xCC); // Skip ROM
        OwWrite(0xBE); // Read Scratchpad

        byte[] sp = ReadBytes(9);
        if (Crc8(sp.AsSpan(0, 8)) != sp[8])
            throw new InvalidOperationException("CRC error");

        short raw = BitConverter.ToInt16(sp, 0);
        return raw / 16.0;
    }

    private void OwReset()
    {
        if (_port == null)
            throw new InvalidOperationException("Not connected");

        _port.DiscardInBuffer();
        _port.DiscardOutBuffer();
        _port.BaudRate = 9600;

        _port.Write(new byte[] { 0xF0 }, 0, 1);

        byte[] buf = new byte[1];
        int n = _port.Read(buf, 0, 1);
        _port.BaudRate = 115200;

        if (n != 1)
            throw new InvalidOperationException("Read/write error");

        byte d = buf[0];
        if (d == 0xF0) throw new InvalidOperationException("No device present");
        if (d == 0x00) throw new InvalidOperationException("Short circuit");
        if (d < 0x10 || d > 0xE0) throw new InvalidOperationException($"Presence error: 0x{d:X2}");
    }

    private byte OwWriteByte(byte b)
    {
        byte[] w = new byte[8];
        byte v = b;
        for (int i = 0; i < 8; i++)
        {
            w[i] = (v & 0x01) != 0 ? (byte)0xFF : (byte)0x00;
            v >>= 1;
        }

        _port!.DiscardInBuffer();
        _port.DiscardOutBuffer();
        _port.Write(w, 0, 8);

        byte[] r = new byte[8];
        int total = 0;
        while (total < 8)
        {
            int n = _port.Read(r, total, 8 - total);
            if (n == 0)
                throw new InvalidOperationException("Read timeout");
            total += n;
        }

        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            result >>= 1;
            if (r[i] == 0xFF)
                result |= 0x80;
        }
        return result;
    }

    private void OwWrite(byte b)
    {
        byte got = OwWriteByte(b);
        if (got != b)
            throw new InvalidOperationException($"Expected 0x{b:X2}, got 0x{got:X2}");
    }

    private byte[] ReadBytes(int n)
    {
        byte[] data = new byte[n];
        for (int i = 0; i < n; i++)
            data[i] = OwWriteByte(0xFF);
        return data;
    }

    private static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (byte b in data)
        {
            byte val = b;
            for (int i = 0; i < 8; i++)
            {
                byte mix = (byte)((crc ^ val) & 0x01);
                crc >>= 1;
                if (mix != 0) crc ^= 0x8C;
                val >>= 1;
            }
        }
        return crc;
    }
}
