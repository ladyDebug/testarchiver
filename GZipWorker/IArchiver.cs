namespace GZipWorker
{
    public interface IArchiver
    {
        bool Compress(string fileToArchive, string archivedFile);
        bool Decompress(string archivedFile, string unarchivedFile);
    }
}