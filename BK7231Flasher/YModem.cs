using System;
using System.IO;
using System.IO.Ports;
using System.Text;

public class YModem
{
    SerialPort port;
    byte header_pad = 0x00;
    byte data_pad = 0x1A;

    const byte SOH = 0x01;
    const byte STX = 0x02;
    const byte EOT = 0x04;
    const byte ACK = 0x06;
    const byte NAK = 0x15;
    const byte CAN = 0x18;
    const byte CRC = (byte)'C';

    public YModem(SerialPort port)
    {
        this.port = port;
    }

    public void abort(int count = 2)
    {
        for (int i = 0; i < count; i++)
            port.Write(new byte[] { CAN }, 0, 1);
    }

    public int send_file(string file_path, bool packet_size_16k = true, int retry = 20, object callback = null)
    {
        FileStream file_stream = null;
        try
        {
            file_stream = new FileStream(file_path, FileMode.Open, FileAccess.Read);
            string file_name = Path.GetFileName(file_path);
            long file_size = new FileInfo(file_path).Length;
            return send(file_stream, file_name, file_size, packet_size_16k, retry, callback);
        }
        catch (IOException e)
        {
            Console.WriteLine("ERROR: " + e.Message);
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
            if (c == ch) break;
            else if (c == CAN)
            {
                cancel_count++;
                if (cancel_count == 2)
                    return -1;
            }
        }
        return 0;
    }
    public int send(byte[] data, string data_name, long data_size, bool packet_size_16k = true, int retry = 20, object callback = null)
    {
        MemoryStream ms = new MemoryStream(data, 0, (int)data_size);
        return send(ms, data_name, data_size, packet_size_16k, retry, callback);
    }

    public int send(Stream data_stream, string data_name, long data_size, bool packet_size_16k = true, int retry = 20, object callback = null)
    {
        int packet_size = packet_size_16k ? 4096 * 4 : 1024;

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

        while (true)
        {
            Console.Write("Sending at " + data_stream.Position+"... ");

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
        Console.WriteLine("Done!");
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
        ushort crc = calc_crc(data);
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
        ushort our_crc = calc_crc(data);
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

    public static ushort calc_crc(byte[] data)
    {
        ushort crc = 0;
        foreach (byte b in data)
        {
           // Console.WriteLine(" " + b + " crc " + crc);
            int i = ((crc >> 8) ^ b) & 0xFF;
            crc = (ushort)((crc << 8) ^ crctable[i]);
        }
        return crc;
    }

    static readonly ushort[] crctable = new ushort[256]
    {
        0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
        0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
        0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
        0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
        0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
        0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
        0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
        0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
        0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
        0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
        0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
        0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
        0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
        0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
        0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
        0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
        0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
        0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
        0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
        0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
        0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
        0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
        0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
        0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
        0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
        0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
        0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
        0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
        0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
        0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
        0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
        0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0,
    };
}
