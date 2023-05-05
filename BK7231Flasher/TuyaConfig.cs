using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace BK7231Flasher
{
    public class TuyaConfig
    {
        // thanks to kmnh/bk7231 tools for figuring out this format
        static string KEY_MASTER = "qwertyuiopasdfgh";
        static int SECTOR_SIZE = 4096;
        static uint MAGIC_FIRST_BLOCK = 0x13579753;
        static uint MAGIC_NEXT_BLOCK = 0x98761234;
        static byte[] KEY_PART_1 = Encoding.ASCII.GetBytes("8710_2M");
        static byte[] KEY_PART_2 = Encoding.ASCII.GetBytes("HHRRQbyemofrtytf");
        static byte[] MAGIC_CONFIG_START = new byte[] { 0x46, 0xDC, 0xED, 0x0E, 0x67, 0x2F, 0x3B, 0x70, 0xAE, 0x12, 0x76, 0xA3, 0xF8, 0x71, 0x2E, 0x03 };

        MemoryStream decrypted;
        byte[] descryptedRaw;
        // Dictionary<string, string> parms = new Dictionary<string, string>();
        List<KeyValue> parms = new List<KeyValue>();

        public bool fromFile(string fname)
        {
            using (var fs = new FileStream(fname, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[fs.Length]; 
                fs.Read(buffer, 0, buffer.Length);
                return fromBytes(buffer);
            }
        }
        public byte []myDecrypt(byte [] data, int baseOffset, int blockIndex, byte [] key)
        {
            byte[] decryptedData = null;
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Mode = CipherMode.ECB;
                    aes.Key = key;
                    aes.Padding = PaddingMode.Zeros;
                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                    using (MemoryStream msDecrypt = new MemoryStream(data, baseOffset + SECTOR_SIZE * blockIndex, SECTOR_SIZE))
                    {
                        using (MemoryStream msPlain = new MemoryStream())
                        {
                            using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                            {
                                byte[] buffer = new byte[1024];
                                int bytesRead;
                                while ((bytesRead = csDecrypt.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    msPlain.Write(buffer, 0, bytesRead);
                                }
                            }
                            decryptedData = msPlain.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return decryptedData;
        }
        public bool fromBytes(byte[] data)
        {
            int needle = MiscUtils.indexOf(data, MAGIC_CONFIG_START);
            needle -= 32;

            byte[] key = null;

            byte[] first = myDecrypt(data, needle, 0, Encoding.ASCII.GetBytes(KEY_MASTER));
            using (BinaryReader br = new BinaryReader(new MemoryStream(first)))
            {
                UInt32 mag = br.ReadUInt32();
                if (mag != MAGIC_FIRST_BLOCK)
                {
                    return true;
                }
                uint crc = br.ReadUInt32();
                key = br.ReadBytes(16);
                if (checkCRC(crc, key, 0, key.Length) == false)
                {
                    return true;
                }
                key = makeSecondaryKey(key);
            }
            decrypted = new MemoryStream();
            BinaryWriter bw = new BinaryWriter(decrypted);
            for (int blockIndex = 1; blockIndex < 500; blockIndex++)
            {
                byte[] next = myDecrypt(data, needle, blockIndex, key);
                if (next == null)
                {
                    break;
                }
                using (BinaryReader br = new BinaryReader(new MemoryStream(next)))
                {
                    UInt32 mag = br.ReadUInt32();
                    if (mag != MAGIC_NEXT_BLOCK)
                    {
                        return true;
                    }
                    uint crc = br.ReadUInt32();
                    if (checkCRC(crc, next, 8, next.Length - 8) == false)
                    {
                        return true;
                    }
                    bw.Write(next, 8, next.Length - 8);
                }
            }
            descryptedRaw = decrypted.ToArray();
            File.WriteAllBytes("lastRawDecryptedStrings.bin", descryptedRaw);
            return false;
        }
        public string getKeysAsJSON()
        {
            string r = "{" + Environment.NewLine;
            for (int i = 0; i < parms.Count; i++)
            {
                var p = parms[i];
                r += "\t\""+p.Key + "\":\"" + p.Value + "\"";
                if (i != parms.Count-1)
                {
                    r += ",";
                }
                r += Environment.NewLine;
            }
            r += "}" + Environment.NewLine;
            return r;
        }
        public bool extractKeys() { 
            int keys_at = MiscUtils.indexOf(descryptedRaw, Encoding.ASCII.GetBytes("ap_s{"));
            if (keys_at == -1)
            {
                return true;
            }
            int stopAT = MiscUtils.findFirst(descryptedRaw, (byte)'}', keys_at);
            if (stopAT == -1)
            {
                return true;
            }
            int first_at = keys_at + 5;
            byte[] str = MiscUtils.subArray(descryptedRaw, first_at, stopAT - first_at);
            string asciiString = Encoding.ASCII.GetString(str);
            string[] pairs = asciiString.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for(int i = 0; i < pairs.Length; i++)
            {
                string []kp = pairs[i].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                string skey = kp[0];
                string svalue = kp[1];
                skey = skey.Trim(new char[] { '"' });
                svalue = svalue.Trim(new char[] { '"' });
                //parms.Add(skey, svalue);
                KeyValue kv = new KeyValue(skey, svalue);
                parms.Add(kv);
            }
            return false;
        }
        byte[] makeSecondaryKey(byte[] innerKey)
        {
            byte[] key = new byte[0x10];
            for (int i = 0; i < 16; i++)
            {
                key[i] = (byte)(KEY_PART_1[i & 3] + KEY_PART_2[i]);
            }
            for (int i = 0; i < 16; i++)
            {
                key[i] = (byte)((key[i] + innerKey[i]) % 256);
            }
            return key;
        }
        bool checkCRC(uint expected, byte [] dat, int ofs, int len)
        {
            uint n = 0;
            for(int i = 0; i < len; i++)
            {
                n += dat[ofs + i];
            }
            n = n & 0xFFFFFFFF;
            if (n == expected)
                return true;
            return false;
        }
    }
}
