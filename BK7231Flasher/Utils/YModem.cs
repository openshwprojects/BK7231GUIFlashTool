using BK7231Flasher;
using System;
using System.IO;
using System.IO.Ports;
using System.Text;

public class YModem
{
    SerialPort port;
    byte header_pad = 0x00;
    byte data_pad = 0x1A;
    ILogListener logger;
    const byte SOH = 0x01;
    const byte STX = 0x02;
    const byte EOT = 0x04;
    const byte ACK = 0x06;
    const byte NAK = 0x15;
    const byte CAN = 0x18;
    const byte CRC = (byte)'C';

    public YModem(SerialPort port, ILogListener logger)
    {
        this.port = port;
        this.logger = logger;
    }

    public void abort(int count = 2)
    {
        for (int i = 0; i < count; i++)
            port.Write(new byte[] { CAN }, 0, 1);
    }

    public int send_file(string file_path, bool packet_size_16k = true, int retry = 20)
    {
        FileStream file_stream = null;
        try
        {
            file_stream = new FileStream(file_path, FileMode.Open, FileAccess.Read);
            string file_name = Path.GetFileName(file_path);
            long file_size = new FileInfo(file_path).Length;
            return send(file_stream, file_name, file_size, packet_size_16k, retry);
        }
        catch (IOException e)
        {
            logger.addLog("ERROR: " + e.Message+Environment.NewLine,System.Drawing.Color.Red);
            return -1;
        }
        finally
        {
            if (file_stream != null)
                file_stream.Close();
        }
    }

    public int wait_for_next(byte ch)
    {
        int cancel_count = 0;
        while (true)
        {
            int c = read_byte_timeout(10000);
            if (c == -1) return -1;
            if (c == ch)
                break;
            else if (c == CAN)
            {
                cancel_count++;
                if (cancel_count == 2)
                    return -1;
            }
        }
        return 0;
    }
    public int send(byte[] data, string data_name, long data_size, bool packet_size_16k = true, int retry = 20)
    {
        MemoryStream ms = new MemoryStream(data, 0, (int)data_size);
        return send(ms, data_name, data_size, packet_size_16k, retry);
    }

