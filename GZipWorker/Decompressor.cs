using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace GZipWorker
{
    public class Decompressor
    {
        private readonly ConcurrentDictionary<int, byte[]> _chunksDict = new ConcurrentDictionary<int, byte[]>();

        private readonly ConcurrentDictionary<int, byte[]> _decompressedChunksDict =
            new ConcurrentDictionary<int, byte[]>();

        private readonly object _lockObg = new object();
        private volatile int _chunkIndexWritten;
        private volatile int _currentIndex;
        private volatile int _decompressedCount;
        private volatile int _fileChunkCount;
        private volatile bool _result = true;
        private volatile int _threadAmount;

        public Decompressor()
        {
            _threadAmount = SystemInfo.GetCoreAmount() - 1;
        }

        public bool Decompress(string archivedFile, string unarchivedFile)
        {
            var fileInfo = new FileInfo(archivedFile);
            ReadChunkCount(fileInfo);
            new Thread(() => { ReadArchivedFile(fileInfo); }).Start();
            var writeThread = new Thread(() => { WriteUnarchivedFile(unarchivedFile); });
            writeThread.Start();
            using (var pool = new CustomThreadPool(_threadAmount -2))
            {
                var res = true;
                while (res && _result)
                {
                    pool.QueueTask(RunDecompressor);
                    _decompressedCount++;
                    if (_decompressedCount >= _fileChunkCount)
                    {
                        res = false;
                    }
                }
                if (!_result)
                {
                    pool.Abort();
                }
            }
            writeThread.Join();
            return _result;
        }

        private void ReadChunkCount(FileInfo fileInfo)
        {
            using (var stream = fileInfo.OpenRead())
            {
                try
                {
                    var indexBytes = new byte[4];
                    stream.Read(indexBytes, 0, 4);
                    _fileChunkCount = BitConverter.ToInt32(indexBytes, 0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    _result = false;
                }
            }
        }

        private void WriteDecompress(byte[] bytes, string fileName)
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

        private void ReadArchivedFile(FileInfo fileInfo)
        {
            var offset = 0;
            using (var stream = fileInfo.OpenRead())
            {
                try
                {
                    stream.Seek(4, SeekOrigin.Begin);
                    while (offset <= fileInfo.Length)
                    {
                        var indexBytes = new byte[4];
                        stream.Read(indexBytes, 0, 4);
                        var sizeToRead = BitConverter.ToInt32(indexBytes, 0);
                        var bytes = new byte[sizeToRead];
                        var readBytes = stream.Read(bytes, 0, sizeToRead);
                        offset += (readBytes + 4);
                        _chunksDict.AddToDictionary(_currentIndex, bytes);
                        _currentIndex++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    _result = false;
                }
            }
        }

        private void WriteUnarchivedFile(string fileName)
        {
            CreateFile(fileName);
            var res = true;
            while (res && _result)
            {
                if (_decompressedChunksDict.ContainsKey(_chunkIndexWritten))
                {
                    var archivedStream = _decompressedChunksDict[_chunkIndexWritten];
                    WriteDecompress(archivedStream, fileName);
                    _decompressedChunksDict.RemoveFromDictionary(_chunkIndexWritten);
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
                new FileStream(fileName, FileMode.Create).Close();
            }
            catch (IOException exception)
            {
                _result = false;
                Console.WriteLine(exception.Message);
            }
        }

        private void RunDecompressor()
        {
            Monitor.Enter(_lockObg);
            var archivedStream = _chunksDict.FirstOrDefault();
            if (archivedStream.Value != null)
            {
                _chunksDict.RemoveFromDictionary(archivedStream.Key);
            }
            Monitor.Exit(_lockObg);

            if (archivedStream.Value != null)
            {
                try
                {
                    DecompressChunk(archivedStream.Value, archivedStream.Key);
                }
                catch (Exception ex)
                {
                    _result = false;
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private void DecompressChunk(byte[] bytes, int index)
        {
            var decodedStream = new MemoryStream();
            var buffer = new byte[128*1024];
            using (var outStream = new MemoryStream())
            {
                outStream.Write(bytes, 0, bytes.Length);
                outStream.Seek(0, SeekOrigin.Begin);
                using (var tinyStream = new GZipStream(outStream, CompressionMode.Decompress))
                {
                    var read = tinyStream.Read(buffer, 0, buffer.Length);
                    while (read > 0)
                    {
                        decodedStream.Write(buffer, 0, read);
                        read = tinyStream.Read(buffer, 0, buffer.Length);
                    }
                }
                var arr = decodedStream.ToArray();
                _decompressedChunksDict.AddToDictionary(index, arr);
            }
            _threadAmount++;
        }
    }
}