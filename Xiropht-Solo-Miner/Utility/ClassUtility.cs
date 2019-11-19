using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Xiropht_Solo_Miner.ConsoleMiner;

namespace Xiropht_Solo_Miner.Utility
{

    public class ClassUtilityAffinity
    {
        [DllImport("libc.so.6", SetLastError = true)]
        private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref ulong cpuset);

        [DllImport("kernel32.dll")]
        static extern int GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        static extern IntPtr SetThreadAffinityMask(IntPtr hThread, IntPtr dwThreadAffinityMask);

        /// <summary>
        /// Set automatic affinity, use native function depending of the Operating system.
        /// </summary>
        /// <param name="threadIdMining"></param>
        public static void SetAffinity(int threadIdMining)
        {
            try
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix) // Linux/UNIX
                {
                    if (Environment.ProcessorCount > threadIdMining && threadIdMining >= 0)
                    {
                        ulong processorMask = 1UL << threadIdMining;
                        sched_setaffinity(0, new IntPtr(sizeof(ulong)), ref processorMask);
                    }
                }
                else
                {
                    if (Environment.ProcessorCount > threadIdMining && threadIdMining >= 0)
                    {
                        Thread.BeginThreadAffinity();
                        int threadId = GetCurrentThreadId();
                        ProcessThread thread = Process.GetCurrentProcess().Threads.Cast<ProcessThread>()
                            .Single(t => t.Id == threadId);

                        ulong cpuMask = 1UL << threadIdMining;

                        thread.ProcessorAffinity = (IntPtr)cpuMask;
                        SetThreadAffinityMask((IntPtr)threadIdMining, (IntPtr)cpuMask);

                    }
                }
            }
            catch (Exception error)
            {
                ClassConsole.WriteLine(
                    "Cannot apply Automatic Affinity with thread id: " + threadIdMining + " | Exception: " + error.Message, 3);
            }
        }

        /// <summary>
        /// Set manual affinity, use native function depending of the Operating system.
        /// </summary>
        /// <param name="threadAffinity"></param>
        public static void SetManualAffinity(string threadAffinity)
        {
            try
            {
                ulong handle = Convert.ToUInt64(threadAffinity, 16);

                if (Environment.OSVersion.Platform == PlatformID.Unix) // Linux/UNIX
                {
                    sched_setaffinity(0, new IntPtr(sizeof(ulong)), ref handle);

                }
                else
                {

                    Thread.BeginThreadAffinity();
                    int threadId = GetCurrentThreadId();
                    ProcessThread thread = Process.GetCurrentProcess().Threads.Cast<ProcessThread>()
                        .Single(t => t.Id == threadId);
                    thread.ProcessorAffinity = (IntPtr)handle;
                    SetThreadAffinityMask((IntPtr)threadId, (IntPtr)handle);
                }
            }
            catch (Exception error)
            {
                ClassConsole.WriteLine(
                    "Cannot apply Manual Affinity with: " + threadAffinity + " | Exception: " + error.Message, 3);
            }
        }
    }

    public class ClassUtility
    {
        private static readonly char[] HexArrayList = "0123456789ABCDEF".ToCharArray();

        /// <summary>
        ///     Get current path of the miner.
        /// </summary>
        /// <returns></returns>
        public static string GetCurrentPathConfig(string configFile)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + configFile;
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                path = path.Replace("\\", "/");
            }

            return path;
        }

        /// <summary>
        ///     Get current path of the miner.
        /// </summary>
        /// <returns></returns>
        public static string ConvertPath(string path)
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                path = path.Replace("\\", "/");
            }

            return path;
        }

        /// <summary>
        /// Convert a string into hex string.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static string StringToHex(string hex)
        {
            return BitConverter.ToString(Encoding.UTF8.GetBytes(hex)).Replace("-", "");
        }

        /// <summary>
        /// Remove special characters
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveSpecialCharacters(string str)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert a byte array to hex string like Bitconverter class.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string GetHexStringFromByteArray(byte[] value, int startIndex, int length)
        {
            int newSize = length * 3;
            char[] hexCharArray = new char[newSize];
            int currentIndex;
            for (currentIndex = 0; currentIndex < newSize; currentIndex += 3)
            {
                byte currentByte = value[startIndex++];
                hexCharArray[currentIndex] = GetHexValue(currentByte / 0x10);
                hexCharArray[currentIndex + 1] = GetHexValue(currentByte % 0x10);
                hexCharArray[currentIndex + 2] = '-';
            }
            return new string(hexCharArray, 0, hexCharArray.Length - 1);
        }

        /// <summary>
        /// Get Hex value from char index value.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private static char GetHexValue(int i)
        {
            if (i < 10)
            {
                return (char)(i + 0x30);
            }
            return (char)((i - 10) + 0x41);
        }

        /// <summary>
        /// Convert a byte array into a hex string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ByteArrayToHexString(byte[] bytes)
        {
            char[] hexChars = new char[(bytes.Length * 2)];
            for (int j = 0; j < bytes.Length; j++)
            {
                int v = bytes[j] & 255;
                hexChars[j * 2] = HexArrayList[(int)((uint)v >> 4)];
                hexChars[(j * 2) + 1] = HexArrayList[v & 15];
            }
            return new string(hexChars);
        }

    }
}