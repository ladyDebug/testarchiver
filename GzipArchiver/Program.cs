using System;
using System.IO;
using System.IO.Compression;


namespace GzipArchiver
{
    internal class Program
    {
        public static void Usage()
        {
            Console.WriteLine("pass parameters {0} <compress|decompress> <fromFile> <toFile>",System.AppDomain.CurrentDomain.FriendlyName);
            Environment.Exit(0);
        }
        public static void Main(string[] args)
        {
            //var infile = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.Read);
            //byte[] buffer = new byte[infile.Length];
            //// Read the file to ensure it is readable.
            //int count = infile.Read(buffer, 0, buffer.Length);
            //if (count != buffer.Length)
            //{
            //    infile.Close();
            //    Console.WriteLine("Test Failed: Unable to read data from file");
            //    return;
            //}
            //infile.Close();
            //MemoryStream ms = new MemoryStream();
            //// Use the newly created memory stream for the compressed data.
            //GZipStream compressedzipStream = new GZipStream(ms, CompressionMode.Compress, true);
            //Console.WriteLine("Compression");
            //compressedzipStream.Write(buffer, 0, buffer.Length);            // Close the stream.

            //compressedzipStream.Close();
            //FileStream fout = new FileStream("a.gzip", FileMode.Create);
            //byte[] arr = ms.ToArray();
            //fout.Write(arr,0,arr.Length);
            //fout.Close();
            //ms.Position = 0;
            //FileStream fs = new FileStream("a.gzip",FileMode.Open);
            //GZipStream dStream= new GZipStream(fs, CompressionMode.Decompress, true);
            //Console.WriteLine("Dec");
            //dStream.Read(buffer, 0, buffer.Length);            // Close the stream.
            //fout = new FileStream("a1.html", FileMode.Create);

            //fout.Write(buffer, 0, buffer.Length);
            //fout.Close();
            
            if (args.Length < 3)
            {
                Usage();
            }
            else
            {
                if (!args[0].Equals("compress") && !args[0].Equals("decompress"))
                {
                    Usage();
                }
                else
                {
                    string fromFile = args[1];
                    string toFile = args[2];

                    GZipWorker.GzipWorker worker = new GZipWorker.GzipWorker();
                    if (args[0].Equals("compress"))
                    {
                        worker.Compress(fromFile, toFile);
                    }
                    if (args[0].Equals("decompress"))
                    {
                        worker.Decompress(fromFile, toFile);
                    }
                }
            }
        }
    }
        }
