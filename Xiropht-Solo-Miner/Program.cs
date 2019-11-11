using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Solo_Miner.Cache;
using Xiropht_Solo_Miner.ConsoleMiner;
using Xiropht_Solo_Miner.Mining;
using Xiropht_Solo_Miner.Setting;
using Xiropht_Solo_Miner.Token;
using Xiropht_Solo_Miner.Utility;
using ClassAlgoMining = Xiropht_Solo_Miner.Algo.ClassAlgoMining;
// ReSharper disable FunctionNeverReturns

namespace Xiropht_Solo_Miner
{
    class Program
    {
        private const int HashrateIntervalCalculation = 10;

        /// <summary>
        /// About configuration file.
        /// </summary>
        private const int TotalConfigLine = 9;
        private const string OldConfigFile = "\\config.ini";
        private static string _configFile = "\\config.json";
        private const string WalletCacheFile = "\\wallet-cache.xiro";
        public static ClassMinerConfig ClassMinerConfigObject;

        /// <summary>
        ///     For mining method.
        /// </summary>
        public static List<string> ListeMiningMethodName = new List<string>();
        public static List<string> ListeMiningMethodContent = new List<string>();

        /// <summary>
        ///     For mining stats.
        /// </summary>
        public static int TotalBlockAccepted;

        public static int TotalBlockRefused;

        /// <summary>
        ///     Current block information for mining it.
        /// </summary>
        public static string CurrentBlockId;
        public static string CurrentBlockHash;
        public static string CurrentBlockAlgorithm;
        public static string CurrentBlockSize;
        public static string CurrentBlockMethod;
        public static string CurrentBlockKey;
        public static string CurrentBlockJob;
        public static string CurrentBlockReward;
        public static string CurrentBlockDifficulty;
        public static string CurrentBlockTimestampCreate;
        public static string CurrentBlockIndication;
        public static string CurrentBlockNetworkHashrate;
        public static string CurrentBlockLifetime;

        /// <summary>
        ///     For network.
        /// </summary>
        public static bool CanMining;
        public static int TotalShareAccepted;
        public static int TotalShareInvalid;
        public static List<int> TotalMiningHashrateRound = new List<int>();
        public static List<int> TotalMiningCalculationRound = new List<int>();
        public static float TotalHashrate;
        public static float TotalCalculation;
        public static Dictionary<string, string> DictionaryWalletAddressCache = new Dictionary<string, string>();



        /// <summary>
        ///     Wallet mining.
        /// </summary>
        private static bool _calculateHashrateEnabled;

        public static bool IsLinux;

        /// <summary>
        ///     Main
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            Thread.CurrentThread.Name = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            ClassConsole.WriteLine("Xiropht Solo Miner - " + Assembly.GetExecutingAssembly().GetName().Version + "R",
                4);

            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args2)
            {
                var filePath = ConvertPath(AppDomain.CurrentDomain.BaseDirectory + "\\error_miner.txt");
                var exception = (Exception) args2.ExceptionObject;
                using (var writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine("Message :" + exception.Message + "<br/>" + Environment.NewLine +
                                     "StackTrace :" +
                                     exception.StackTrace +
                                     "" + Environment.NewLine + "Date :" + DateTime.Now);
                    writer.WriteLine(Environment.NewLine +
                                     "-----------------------------------------------------------------------------" +
                                     Environment.NewLine);
                }

                Trace.TraceError(exception.StackTrace);

                Environment.Exit(1);
            };

