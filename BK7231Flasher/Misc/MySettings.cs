using System.Collections.Generic;
using System.IO;

namespace BK7231Flasher
{
    class MySettings
    {
        private Dictionary<string, string> SettingsList { get; }

        private List<string> RecentTargetIPs { get; }

        public MySettings()
        {
            SettingsList = new Dictionary<string, string>();
            RecentTargetIPs = new List<string>();
        }

        public string FindKeyValue(string key, string def = null)
        {
            if(SettingsList.TryGetValue(key, out var value)) return value;

            if(def != null && def.Length > 0)
            {
                return def;
            }
            return null;
        }

        public void Save(string fileName)
        {
            using StreamWriter sw = new StreamWriter(fileName);
            foreach(var kv in SettingsList)
            {
                sw.WriteLine(kv.Key + "=" + kv.Value);
            }
            foreach(string s in RecentTargetIPs)
            {
                sw.WriteLine("RecentIP=" + s);
            }
        }

        public void addRecentTargetIP(string s)
        {
            RecentTargetIPs.Remove(s);
            if(RecentTargetIPs.Count > 10)
            {
                RecentTargetIPs.RemoveAt(0);
            }
            RecentTargetIPs.Add(s);
        }

        public void SetKeyValue(string key, string value)
        {
            if(key == "RecentIP"){
               // recentTargetIPs.Add(key);
                addRecentTargetIP(value);
                return;
            }
            if(SettingsList.ContainsKey(key))
            {
                SettingsList[key] = value;
            }
            else
            {
                SettingsList.Add(key, value);
            }
        }

        public bool HasKey(string key) => SettingsList.ContainsKey(key);

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
                using StreamReader sr = new StreamReader(fileName);
                while(!sr.EndOfStream)
                {
                    string line = sr.ReadLine();
                    int index = line.IndexOf('=');
                    string key = line.Substring(0, index);
                    string value = line.Substring(index + 1);
                    SetKeyValue(key, value);
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
            return RecentTargetIPs;
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