    public int send(Stream data_stream, 
        string data_name, long data_size, 
        bool packet_size_16k = true,
        int retry = 20, int baudToSet = -1)
    {
        int packet_size = packet_size_16k ? 4096 * 4 : 1024;
        string tg = "";
        Console.WriteLine("YModem::send: v2 will wait for CRC...");
        if (wait_for_next(CRC) != 0)
        {
            Console.WriteLine("YModem::send: v2 wait_for_next CRC failed!");
            return -1;
        }
        Console.WriteLine("YModem::send: v2 wait_for_next CRC ok!");

        byte[] header = _make_edge_packet_header();

        if (data_name.Length > 100)
            data_name = data_name.Substring(0, 100);

        string data_size_str = data_size.ToString();
        if (data_size_str.Length > 20)
            return -2;

        string meta = data_name + '\0' + data_size_str + '\0';
        byte[] meta_bytes = Encoding.ASCII.GetBytes(meta);
        byte[] packet0 = new byte[128];
        Array.Copy(meta_bytes, packet0, meta_bytes.Length);
        for (int i = meta_bytes.Length; i < 128; i++)
            packet0[i] = header_pad;

        byte[] data_for_send = BuildPacket(header, packet0);
      //  Console.WriteLine("YModem::send: first packet " + BitConverter.ToString(data_for_send).Replace("-", " "));
        port.Write(data_for_send, 0, data_for_send.Length);

        Console.WriteLine("YModem::send: v2 will wait for ACK...");
        if (wait_for_next(ACK) != 0)
        {
            Console.WriteLine("YModem::send: v2 wait_for_next ACK failed!");
            return -1;
        }
        Console.WriteLine("YModem::send: v2 wait_for_next ACK ok!");

        Console.WriteLine("YModem::send: v2 will wait for second CRC...");
        if (wait_for_next(CRC) != 0)
        {
            Console.WriteLine("YModem::send: v2 wait_for_next second CRC failed!");
            return -1;
        }
        Console.WriteLine("YModem::send: v2 wait_for_next second CRC ok!");

        int sequence = 1;
        byte[] buffer = new byte[packet_size];

        logger.setState("Writing " + tg + "...", System.Drawing.Color.White);
        while (true)
        {
            logger.setProgress((int)data_stream.Position, (int)data_stream.Length);
            if((sequence % 4) == 1 || packet_size_16k)
            {
                logger.addLog(BaseFlasher.formatHex(data_stream.Position) + "... ", System.Drawing.Color.Black);
            }
            int read = data_stream.Read(buffer, 0, packet_size);
            if (read == 0)
                break;

            if (read <= 128)
                packet_size = 128;

            byte[] data = new byte[packet_size];
            Array.Copy(buffer, 0, data, 0, read);
            for (int i = read; i < packet_size; i++)
                data[i] = data_pad;

            header = _make_data_packet_header(packet_size, sequence);
            data_for_send = BuildPacket(header, data);

            int error_count = 0;
            while (true)
            {
                port.Write(data_for_send, 0, data_for_send.Length);
                int c = read_byte_timeout(10000);
                if (c == ACK)
                    break;
                else
                {
                    error_count++;
                    if (error_count > retry)
                    {
                        abort();
                        return -2;
                    }
                }
            }

            sequence = (sequence + 1) % 0x100;
        }
        logger.setProgress((int)data_stream.Length, (int)data_stream.Length);
        logger.addLog("Done!"+Environment.NewLine,System.Drawing.Color.Black);
        logger.setState("Finalizing " + tg + " write...", System.Drawing.Color.White);
        Console.WriteLine("YModem::send: sending loop done!");

        port.Write(new byte[] { EOT }, 0, 1);
        if (wait_for_next(NAK) != 0) return -1;
        port.Write(new byte[] { EOT }, 0, 1);
        if (wait_for_next(ACK) != 0) return -1;
        if (wait_for_next(CRC) != 0) return -1;

        header = _make_edge_packet_header();
        byte[] final = new byte[128];
        for (int i = 0; i < 128; i++) final[i] = header_pad;
        data_for_send = BuildPacket(header, final);
        port.Write(data_for_send, 0, data_for_send.Length);
        if (wait_for_next(ACK) != 0) return -1;
        if(baudToSet != -1)
        {
            port.BaudRate = baudToSet;
        }
        logger.addLog("Done!" + Environment.NewLine, System.Drawing.Color.Black);
        logger.setState("Writing " + tg + " done!", System.Drawing.Color.White);
        return (int)data_size;
    }

    public int recv_file(string root_path, object callback = null)
    {
        while (true)
        {
            port.Write(new byte[] { CRC }, 0, 1);
            int c = read_byte_timeout(10000);
            if (c == SOH)
                break;
            if (c == STX)
                break;
        }

        bool IS_FIRST_PACKET = true;
        bool FIRST_PACKET_RECEIVED = false;
        bool WAIT_FOR_EOT = false;
        bool WAIT_FOR_END_PACKET = false;
        int sequence = 0;

        FileStream file_stream = null;
        int received_bytes = 0;

        while (true)
        {
            if (WAIT_FOR_EOT)
            {
                wait_for_eot();
                WAIT_FOR_EOT = false;
                WAIT_FOR_END_PACKET = true;
                sequence = 0;
            }
            else
            {
                if (!IS_FIRST_PACKET)
                {
                    int h = wait_for_header();
                    if (h == -1) return -1;
                }
                else
                    IS_FIRST_PACKET = false;

                int seq = read_byte_timeout(5000);
                int seq_oc = read_byte_timeout(5000);
                int packet_size = 1024;
                if (seq == -1 || seq_oc == -1) return -1;

                int header_type = port.ReadByte();
                if (header_type == SOH)
                    packet_size = 128;
                else if (header_type == STX)
                    packet_size = 1024;

                byte[] data = new byte[packet_size + 2];
                int read = port.Read(data, 0, packet_size + 2);
                if (read != packet_size + 2)
                    continue;

                if (seq != (0xFF - seq_oc)) continue;
                if (seq != sequence) continue;

                bool valid = _verify_recv_checksum(data, out byte[] valid_data);
                if (!valid) continue;

                if (seq == 0 && !FIRST_PACKET_RECEIVED && !WAIT_FOR_END_PACKET)
                {
                    port.Write(new byte[] { ACK }, 0, 1);
                    port.Write(new byte[] { CRC }, 0, 1);

                    string[] parts = Encoding.ASCII.GetString(valid_data).TrimEnd('\0').Split('\0');
                    string file_name = parts[0];
                    int file_size = int.Parse(parts[1]);
                    file_stream = new FileStream(Path.Combine(root_path, file_name), FileMode.Create);
                    FIRST_PACKET_RECEIVED = true;
                    sequence = (sequence + 1) % 0x100;
                }
                else if (!WAIT_FOR_END_PACKET)
                {
                    if (file_stream == null)
                        return -2;

                    file_stream.Write(valid_data, 0, valid_data.Length);
                    received_bytes += valid_data.Length;
                    port.Write(new byte[] { ACK }, 0, 1);
                    sequence = (sequence + 1) % 0x100;
                    if (received_bytes >= file_stream.Length)
                        WAIT_FOR_EOT = true;
                }
                else
                {
                    port.Write(new byte[] { ACK }, 0, 1);
                    break;
                }
            }
        }

        if (file_stream != null)
            file_stream.Close();
        return received_bytes;
    }