            HandleArgumentStartup(args);
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                IsLinux = true;
            }

            LoadWalletAddressCache();

            if (File.Exists(GetCurrentPathConfig(OldConfigFile)))
            {
                if (LoadConfig(true))
                {
                    ClassMining.ThreadMining = new Task[ClassMinerConfigObject.mining_thread];
                    ClassAlgoMining.Sha512ManagedMining = new SHA512Managed[ClassMinerConfigObject.mining_thread];
                    ClassAlgoMining.CryptoTransformMining = new ICryptoTransform[ClassMinerConfigObject.mining_thread];
                    ClassAlgoMining.AesManagedMining = new AesManaged[ClassMinerConfigObject.mining_thread];
                    ClassAlgoMining.CryptoStreamMining = new CryptoStream[ClassMinerConfigObject.mining_thread];
                    ClassAlgoMining.MemoryStreamMining = new MemoryStream[ClassMinerConfigObject.mining_thread];
                    ClassAlgoMining.TotalNonceMining = new int[ClassMinerConfigObject.mining_thread];

                    TotalMiningHashrateRound = new List<int>();
                    TotalMiningCalculationRound = new List<int>();
                    for (int i = 0; i < ClassMinerConfigObject.mining_thread; i++)
                    {
                        if (i < ClassMinerConfigObject.mining_thread)
                        {
                            TotalMiningHashrateRound.Add(0);
                            TotalMiningCalculationRound.Add(0);
                        }
                    }

                    ClassConsole.WriteLine("Connecting to the network..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                    Task.Factory.StartNew(ClassMiningNetwork.StartConnectMinerAsync).ConfigureAwait(false);
                }
                else
                {
                    ClassConsole.WriteLine(
                        "config file invalid, do you want to follow instructions to setting again your config file ? [Y/N]",
                        2);
                    string choose = Console.ReadLine();
                    if (choose != null)
                    {
                        if (choose.ToLower() == "y")
                        {
                            FirstSettingConfig();
                        }
                        else
                        {
                            ClassConsole.WriteLine("Close solo miner program.");
                            Process.GetCurrentProcess().Kill();
                        }
                    }
                    else
                    {
                        ClassConsole.WriteLine("Close solo miner program.");
                        Process.GetCurrentProcess().Kill();
                    }
                }
            }
            else
            {
                if (File.Exists(ConvertPath(_configFile)))
                {
                    if (LoadConfig(false))
                    {
                        ClassMining.ThreadMining = new Task[ClassMinerConfigObject.mining_thread];
                        ClassAlgoMining.Sha512ManagedMining = new SHA512Managed[ClassMinerConfigObject.mining_thread];
                        ClassAlgoMining.CryptoTransformMining = new ICryptoTransform[ClassMinerConfigObject.mining_thread];
                        ClassAlgoMining.AesManagedMining = new AesManaged[ClassMinerConfigObject.mining_thread];
                        ClassAlgoMining.CryptoStreamMining = new CryptoStream[ClassMinerConfigObject.mining_thread];
                        ClassAlgoMining.MemoryStreamMining = new MemoryStream[ClassMinerConfigObject.mining_thread];
                        ClassAlgoMining.TotalNonceMining = new int[ClassMinerConfigObject.mining_thread];

                        TotalMiningHashrateRound = new List<int>();
                        TotalMiningCalculationRound = new List<int>();
                        for (int i = 0; i < ClassMinerConfigObject.mining_thread; i++)
                        {
                            if (i < ClassMinerConfigObject.mining_thread)
                            {
                                TotalMiningHashrateRound.Add(0);
                                TotalMiningCalculationRound.Add(0);
                            }
                        }

                        ClassConsole.WriteLine("Connecting to the network..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                        Task.Factory.StartNew(ClassMiningNetwork.StartConnectMinerAsync).ConfigureAwait(false);
                    }
                    else
                    {
                        ClassConsole.WriteLine(
                            "config file invalid, do you want to follow instructions to setting again your config file ? [Y/N]",
                            2);
                        string choose = Console.ReadLine();
                        if (choose != null)
                        {
                            if (choose.ToLower() == "y")
                            {
                                FirstSettingConfig();
                            }
                            else
                            {
                                ClassConsole.WriteLine("Close solo miner program.");
                                Process.GetCurrentProcess().Kill();
                            }
                        }
                        else
                        {
                            ClassConsole.WriteLine("Close solo miner program.");
                            Process.GetCurrentProcess().Kill();
                        }
                    }
                }
                else
                {
                    FirstSettingConfig();
                }
            }

            ClassConsole.WriteLine("Command Line: "+ClassConsoleKeyCommandEnumeration.ConsoleCommandKeyHashrate+" -> show hashrate.", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
            ClassConsole.WriteLine("Command Line: "+ClassConsoleKeyCommandEnumeration.ConsoleCommandKeyDifficulty+" -> show current difficulty.", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
            ClassConsole.WriteLine("Command Line: "+ClassConsoleKeyCommandEnumeration.ConsoleCommandKeyRange+" -> show current range", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);

            Console.CancelKeyPress += Console_CancelKeyPress;

            var threadCommand = new Thread(delegate()
            {
                while (true)
                {
                    try
                    {
                        StringBuilder input = new StringBuilder();
                        var key = Console.ReadKey(true);
                        input.Append(key.KeyChar);
                        ClassConsole.CommandLine(input.ToString());
                        input.Clear();
                    }
                    catch
                    {
                        // Ignored.
                    }
                }
            });
            threadCommand.Start();
        }

        /// <summary>
        /// Handle arguments of startup.
        /// </summary>
        /// <param name="args"></param>
        private static void HandleArgumentStartup(string[] args)
        {
            bool enableCustomConfigPath = false;
            if (args.Length > 0)
            {
                foreach (var argument in args)
                {
                    if (!string.IsNullOrEmpty(argument))
                    {
                        if (argument.Contains(ClassStartupArgumentEnumeration.ArgumentCharacterSplitter))
                        {
                            var splitArgument = argument.Split(new[] { ClassStartupArgumentEnumeration.ArgumentCharacterSplitter }, StringSplitOptions.None);
                            switch (splitArgument[0])
                            {
                                case ClassStartupArgumentEnumeration.ConfigFileArgument:
                                    if (splitArgument.Length > 1)
                                    {
                                        _configFile = ConvertPath(splitArgument[1]);
                                        ClassConsole.WriteLine("Enable using of custom config.json file from path: "+ ConvertPath(splitArgument[1]), 4);
                                        enableCustomConfigPath = true;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            if (!enableCustomConfigPath)
            {
                _configFile = GetCurrentPathConfig(_configFile);
            }
        }

        /// <summary>
        /// Load wallet address cache.
        /// </summary>
        private static void LoadWalletAddressCache()
        {
            if (!File.Exists(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                File.Create(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)).Close();
            }
            else
            {
                ClassConsole.WriteLine("Loading wallet address cache..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                using (var reader =
                    new StreamReader(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length >= ClassConnectorSetting.MinWalletAddressSize &&
                            line.Length <= ClassConnectorSetting.MaxWalletAddressSize)
                        {
                            if (!DictionaryWalletAddressCache.ContainsKey(line))
                            {
                                DictionaryWalletAddressCache.Add(line, string.Empty);
                            }
                        }
                    }
                }

                ClassConsole.WriteLine("Loading wallet address cache successfully loaded.", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
            }
        }

        /// <summary>
        /// Save wallet address cache.
        /// </summary>
        /// <param name="walletAddress"></param>
        public static void SaveWalletAddressCache(string walletAddress)
        {
            ClassConsole.WriteLine("Save wallet address cache..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
            if (!File.Exists(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                File.Create(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)).Close();
            }

            using (var writer = new StreamWriter(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                writer.WriteLine(walletAddress);
            }

            ClassConsole.WriteLine("Save wallet address cache successfully done.", ClassConsoleColorEnumeration.ConsoleTextColorYellow);

        }

        /// <summary>
        ///     First time to setting config file.
        /// </summary>
        private static void FirstSettingConfig()
        {
            ClassMinerConfigObject = new ClassMinerConfig();
            ClassConsole.WriteLine("Do you want to use a proxy instead seed node? [Y/N]", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
            var choose = Console.ReadLine();
            if (choose == null)
            {
                choose = "n";
            }
            if (choose.ToLower() == "y")
            {
                Console.WriteLine("Please, write your wallet address or a worker name to start your solo mining: ");
                ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();

                Console.WriteLine("Write the IP/HOST of your mining proxy: ");
                ClassMinerConfigObject.mining_proxy_host = Console.ReadLine();
                Console.WriteLine("Write the port of your mining proxy: ");

                while (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_proxy_port))
                {
                    Console.WriteLine("This is not a port number, please try again: ");
                }

                Console.WriteLine("Do you want select a mining range percentage of difficulty? [Y/N]");
                choose = Console.ReadLine();
                if (choose == null)
                {
                    choose = "n";
                }
                if (choose.ToLower() == "y")
                {
                    Console.WriteLine("Select the start percentage range of difficulty [0 to 100]:");
                    while (!int.TryParse(Console.ReadLine(),
                        out ClassMinerConfigObject.mining_percent_difficulty_start))
                    {
                        Console.WriteLine("This is not a number, please try again: ");
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_start > 100)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_start = 100;
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_start < 0)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_start = 0;
                    }

                    Console.WriteLine("Select the end percentage range of difficulty [" +
                                      ClassMinerConfigObject.mining_percent_difficulty_start + " to 100]: ");
                    while (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_percent_difficulty_end))
                    {
                        Console.WriteLine("This is not a number, please try again: ");
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_end < 1)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_end = 1;
                    }
                    else if (ClassMinerConfigObject.mining_percent_difficulty_end > 100)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_end = 100;
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_start >
                        ClassMinerConfigObject.mining_percent_difficulty_end)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_start -=
                            (ClassMinerConfigObject.mining_percent_difficulty_start -
                             ClassMinerConfigObject.mining_percent_difficulty_end);
                    }
                    else
                    {
                        if (ClassMinerConfigObject.mining_percent_difficulty_start ==
                            ClassMinerConfigObject.mining_percent_difficulty_end)
                        {
                            ClassMinerConfigObject.mining_percent_difficulty_start--;
                        }
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_end <
                        ClassMinerConfigObject.mining_percent_difficulty_start)
                    {
                        var tmpPercentStart = ClassMinerConfigObject.mining_percent_difficulty_start;
                        ClassMinerConfigObject.mining_percent_difficulty_start =
                            ClassMinerConfigObject.mining_percent_difficulty_end;
                        ClassMinerConfigObject.mining_percent_difficulty_end = tmpPercentStart;
                    }
                }

                ClassMinerConfigObject.mining_enable_proxy = true;
            }
            else
            {
                Console.WriteLine("Please, write your wallet address to start your solo mining: ");
                ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                ClassMinerConfigObject.mining_wallet_address =
                    ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);

                ClassConsole.WriteLine("Checking wallet address..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                bool checkWalletAddress = ClassTokenNetwork.CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;

                while (ClassMinerConfigObject.mining_wallet_address.Length <
                       ClassConnectorSetting.MinWalletAddressSize ||
                       ClassMinerConfigObject.mining_wallet_address.Length >
                       ClassConnectorSetting.MaxWalletAddressSize || !checkWalletAddress)
                {
                    Console.WriteLine(
                        "Invalid wallet address - Please, write your wallet address to start your solo mining: ");
                    ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                    ClassMinerConfigObject.mining_wallet_address = ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);
                    ClassConsole.WriteLine("Checking wallet address..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                    checkWalletAddress = ClassTokenNetwork.CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;
                }

                ClassConsole.WriteLine("Wallet address: " + ClassMinerConfigObject.mining_wallet_address + " is valid.", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
            }

            Console.WriteLine("How many threads do you want to run? Number of cores detected: " +
                              Environment.ProcessorCount);

            var tmp = Console.ReadLine();
            if (!int.TryParse(tmp, out ClassMinerConfigObject.mining_thread))
            {
                ClassMinerConfigObject.mining_thread = Environment.ProcessorCount;
            }

            ClassMining.ThreadMining = new Task[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.Sha512ManagedMining = new SHA512Managed[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.CryptoTransformMining = new ICryptoTransform[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.AesManagedMining = new AesManaged[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.CryptoStreamMining = new CryptoStream[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.MemoryStreamMining = new MemoryStream[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.TotalNonceMining = new int [ClassMinerConfigObject.mining_thread];


            Console.WriteLine("Do you want share job range per thread ? [Y/N]");
            choose = Console.ReadLine();
            if (choose == null)
            {
                choose = "n";
            }
            if (choose.ToLower() == "y")
            {
                ClassMinerConfigObject.mining_thread_spread_job = true;
            }

            TotalMiningHashrateRound = new List<int>();
            TotalMiningCalculationRound = new List<int>();
            for (int i = 0; i < ClassMinerConfigObject.mining_thread; i++)
            {
                if (i < ClassMinerConfigObject.mining_thread)
                {
                    TotalMiningHashrateRound.Add(0);
                    TotalMiningCalculationRound.Add(0);
                }
            }

            Console.WriteLine(
                "Select thread priority: 0 = Lowest, 1 = BelowNormal, 2 = Normal, 3 = AboveNormal, 4 = Highest [Default: 2]:");

            if (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_thread_priority))
            {
                ClassMinerConfigObject.mining_thread_priority = 2;
            }


            WriteMinerConfig();
            if (ClassMinerConfigObject.mining_enable_cache)
            {
                InitializeMiningCache();
            }

            ClassConsole.WriteLine("Start to connect to the network..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
            Task.Factory.StartNew(ClassMiningNetwork.StartConnectMinerAsync).ConfigureAwait(false);
        }

        /// <summary>
        ///     Load config file.
        /// </summary>
        /// <returns></returns>
        private static bool LoadConfig(bool oldConfigType)
        {
            string configContent = string.Empty;

            if (oldConfigType)
            {
                ClassMinerConfigObject = new ClassMinerConfig();

                using (StreamReader reader = new StreamReader(GetCurrentPathConfig(OldConfigFile)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        configContent += line + "\n";
                    }
                }

                if (!string.IsNullOrEmpty(configContent))
                {
                    var splitConfigContent = configContent.Split(new[] {"\n"}, StringSplitOptions.None);

                    bool proxyLineFound = false;
                    int totalConfigLine = 0;
                    // Check at first if the miner use a proxy.
                    foreach (var configLine in splitConfigContent)
                    {
                        if (configLine.Contains("PROXY_ENABLE=") && !proxyLineFound)
                        {
                            proxyLineFound = true;
                            if (configLine.Replace("PROXY_ENABLE=", "").ToLower() == "y")
                            {
                                ClassMinerConfigObject.mining_enable_proxy = true;
                            }

                            totalConfigLine++;
                        }
                    }

                    if (proxyLineFound == false)
                    {
                        return false;
                    }

                    bool walletAddressCorrected = false;
                    bool walletAddressLineFound = false;
                    bool miningThreadLineFound = false;
                    foreach (var configLine in splitConfigContent)
                    {
                        if (configLine.Contains("WALLET_ADDRESS=") && !walletAddressLineFound)
                        {
                            walletAddressLineFound = true;
                            ClassMinerConfigObject.mining_wallet_address = configLine.Replace("WALLET_ADDRESS=", "");
                            if (!ClassMinerConfigObject.mining_enable_proxy)
                            {
                                ClassMinerConfigObject.mining_wallet_address =
                                    ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);
                                ClassConsole.WriteLine("Checking wallet address before to connect..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);

                                bool checkWalletAddress = ClassTokenNetwork
                                    .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;

                                while (ClassMinerConfigObject.mining_wallet_address.Length <
                                       ClassConnectorSetting.MinWalletAddressSize ||
                                       ClassMinerConfigObject.mining_wallet_address.Length >
                                       ClassConnectorSetting.MaxWalletAddressSize || !checkWalletAddress)
                                {
                                    Console.WriteLine(
                                        "Invalid wallet address inside your config.ini file - Please, write your wallet address to start your solo mining: ");
                                    ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                                    ClassMinerConfigObject.mining_wallet_address =
                                        ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject
                                            .mining_wallet_address);
                                    ClassConsole.WriteLine("Checking wallet address before to connect..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                    walletAddressCorrected = true;
                                    checkWalletAddress = ClassTokenNetwork
                                        .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address)
                                        .Result;
                                }

                                ClassConsole.WriteLine("Wallet address: " + ClassMinerConfigObject.mining_wallet_address + " is valid.", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("MINING_THREAD=") && !miningThreadLineFound)
                        {
                            miningThreadLineFound = true;
                            if (!int.TryParse(configLine.Replace("MINING_THREAD=", ""),
                                out ClassMinerConfigObject.mining_thread))
                            {
                                ClassConsole.WriteLine("MINING_THREAD line contain an invalid integer value.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                return false;
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("MINING_THREAD_PRIORITY="))
                        {
                            if (!int.TryParse(configLine.Replace("MINING_THREAD_PRIORITY=", ""),
                                out ClassMinerConfigObject.mining_thread_priority))
                            {
                                ClassConsole.WriteLine("MINING_THREAD_PRIORITY= line contain an invalid integer value.",
                                    3);
                                return false;
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("MINING_THREAD_SPREAD_JOB="))
                        {
                            if (configLine.Replace("MINING_THREAD_SPREAD_JOB=", "").ToLower() == "y")
                            {
                                ClassMinerConfigObject.mining_thread_spread_job = true;
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("PROXY_PORT="))
                        {
                            if (!int.TryParse(configLine.Replace("PROXY_PORT=", ""),
                                out ClassMinerConfigObject.mining_proxy_port))
                            {
                                ClassConsole.WriteLine("PROXY_PORT= line contain an invalid integer value.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                return false;
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("PROXY_HOST="))
                        {
                            ClassMinerConfigObject.mining_proxy_host = configLine.Replace("PROXY_HOST=", "");
                            totalConfigLine++;
                        }

                        if (configLine.Contains("MINING_PERCENT_DIFFICULTY_START="))
                        {
                            if (!int.TryParse(configLine.Replace("MINING_PERCENT_DIFFICULTY_START=", ""),
                                out ClassMinerConfigObject.mining_percent_difficulty_start))
                            {
                                ClassConsole.WriteLine(
                                    "MINING_PERCENT_DIFFICULTY_START= line contain an invalid integer value.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                return false;
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("MINING_PERCENT_DIFFICULTY_END="))
                        {
                            if (!int.TryParse(configLine.Replace("MINING_PERCENT_DIFFICULTY_END=", ""),
                                out ClassMinerConfigObject.mining_percent_difficulty_end))
                            {
                                ClassConsole.WriteLine(
                                    "MINING_PERCENT_DIFFICULTY_END= line contain an invalid integer value.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                return false;
                            }

                            totalConfigLine++;
                        }
                    }

                    if (totalConfigLine == TotalConfigLine)
                    {
                        if (walletAddressCorrected) // Resave config file.
                        {
                            WriteMinerConfig();
                        }

                        File.Delete(GetCurrentPathConfig(OldConfigFile));
                        WriteMinerConfig();
                        if (ClassMinerConfigObject.mining_enable_cache)
                        {
                            InitializeMiningCache();
                        }

                        return true;
                    }
                }
            }
            else
            {
                using (StreamReader reader = new StreamReader(ConvertPath(_configFile)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        configContent += line;
                    }
                }

                ClassMinerConfigObject = JsonConvert.DeserializeObject<ClassMinerConfig>(configContent);

                if (!ClassMinerConfigObject.mining_enable_proxy)
                {
                    ClassMinerConfigObject.mining_wallet_address =
                        ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);
                    ClassConsole.WriteLine("Checking wallet address before to connect..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                    bool walletAddressCorrected = false;
                    bool checkWalletAddress = ClassTokenNetwork
                        .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;
                    while (ClassMinerConfigObject.mining_wallet_address.Length <
                           ClassConnectorSetting.MinWalletAddressSize ||
                           ClassMinerConfigObject.mining_wallet_address.Length >
                           ClassConnectorSetting.MaxWalletAddressSize || !checkWalletAddress)
                    {
                        Console.WriteLine(
                            "Invalid wallet address inside your config.ini file - Please, write your wallet address to start your solo mining: ");
                        ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                        ClassMinerConfigObject.mining_wallet_address =
                            ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);
                        ClassConsole.WriteLine("Checking wallet address before to connect..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                        walletAddressCorrected = true;
                        checkWalletAddress = ClassTokenNetwork
                            .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;
                    }

                    ClassConsole.WriteLine("Wallet address: " + ClassMinerConfigObject.mining_wallet_address + " is valid.", ClassConsoleColorEnumeration.ConsoleTextColorGreen);

                    if (walletAddressCorrected)
                    {
                        WriteMinerConfig();
                    }

                    if (!configContent.Contains("mining_enable_automatic_thread_affinity") ||
                        !configContent.Contains("mining_manual_thread_affinity") ||
                        !configContent.Contains("mining_enable_cache") ||
                        !configContent.Contains("mining_show_calculation_speed") || !configContent.Contains("mining_enable_pow_mining_way"))
                    {
                        ClassConsole.WriteLine(
                            "Config.json has been updated, a new option has been implemented.",
                            3);
                        WriteMinerConfig();
                    }

                    if (ClassMinerConfigObject.mining_enable_cache)
                    {
                        InitializeMiningCache();
                    }

                    return true;
                }
                if (!configContent.Contains("mining_enable_automatic_thread_affinity") ||
                    !configContent.Contains("mining_manual_thread_affinity") ||
                    !configContent.Contains("mining_enable_cache") ||
                    !configContent.Contains("mining_show_calculation_speed") || !configContent.Contains("mining_enable_pow_mining_way"))
                {
                    ClassConsole.WriteLine(
                        "Config.json has been updated, mining thread affinity, mining cache settings are implemented, close your solo miner and edit those settings if you want to enable them.",
                        3);
                    WriteMinerConfig();
                }

                if (ClassMinerConfigObject.mining_enable_cache)
                {
                    InitializeMiningCache();
                }

                return true;
            }

            return false;
        }

        /// <summary>
        ///     Initialize mining cache.
        /// </summary>
        private static void InitializeMiningCache()
        {
            ClassConsole.WriteLine("Be carefull, the mining cache feature is in beta and can use a lot of RAM, this function is not tested at 100% and need more features for probably provide more luck on mining.", ClassConsoleColorEnumeration.ConsoleTextColorRed);

            if (ClassMining.DictionaryCacheMining == null)
            {
                ClassMining.DictionaryCacheMining = new ClassMiningCache();
            }
            ClassMining.DictionaryCacheMining?.CleanMiningCache();
        }

        /// <summary>
        ///     Force to close the process of the program by CTRL+C
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            Console.WriteLine("Closing miner.");
            Process.GetCurrentProcess().Kill();
        }

        /// <summary>
        ///     Write miner config file.
        /// </summary>
        private static void WriteMinerConfig()
        {
            ClassConsole.WriteLine("Save: " + ConvertPath(_configFile), 1);
            File.Create(ConvertPath(_configFile)).Close();
            using (StreamWriter writeConfig = new StreamWriter(ConvertPath(_configFile))
            {
                AutoFlush = true
            })
            {
                writeConfig.Write(JsonConvert.SerializeObject(ClassMinerConfigObject, Formatting.Indented));
            }
        }




        /// <summary>
        ///     Calculate the hashrate of the solo miner.
        /// </summary>
        public static void CalculateHashrate()
        {
            if (!_calculateHashrateEnabled)
            {
                _calculateHashrateEnabled = true;
                Task.Factory.StartNew(async () =>
                {
                    var counterTime = 0;
                    while (true)
                    {
                        try
                        {
                            float totalRoundHashrate = 0;

                            float totalRoundCalculation = 0;


                            for (int i = 0; i < TotalMiningHashrateRound.Count; i++)
                            {
                                if (i < TotalMiningHashrateRound.Count)
                                {
                                    totalRoundHashrate += TotalMiningHashrateRound[i];
                                    totalRoundCalculation += TotalMiningCalculationRound[i];
                                    if (counterTime >= HashrateIntervalCalculation && CanMining)
                                    {
                                        if (ClassMinerConfigObject.mining_show_calculation_speed)
                                        {
                                            ClassConsole.WriteLine(
                                                "Encryption Speed Thread " + i + " : " + TotalMiningHashrateRound[i] +
                                                " H/s | Calculation Speed Thread " + i + " : " +
                                                TotalMiningCalculationRound[i] + " C/s", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                        }
                                        else
                                        {
                                            ClassConsole.WriteLine(
                                                "Encryption Speed Thread " + i + " : " + TotalMiningHashrateRound[i] +
                                                " H/s", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                        }
                                    }
                                }
                            }


                            TotalHashrate = totalRoundHashrate;

                            TotalCalculation = totalRoundCalculation;

                            for (int i = 0; i < TotalMiningHashrateRound.Count; i++)
                            {
                                if (i < TotalMiningHashrateRound.Count)
                                {
                                    TotalMiningCalculationRound[i] = 0;
                                    TotalMiningHashrateRound[i] = 0;
                                }
                            }

                            if (CanMining)
                            {
                                if (counterTime == HashrateIntervalCalculation)
                                {
                                    if (ClassMinerConfigObject.mining_show_calculation_speed)
                                    {
                                        ClassConsole.WriteLine(
                                            TotalHashrate + " H/s | " + TotalCalculation + " C/s  > ACCEPTED[" +
                                            TotalBlockAccepted + "] REFUSED[" +
                                            TotalBlockRefused + "]", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                    }
                                    else
                                    {
                                        ClassConsole.WriteLine(
                                            TotalHashrate + " H/s | ACCEPTED[" +
                                            TotalBlockAccepted + "] REFUSED[" +
                                            TotalBlockRefused + "]", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                    }
                                }

                                if (counterTime < HashrateIntervalCalculation)
                                {
                                    counterTime++;
                                }
                                else
                                {
                                    counterTime = 0;
                                }

                                if (ClassMinerConfigObject.mining_enable_proxy
                                ) // Share hashrate information to the proxy solo miner.
                                {
                                    if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                        ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ShareHashrate +
                                        ClassConnectorSetting.PacketContentSeperator + TotalHashrate, string.Empty))
                                    {
                                        ClassMiningNetwork.DisconnectNetwork();
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Ignored.
                        }

                        await Task.Delay(1000);
                    }
                }).ConfigureAwait(false);
            }
        }

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
    }
}