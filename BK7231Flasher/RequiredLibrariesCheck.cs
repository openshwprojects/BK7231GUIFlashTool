namespace BK7231Flasher
{
    class RequiredLibrariesCheck
    {
        private static bool bDone = false;
        
        public static bool doCheck()
        {
            if (bDone)
                return false;
            bDone = true;
            //if (File.Exists("Newtonsoft.Json.dll") == false)
            //{
            //    MessageBox.Show("Newtonsoft.Json.dll seems to be missing. This functionality may crash.");
            //    return true;
            //}
            return false;
        }
    }
}
