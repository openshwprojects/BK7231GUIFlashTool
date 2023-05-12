using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BK7231Flasher
{
    public delegate void ProcessJSONReply(JObject json);
    public delegate void ProcessBytesReply(byte[] data);
    public class OBKDeviceAPI
    {
        int userIndex;
        string adr;
        JObject info;

        class GetFlashChunkArguments
        {
            public int adr;
            public int size;
            public ProcessBytesReply cb;
        }
        public void setUserIndex(int i)
        {
            userIndex = i;
        }
        public int getUserIndex()
        {
            return userIndex;
        }
        public void setAdr(string s)
        {
            adr = s;
        }
        public OBKDeviceAPI()
        {
            this.adr = "";
        }
        public OBKDeviceAPI(string na)
        {
            this.adr = na;
        }
        private byte []sendGetInternal(string path)
        {
            byte[] ret = null;
            try
            {
                using (var tcp = new TcpClient(adr, 80))
                using (var stream = tcp.GetStream())
                {
                    tcp.SendTimeout = 500;
                    tcp.ReceiveTimeout = 1000;
                    // Send request headers
                    var builder = new StringBuilder();
                    builder.AppendLine("GET " + path + " HTTP/1.1");
                    //builder.AppendLine("Host: any.com");
                    //builder.AppendLine("Content-Length: " + data.Length);   // only for POST request
                    builder.AppendLine("Connection: close");
                    builder.AppendLine();
                    var header = Encoding.ASCII.GetBytes(builder.ToString());
                    stream.Write(header, 0, header.Length);
                    // receive data
                    using (var memory = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memory.Write(buffer, 0, bytesRead);
                        }
                        memory.Position = 0;
                        byte[] data = memory.ToArray();

                        var index = BinaryMatch(data, Encoding.ASCII.GetBytes("\r\n\r\n")) + 4;
                        var headers = Encoding.ASCII.GetString(data, 0, index);
                        memory.Position = index;

                        ret = MiscUtils.subArray(data, index, data.Length - index);
                    }
                }
            }
            catch(Exception ex)
            {

            }
            return ret;
        }

        internal bool hasAdr(string s)
        {
            return this.adr == s;
        }

        internal string getAdr()
        {
            return adr;
        }

        internal string getShortName()
        {
            return info["shortName"].ToString();
        }
        internal string getChipSet()
        {
            return info["chipset"].ToString();
        }
        internal string getMAC()
        {
            return info["mac"].ToString();
        }
        internal string getBuild()
        {
            return info["build"].ToString();
        }

        internal JObject getInfo()
        {
            return info;
        }

        internal void clear()
        {
            adr = "";
            info = null;
        }

        private string sendGet(string path)
        {
            byte[] res = sendGetInternal(path);
            string sResult = "";
            if (res != null)
            {
                sResult = Encoding.UTF8.GetString(res);
            }
            return sResult;
        }
        string escapeString(string s)
        {
            return Uri.EscapeDataString(s);
        }
        public void SendGetRequestTasmotaStatus(object ocb)
        {
            string jsonText = sendGet("/cm?cmnd - TODO");
            ProcessJSONReply cb = ocb as ProcessJSONReply;
            // Parse the response as a JSON object
            JObject jsonObject = null;
            try
            {
                jsonObject = JObject.Parse(jsonText);
            }
            catch (Exception ex)
            {

            }
            if (cb != null)
            {
                cb(jsonObject);
            }
            this.info = jsonObject;
        }
        public void SendGetRequestJSON(object ocb)
        {
            string jsonText = sendGet("/api/info");
            ProcessJSONReply cb = ocb as ProcessJSONReply;
            // Parse the response as a JSON object
            JObject jsonObject = null;
            try
            {
                jsonObject = JObject.Parse(jsonText);
            }
            catch(Exception ex)
            {

            }
            if (cb != null)
            {
                cb(jsonObject);
            }
            this.info = jsonObject;
        }
        public void SendGetRequestBytes(object obj)
        {
            GetFlashChunkArguments arg = obj as GetFlashChunkArguments;
            int size = arg.size;
            int adr = arg.adr;
            string hexString = string.Format("/api/flash/{0:X}-{1:X}", adr, size);
            //byte [] flash = sendGetInternal("/api/flash/1e3000-2000");
            byte[] flash = sendGetInternal(hexString);
            if (arg.cb != null)
            {
                arg.cb(flash);
            }
        }
        public void sendGetInfo(ProcessJSONReply cb)
        {
            startThread(SendGetRequestJSON, cb);
        }
        public void sendGetFlashChunk(ProcessBytesReply cb, int adr, int size)
        {
            GetFlashChunkArguments arg = new GetFlashChunkArguments();
            arg.cb = cb;
            arg.adr = adr;
            arg.size = size;
            startThread(SendGetRequestBytes, arg);
        }
        private void startThread(System.Threading.ParameterizedThreadStart th, object arg)
        { 
            System.Threading.Thread thread = new System.Threading.Thread(th);
            thread.Start(arg);
        }
        private static int BinaryMatch(byte[] input, byte[] pattern)
        {
            int sLen = input.Length - pattern.Length + 1;
            for (int i = 0; i < sLen; ++i)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; ++j)
                {
                    if (input[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
