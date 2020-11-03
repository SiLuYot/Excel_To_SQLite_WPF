using Octokit;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF
{
    public class GitHubManager
    {
        private static GitHubManager instance = null;
        public static GitHubManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GitHubManager();
                }
                return instance;
            }
        }

        private GitHubClient client = null;
        private GitHubClient Client
        {
            get
            {
                return client ?? (client = new GitHubClient(new ProductHeaderValue("Octokit")));
            }
        }

        public bool IsGetUserSuccess
        {
            get => GetUser != null;
        }

        public User GetUser { get; private set; }
        public Repository GetRepository { get; private set; }

        public string DefaultBranch
        {
            get
            {
                return GetRepository?.DefaultBranch;
            }
        }

        public string ReferenceName
        {
            get
            {
                return string.Format("heads/{0}", DefaultBranch);
            }
        }

        public string BaseDataPath
        {
            get
            {
                if (isUnity)
                    return "Assets/StreamingAssets/" + REPO_NAME + "_data";
                else
                    return REPO_NAME + "_data";
            }
        }

        public string VersionDataPath
        {
            get
            {
                return BaseDataPath + "/version.txt";
            }
        }

        private bool isUnity = false;

        public const string OWNER = "SiLuYot";
        public const string REPO_NAME = "my_data_repository";

        public void SetUnityPath(bool isUnity)
        {
            this.isUnity = isUnity;
        }

        public async Task<string> GetCurrentUser(string id, string password)
        {
            User getUser = null;

            var newCredentials = new Credentials(id, password);
            Client.Credentials = newCredentials;

            try
            {
                getUser = await Client.User.Current();
            }
            catch (AuthorizationException ex)
            {
                return ex.Message;
            }
            finally
            {
                GetUser = getUser;
            }

            return string.Empty;
        }

        public async Task<VersionData> GetVersionFile(string[] fileArray, Action<string> updateLabel)
        {
            var versionData = new VersionData();

            try
            {
                updateLabel?.Invoke("Get Repository..");
                GetRepository = await Client.Repository.Get(OWNER, REPO_NAME);

                updateLabel?.Invoke("Check Version Data..");
                var existingFile = await Client.Repository.Content.GetAllContentsByRef(OWNER, REPO_NAME, VersionDataPath, ReferenceName);

                var versionInfo = existingFile.First().Content;
                versionData.AddVerionData(versionInfo);
            }
            catch (NotFoundException)
            {
                foreach (var path in fileArray)
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    versionData.AddNewVerionData(fileName);
                }
            }

            return versionData;
        }

        public async Task UploadVersionFile(VersionData versionData, Action<string> updateLabel)
        {
            try
            {
                updateLabel?.Invoke("Check Version Data..");
                var existingFile = await Client.Repository.Content.GetAllContentsByRef(OWNER, REPO_NAME, VersionDataPath, ReferenceName);

                var updateFileRequest = new UpdateFileRequest("update version", versionData.ToString(), existingFile.First().Sha, DefaultBranch);

                updateLabel?.Invoke("Update Version Data..");
                var updateChangeSet = await Client.Repository.Content.UpdateFile(OWNER, REPO_NAME, VersionDataPath, updateFileRequest);
            }
            catch (Octokit.NotFoundException)
            {
                var createFileRequest = new CreateFileRequest("create version", versionData.ToString(), DefaultBranch);

                updateLabel?.Invoke("Create Version Data..");
                var createChangeSet = await Client.Repository.Content.CreateFile(OWNER, REPO_NAME, VersionDataPath, createFileRequest);
            }
        }

        public async Task<string> Commit_Base64(string[] pathArray, VersionData versionData, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            float awaitCount = 0;
            float awaitMaxCount = 5 + pathArray.Length;

            StringBuilder st = new StringBuilder();
            st.Append("update data / ");

            try
            {
                updateLabel?.Invoke("Get Master Reference..");
                var masterReference = await Client.Git.Reference.Get(OWNER, REPO_NAME, ReferenceName);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Get Latest Commit..");
                var latestCommit = await Client.Git.Commit.Get(OWNER, REPO_NAME, masterReference.Object.Sha);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                var nt = new NewTree
                {
                    BaseTree = latestCommit.Tree.Sha
                };

                foreach (var path in pathArray)
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    var fileExtension = Path.GetExtension(path);

                    var curVersion = versionData.GetVersionValue(fileName);
                    if (curVersion == null)
                    {
                        curVersion = versionData.AddNewVerionData(fileName);
                    }
                    var fileVersionName = versionData.GetNextVersion(fileName);

                    var fileFullName = string.Format("{0}{1}", fileVersionName, fileExtension);

                    fileExtension = fileExtension.Replace(".", "");

                    var fileToBase64 = Convert.ToBase64String(File.ReadAllBytes(path));
                    var newBlob = new NewBlob
                    {
                        Encoding = EncodingType.Base64,
                        Content = fileToBase64
                    };

                    updateLabel?.Invoke(string.Format("Create {0} Blob Ref..", fileFullName));
                    var newBlobRef = await Client.Git.Blob.Create(OWNER, REPO_NAME, newBlob);
                    updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                    var newTreeItem = new NewTreeItem
                    {
                        Path = string.Format("{0}/{1}/{2}/{3}", BaseDataPath, fileExtension, fileName, fileFullName),
                        Mode = "100644",
                        Type = TreeType.Blob,
                        Sha = newBlobRef.Sha
                    };

                    nt.Tree.Add(newTreeItem);
                    st.AppendFormat(" {0} /", fileFullName);
                }

                updateLabel?.Invoke("Create New Tree..");
                var newTree = await Client.Git.Tree.Create(OWNER, REPO_NAME, nt);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                var newCommit = new NewCommit(st.ToString(), newTree.Sha, masterReference.Object.Sha);

                updateLabel?.Invoke("Create New Commit..");
                var commit = await Client.Git.Commit.Create(OWNER, REPO_NAME, newCommit);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Update Reference..");
                await Client.Git.Reference.Update(OWNER, REPO_NAME, ReferenceName, new ReferenceUpdate(commit.Sha));
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

            }
            catch (Exception e)
            {
                return e.Message;
            }

            return string.Empty;
        }
    }
}