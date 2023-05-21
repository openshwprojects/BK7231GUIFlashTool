using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BK7231Flasher
{
    class KeyValue
    {
        public string Key { get; set; }
        public string Value { get; set; }

        public KeyValue(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }

    class MySettings
    {
        private List<KeyValue> settingsList;
        private List<string> recentTargetIPs = new List<string>();

        public MySettings()
        {
            settingsList = new List<KeyValue>();
        }

        public void Add(string key, string value)
        {
            settingsList.Add(new KeyValue(key, value));
        }

        public string FindKeyValue(string key, string def = null)
        {
            foreach (KeyValue kv in settingsList)
            {
                if (kv.Key == key)
                {
                    return kv.Value;
                }
            }
            if(def != null && def.Length > 0)
            {
                return def;
            }
            return null;
        }

        public void Save(string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                foreach (KeyValue kv in settingsList)
                {
                    sw.WriteLine(kv.Key + "=" + kv.Value);
                }
                foreach(string s in recentTargetIPs)
                {
                    sw.WriteLine("RecentIP=" + s);
                }
            }
        }
        public void addRecentTargetIP(string s)
        {
            recentTargetIPs.Remove(s);
            if(recentTargetIPs.Count > 10)
            {
                recentTargetIPs.RemoveAt(0);
            }
            recentTargetIPs.Add(s);
        }
        public void SetKeyValue(string key, string value)
        {
            if(key == "RecentIP"){
               // recentTargetIPs.Add(key);
                addRecentTargetIP(value);
                return;
            }
            bool found = false;
            foreach (KeyValue kv in settingsList)
            {
                if (kv.Key == key)
                {
                    kv.Value = value;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                settingsList.Add(new KeyValue(key, value));
            }
        }
        public bool HasKey(string key)
        {
            foreach (KeyValue kv in settingsList)
            {
                if (kv.Key == key)
                {
                    return true;
                }
            }
            return false;
        }
        public static MySettings CreateAndLoad(string fileName)
        {
            MySettings settings = new MySettings();
            settings.Load(fileName);
            return settings;
        }
        public void Load(string fileName)
        {
            if (File.Exists(fileName))
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        int index = line.IndexOf('=');
                        string key = line.Substring(0, index);
                        string value = line.Substring(index + 1);
                        SetKeyValue(key, value);
                    }
                }
            }
        }
        public int FindKeyValueInt(string key)
        {
            string value = FindKeyValue(key);
            if (value != null)
            {
                return int.Parse(value);
            }
            return 0;
        }

        internal List<string> getRecentIPs()
        {
            return recentTargetIPs;
        }

        public bool FindKeyValueBool(string key)
        {
            string value = FindKeyValue(key);
            if (value != null)
            {
                return bool.Parse(value);
            }
            return false;
        }
        public float FindKeyValueFloat(string key)
        {
            string value = FindKeyValue(key);
            if (value != null)
            {
                return float.Parse(value);
            }
            return 0;
        }
    }
}
