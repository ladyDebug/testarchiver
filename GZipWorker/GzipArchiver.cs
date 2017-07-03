namespace GZipWorker
{
    public class GzipArchiver: IArchiver
    {
        private readonly Compressor _compressor;
        private readonly Decompressor _decompressor;

        public GzipArchiver()
        {
            _compressor = new Compressor();
            _decompressor = new Decompressor();
        }

        public bool Compress(string fileToArchive, string archivedFile)
        {
            var res = _compressor.Compress(fileToArchive, archivedFile);
            return res;
        }

        public bool Decompress(string archivedFile, string unarchivedFile)
        {
            var res = _decompressor.Decompress(archivedFile, unarchivedFile);
            return res;
        }
    }
}