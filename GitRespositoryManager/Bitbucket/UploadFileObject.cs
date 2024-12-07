namespace Excel_To_SQLite_WPF.GitRespositoryManager.Bitbucket
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
}