    public int wait_for_header()
    {
        int cancel_count = 0;
        while (true)
        {
            int c = read_byte_timeout(10000);
            if (c == -1) return -1;
            if (c == SOH || c == STX) return c;
            else if (c == CAN)
            {
                cancel_count++;
                if (cancel_count == 2)
                    return -1;
            }
        }
    }

    public void wait_for_eot()
    {
        int eot_count = 0;
        while (true)
        {
            int c = read_byte_timeout(10000);
            if (c == EOT)
            {
                eot_count++;
                if (eot_count == 1)
                    port.Write(new byte[] { NAK }, 0, 1);
                else if (eot_count == 2)
                {
                    port.Write(new byte[] { ACK }, 0, 1);
                    port.Write(new byte[] { CRC }, 0, 1);
                    break;
                }
            }
        }
    }

    public static byte[] _make_edge_packet_header()
    {
        return new byte[] { SOH, 0x00, 0xFF };
    }

    public byte[] _make_data_packet_header(int packet_size, int sequence)
    {
        byte marker = (packet_size == 128) ? SOH : STX;
        return new byte[] { marker, (byte)sequence, (byte)(0xFF - sequence) };
    }

    //public static void test()
    //{
    //    byte[] h = _make_edge_packet_header();
    //    byte[] data = new byte[] {
    //        0x4C, 0x4E, 0x38, 0x38, 0x32, 0x48, 0x5F, 0x52,
    //        0x41, 0x4D, 0x5F, 0x42, 0x49, 0x4E, 0x2E, 0x62,
    //        0x69, 0x6E, 0x00, 0x33, 0x37, 0x38, 0x37, 0x32,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    //        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    //    };
    //    BuildPacket(h, data);
    //}
   static public byte[] BuildPacket(byte[] header, byte[] data)
    {
        ushort crc = CRC16.Compute(CRC16Type.XMODEM, data);
        byte[] packet = new byte[header.Length + data.Length + 2];
        Array.Copy(header, 0, packet, 0, header.Length);
        Array.Copy(data, 0, packet, header.Length, data.Length);
       // string s = crc.ToString("X2");
        packet[header.Length + data.Length] = (byte)(crc >> 8);
        packet[header.Length + data.Length + 1] = (byte)(crc & 0xFF);
        return packet;
    }

    public bool _verify_recv_checksum(byte[] packet, out byte[] data)
    {
        int len = packet.Length - 2;
        data = new byte[len];
        Array.Copy(packet, 0, data, 0, len);
        ushort their_crc = (ushort)((packet[len] << 8) | packet[len + 1]);
        ushort our_crc = CRC16.Compute(CRC16Type.XMODEM, data);
        return their_crc == our_crc;
    }

    public int read_byte_timeout(int timeout)
    {
        int prevTimeout = port.ReadTimeout;
        port.ReadTimeout = timeout;
        try { return port.ReadByte(); }
        catch { return -1; }
        finally { port.ReadTimeout = prevTimeout; }
    }
}
