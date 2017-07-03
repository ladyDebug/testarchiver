using System;
using GZipWorker;

namespace GzipArchiver
{
    internal class Program
    {
        public static void Usage()
        {
            Console.WriteLine("pass parameters {0} <compress|decompress> <fromFile> <toFile>",
                AppDomain.CurrentDomain.FriendlyName);
            Environment.Exit(0);
        }

        public static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Usage();
            }
            else
            {
                var command = args[0].ToLower();
                if (!command.Equals("compress") && !command.Equals("decompress"))
                {
                    Usage();
                }
                else
                {
                    var fromFile = args[1];
                    var toFile = args[2];
                    var archiver = new Archiver(new GZipWorker.GzipArchiver());
                    var result = false;
                    if (command.Equals("compress"))
                    {
                        result = archiver.Compress(fromFile, toFile);
                    }
                    else if (command.Equals("decompress"))
                    {
                        result = archiver.Decompress(fromFile, toFile);
                    }
                    Environment.Exit(result ? 0 : 1);
                }
            }
        }
    }
}