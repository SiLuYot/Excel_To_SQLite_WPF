using Octokit;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.GitRespositoryManager
{
    public class GitHubManager : RespositoryManager
    {
        private GitHubClient client = null;
        public GitHubClient Client => client ?? (client = new GitHubClient(new ProductHeaderValue("Octokit")));

        public override bool IsGetUserSuccess => GetUser != null;
        public override string GetUserName => GetUser.Name;

        public User GetUser { get; private set; }
        public Repository GetRepository { get; private set; }

        public string DefaultBranch => GetRepository?.DefaultBranch;

        public string ReferenceName => string.Format("heads/{0}", DefaultBranch);

        public override string OwnerSpaceName => "SiLuYot";
        public override string RepositoryName => "my_data_repository";

        public override async Task<string> GetCurrentUser(string id, string password)
        {
            //Deprecating password authentication
            //https://developer.github.com/changes/2020-02-14-deprecating-password-auth/
            //var newCredentials = new Credentials(string id, string password);

            var newCredentials = new Credentials("your_personal_access_tokens");
            Client.Credentials = newCredentials;

            try
            {
                GetUser = await Client.User.Current();
            }
            catch (AuthorizationException ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }

        public override async Task<VersionData> GetVersionFile(string[] fileArray, Action<string> updateLabel)
        {
            var versionData = new VersionData();

            try
            {
                updateLabel?.Invoke("Get Repository..");
                GetRepository = await Client.Repository.Get(OwnerSpaceName, RepositoryName);

                updateLabel?.Invoke("Check Version Data..");
                var existingFile = await Client.Repository.Content.GetAllContentsByRef(OwnerSpaceName, RepositoryName, VersionDataPath, ReferenceName);

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

        public override async Task<string> CommitProcess(string[] excelFileArray, string[] dbFileArray, VersionData versionData, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            StringBuilder sb = new StringBuilder();

            var excelMsg = await Commit_Base64(excelFileArray, versionData, updateLabel, updateProgress);
            sb.Append(excelMsg);

            var dbMsg = await Commit_Base64(dbFileArray, versionData, updateLabel, updateProgress);
            sb.Append(dbMsg);

            await UploadVersionFile(versionData, updateLabel);

            return sb.ToString();
        }

        public override Task<string> ClearProcess(VersionData versionData, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            return null;
        }

        public async Task UploadVersionFile(VersionData versionData, Action<string> updateLabel)
        {
            try
            {
                updateLabel?.Invoke("Check Version Data..");
                var existingFile = await Client.Repository.Content.GetAllContentsByRef(OwnerSpaceName, RepositoryName, VersionDataPath, ReferenceName);

                var updateFileRequest = new UpdateFileRequest("update version", versionData.ToString(), existingFile.First().Sha, DefaultBranch);

                updateLabel?.Invoke("Update Version Data..");
                var updateChangeSet = await Client.Repository.Content.UpdateFile(OwnerSpaceName, RepositoryName, VersionDataPath, updateFileRequest);
            }
            catch (Octokit.NotFoundException)
            {
                var createFileRequest = new CreateFileRequest("create version", versionData.ToString(), DefaultBranch);

                updateLabel?.Invoke("Create Version Data..");
                var createChangeSet = await Client.Repository.Content.CreateFile(OwnerSpaceName, RepositoryName, VersionDataPath, createFileRequest);
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
                var masterReference = await Client.Git.Reference.Get(OwnerSpaceName, RepositoryName, ReferenceName);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Get Latest Commit..");
                var latestCommit = await Client.Git.Commit.Get(OwnerSpaceName, RepositoryName, masterReference.Object.Sha);
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
                    var newBlobRef = await Client.Git.Blob.Create(OwnerSpaceName, RepositoryName, newBlob);
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
                var newTree = await Client.Git.Tree.Create(OwnerSpaceName, RepositoryName, nt);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                var newCommit = new NewCommit(st.ToString(), newTree.Sha, masterReference.Object.Sha);

                updateLabel?.Invoke("Create New Commit..");
                var commit = await Client.Git.Commit.Create(OwnerSpaceName, RepositoryName, newCommit);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Update Reference..");
                await Client.Git.Reference.Update(OwnerSpaceName, RepositoryName, ReferenceName, new ReferenceUpdate(commit.Sha));
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