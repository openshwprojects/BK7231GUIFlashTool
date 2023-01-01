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

        public MySettings()
        {
            settingsList = new List<KeyValue>();
        }

        public void Add(string key, string value)
        {
            settingsList.Add(new KeyValue(key, value));
        }

        public string FindKeyValue(string key)
        {
            foreach (KeyValue kv in settingsList)
            {
                if (kv.Key == key)
                {
                    return kv.Value;
                }
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
            }
        }
        public void SetKeyValue(string key, string value)
        {
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
                        settingsList.Add(new KeyValue(key, value));
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
