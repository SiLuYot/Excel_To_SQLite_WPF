//using System.Collections.Generic;
//using System.Text;

//namespace Excel_To_SQLite_WPF
//{
//    public class Version
//    {
//        public string key;
//        public string version;
//        public bool isNeedVersionUp;

//        public Version(string key, string version, bool needVersionUp = false)
//        {
//            this.key = key;
//            this.version = version;
//            this.isNeedVersionUp = needVersionUp;
//        }

//        public Version(Version v)
//        {
//            this.key = v.key;
//            this.version = v.version;
//            this.isNeedVersionUp = v.isNeedVersionUp;
//        }

//        public void VersionUp()
//        {
//            int newVersion = int.Parse(version);
//            newVersion += 1;

//            version = newVersion.ToString();
//        }

//        public Version GetNextVersion()
//        {
//            var newVersion = new Version(this);
//            newVersion.VersionUp();

//            this.isNeedVersionUp = true;

//            return newVersion;
//        }

//        public override string ToString()
//        {
//            return string.Format("{0}_{1}", key, version);
//        }
//    }

//    public class VersionData
//    {
//        private Dictionary<string, Version> versionDic = null;

//        public VersionData()
//        {
//            versionDic = new Dictionary<string, Version>();
//        }

//        public void AddVerionData(string infoString)
//        {
//            if (infoString == string.Empty)
//                return;

//            var infoArray = infoString.Split('/');
//            foreach (var info in infoArray)
//            {
//                if (info != string.Empty)
//                {
//                    var split = info.Split('_');
//                    var fileName = split[0];
//                    var fileVersion = split[1];

//                    AddNewVerionData(fileName, fileVersion);
//                }
//            }
//        }

//        public Version AddNewVerionData(string fileName, string version = "-1")
//        {
//            var value = new Version(fileName, version);
//            if (!versionDic.ContainsKey(fileName))
//            {
//                versionDic.Add(fileName, value);
//            }

//            return value;
//        }

//        public Version GetVersionValue(string key)
//        {
//            if (versionDic.ContainsKey(key))
//            {
//                return versionDic[key];
//            }
//            return null;
//        }

//        public string GetNextVersion(string key)
//        {
//            if (versionDic.ContainsKey(key))
//            {
//                return versionDic[key].GetNextVersion().ToString();
//            }
//            return string.Empty;
//        }

//        public override string ToString()
//        {
//            var sb = new StringBuilder();

//            foreach (var file in versionDic)
//            {
//                var ver = file.Value;
//                if (ver.isNeedVersionUp)
//                {
//                    ver.VersionUp();
//                }

//                sb.AppendFormat("{0}/", ver.ToString());
//            }

//            return sb.ToString();
//        }
//    }
//}
