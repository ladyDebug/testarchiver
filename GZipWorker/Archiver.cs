namespace GZipWorker
{
    public class Archiver
    {
        public Archiver(IArchiver archiver)
        {
            Archiever = archiver;
        }

        public IArchiver Archiever { get; set; }

        public bool Compress(string fileToArchive, string archivedFile)
        {
            return Archiever.Compress(fileToArchive, archivedFile);
        }

        public bool Decompress(string archivedFile, string unarchivedFile)
        {
            return Archiever.Decompress(archivedFile, unarchivedFile);
        }
    }
}