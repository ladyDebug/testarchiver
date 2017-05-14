using System;

namespace GZipWorker
{
    public static class SystemInfo
    {
        public static int GetCoreAmount()
        {
            return Environment.ProcessorCount;
        }
    }
}