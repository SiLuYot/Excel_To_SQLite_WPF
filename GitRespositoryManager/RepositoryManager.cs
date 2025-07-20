using System;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.Repository
{
    public abstract class RepositoryManager
    {
        private static RepositoryManager instance;
        protected bool isUnity = false;

        public static void SetManager(RepositoryManager instance)
        {
            RepositoryManager.instance = instance;
        }

        public static RepositoryManager GetManager()
        {
            return instance;
        }

        public void SetUnityPath(bool isUnity)
        {
            this.isUnity = isUnity;
        }

        public string DataPath
        {
            get
            {
                if (isUnity)
                    return "Assets/StreamingAssets/" + DataDirectoryName;
                else
                    return DataDirectoryName;
            }
        }

        public string CodePath
        {
            get
            {
                if (isUnity)
                    return "Assets/Scripts/" + DataDirectoryName;
                else
                    return DataDirectoryName;
            }
        }

        protected string DataDirectoryName => RepositoryName + "_data";

        public abstract bool IsGetUserSuccess { get; }
        public abstract string GetUserName { get; }

        //owner / work space
        public abstract string OwnerSpaceName { get; }
        //repository name / repository slug
        public abstract string RepositoryName { get; }

        public abstract Task<string> GetCurrentUser(string token, string id, string password);

        public abstract Task<string> GetFileContent(string path);

        public abstract Task<string> CommitProcess(string[] excelFileArray, string[] dbFileArray, Action<string> updateLabel, Action<float, float> updateProgress);
    }
}
