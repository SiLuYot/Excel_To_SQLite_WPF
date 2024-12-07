using System.Collections.Generic;

namespace Excel_To_SQLite_WPF.GitRespositoryManager.Bitbucket
{
    public class CommitObject
    {
        public string msg;
        public List<UploadFileObject> uploadFileObjectList;
        public List<RemoveFileObject> removeFileObjectList;

        public CommitObject(string msg, List<UploadFileObject> uploadFileObjectList)
        {
            this.msg = msg;
            this.uploadFileObjectList = uploadFileObjectList;
            this.removeFileObjectList = null;
        }

        public CommitObject(string msg, List<RemoveFileObject> removeFileObjectList)
        {
            this.msg = msg;
            this.uploadFileObjectList = null;
            this.removeFileObjectList = removeFileObjectList;
        }
    }
}