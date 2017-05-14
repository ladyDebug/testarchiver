﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace GZipWorker
{
    public class GzipWorker
    {
        private readonly Dictionary<int, byte[]> _chunksDict = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, byte[]> _decompressedChunksDict = new Dictionary<int, byte[]>();
        private readonly object _lockObg;
        private volatile int _chunkIndexWritten;
        private volatile int _currentIndex;
        private volatile int _threadAmount;
        private int _chunkSize;
        private volatile int _fileChunkCount;
        private long _fileLength;
        private int _threadCount;

        public GzipWorker()
        {
            _lockObg = new object();
            _threadAmount = SystemInfo.GetCoreAmount() - 1;
        }

        public void Decompress(string archivedFile, string unarchivedFile)
        {
            var fileInfo = new FileInfo(archivedFile);

            new Thread(() => { ReadArchivedFile(fileInfo); }).Start();
            new Thread(() => { WriteUnarchivedFile(unarchivedFile); }).Start();
            StartDecompressingThreads();
        }

        public void Compress(string fileToArchive, string archivedFile)
        {
            var fileInfo = new FileInfo(fileToArchive);

            _chunkSize = 128*1024;
            _fileLength = fileInfo.Length;
            _fileChunkCount = (int) Math.Ceiling((decimal) fileInfo.Length/_chunkSize);
            Console.WriteLine("fileChunkCount {0}", _fileChunkCount);

            _threadCount = 0;

            new Thread(() => { StartArchiveWriter(archivedFile); }).Start();
            int idx = 0;
            while (idx<=_fileChunkCount)
            {
                Console.WriteLine("fileChunkCount: {0} chunkIndexWritten: {1}",_fileChunkCount,_chunkIndexWritten);

                if (_threadAmount > 0)
                {
                    Console.WriteLine("_threadAmount: "+_threadAmount);
                    Console.WriteLine("_threadAmount: " + _threadAmount);
                    new Thread(() => { GetOffset(fileInfo); }).Start();
                    idx++;
                    //GetOffset(fileInfo);
                    _threadAmount--;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }
        
        private void ReadChunk(FileInfo fileInfo)
        {
            var index = _currentIndex;
            _currentIndex++;
            var bytes = new byte[_chunkSize];
            Console.WriteLine("CURRENT INDEX READ CHUNK: {0} ",index);
            using (var stream = fileInfo.OpenRead())
            {
            //    Console.WriteLine("index {0} : offset {1} : fileinfo {2}", index, index*_chunkSize, fileInfo.Length);
                try
                {
                    stream.Seek(index==0?0:index * _chunkSize, SeekOrigin.Begin);
                    stream.Read(bytes, 0,_chunkSize);
                        if (bytes.Length > 0)
                        {
                            ArchiveChunk(bytes,index);
                        }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private byte[] FormatBytesForIndex(int index, byte[] bytes)
        {
            var completedBytes = new byte[bytes.Length + 4];
            var intBytes = BitConverter.GetBytes(bytes.Length);
            Buffer.BlockCopy(intBytes, 0, completedBytes, 0, intBytes.Length);
            Buffer.BlockCopy(bytes, 0, completedBytes, 4, bytes.Length);
            return completedBytes;
        }

        private void AddToDictionary(int index, byte[] stream)
        {
            Monitor.Enter(_lockObg);
            if (!_chunksDict.ContainsKey(index))
            {
                Console.WriteLine("Add to Chunk Dict index {0}", index);
                _chunksDict.Add(index, stream);
            }
            Monitor.Exit(_lockObg);
        }


        private void RemoveFromDictionary(int index)
        {
            Monitor.Enter(_lockObg);
            if (_chunksDict.ContainsKey(index))
            {
                _chunksDict.Remove(index);
                Console.WriteLine("Removed dictionary index {0}", index);
            }
            Monitor.Exit(_lockObg);
        }

        private void ArchiveChunk(byte[] bytes,int index)
        {
            using (var outStream = new MemoryStream())
            {
                using (var tinyStream = new GZipStream(outStream, CompressionMode.Compress, false))
                {
                    tinyStream.Write(bytes, 0, bytes.Length);
                    byte[] arr = outStream.ToArray();
                    AddToDictionary(index, arr);
                //    _currentIndex++;
                    Console.WriteLine("Written to dict file chunk index {0} size: {1}", _currentIndex, arr.Length);
                }
            }
            _threadAmount++;
        }

     
        private void GetOffset(object fileInfo)
        {
            ReadChunk((FileInfo) fileInfo);
        }

        private void StartArchiveWriter(String fileName)
        {
            new FileStream(fileName, FileMode.Create).Close();
            while (true)
            {
                
                if (_chunksDict.ContainsKey(_chunkIndexWritten))
                {
                    var archivedStream = _chunksDict[_chunkIndexWritten];
                    Write(FormatBytesForIndex(_chunkIndexWritten, archivedStream), fileName);
                    RemoveFromDictionary(_chunkIndexWritten);
                    _chunkIndexWritten++;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void Write(byte[] bytes, string fileName)
        {
            Stream stream = new MemoryStream(bytes);

            using (var fsAppend = new FileStream(fileName, FileMode.Append))
            {
                var buffer = new byte[32*1024];
                if (stream.CanRead)
                {
                    var readBytes = stream.Read(buffer, 0, buffer.Length);
                    while (readBytes > 0)
                    {
                        fsAppend.Write(buffer, 0, readBytes);
                        fsAppend.Flush();
                        readBytes = stream.Read(buffer, 0, buffer.Length);
                    }
                    Console.WriteLine("_chunkIndexWritten: {0}", _chunkIndexWritten);
                    RemoveFromDictionary(_chunkIndexWritten);
                    
                }
                stream.Close();
            }
        }

        //////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////// DECOMPRESSION /////////////////////////////////////
        //////////////////////////////////////////////////////////////////////////////////

        private void ReadArchivedFile(FileInfo fileInfo)
        {
            var offset = 0;
            using (var stream = fileInfo.OpenRead())
            {
                try
                {
                    while (offset <= fileInfo.Length)
                    {
                        var indexBytes = new byte[4];
                        //  stream.Seek(offset, SeekOrigin.Begin);
                        stream.Read(indexBytes, 0, 4);
                        var sizeToRead = BitConverter.ToInt32(indexBytes, 0);
                        var bytes = new byte[sizeToRead];
                        var readBytes = stream.Read(bytes, 0, sizeToRead);
                        offset += (readBytes + 4);
                        AddToDictionary(_currentIndex, bytes);
                        _currentIndex++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }


        private void WriteUnarchivedFile(String fileName)
        {
            new FileStream(fileName, FileMode.Create).Close();
            while (true)
            {
                if (_decompressedChunksDict.ContainsKey(_chunkIndexWritten))
                {
                    var archivedStream = _decompressedChunksDict[_chunkIndexWritten];
                    Write(archivedStream, fileName);
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void AddToDecompressedDictionary(int index, byte[] stream)
        {
            Monitor.Enter(_lockObg);
            if (!_decompressedChunksDict.ContainsKey(index))
            {
                _decompressedChunksDict.Add(index, stream);
            }
            Monitor.Exit(_lockObg);
        }


        private void StartDecompressingThreads()
        {
            while (true)
            {
                if (_threadAmount > 0)
                {
                    Console.WriteLine("_threadAmount: " + _threadAmount);
                    new Thread(() => { RunDecompressor(); }).Start();
                    _threadAmount--;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        private void RunDecompressor()
        {
            Monitor.Enter(_lockObg);
            var archivedStream = _chunksDict.FirstOrDefault();
            Monitor.Exit(_lockObg);
            if (archivedStream.Value != null)
            {
                RemoveFromDictionary(archivedStream.Key);
            }

            if (archivedStream.Value != null)
            {
                DecompressChunk(archivedStream.Value, archivedStream.Key);
            }
        }

        private void DecompressChunk(byte[] bytes, int index)
        {
            var decodedStream = new MemoryStream();
            var buffer = new byte[128 * 1024];
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
                    var arr = decodedStream.ToArray();
                    Console.WriteLine("AddToDecompressedDictionary index {0} {1}", index, arr.Length);
                    AddToDecompressedDictionary(index, arr);
                }
            }
            _threadAmount++;
        }






    }









}