using System;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.GitRespositoryManager
{
    public abstract class RespositoryManager
    {
        private static RespositoryManager instance;
        protected bool isUnity = false;

        public static void SetManager(RespositoryManager instance)
        {
            RespositoryManager.instance = instance;
        }

        public static RespositoryManager GetManager()
        {
            return instance;
        }

        public void SetUnityPath(bool isUnity)
        {
            this.isUnity = isUnity;
        }

        protected string BaseDataPath
        {
            get
            {
                if (isUnity)
                    return "Assets/StreamingAssets/" + RepositoryName + "_data";
                else
                    return RepositoryName + "_data";
            }
        }

        protected string VersionDataPath
        {
            get
            {
                return BaseDataPath + "/version.txt";
            }
        }

        public abstract bool IsGetUserSuccess { get; }
        public abstract string GetUserName { get; }

        //owner / work space
        public abstract string OwnerSpaceName { get; }
        //repository name / repository slug
        public abstract string RepositoryName { get; }

        public abstract Task<string> GetCurrentUser(string id, string password);

        public abstract Task<VersionData> GetVersionFile(string[] fileArray, Action<string> updateLabel);

        public abstract Task<string> CommitProcess(string[] excelFileArray, string[] dbFileArray, VersionData versionData, Action<string> updateLabel, Action<float, float> updateProgress);
    }
}
