using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
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

        #region Math functions
        public static string[] RandomOperatorCalculation = { "+", "*", "%", "-", "/" };

        private static char[] randomNumberCalculation = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };


        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngNormal;
        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngSize;
        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngInteger;
        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngJob;
        [ThreadStatic] private static StringBuilder _numberBuilder;


        /// <summary>
        ///     Get a random number in integer size.
        /// </summary>
        /// <returns></returns>
        public static int GetRandom()
        {

            if (_generatorRngNormal == null)
            {
                _generatorRngNormal = new RNGCryptoServiceProvider();
            }

            var randomByte = new byte[sizeof(int)];

            _generatorRngNormal.GetBytes(randomByte);

            return BitConverter.ToInt32(randomByte, 0);
        }

        /// <summary>
        ///     Get a random number in integer size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static int GetRandomBetweenSize(int minimumValue, int maximumValue)
        {

            if (_generatorRngSize == null)
            {
                _generatorRngSize = new RNGCryptoServiceProvider();
            }

            byte[] randomByteSize = new byte[1];

            _generatorRngSize.GetBytes(randomByteSize);

            var asciiValueOfRandomCharacter = Convert.ToDecimal(randomByteSize[0]);

            var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

            var range = maximumValue - minimumValue + 1;

            var randomValueInRange = Math.Floor(multiplier * range);


            return (int)(minimumValue + randomValueInRange);

        }


        /// <summary>
        ///     Get a random number in integer size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static int GetRandomBetween(int minimumValue, int maximumValue)
        {

            if (_generatorRngInteger == null)
            {
                _generatorRngInteger = new RNGCryptoServiceProvider();
            }

            byte[] randomByteSize = new byte[sizeof(int)];

            _generatorRngInteger.GetBytes(randomByteSize);

            var asciiValueOfRandomCharacter =
                Convert.ToDecimal(randomByteSize[GetRandomBetweenSize(0, randomByteSize.Length - 1)]);

            var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

            var range = maximumValue - minimumValue + 1;

            var randomValueInRange = Math.Floor(multiplier * range);


            return (int)(minimumValue + randomValueInRange);

        }


        /// <summary>
        /// Get a random number in float size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static decimal GetRandomBetweenJob(decimal minimumValue, decimal maximumValue)
        {

            if (_generatorRngJob == null)
            {
                _generatorRngJob = new RNGCryptoServiceProvider();
            }

            byte[] randomByteSize = new byte[sizeof(decimal)];

            _generatorRngJob.GetBytes(randomByteSize);

            var asciiValueOfRandomCharacter =
                Convert.ToDecimal(randomByteSize[GetRandomBetweenSize(0, randomByteSize.Length - 1)]);

            var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

            var range = maximumValue - minimumValue + 1;

            var randomValueInRange = Math.Floor(multiplier * range);


            return (minimumValue + randomValueInRange);

        }




        /// <summary>
        /// Return result from a math calculation.
        /// </summary>
        /// <param name="firstNumber"></param>
        /// <param name="operatorCalculation"></param>
        /// <param name="secondNumber"></param>
        /// <returns></returns>
        public static decimal ComputeCalculation(decimal firstNumber, string operatorCalculation, decimal secondNumber)
        {
            decimal number1 = firstNumber;
            decimal number2 = secondNumber;
            if (number1 == 0 || number2 == 0)
            {
                return 0;
            }

            try
            {
                switch (operatorCalculation)
                {
                    case "+":
                        return number1 + number2;
                    case "-":
                        return number1 - number2;
                    case "*":
                        return number1 * number2;
                    case "%":
                        return number1 % number2;
                    case "/":
                        return number1 / number2;
                }
            }
            catch
            {
                // Ignored.
            }

            return 0;
        }

        #endregion



        /// <summary>
        /// Return a number for complete a math calculation text.
        /// </summary>
        /// <returns></returns>
        public static decimal GenerateNumberMathCalculation(decimal minRange, decimal maxRange)
        {
            if (_numberBuilder == null)
            {
                _numberBuilder = new StringBuilder();
            }
            else
            {
                _numberBuilder.Clear();
            }

            int randomSize = GetRandomBetweenSize(minRange.ToString("F0").Length, GetRandomBetweenJob(minRange, maxRange).ToString("F0").Length);
            int counter = 0;

            bool cleanGenerator = false;
            while (Program.CanMining)
            {


                    _numberBuilder.Append(
                        randomNumberCalculation[GetRandomBetween(0, randomNumberCalculation.Length - 1)]);
                

                counter++;
                if (_numberBuilder[0] == randomNumberCalculation[0])
                {
                    cleanGenerator = true;
                }

                if (!cleanGenerator)
                {
                    if (counter == randomSize)
                    {
                        if (decimal.TryParse(_numberBuilder.ToString(), out var number))
                        {

                            return number;

                        }

                        cleanGenerator = true;
                    }
                }

                if (cleanGenerator)
                {
                    _numberBuilder.Clear();
                    counter = 0;
                    cleanGenerator = false;

                }
            }

            if (decimal.TryParse(_numberBuilder.ToString(), out var numberResult))
            {
                return numberResult;
            }

            return 0;
        }


        #region Other functions 

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
        /// Necessary to get the amount of ram currently available on Linux OS. 
        /// </summary>
        /// <returns></returns>
        public static string RunCommandLineMemoryAvailable()
        {
            string commandLine = "awk '/^Mem/ {print $4}' <(free -m)";
            var errorBuilder = new StringBuilder();
            var outputBuilder = new StringBuilder();
            var arguments = $"-c \"{commandLine}\"";
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            })
            {
                process.Start();
                process.OutputDataReceived += (sender, args) => { outputBuilder.AppendLine(args.Data); };
                process.BeginOutputReadLine();
                process.ErrorDataReceived += (sender, args) => { errorBuilder.AppendLine(args.Data); };
                process.BeginErrorReadLine();
                if (!process.WaitForExit(500))
                {
                    var timeoutError =
                        $@"Process timed out. Command line: bash {arguments}. Output: {outputBuilder} Error: {errorBuilder}";
                    throw new Exception(timeoutError);
                }

                if (process.ExitCode == 0) return outputBuilder.ToString();

                var error =
                    $@"Could not execute process. Command line: bash {arguments}.Output: {outputBuilder} Error: {errorBuilder}";
                throw new Exception(error);
            }
        }

        #endregion
    }
}