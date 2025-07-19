using Excel_To_SQLite_WPF.Repository;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.GitRespositoryManager.Bitbucket
{
    public class BitbucketManager : RepositoryManager
    {
        private RestClient _client;

        private string _ownerSpaceName;
        private string _repositoryName;
        private string _branchName;

        private string _userName = string.Empty;
        private string _email = string.Empty;
        private string _hash = string.Empty;

        private float _awaitCount = 0;
        private float _awaitMaxCount = 0;

        private List<CommitObject> _commitObjects = new List<CommitObject>();

        public override bool IsGetUserSuccess => _userName != string.Empty;
        public override string GetUserName => _userName;
        public override string OwnerSpaceName => _ownerSpaceName;
        public override string RepositoryName => _repositoryName;

        public BitbucketManager(string ownerSpaceName, string repositoryName, string branchName)
        {
            _ownerSpaceName = ownerSpaceName;
            _repositoryName = repositoryName;
            _branchName = branchName;
        }

        public override async Task<string> GetCurrentUser(string token, string id, string appPassword)
        {
            //https://bitbucket.org/account/settings/app-passwords/
            var clientOptions = new RestClientOptions("https://api.bitbucket.org/2.0/")
            {
                Authenticator = new HttpBasicAuthenticator(id, appPassword)
            };
            _client = new RestClient(clientOptions);

            try
            {
                var infoMsg = await RequestUserInfo();
                if (infoMsg != string.Empty)
                {
                    return infoMsg + " 로그인 실패";
                }

                var emailMsg = await RequestUserEmail();
                if (emailMsg != string.Empty)
                {
                    return emailMsg + " 로그인 실패";
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return string.Empty;
        }

        public override async Task<string> CommitProcess(string[] excelPaths, string[] filePaths, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            var paths = excelPaths.Concat(filePaths).ToArray();

            CreateUploadCommit(paths);

            return await PushCommit(3, RequestUploadFileCommit, updateLabel, updateProgress);
        }

        public void CreateUploadCommit(string[] dataPaths)
        {
            _commitObjects.Clear();

            var uploadFileObjects = new List<UploadFileObject>();

            var sb = new StringBuilder();
            sb.Append("[Update Data] ");

            foreach (var path in dataPaths)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var fileExtension = Path.GetExtension(path);

                var fileFullName = string.Format("{0}{1}", fileName, fileExtension);
                var fullPath = string.Empty;

                if (fileExtension == ".cs")
                    fullPath = string.Format("{0}/{1}", CodePath, fileFullName);
                else
                    fullPath = string.Format("{0}/{1}/{2}", DataPath, fileExtension.Replace(".", ""), fileFullName);

                uploadFileObjects.Add(new UploadFileObject(fullPath, fileFullName, File.ReadAllBytes(path)));
                sb.AppendFormat(" {0}, ", fileFullName);
            }

            _commitObjects.Add(new CommitObject(sb.ToString(), uploadFileObjects));
        }

        public async Task<string> PushCommit(
            int basicAwaitCount,
            Func<Action<string>,
            Action<float, float>,
            Task<string>> requestCommit,
            Action<string> updateLabel,
            Action<float, float> updateProgress)
        {
            _awaitCount = 0;
            _awaitMaxCount = basicAwaitCount + _commitObjects.Count;

            var sb = new StringBuilder();

            try
            {
                updateLabel?.Invoke("Get Branch Hash..");
                await RequestUpdateHash();
                updateProgress?.Invoke(++_awaitCount, _awaitMaxCount);

                updateLabel?.Invoke("Commit..");
                var msg = await requestCommit?.Invoke(updateLabel, updateProgress);
                sb.Append(msg);

                updateProgress?.Invoke(++_awaitCount, _awaitMaxCount);
            }
            catch (Exception e)
            {
                sb.Append(e.Message);
                return sb.ToString();
            }

            return sb.ToString();
        }

        public async Task<string> RequestUserInfo()
        {
            var request = new RestRequest("user");
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var jObject = JObject.Parse(response.Content);
                _userName = (string)jObject["username"];

                return string.Empty;
            }
            else return response.StatusDescription;
        }

        public async Task<string> RequestUserEmail()
        {
            var request = new RestRequest("user/emails");
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var jObject = JObject.Parse(response.Content);
                var jArray = JArray.Parse(jObject["values"].ToString());
                _email = jArray.First["email"].ToString();

                return string.Empty;
            }
            else return response.StatusDescription;
        }

        public async Task RequestUpdateHash()
        {
            var request = new RestRequest(string.Format("repositories/{0}/{1}/refs/branches/{2}", OwnerSpaceName, RepositoryName, _branchName));
            var response = await _client.ExecuteAsync(request);

            string branchJson = response.Content;

            var jObj = JObject.Parse(branchJson);
            if (jObj.ContainsKey("target"))
            {
                var target = jObj["target"];
                _hash = target.Value<string>("hash");
            }
        }

        public async Task<string> RequestUploadFileCommit(Action<string> updateLabel, Action<float, float> updateProgress)
        {
            var sb = new StringBuilder();
            var uploadFileObjectList = new List<UploadFileObject>();

            foreach (var obj in _commitObjects)
            {
                sb.AppendLine(obj.msg);
                uploadFileObjectList.AddRange(obj.uploadFileObjectList);
            }

            var request = new RestRequest(string.Format("repositories/{0}/{1}/src", OwnerSpaceName, RepositoryName));
            request.Method = Method.Post;
            request.AddParameter("message", sb.ToString());
            request.AddParameter("author", string.Format("{0} <{1}>", _userName, _email));
            request.AddParameter("parents", _hash);
            request.AddParameter("branch", _branchName);

            foreach (var obj in uploadFileObjectList)
            {
                request.AddFile(obj.path, obj.byteArray, obj.fileName, "multipart/form-data");
            }

            updateLabel?.Invoke("Push..");
            await _client.ExecuteAsync(request);
            updateProgress?.Invoke(++_awaitCount, _awaitMaxCount);

            return string.Empty;
        }

        public async Task<string> RequestFindFile(string path)
        {
            var request = new RestRequest(string.Format("repositories/{0}/{1}/src/{2}/{3}", OwnerSpaceName, RepositoryName, _hash, path));
            var response = await _client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response.Content;
            }
            else return string.Empty;
        }
    }
}
