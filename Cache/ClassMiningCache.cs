using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Xiropht_Solo_Miner.Cache
{
    public class ClassMiningCache
    {
        public List<Dictionary<string, int>> MiningListCache;
        private const int MaxMathCombinaisonPerDictionaryCache = int.MaxValue-1;
        private const int RamCounterInterval = 30; // Each 30 seconds.
        private const long RamLimitInMb = 128;
        private PerformanceCounter _ramCounter = new PerformanceCounter("Memory", "Available MBytes", true);
        private long _lastDateRamCounted;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClassMiningCache()
        {
            MiningListCache = new List<Dictionary<string, int>>();
        }

        /// <summary>
        /// Return the number of combinaisons stored.
        /// </summary>
        public long Count => CountCombinaison();

        /// <summary>
        /// Clear mining cache.
        /// </summary>
        public void CleanMiningCache()
        {
            MiningListCache.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            MiningListCache.Add(new Dictionary<string, int>());
        }

        /// <summary>
        /// Check if mining cache combinaison exist.
        /// </summary>
        /// <param name="combinaison"></param>
        /// <returns></returns>
        public bool CheckMathCombinaison(string combinaison)
        {
            if (MiningListCache.Count > 0)
            {
                for (int i = 0; i < MiningListCache.Count; i++)
                {
                    if (i < MiningListCache.Count)
                    {
                        if (MiningListCache[i].ContainsKey(combinaison))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Insert new math combinaison
        /// </summary>
        /// <param name="combinaison"></param>
        /// <param name="idThread"></param>
        public bool InsertMathCombinaison(string combinaison, int idThread)
        {
            if (_lastDateRamCounted < DateTimeOffset.Now.ToUnixTimeSeconds())
            {
                if (Environment.OSVersion.Platform == PlatformID.Unix)
                {
                    var availbleRam = long.Parse(RunCommandLineMemoryAvailable());
                    _lastDateRamCounted = DateTimeOffset.Now.ToUnixTimeSeconds() + RamCounterInterval;
                    if (availbleRam <= RamLimitInMb)
                    {
                        return true;
                    }
                }
                else
                {

                    float availbleRam = _ramCounter.NextValue();
                    _lastDateRamCounted = DateTimeOffset.Now.ToUnixTimeSeconds() + RamCounterInterval;
                    if (availbleRam <= RamLimitInMb)
                    {
                        return true;
                    }
                }
            }

            try
            {
                if (MiningListCache.Count > 0)
                {
                    bool inserted = false;
                    for (int i = 0; i < MiningListCache.Count; i++)
                    {
                        if (!inserted)
                        {
                            if (i < MiningListCache.Count)
                            {
                                if (MiningListCache[i].Count < MaxMathCombinaisonPerDictionaryCache)
                                {
                                    if (MiningListCache[i].ContainsKey(combinaison))
                                    {
                                        return false;
                                    }

                                    MiningListCache[i].Add(combinaison, idThread);
                                    inserted = true;
                                }
                            }
                        }
                    }

                    if (!inserted)
                    {
                        MiningListCache.Add(new Dictionary<string, int>());
                        if (!MiningListCache[MiningListCache.Count -1].ContainsKey(combinaison))
                        {
                            MiningListCache[MiningListCache.Count - 1].Add(combinaison, idThread);
                            return true;
                        }
                    }
                }
                else
                {
                    MiningListCache.Add(new Dictionary<string, int>());
                    if (!MiningListCache[0].ContainsKey(combinaison))
                    {
                        MiningListCache[0].Add(combinaison, idThread);
                        return true;
                    }

                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get total combinaison
        /// </summary>
        /// <returns></returns>
        public long CountCombinaison()
        {
            long totalCombinaison = 0;
            if (MiningListCache.Count > 0)
            {
                for (int i = 0; i < MiningListCache.Count; i++)
                {
                    if (i < MiningListCache.Count)
                    {
                        totalCombinaison += MiningListCache[i].Count;
                    }
                }
            }

            return totalCombinaison;
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
                    var timeoutError = $@"Process timed out. Command line: bash {arguments}. Output: {outputBuilder} Error: {errorBuilder}";
                    throw new Exception(timeoutError);
                }

                if (process.ExitCode == 0) return outputBuilder.ToString();

                var error = $@"Could not execute process. Command line: bash {arguments}.Output: {outputBuilder} Error: {errorBuilder}";
                throw new Exception(error);
            }
        }
    }
}
