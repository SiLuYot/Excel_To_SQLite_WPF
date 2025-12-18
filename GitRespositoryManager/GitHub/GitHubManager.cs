using Excel_To_SQLite_WPF.Repository;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.GitRespositoryManager.GitHub
{
    public class GitHubManager : RepositoryManager
    {
        private GitHubClient _client;
        private User _user;

        private string _ownerSpaceName;
        private string _repositoryName;
        private string _branchName;

        public string ReferenceName => string.Format("heads/{0}", _branchName);

        public override bool IsGetUserSuccess => _user != null;
        public override string GetUserName => _user.Name;
        public override string OwnerSpaceName => _ownerSpaceName;
        public override string RepositoryName => _repositoryName;

        public GitHubManager(string ownerSpaceName, string repositoryName, string branchName)
        {
            _client = new GitHubClient(new ProductHeaderValue("Octokit"));

            _ownerSpaceName = ownerSpaceName;
            _repositoryName = repositoryName;
            _branchName = branchName;
        }

        public override async Task<string> GetCurrentUser(string token, string id, string password)
        {
            //Deprecating password authentication
            //https://developer.github.com/changes/2020-02-14-deprecating-password-auth/
            //var newCredentials = new Credentials(string id, string password);

            var newCredentials = new Credentials(token);
            _client.Credentials = newCredentials;

            try
            {
                _user = await _client.User.Current();
            }
            catch (AuthorizationException ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }

        public override async Task<string> GetFileContent(string path)
        {
            try
            {
                var contents = await _client.Repository.Content.GetAllContentsByRef(OwnerSpaceName, RepositoryName, path, _branchName);
                var file = contents.FirstOrDefault(c => c.Type == ContentType.File);
                return file?.Content;
            }
            catch (Exception e)
            {
                return "ERROR:" + e.Message;
            }
        }

        public override async Task<string> CommitProcess(string[] excelArray, string[] dbArray, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            var sb = new StringBuilder();
            var pathArray = excelArray.Concat(dbArray);

            var newTreeItemList = await CreateNewTreeItemList(sb, pathArray);

            return await CommitNewTreeItem(newTreeItemList, sb, updateLabel, updateProgress);
        }

        public override async Task<List<string>> GetBranches()
        {
            try
            {
                var branches = await _client.Repository.Branch.GetAll(OwnerSpaceName, RepositoryName);
                return branches.Select(b => b.Name).ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        public override string GetCurrentBranch()
        {
            return _branchName;
        }

        public override void SetBranch(string branchName)
        {
            _branchName = branchName;
        }

        private async Task<List<NewTreeItem>> CreateNewTreeItemList(StringBuilder sb, IEnumerable<string> pathArray)
        {
            var newTreeItemList = new List<NewTreeItem>();
            sb.Append("[Update Data] ");

            foreach (var path in pathArray)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var fileExtension = Path.GetExtension(path);

                var fileFullName = string.Format("{0}{1}", fileName, fileExtension);
                var fullPath = string.Empty;

                if (fileExtension == ".cs")
                    fullPath = string.Format("{0}/{1}", CodePath, fileFullName);
                else
                    fullPath = string.Format("{0}/{1}/{2}", DataPath, fileExtension.Replace(".", ""), fileFullName);

                var fileToBase64 = Convert.ToBase64String(File.ReadAllBytes(path));
                var newBlob = new NewBlob
                {
                    Encoding = EncodingType.Base64,
                    Content = fileToBase64
                };

                var newBlobRef = await _client.Git.Blob.Create(OwnerSpaceName, RepositoryName, newBlob);
                var newTreeItem = new NewTreeItem
                {
                    Path = fullPath,
                    Mode = "100644",
                    Type = TreeType.Blob,
                    Sha = newBlobRef.Sha
                };

                newTreeItemList.Add(newTreeItem);
                sb.AppendFormat(" {0} /", fileFullName);
            }

            return newTreeItemList;
        }

        private async Task<string> CommitNewTreeItem(List<NewTreeItem> list, StringBuilder sb, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            float awaitCount = 0;
            float awaitMaxCount = 5 + list.Count;

            try
            {
                updateLabel?.Invoke("Get Branch Reference..");
                var masterReference = await _client.Git.Reference.Get(OwnerSpaceName, RepositoryName, ReferenceName);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Get Latest Commit..");
                var latestCommit = await _client.Git.Commit.Get(OwnerSpaceName, RepositoryName, masterReference.Object.Sha);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                var nt = new NewTree
                {
                    BaseTree = latestCommit.Tree.Sha
                };

                foreach (var item in list)
                {
                    nt.Tree.Add(item);
                }

                updateLabel?.Invoke("Create New Tree..");
                var newTree = await _client.Git.Tree.Create(OwnerSpaceName, RepositoryName, nt);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                var newCommit = new NewCommit(sb.ToString(), newTree.Sha, masterReference.Object.Sha);

                updateLabel?.Invoke("Create New Commit..");
                var commit = await _client.Git.Commit.Create(OwnerSpaceName, RepositoryName, newCommit);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Update Reference..");
                await _client.Git.Reference.Update(OwnerSpaceName, RepositoryName, ReferenceName, new ReferenceUpdate(commit.Sha));
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