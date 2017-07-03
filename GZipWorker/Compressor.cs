using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipWorker
{
    public class Compressor
    {
        private readonly ConcurrentDictionary<int, byte[]> _chunksDict = new ConcurrentDictionary<int, byte[]>();
        private readonly int _chunkSize;
        private readonly int _threadAmount;
        private volatile int _chunkIndexWritten;
        private volatile int _currentIndex;
        private int _fileChunkCount;
        private volatile bool _result = true;

        public Compressor()
        {
            _threadAmount = SystemInfo.GetCoreAmount() - 1;
            _chunkSize = 128*1024;
        }

        public bool Compress(string fileToArchive, string archivedFile)
        {
            var fileInfo = new FileInfo(fileToArchive);
            _fileChunkCount = (int) Math.Ceiling((decimal) fileInfo.Length/_chunkSize);

            var writeThread = new Thread(() => { StartArchiveWriter(archivedFile); });
            writeThread.Start();
            using (var pool = new CustomThreadPool(_threadAmount - 1))
            {
                
                /*pool.QueueTask(() => { StartArchiveWriter(archivedFile); });*/
                var idx = 0;
                if (!_result)
                {
                    pool.Abort();
                }
                while (idx <= _fileChunkCount && _result)
                {
                    Console.WriteLine("fileChunkCount: {0} chunkIndexWritten: {1}", _fileChunkCount, _chunkIndexWritten);
                    pool.QueueTask(() => { ArchivedByChunk(fileInfo); });
                    idx++;
                }
            }
            writeThread.Join();
            return _result;
        }

        private void ArchivedByChunk(FileInfo fileInfo)
        {
            var localRes = false;
            var index = _currentIndex;
            _currentIndex++;
            using (var stream = fileInfo.OpenRead())
            {
                try
                {
                    stream.Seek(index*_chunkSize, SeekOrigin.Begin);
                    var bytes = new byte[_chunkSize];
                    var read = stream.Read(bytes, 0, _chunkSize);
                    var dest = new byte[read];
                    if (read < bytes.Length)
                    {
                        Array.Copy(bytes, 0, dest, 0, read);
                        bytes = dest;
                    }
                    if (bytes.Length > 0)
                    {
                        ArchiveChunk(bytes, index);
                    }
                    localRes = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                if (!localRes)
                {
                    _result = false;
                }
            }
        }

        private byte[] FormatBytesForIndex(byte[] bytes)
        {
            var completedBytes = new byte[bytes.Length + 4];
            var intBytes = BitConverter.GetBytes(bytes.Length);
            Buffer.BlockCopy(intBytes, 0, completedBytes, 0, intBytes.Length);
            Buffer.BlockCopy(bytes, 0, completedBytes, 4, bytes.Length);
            return completedBytes;
        }

        private void ArchiveChunk(byte[] bytes, int index)
        {
            try
            {
                using (var outStream = new MemoryStream())
                {
                    using (var tinyStream = new GZipStream(outStream, CompressionMode.Compress, false))
                    {
                        tinyStream.Write(bytes, 0, bytes.Length);
                    }
                    var arr = outStream.ToArray();
                    _chunksDict.AddToDictionary(index, arr);
                    Console.WriteLine("Written to dict file chunk index {0} size: {1}", index, arr.Length);
                }
            }
            catch (Exception ex)
            {
                _result = false;
                Console.WriteLine(ex.Message);
            }
        }

        private void StartArchiveWriter(string fileName)
        {
            CreateFile(fileName);
            var res = true;
            while (res && _result)
            {
                if (_chunksDict.ContainsKey(_chunkIndexWritten))
                {
                    var archivedStream = _chunksDict[_chunkIndexWritten];
                    var archivedBytes = FormatBytesForIndex(archivedStream);
                    Write(archivedBytes, fileName);
                    _chunksDict.RemoveFromDictionary(_chunkIndexWritten);
                    _chunkIndexWritten++;
                    if (_chunkIndexWritten >= _fileChunkCount)
                    {
                        res = false;
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void CreateFile(string fileName)
        {
            try
            {
                var fs = new FileStream(fileName, FileMode.Create);
                fs.Write(BitConverter.GetBytes(_fileChunkCount), 0, 4);
                fs.Close();
            }
            catch (IOException exception)
            {
                _result = false;
                Console.WriteLine(exception.Message);
            }
        }

        private void Write(byte[] bytes, string fileName)
        {
            if (bytes.Length <= 0) return;
            Stream stream = new MemoryStream(bytes);
            try
            {
                using (var fsAppend = new FileStream(fileName, FileMode.Append))
                {
                    var buffer = new byte[bytes.Length];
                    if (stream.CanRead)
                    {
                        var readBytes = stream.Read(buffer, 0, buffer.Length);
                        if (readBytes > 0)
                        {
                            fsAppend.Write(buffer, 0, readBytes);
                        }
                        fsAppend.Flush();
                        Console.WriteLine("_chunkIndexWritten: {0}", _chunkIndexWritten);
                    }
                    stream.Close();
                }
            }
            catch (IOException exception)
            {
                _result = false;
                Console.WriteLine(exception.Message);
            }
        }
    }
}