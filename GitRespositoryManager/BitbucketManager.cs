﻿using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Excel_To_SQLite_WPF.GitRespositoryManager
{
    public class UploadFileObject
    {
        public string path;
        public string fileName;
        public byte[] byteArray;

        public UploadFileObject(string path, string fileName, byte[] byteArray)
        {
            this.path = path;
            this.fileName = fileName;
            this.byteArray = byteArray;
        }
    }

    public class CommitObject
    {
        public string msg;
        public List<UploadFileObject> uploadFileObjectList;

        public CommitObject(string msg, List<UploadFileObject> uploadFileObjectList)
        {
            this.msg = msg;
            this.uploadFileObjectList = uploadFileObjectList;
        }
    }

    public class BitbucketManager : RespositoryManager
    {                
        private RestClient client = null;
        public RestClient Client => client ?? (client = new RestClient("https://api.bitbucket.org/2.0/"));

        private List<CommitObject> commitObjectList = null;
        public List<CommitObject> CommitObjectList
        {
            get
            {
                return commitObjectList ?? (commitObjectList = new List<CommitObject>());
            }
        }
        public override bool IsGetUserSuccess => userName != string.Empty;
        public override string GetUserName => userName;

        private string userName = string.Empty;
        private string email = string.Empty;
        private string hash = string.Empty;

        float awaitCount = 0;
        float awaitMaxCount = 0;        

        public override string OwnerSpaceName => "siluyot";
        public override string RepositoryName => "test";

        public override async Task<string> GetCurrentUser(string id, string password)
        {
            //비트버킷 계정이 구글계정 혹은 
            //다른 사이트인 경우 비밀번호 재설정 필요
            //https://community.atlassian.com/t5/Bitbucket-questions/REST-API-authentication-with-Google-Account/qaq-p/1497836

            Client.Authenticator = new HttpBasicAuthenticator(id, password);

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

        public override async Task<VersionData> GetVersionFile(string[] fileArray, Action<string> updateLabel)
        {
            var versionData = new VersionData();
            string responseString = string.Empty;

            try
            {
                updateLabel?.Invoke("Get Master Branches..");
                await RequestUpdateHash();

                updateLabel?.Invoke("Check Version Data..");
                responseString = await RequestFindFile(VersionDataPath);
            }
            catch (Exception e)
            {

            }

            if (responseString == string.Empty)
            {
                foreach (var path in fileArray)
                {
                    var fileName = Path.GetFileNameWithoutExtension(path);
                    versionData.AddNewVerionData(fileName);
                }
            }
            else versionData.AddVerionData(responseString);

            return versionData;
        }

        public override async Task<string> CommitProcess(string[] excelFileArray, string[] dbFileArray, VersionData versionData, Action<string> updateLabel, Action<float, float> updateProgress)
        {
            InitCommitList();
            CreateCommit(excelFileArray, versionData);
            CreateCommit(dbFileArray, versionData);
            CreateCommit(versionData);

            return await PushCommit(updateLabel, updateProgress);
        }

        public void InitCommitList()
        {
            CommitObjectList.Clear();
        }

        public void CreateCommit(VersionData versionData)
        {
            byte[] strByte = Encoding.UTF8.GetBytes(versionData.ToString());
            var uploadFileObject = new UploadFileObject(VersionDataPath, "version.txt", strByte);

            CommitObjectList.Add(new CommitObject("Update Version Data", new List<UploadFileObject>() { uploadFileObject }));
        }

        public void CreateCommit(string[] pathArray, VersionData versionData)
        {
            var uploadFileObjectList = new List<UploadFileObject>();

            var st = new StringBuilder();
            st.Append("update data / ");

            foreach (var path in pathArray)
            {
                var fileName = Path.GetFileNameWithoutExtension(path);
                var fileExtension = Path.GetExtension(path);

                var fileVersionName = versionData.GetNextVersion(fileName);
                var fileFullName = string.Format("{0}{1}", fileVersionName, fileExtension);

                fileExtension = fileExtension.Replace(".", "");

                var byteArray = File.ReadAllBytes(path);
                var fullPath = string.Format("{0}/{1}/{2}/{3}", BaseDataPath, fileExtension, fileName, fileFullName);

                uploadFileObjectList.Add(new UploadFileObject(fullPath, fileFullName, byteArray));

                st.AppendFormat(" {0} /", fileFullName);
            }

            CommitObjectList.Add(new CommitObject(st.ToString(), uploadFileObjectList));
        }

        public async Task<string> PushCommit(Action<string> updateLabel, Action<float, float> updateProgress)
        {
            awaitCount = 0;
            awaitMaxCount = 3 + CommitObjectList.Count;

            try
            {
                updateLabel?.Invoke("Get Master Branches..");
                await RequestUpdateHash();
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);

                updateLabel?.Invoke("Commit..");
                await RequestCommit(updateLabel, updateProgress);
                updateProgress?.Invoke(++awaitCount, awaitMaxCount);
            }
            catch (Exception e)
            {
                return e.Message;
            }

            return string.Empty;
        }

        public async Task<string> RequestUserInfo()
        {
            var request = new RestRequest("user");
            var response = await Client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                JObject jObject = JObject.Parse(response.Content);
                userName = (string)jObject["username"];

                return string.Empty;
            }
            else return response.StatusDescription;
        }

        public async Task<string> RequestUserEmail()
        {
            var request = new RestRequest("user/emails");
            var response = await Client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var jObject = JObject.Parse(response.Content);
                var jArray = JArray.Parse(jObject["values"].ToString());
                email = jArray.First["email"].ToString();

                return string.Empty;
            }
            else return response.StatusDescription;
        }

        public async Task RequestUpdateHash()
        {
            var request = new RestRequest(string.Format("repositories/{0}/{1}/refs/branches/master", OwnerSpaceName, RepositoryName));
            var response = await Client.ExecuteAsync(request);

            string branchJson = response.Content;

            var jObj = JObject.Parse(branchJson);
            if (jObj.ContainsKey("target"))
            {
                var target = jObj["target"];
                hash = target.Value<string>("hash");
            }
        }

        public async Task RequestCommit(Action<string> updateLabel, Action<float, float> updateProgress)
        {
            StringBuilder sb = new StringBuilder();
            List<UploadFileObject> uploadFileObjectList = new List<UploadFileObject>();

            foreach (var obj in CommitObjectList)
            {
                sb.AppendLine(obj.msg);
                uploadFileObjectList.AddRange(obj.uploadFileObjectList);
            }

            var request = new RestRequest(string.Format("repositories/{0}/{1}/src", OwnerSpaceName, RepositoryName));
            request.Method = Method.POST;
            request.AddParameter("message", sb.ToString());
            request.AddParameter("author", string.Format("{0} <{1}>", userName, email));
            request.AddParameter("parents", hash);
            request.AddParameter("branch", "master");

            foreach (var obj in uploadFileObjectList)
            {
                request.AddFileBytes(obj.path, obj.byteArray, obj.fileName, "multipart/form-data");
            }

            updateLabel?.Invoke("Push..");
            await Client.ExecuteAsync(request);
            updateProgress?.Invoke(++awaitCount, awaitMaxCount);
        }

        public async Task<string> RequestFindFile(string path)
        {
            var request = new RestRequest(string.Format("repositories/{0}/{1}/src/{2}/{3}", OwnerSpaceName, RepositoryName, hash, path));
            var response = await Client.ExecuteAsync(request);

            if (response.StatusCode == HttpStatusCode.OK)
            {
                return response.Content;
            }
            else return string.Empty;
        }
    }
}
