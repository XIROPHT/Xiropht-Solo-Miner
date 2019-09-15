using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xiropht_Connector_All.RPC.Token;
using Xiropht_Connector_All.Seed;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Connector_All.Utils;
using Xiropht_Solo_Miner.Cache;
using Xiropht_Solo_Miner.ConsoleMiner;
using Xiropht_Solo_Miner.Setting;
using Xiropht_Solo_Miner.Token;
using Xiropht_Solo_Miner.Utility;
using ClassAlgoMining = Xiropht_Solo_Miner.Algo.ClassAlgoMining;

namespace Xiropht_Solo_Miner
{
    class Program
    {
        private const int HashrateIntervalCalculation = 10;
        private const int TimeoutPacketReceived = 60; // Max 60 seconds.


        /// <summary>
        ///     Threading.
        /// </summary>
        private const int ThreadCheckNetworkInterval = 1 * 1000; // Check each 5 seconds.

        private const int TotalConfigLine = 9;
        private const string OldConfigFile = "\\config.ini";
        private const string ConfigFile = "\\config.json";
        private const string WalletCacheFile = "\\wallet-cache.xiro";
        public const string MiningInnacurateMathCacheFile = "\\mining-innacurate-math.cache";

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
        private static ClassSeedNodeConnector ObjectSeedNodeNetwork;

        private static string CertificateConnection;
        public static bool IsConnected;
        public static bool CanMining;
        private static bool LoginAccepted;
        public static int TotalShareAccepted;
        public static int TotalShareInvalid;
        public static long LastPacketReceived;
        private static Task[] ThreadMining;
        private static CancellationTokenSource CancellationTaskMining;
        private static CancellationTokenSource CancellationTaskNetwork;
        public static List<int> TotalMiningHashrateRound = new List<int>();
        public static List<int> TotalMiningCalculationRound = new List<int>();
        public static float TotalHashrate;
        public static float TotalCalculation;
        private static bool CheckConnectionStarted;

        /// <summary>
        ///     Encryption informations and objects.
        /// </summary>
        private static byte[] CurrentAesKeyBytes;

        private static byte[] CurrentAesIvBytes;
        public static int CurrentRoundAesRound;
        public static int CurrentRoundAesSize;
        public static string CurrentRoundAesKey;
        public static int CurrentRoundXorKey;

        /// <summary>
        ///     Wallet mining.
        /// </summary>
        private static bool CalculateHashrateEnabled;

        private static string MalformedPacket;
        public static ClassMinerConfig ClassMinerConfigObject;

        public static ClassMiningCache DictionaryCacheMining;

        public static Dictionary<string, string> DictionaryWalletAddressCache = new Dictionary<string, string>();
        public static bool IsLinux;

        /// <summary>
        ///     Main
        /// </summary>
        /// <param name="args"></param>
        [STAThread]
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

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                IsLinux = true;
            }

            LoadWalletAddressCache();

            if (File.Exists(GetCurrentPathConfig(OldConfigFile)))
            {
                if (LoadConfig(true))
                {
                    ThreadMining = new Task[ClassMinerConfigObject.mining_thread];
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

                    ClassConsole.WriteLine("Connecting to the network..", 2);
                    Task.Factory.StartNew(StartConnectMinerAsync).ConfigureAwait(false);
                }
                else
                {
                    ClassConsole.WriteLine(
                        "config file invalid, do you want to follow instructions to setting again your config file ? [Y/N]",
                        2);
                    string choose = Console.ReadLine();
                    if (choose.ToLower() == "y")
                    {
                        FirstSettingConfig();
                    }
                    else
                    {
                        ClassConsole.WriteLine("Close solo miner program.", 0);
                        Process.GetCurrentProcess().Kill();
                    }
                }
            }
            else
            {
                if (File.Exists(GetCurrentPathConfig(ConfigFile)))
                {
                    if (LoadConfig(false))
                    {
                        ThreadMining = new Task[ClassMinerConfigObject.mining_thread];
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

                        ClassConsole.WriteLine("Connecting to the network..", 2);
                        Task.Factory.StartNew(StartConnectMinerAsync).ConfigureAwait(false);
                    }
                    else
                    {
                        ClassConsole.WriteLine(
                            "config file invalid, do you want to follow instructions to setting again your config file ? [Y/N]",
                            2);
                        string choose = Console.ReadLine();
                        if (choose.ToLower() == "y")
                        {
                            FirstSettingConfig();
                        }
                        else
                        {
                            ClassConsole.WriteLine("Close solo miner program.", 0);
                            Process.GetCurrentProcess().Kill();
                        }
                    }
                }
                else
                {
                    FirstSettingConfig();
                }
            }

            ClassConsole.WriteLine("Command Line: h -> show hashrate.", 4);
            ClassConsole.WriteLine("Command Line: d -> show current difficulty.", 4);
            ClassConsole.WriteLine("Command Line: r -> show current range", 4);




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

                    }
                }
            });
            threadCommand.Start();
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
                ClassConsole.WriteLine("Loading wallet address cache..", 2);
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

                ClassConsole.WriteLine("Loading wallet address cache successfully loaded.", 1);
            }
        }

        /// <summary>
        /// Save wallet address cache.
        /// </summary>
        /// <param name="walletAddress"></param>
        public static void SaveWalletAddressCache(string walletAddress)
        {
            ClassConsole.WriteLine("Save wallet address cache..", 2);
            if (!File.Exists(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                File.Create(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)).Close();
            }

            using (var writer = new StreamWriter(ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                writer.WriteLine(walletAddress);
            }

            ClassConsole.WriteLine("Save wallet address cache successfully done.", 2);

        }


        /// <summary>
        ///     First time to setting config file.
        /// </summary>
        private static void FirstSettingConfig()
        {
            ClassMinerConfigObject = new ClassMinerConfig();
            ClassConsole.WriteLine("Do you want to use a proxy instead seed node? [Y/N]", 2);
            var choose = Console.ReadLine();

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

                ClassConsole.WriteLine("Checking wallet address..", 4);
                bool checkWalletAddress = ClassTokenNetwork
                    .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;

                while (ClassMinerConfigObject.mining_wallet_address.Length <
                       ClassConnectorSetting.MinWalletAddressSize ||
                       ClassMinerConfigObject.mining_wallet_address.Length >
                       ClassConnectorSetting.MaxWalletAddressSize || !checkWalletAddress)
                {
                    Console.WriteLine(
                        "Invalid wallet address - Please, write your wallet address to start your solo mining: ");
                    ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                    ClassMinerConfigObject.mining_wallet_address =
                        ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);
                    ClassConsole.WriteLine("Checking wallet address..", 4);
                    checkWalletAddress = ClassTokenNetwork
                        .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;
                }

                if (checkWalletAddress)
                {
                    ClassConsole.WriteLine(
                        "Wallet address: " + ClassMinerConfigObject.mining_wallet_address + " is valid.", 1);
                }
            }

            Console.WriteLine("How many threads do you want to run? Number of cores detected: " +
                              Environment.ProcessorCount);

            var tmp = Console.ReadLine();
            if (!int.TryParse(tmp, out ClassMinerConfigObject.mining_thread))
            {
                ClassMinerConfigObject.mining_thread = Environment.ProcessorCount;
            }

            ThreadMining = new Task[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.Sha512ManagedMining = new SHA512Managed[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.CryptoTransformMining = new ICryptoTransform[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.AesManagedMining = new AesManaged[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.CryptoStreamMining = new CryptoStream[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.MemoryStreamMining = new MemoryStream[ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.TotalNonceMining = new int [ClassMinerConfigObject.mining_thread];


            Console.WriteLine("Do you want share job range per thread ? [Y/N]");
            choose = Console.ReadLine();
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

            ClassConsole.WriteLine("Start to connect to the network..", 2);
            Task.Factory.StartNew(StartConnectMinerAsync).ConfigureAwait(false);
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
                    string line = string.Empty;
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
                                ClassConsole.WriteLine("Checking wallet address before to connect..", 4);

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
                                    ClassConsole.WriteLine("Checking wallet address before to connect..", 4);
                                    walletAddressCorrected = true;
                                    checkWalletAddress = ClassTokenNetwork
                                        .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address)
                                        .Result;
                                }

                                if (checkWalletAddress)
                                {
                                    ClassConsole.WriteLine(
                                        "Wallet address: " + ClassMinerConfigObject.mining_wallet_address +
                                        " is valid.", 1);
                                }
                            }

                            totalConfigLine++;
                        }

                        if (configLine.Contains("MINING_THREAD=") && !miningThreadLineFound)
                        {
                            miningThreadLineFound = true;
                            if (!int.TryParse(configLine.Replace("MINING_THREAD=", ""),
                                out ClassMinerConfigObject.mining_thread))
                            {
                                ClassConsole.WriteLine("MINING_THREAD line contain an invalid integer value.", 3);
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
                                ClassConsole.WriteLine("PROXY_PORT= line contain an invalid integer value.", 3);
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
                                    "MINING_PERCENT_DIFFICULTY_START= line contain an invalid integer value.", 3);
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
                                    "MINING_PERCENT_DIFFICULTY_END= line contain an invalid integer value.", 3);
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
                using (StreamReader reader = new StreamReader(GetCurrentPathConfig(ConfigFile)))
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
                    ClassConsole.WriteLine("Checking wallet address before to connect..", 4);
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
                        ClassConsole.WriteLine("Checking wallet address before to connect..", 4);
                        walletAddressCorrected = true;
                        checkWalletAddress = ClassTokenNetwork
                            .CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;
                    }

                    if (checkWalletAddress)
                    {
                        ClassConsole.WriteLine(
                            "Wallet address: " + ClassMinerConfigObject.mining_wallet_address + " is valid.", 1);
                    }

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
            ClassConsole.WriteLine(
                "Be carefull, the mining cache feature is in beta and can use a lot of RAM, this function is not tested at 100% and need more features for probably provide more luck on mining.",
                3);

            if (DictionaryCacheMining == null)
            {
                DictionaryCacheMining = new ClassMiningCache();
            }
            DictionaryCacheMining?.CleanMiningCache();
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
            ClassConsole.WriteLine("Save: " + GetCurrentPathConfig(ConfigFile), 1);
            File.Create(GetCurrentPathConfig(ConfigFile)).Close();
            using (StreamWriter writeConfig = new StreamWriter(GetCurrentPathConfig(ConfigFile))
            {
                AutoFlush = true
            })
            {
                writeConfig.Write(JsonConvert.SerializeObject(ClassMinerConfigObject, Formatting.Indented));
            }
        }

        /// <summary>
        ///     Start to connect the miner to the network
        /// </summary>
        private static async Task<bool> StartConnectMinerAsync()
        {
            CertificateConnection = ClassUtils.GenerateCertificate();
            MalformedPacket = string.Empty;


            ObjectSeedNodeNetwork?.DisconnectToSeed();


            ObjectSeedNodeNetwork = new ClassSeedNodeConnector();

            CancellationTaskNetwork = new CancellationTokenSource();

            if (!ClassMinerConfigObject.mining_enable_proxy)
            {
                while (!await ObjectSeedNodeNetwork.StartConnectToSeedAsync(string.Empty,
                    ClassConnectorSetting.SeedNodePort))
                {
                    ClassConsole.WriteLine("Can't connect to the network, retry in 5 seconds..", 3);
                    await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                }
            }
            else
            {
                while (!await ObjectSeedNodeNetwork.StartConnectToSeedAsync(ClassMinerConfigObject.mining_proxy_host,
                    ClassMinerConfigObject.mining_proxy_port))
                {
                    ClassConsole.WriteLine("Can't connect to the proxy, retry in 5 seconds..", 3);
                    await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                }
            }

            if (!ClassMinerConfigObject.mining_enable_proxy)
            {
                ClassConsole.WriteLine("Miner connected to the network, generate certificate connection..", 2);
            }

            if (!ClassMinerConfigObject.mining_enable_proxy)
            {
                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(CertificateConnection, string.Empty, false,
                    false))
                {
                    IsConnected = false;
                    return false;
                }
            }

            LastPacketReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (!ClassMinerConfigObject.mining_enable_proxy)
            {

                ClassConsole.WriteLine("Send wallet address for login your solo miner..", 2);
                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                    ClassConnectorSettingEnumeration.MinerLoginType + ClassConnectorSetting.PacketContentSeperator +
                    ClassMinerConfigObject.mining_wallet_address + ClassConnectorSetting.PacketSplitSeperator,
                    CertificateConnection, false, true))
                {
                    IsConnected = false;
                    return false;
                }
            }
            else
            {
                ClassConsole.WriteLine("Send wallet address for login your solo miner..", 2);
                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                    ClassConnectorSettingEnumeration.MinerLoginType + ClassConnectorSetting.PacketContentSeperator +
                    ClassMinerConfigObject.mining_wallet_address + ClassConnectorSetting.PacketContentSeperator +
                    ClassMinerConfigObject.mining_percent_difficulty_end +
                    ClassConnectorSetting.PacketContentSeperator +
                    ClassMinerConfigObject.mining_percent_difficulty_start +
                    ClassConnectorSetting.PacketContentSeperator +
                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty, false, false))
                {
                    IsConnected = false;
                    return false;
                }
            }

            IsConnected = true;
            ListenNetwork();
            if (!CheckConnectionStarted)
            {
                CheckConnectionStarted = true;
                CheckNetwork();
            }

            return true;
        }

        /// <summary>
        ///     Check the connection of the miner to the network.
        /// </summary>
        private static void CheckNetwork()
        {
            Task.Factory.StartNew(async delegate()
            {
                ClassConsole.WriteLine("Check connection enabled.", 2);
                await Task.Delay(ThreadCheckNetworkInterval);
                while (true)
                {
                    try
                    {
                        if (!IsConnected || !LoginAccepted || !ObjectSeedNodeNetwork.ReturnStatus() ||
                            LastPacketReceived + TimeoutPacketReceived < DateTimeOffset.Now.ToUnixTimeSeconds())
                        {
                            ClassConsole.WriteLine("Miner connection lost or aborted, retry to connect..", 3);
                            StopMining();
                            CurrentBlockId = "";
                            CurrentBlockHash = "";
                            DisconnectNetwork();
                            while (!await StartConnectMinerAsync())
                            {
                                ClassConsole.WriteLine("Can't connect to the proxy, retry in 5 seconds..", 3);
                                await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                            }
                        }
                    }
                    catch
                    {
                        ClassConsole.WriteLine("Miner connection lost or aborted, retry to connect..", 3);
                        StopMining();
                        CurrentBlockId = "";
                        CurrentBlockHash = "";
                        DisconnectNetwork();
                        while (!await StartConnectMinerAsync())
                        {
                            ClassConsole.WriteLine("Can't connect to the proxy, retry in 5 seconds..", 3);
                            await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                        }
                    }

                    await Task.Delay(ThreadCheckNetworkInterval);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
        }

        /// <summary>
        ///     Force disconnect the miner.
        /// </summary>
        private static void DisconnectNetwork()
        {
            IsConnected = false;
            LoginAccepted = false;

            try
            {
                if (CancellationTaskNetwork != null)
                {
                    if (!CancellationTaskNetwork.IsCancellationRequested)
                    {
                        CancellationTaskNetwork.Cancel();
                    }
                }
            }
            catch
            {

            }

            try
            {
                ObjectSeedNodeNetwork?.DisconnectToSeed();
            }
            catch
            {
            }

            StopMining();
        }

        /// <summary>
        ///     Listen packet received from blockchain.
        /// </summary>
        private static void ListenNetwork()
        {
            try
            {
                Task.Factory.StartNew(async delegate()
                    {
                        while (true)
                        {
                            try
                            {
                                CancellationTaskNetwork.Token.ThrowIfCancellationRequested();

                                if (!ClassMinerConfigObject.mining_enable_proxy)
                                {
                                    string packet =
                                        await ObjectSeedNodeNetwork.ReceivePacketFromSeedNodeAsync(
                                            CertificateConnection,
                                            false,
                                            true);
                                    if (packet == ClassSeedNodeStatus.SeedError)
                                    {
                                        ClassConsole.WriteLine("Network error received. Waiting network checker..", 3);
                                        DisconnectNetwork();
                                        break;
                                    }

                                    if (packet.Contains(ClassConnectorSetting.PacketSplitSeperator))
                                    {
                                        if (MalformedPacket != null)
                                        {
                                            if (!string.IsNullOrEmpty(MalformedPacket))
                                            {
                                                packet = MalformedPacket + packet;
                                                MalformedPacket = string.Empty;
                                            }
                                        }

                                        var splitPacket = packet.Split(
                                            new[] {ClassConnectorSetting.PacketSplitSeperator},
                                            StringSplitOptions.None);
                                        foreach (var packetEach in splitPacket)
                                        {
                                            if (!string.IsNullOrEmpty(packetEach))
                                            {
                                                if (packetEach.Length > 1)
                                                {
                                                    var packetRequest =
                                                        packetEach.Replace(ClassConnectorSetting.PacketSplitSeperator,
                                                            "");
                                                    if (packetRequest == ClassSeedNodeStatus.SeedError)
                                                    {
                                                        ClassConsole.WriteLine(
                                                            "Network error received. Waiting network checker..", 3);
                                                        DisconnectNetwork();
                                                        break;
                                                    }

                                                    if (packetRequest != ClassSeedNodeStatus.SeedNone &&
                                                        packetRequest != ClassSeedNodeStatus.SeedError)
                                                    {
                                                        await HandlePacketMiningAsync(packetRequest);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (MalformedPacket != null && (MalformedPacket.Length < int.MaxValue - 1 ||
                                                                        (long) (MalformedPacket.Length +
                                                                                packet.Length) <
                                                                        int.MaxValue - 1))
                                        {
                                            MalformedPacket += packet;
                                        }
                                        else
                                        {
                                            MalformedPacket = string.Empty;
                                        }
                                    }
                                }
                                else
                                {
                                    string packet =
                                        await ObjectSeedNodeNetwork.ReceivePacketFromSeedNodeAsync(string.Empty, false,
                                            false);
                                    if (packet == ClassSeedNodeStatus.SeedError)
                                    {
                                        ClassConsole.WriteLine("Network error received. Waiting network checker..", 3);
                                        DisconnectNetwork();
                                        break;
                                    }

                                    if (packet != ClassSeedNodeStatus.SeedNone)
                                    {
                                        await HandlePacketMiningAsync(packet);
                                    }
                                }
                            }
                            catch (Exception error)
                            {
                                Console.WriteLine("Listen Network error exception: " + error.Message);
                                DisconnectNetwork();
                                break;
                            }
                        }
                    }, CancellationTaskNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current)
                    .ConfigureAwait(false);
            }
            catch
            {

            }
        }

        /// <summary>
        ///     Handle packet for mining.
        /// </summary>
        /// <param name="packet"></param>
        private static async Task HandlePacketMiningAsync(string packet)
        {
            LastPacketReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
            try
            {
                var splitPacket = packet.Split(new[] {ClassConnectorSetting.PacketContentSeperator},
                    StringSplitOptions.None);
                switch (splitPacket[0])
                {
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted:
                        CalculateHashrate();
                        ClassConsole.WriteLine("Miner login accepted, start to mine..", 1);
                        LoginAccepted = true;
                        MiningProcessingRequest();
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendListBlockMethod:
                        var methodList = splitPacket[1];

                        try
                        {
                            await Task.Factory.StartNew(async delegate
                                    {
                                        if (methodList.Contains("#"))
                                        {
                                            var splitMethodList =
                                                methodList.Split(new[] {"#"}, StringSplitOptions.None);
                                            if (ListeMiningMethodName.Count > 1)
                                            {
                                                foreach (var methodName in splitMethodList)
                                                {
                                                    if (!string.IsNullOrEmpty(methodName))
                                                    {
                                                        if (ListeMiningMethodName.Contains(methodName) == false)
                                                        {
                                                            ListeMiningMethodName.Add(methodName);
                                                        }

                                                        if (!ClassMinerConfigObject.mining_enable_proxy)
                                                        {
                                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                                ClassSoloMiningPacketEnumeration
                                                                    .SoloMiningSendPacketEnumeration
                                                                    .ReceiveAskContentBlockMethod +
                                                                ClassConnectorSetting.PacketContentSeperator +
                                                                methodName + ClassConnectorSetting
                                                                    .PacketMiningSplitSeperator,
                                                                CertificateConnection, false, true))
                                                            {
                                                                DisconnectNetwork();
                                                                break;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                                ClassSoloMiningPacketEnumeration
                                                                    .SoloMiningSendPacketEnumeration
                                                                    .ReceiveAskContentBlockMethod +
                                                                ClassConnectorSetting.PacketContentSeperator +
                                                                methodName,
                                                                string.Empty,
                                                                false, false))
                                                            {
                                                                DisconnectNetwork();
                                                                break;
                                                            }
                                                        }

                                                        CancellationTaskNetwork.Token.ThrowIfCancellationRequested();
                                                        await Task.Delay(1000);
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                foreach (var methodName in splitMethodList)
                                                {
                                                    if (!string.IsNullOrEmpty(methodName))
                                                    {
                                                        if (ListeMiningMethodName.Contains(methodName) == false)
                                                        {
                                                            ListeMiningMethodName.Add(methodName);
                                                        }

                                                        if (!ClassMinerConfigObject.mining_enable_proxy)
                                                        {
                                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                                ClassSoloMiningPacketEnumeration
                                                                    .SoloMiningSendPacketEnumeration
                                                                    .ReceiveAskContentBlockMethod +
                                                                ClassConnectorSetting.PacketContentSeperator +
                                                                methodName + ClassConnectorSetting
                                                                    .PacketMiningSplitSeperator,
                                                                CertificateConnection, false, true))
                                                            {
                                                                DisconnectNetwork();
                                                                break;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                                ClassSoloMiningPacketEnumeration
                                                                    .SoloMiningSendPacketEnumeration
                                                                    .ReceiveAskContentBlockMethod +
                                                                ClassConnectorSetting.PacketContentSeperator +
                                                                methodName,
                                                                string.Empty,
                                                                false, false))
                                                            {
                                                                DisconnectNetwork();
                                                                break;
                                                            }
                                                        }

                                                        CancellationTaskNetwork.Token.ThrowIfCancellationRequested();
                                                        await Task.Delay(1000);
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (ListeMiningMethodName.Contains(methodList) == false)
                                            {
                                                ListeMiningMethodName.Add(methodList);
                                            }

                                            if (!ClassMinerConfigObject.mining_enable_proxy)
                                            {
                                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveAskContentBlockMethod +
                                                    ClassConnectorSetting.PacketContentSeperator + methodList +
                                                    ClassConnectorSetting.PacketMiningSplitSeperator,
                                                    CertificateConnection, false,
                                                    true))
                                                {
                                                    DisconnectNetwork();
                                                }
                                            }
                                            else
                                            {
                                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveAskContentBlockMethod +
                                                    ClassConnectorSetting.PacketContentSeperator + methodList,
                                                    string.Empty,
                                                    false,
                                                    false))
                                                {
                                                    DisconnectNetwork();
                                                }
                                            }
                                        }

                                    }, CancellationTaskNetwork.Token, TaskCreationOptions.LongRunning,
                                    TaskScheduler.Current)
                                .ConfigureAwait(false);
                        }
                        catch
                        {

                        }

                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendContentBlockMethod:
                        if (ListeMiningMethodContent.Count == 0)
                        {
                            ListeMiningMethodContent.Add(splitPacket[1]);
                        }
                        else
                        {
                            ListeMiningMethodContent[0] = splitPacket[1];
                        }

                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendCurrentBlockMining:

                        bool proxy = false;
                        var splitBlockContent = splitPacket[1].Split(new[] {"&"}, StringSplitOptions.None);

                        if (CurrentBlockId != splitBlockContent[0].Replace("ID=", "") ||
                            CurrentBlockHash != splitBlockContent[1].Replace("HASH=", ""))
                        {
                            if (!ClassMinerConfigObject.mining_enable_proxy)
                            {
                                ClassConsole.WriteLine("New block to mine " + splitBlockContent[0], 2);
                            }
                            else
                            {
                                ClassConsole.WriteLine("Current block to mine " + splitBlockContent[0], 2);
                            }

                            try
                            {
                                if (CurrentBlockId == splitBlockContent[0].Replace("ID=", ""))
                                {
                                    ClassConsole.WriteLine(
                                        "Current Block ID: " + CurrentBlockId + " has been renewed.", 3);
                                }

                                CurrentBlockId = splitBlockContent[0].Replace("ID=", "");
                                CurrentBlockHash = splitBlockContent[1].Replace("HASH=", "");
                                CurrentBlockAlgorithm = splitBlockContent[2].Replace("ALGORITHM=", "");
                                CurrentBlockSize = splitBlockContent[3].Replace("SIZE=", "");
                                CurrentBlockMethod = splitBlockContent[4].Replace("METHOD=", "");
                                CurrentBlockKey = splitBlockContent[5].Replace("KEY=", "");
                                CurrentBlockJob = splitBlockContent[6].Replace("JOB=", "");
                                CurrentBlockReward = splitBlockContent[7].Replace("REWARD=", "");
                                CurrentBlockDifficulty = splitBlockContent[8].Replace("DIFFICULTY=", "");
                                CurrentBlockTimestampCreate = splitBlockContent[9].Replace("TIMESTAMP=", "");
                                CurrentBlockIndication = splitBlockContent[10].Replace("INDICATION=", "");
                                CurrentBlockNetworkHashrate = splitBlockContent[11].Replace("NETWORK_HASHRATE=", "");
                                CurrentBlockLifetime = splitBlockContent[12].Replace("LIFETIME=", "");



                                StopMining();
                                if (ClassMinerConfigObject.mining_enable_cache)
                                {
                                    ClearMiningCache();
                                }

                                CanMining = true;
                                var splitCurrentBlockJob = CurrentBlockJob.Split(new[] {";"}, StringSplitOptions.None);
                                var minRange = decimal.Parse(splitCurrentBlockJob[0]);
                                var maxRange = decimal.Parse(splitCurrentBlockJob[1]);


                                if (ClassMinerConfigObject.mining_enable_proxy)
                                {
                                    ClassConsole.WriteLine(
                                        "Job range received from proxy: " + minRange +
                                        ClassConnectorSetting.PacketContentSeperator + maxRange + "", 2);
                                }


                                int idMethod = 0;
                                if (ListeMiningMethodName.Count >= 1)
                                {
                                    for (int i = 0; i < ListeMiningMethodName.Count; i++)
                                    {
                                        if (i < ListeMiningMethodName.Count)
                                        {
                                            if (ListeMiningMethodName[i] == CurrentBlockMethod)
                                            {
                                                idMethod = i;
                                            }
                                        }
                                    }
                                }

                                var splitMethod = ListeMiningMethodContent[idMethod]
                                    .Split(new[] {"#"}, StringSplitOptions.None);

                                CurrentRoundAesRound = int.Parse(splitMethod[0]);
                                CurrentRoundAesSize = int.Parse(splitMethod[1]);
                                CurrentRoundAesKey = splitMethod[2];
                                CurrentRoundXorKey = int.Parse(splitMethod[3]);



                                for (int i = 0; i < ClassMinerConfigObject.mining_thread; i++)
                                {
                                    if (i < ClassMinerConfigObject.mining_thread)
                                    {
                                        int iThread = i;
                                        ThreadMining[i] = new Task(() => InitializeMiningThread(iThread),
                                            CancellationTaskMining.Token);
                                        ThreadMining[i].Start();
                                    }
                                }
                            }
                            catch (Exception error)
                            {
                                ClassConsole.WriteLine(
                                    "Block template not completly received, stop mining and ask again the blocktemplate | Exception: " +
                                    error.Message,
                                    2);
                                StopMining();
                            }
                        }

                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus:
                        switch (splitPacket[1])
                        {
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareUnlock:
                                TotalBlockAccepted++;
                                ClassConsole.WriteLine("Block accepted, stop mining, wait new block.", 1);
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareWrong:
                                TotalBlockRefused++;
                                ClassConsole.WriteLine("Block not accepted, stop mining, wait new block.", 3);
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareAleady:
                                if (CurrentBlockId == splitPacket[1])
                                {
                                    ClassConsole.WriteLine(
                                        splitPacket[1] +
                                        " Orphaned, someone already got it, stop mining, wait new block.", 3);
                                }
                                else
                                {
                                    ClassConsole.WriteLine(splitPacket[1] + " Orphaned, someone already get it.", 3);
                                }

                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareNotExist:
                                ClassConsole.WriteLine("Block mined does not exist, stop mining, wait new block.", 4);
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareGood:
                                TotalShareAccepted++;
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad:
                                ClassConsole.WriteLine(
                                    "Block not accepted, someone already got it or your share is invalid.", 3);
                                TotalShareInvalid++;
                                break;
                        }

                        break;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        ///     Ask new mining method and current blocktemplate automaticaly.
        /// </summary>
        private static void MiningProcessingRequest()
        {
            try
            {
                Task.Factory.StartNew(async delegate()
                    {
                        if (!ClassMinerConfigObject.mining_enable_proxy)
                        {
                            while (IsConnected)
                            {
                                CancellationTaskNetwork.Token.ThrowIfCancellationRequested();

                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                        .ReceiveAskListBlockMethod + ClassConnectorSetting.PacketMiningSplitSeperator,
                                    CertificateConnection, false, true))
                                {
                                    DisconnectNetwork();
                                    break;
                                }

                                await Task.Delay(1000);
                                if (ListeMiningMethodContent.Count > 0)
                                {
                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                        ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                            .ReceiveAskCurrentBlockMining +
                                        ClassConnectorSetting.PacketMiningSplitSeperator, CertificateConnection, false,
                                        true))
                                    {
                                        DisconnectNetwork();
                                        break;
                                    }
                                }

                                await Task.Delay(100);
                            }
                        }
                        else
                        {
                            while (IsConnected)
                            {
                                CancellationTaskNetwork.Token.ThrowIfCancellationRequested();
                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                        .ReceiveAskListBlockMethod,
                                    string.Empty, false, false))
                                {
                                    DisconnectNetwork();
                                }

                                await Task.Delay(1000);

                                if (ListeMiningMethodContent.Count > 0)
                                {
                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                        ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                            .ReceiveAskCurrentBlockMining, string.Empty, false, false))
                                    {
                                        DisconnectNetwork();
                                    }
                                }

                                await Task.Delay(100);
                            }
                        }
                    }, CancellationTaskNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current)
                    .ConfigureAwait(false);
            }
            catch
            {

            }
        }

        /// <summary>
        ///     Clear Mining Cache
        /// </summary>
        private static void ClearMiningCache()
        {

            bool error = true;
            while (error)
            {
                try
                {

                    ClassConsole.WriteLine(
                        "Clear mining cache | total calculation cached: " +
                        DictionaryCacheMining.Count.ToString("F0"), 5);
                    DictionaryCacheMining.CleanMiningCache();

                    error = false;
                }
                catch
                {
                    error = true;
                }
            }

        }

        /// <summary>
        ///     Stop mining.
        /// </summary>
        private static void StopMining()
        {
            CanMining = false;
            CancelTaskMining();

            try
            {
                for (int i = 0; i < ThreadMining.Length; i++)
                {
                    if (i < ThreadMining.Length)
                    {
                        if (ThreadMining[i] != null)
                        {
                            bool error = true;
                            while (error)
                            {
                                try
                                {
                                    if (ThreadMining[i] != null)
                                    {
                                        if (ThreadMining[i] != null)
                                        {
                                            ThreadMining[i].Dispose();
                                            GC.SuppressFinalize(ThreadMining[i]);
                                        }
                                    }

                                    error = false;
                                }
                                catch
                                {
                                    CancelTaskMining();
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            CancellationTaskMining = new CancellationTokenSource();
        }

        /// <summary>
        /// Cancel task of mining.
        /// </summary>
        private static void CancelTaskMining()
        {
            try
            {
                CancellationTaskMining?.Cancel();
            }
            catch
            {
            }

        }

        /// <summary>
        ///     Initialization of the mining thread executed.
        /// </summary>
        /// <param name="iThread"></param>
        private static void InitializeMiningThread(int iThread)
        {

            if (ClassMinerConfigObject.mining_enable_automatic_thread_affinity &&
                string.IsNullOrEmpty(ClassMinerConfigObject.mining_manual_thread_affinity))
            {
                ClassUtilityAffinity.SetAffinity(iThread);
            }
            else
            {
                if (!string.IsNullOrEmpty(ClassMinerConfigObject.mining_manual_thread_affinity))
                {
                    ClassUtilityAffinity.SetManualAffinity(ClassMinerConfigObject.mining_manual_thread_affinity);
                }
            }

            using (var pdb = new PasswordDeriveBytes(CurrentBlockKey,
                Encoding.UTF8.GetBytes(CurrentRoundAesKey)))
            {
                CurrentAesKeyBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
                CurrentAesIvBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
            }

            ClassAlgoMining.AesManagedMining[iThread] = new AesManaged()
            {
                BlockSize = CurrentRoundAesSize,
                KeySize = CurrentRoundAesSize,
                Key = CurrentAesKeyBytes,
                IV = CurrentAesIvBytes
            };
            ClassAlgoMining.CryptoTransformMining[iThread] =
                ClassAlgoMining.AesManagedMining[iThread].CreateEncryptor();

            int i1 = iThread + 1;
            var splitCurrentBlockJob = CurrentBlockJob.Split(new[] {";"}, StringSplitOptions.None);
            var minRange = decimal.Parse(splitCurrentBlockJob[0]);
            var maxRange = decimal.Parse(splitCurrentBlockJob[1]);

            var minProxyStart = maxRange - minRange;
            var incrementProxyRange = minProxyStart / ClassMinerConfigObject.mining_thread;

            switch (ClassMinerConfigObject.mining_thread_priority)
            {
                case 0:
                    Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;

                    break;
                case 1:
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                    break;
                case 2:
                    Thread.CurrentThread.Priority = ThreadPriority.Normal;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                    break;
                case 3:
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case 4:
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                    break;
            }

            if (ClassMinerConfigObject.mining_thread_spread_job)
            {
                if (ClassMinerConfigObject.mining_enable_proxy)
                {
                    if (minRange > 0)
                    {
                        decimal minRangeTmp = minRange;
                        decimal maxRangeTmp = minRangeTmp + incrementProxyRange;
                        StartMiningAsync(iThread, Math.Round(minRangeTmp, 0), (Math.Round(maxRangeTmp, 0)));
                    }
                    else
                    {
                        decimal minRangeTmp = Math.Round((maxRange / ClassMinerConfigObject.mining_thread) * (i1 - 1),
                            0);
                        decimal maxRangeTmp = (Math.Round(((maxRange / ClassMinerConfigObject.mining_thread) * i1), 0));
                        StartMiningAsync(iThread, minRangeTmp, maxRangeTmp);
                    }
                }
                else
                {
                    decimal minRangeTmp = Math.Round((maxRange / ClassMinerConfigObject.mining_thread) * (i1 - 1), 0);
                    decimal maxRangeTmp = (Math.Round(((maxRange / ClassMinerConfigObject.mining_thread) * i1), 0));
                    StartMiningAsync(iThread, minRangeTmp, maxRangeTmp);
                }
            }
            else
            {
                StartMiningAsync(iThread, minRange, maxRange);
            }
        }

        /// <summary>
        ///     Start mining.
        /// </summary>
        /// <param name="idThread"></param>
        /// <param name="minRange"></param>
        /// <param name="maxRange"></param>
        private static void StartMiningAsync(int idThread, decimal minRange, decimal maxRange)
        {
            if (minRange <= 1)
            {
                minRange = 2;
            }

            while (ListeMiningMethodName.Count == 0)
            {
                ClassConsole.WriteLine("No method content received, waiting to receive them before..", 2);
                Thread.Sleep(1000);
            }

            var currentBlockId = CurrentBlockId;
            var currentBlockTimestamp = CurrentBlockTimestampCreate;

            var currentBlockDifficulty = decimal.Parse(CurrentBlockDifficulty);


            ClassConsole.WriteLine(
                "Thread: " + idThread + " min range:" + minRange + " max range:" + maxRange + " | Host target: " +
                ObjectSeedNodeNetwork.ReturnCurrentSeedNodeHost(), 1);

            ClassConsole.WriteLine(
                "Current Mining Method: " + CurrentBlockMethod + " = AES ROUND: " + CurrentRoundAesRound +
                " AES SIZE: " + CurrentRoundAesSize + " AES BYTE KEY: " + CurrentRoundAesKey + " XOR KEY: " +
                CurrentRoundXorKey, 1);


            while (CanMining)
            {
                if (!GetCancellationMiningTaskStatus())
                {
                    CancellationTaskMining.Token.ThrowIfCancellationRequested();

                    if (ThreadMining[idThread].Status == TaskStatus.Canceled)
                    {
                        break;
                    }

                    if (CurrentBlockId != currentBlockId || currentBlockTimestamp != CurrentBlockTimestampCreate)
                    {
                        using (var pdb = new PasswordDeriveBytes(CurrentBlockKey,
                            Encoding.UTF8.GetBytes(CurrentRoundAesKey)))
                        {
                            CurrentAesKeyBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
                            CurrentAesIvBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
                        }

                        ClassAlgoMining.AesManagedMining[idThread] = new AesManaged()
                        {
                            BlockSize = CurrentRoundAesSize,
                            KeySize = CurrentRoundAesSize,
                            Key = CurrentAesKeyBytes,
                            IV = CurrentAesIvBytes
                        };
                        ClassAlgoMining.CryptoTransformMining[idThread] =
                            ClassAlgoMining.AesManagedMining[idThread].CreateEncryptor();
                        currentBlockId = CurrentBlockId;
                        currentBlockTimestamp = CurrentBlockTimestampCreate;
                        currentBlockDifficulty = decimal.Parse(CurrentBlockDifficulty);
                        if (ClassMinerConfigObject.mining_enable_cache)
                        {
                            ClearMiningCache();
                        }
                    }

                    try
                    {

                        MiningComputeProcess(idThread, minRange, maxRange, currentBlockDifficulty);

                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Generate random number.
        /// </summary>
        /// <param name="minRange"></param>
        /// <param name="maxRange"></param>
        /// <param name="rngGenerator"></param>
        /// <returns></returns>
        private static decimal GenerateRandomNumber(decimal minRange, decimal maxRange, bool rngGenerator)
        {
            decimal result = 0;


            while (CanMining)
            {
                result = !rngGenerator ? ClassUtility.GenerateNumberMathCalculation(minRange, maxRange) : ClassUtility.GetRandomBetweenJob(minRange, maxRange);

                if (result < 0)
                {
                    result *= -1;
                }

                result = Math.Round(result, 0);

                if (result >= 2 && result <= maxRange)
                {
                    break;
                }
            }


            return result;
        }

        /// <summary>
        /// Mining compute process.
        /// </summary>
        /// <param name="idThread"></param>
        /// <param name="minRange"></param>
        /// <param name="maxRange"></param>
        /// <param name="currentBlockDifficulty"></param>
        private static void MiningComputeProcess(int idThread, decimal minRange, decimal maxRange,
               decimal currentBlockDifficulty)
        {
            decimal firstNumber = GenerateRandomNumber(minRange, maxRange,
                ClassUtility.GetRandomBetween(1, 100) >= ClassUtility.GetRandomBetweenSize(1, 100));

            decimal secondNumber = GenerateRandomNumber(minRange, maxRange,
                ClassUtility.GetRandomBetween(1, 100) >= ClassUtility.GetRandomBetweenSize(1, 100));

            if (ClassMinerConfigObject.mining_enable_cache)
            {

                string mathCombinaison = firstNumber.ToString("F0") + ClassConnectorSetting.PacketContentSeperator + secondNumber.ToString("F0");

                if (!DictionaryCacheMining.CheckMathCombinaison(mathCombinaison))
                {

                    DictionaryCacheMining.InsertMathCombinaison(mathCombinaison, idThread);



                    for (var index = 0; index < ClassUtility.RandomOperatorCalculation.Length; index++)
                    {

                        if (index < ClassUtility.RandomOperatorCalculation.Length)
                        {
                            var mathOperator = ClassUtility.RandomOperatorCalculation[index];


                            string calcul = firstNumber.ToString("F0") + " " + mathOperator + " " +
                                            secondNumber.ToString("F0");

                            var testCalculationObject = TestCalculation(firstNumber, secondNumber, mathOperator,
                                idThread,
                                currentBlockDifficulty);
                            decimal calculCompute = testCalculationObject.Item2;

                            if (testCalculationObject.Item1)
                            {
                                string encryptedShare = calcul;

                                encryptedShare = MakeEncryptedShare(encryptedShare, idThread);

                                if (encryptedShare != ClassAlgoErrorEnumeration.AlgoError)
                                {
                                    string hashShare = ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);

                                    TotalMiningHashrateRound[idThread]++;
                                    if (!CanMining)
                                    {
                                        return;
                                    }

                                    if (hashShare == CurrentBlockIndication)
                                    {
                                        var compute = calculCompute;
                                        var calcul1 = calcul;
                                        Task.Factory.StartNew(async delegate
                                        {
                                            ClassConsole.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", 1);
#if DEBUG
                                            Debug.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", 1);
#endif
                                            if (!ClassMinerConfigObject.mining_enable_proxy)
                                            {
                                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version +
                                                    ClassConnectorSetting.PacketMiningSplitSeperator,
                                                    CertificateConnection, false, true))
                                                {
                                                    DisconnectNetwork();
                                                }
                                            }
                                            else
                                            {
                                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty,
                                                    false, false))
                                                {
                                                    DisconnectNetwork();
                                                }
                                            }
                                        }).ConfigureAwait(false);
                                    }

                                }
                            }
                        }

                    }


                }


                mathCombinaison = secondNumber.ToString("F0") + ClassConnectorSetting.PacketContentSeperator + firstNumber.ToString("F0");
                if (!DictionaryCacheMining.CheckMathCombinaison(mathCombinaison))
                {

                    DictionaryCacheMining.InsertMathCombinaison(mathCombinaison, idThread);




                    // Test reverted
                    for (var index = 0; index < ClassUtility.RandomOperatorCalculation.Length; index++)
                    {
                        if (index < ClassUtility.RandomOperatorCalculation.Length)
                        {
                            var mathOperator = ClassUtility.RandomOperatorCalculation[index];

                            string calcul = secondNumber.ToString("F0") + " " + mathOperator + " " +
                                            firstNumber.ToString("F0");
                            var testCalculationObject = TestCalculation(secondNumber, firstNumber, mathOperator,
                                idThread,
                                currentBlockDifficulty);
                            decimal calculCompute = testCalculationObject.Item2;



                            if (testCalculationObject.Item1)
                            {
                                string encryptedShare = calcul;

                                encryptedShare = MakeEncryptedShare(encryptedShare, idThread);
                                if (encryptedShare != ClassAlgoErrorEnumeration.AlgoError)
                                {
                                    string hashShare = ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);

                                    TotalMiningHashrateRound[idThread]++;
                                    if (!CanMining)
                                    {
                                        return;
                                    }

                                    if (hashShare == CurrentBlockIndication)
                                    {
                                        var compute = calculCompute;
                                        var calcul1 = calcul;
                                        Task.Factory.StartNew(async delegate
                                        {
                                            ClassConsole.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", 1);
#if DEBUG
                                            Debug.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", 1);
#endif
                                            if (!ClassMinerConfigObject.mining_enable_proxy)
                                            {
                                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version +
                                                    ClassConnectorSetting.PacketMiningSplitSeperator,
                                                    CertificateConnection, false, true))
                                                {
                                                    DisconnectNetwork();
                                                }
                                            }
                                            else
                                            {
                                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty,
                                                    false, false))
                                                {
                                                    DisconnectNetwork();
                                                }
                                            }
                                        }).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                for (var index = 0; index < ClassUtility.RandomOperatorCalculation.Length; index++)
                {
                    if (index < ClassUtility.RandomOperatorCalculation.Length)
                    {
                        var mathOperator = ClassUtility.RandomOperatorCalculation[index];

                        string calcul = firstNumber.ToString("F0") + " " + mathOperator + " " +
                                        secondNumber.ToString("F0");

                        var testCalculationObject = TestCalculation(firstNumber, secondNumber, mathOperator, idThread,
                            currentBlockDifficulty);
                        decimal calculCompute = testCalculationObject.Item2;


                        if (testCalculationObject.Item1)
                        {
                            string encryptedShare = calcul;

                            encryptedShare = MakeEncryptedShare(encryptedShare, idThread);

                            if (encryptedShare != ClassAlgoErrorEnumeration.AlgoError)
                            {
                                string hashShare = ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);

                                TotalMiningHashrateRound[idThread]++;
                                if (!CanMining)
                                {
                                    return;
                                }

                                if (hashShare == CurrentBlockIndication)
                                {
                                    var compute = calculCompute;
                                    var calcul1 = calcul;
                                    Task.Factory.StartNew(async delegate
                                    {
                                        ClassConsole.WriteLine(
                                            "Exact share for unlock the block seems to be found, submit it: " +
                                            calcul1 + " and waiting confirmation..\n", 1);
#if DEBUG
                                            Debug.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", 1);
#endif
                                        if (!ClassMinerConfigObject.mining_enable_proxy)
                                        {
                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version +
                                                ClassConnectorSetting.PacketMiningSplitSeperator,
                                                CertificateConnection, false, true))
                                            {
                                                DisconnectNetwork();
                                            }
                                        }
                                        else
                                        {
                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version, string.Empty,
                                                false, false))
                                            {
                                                DisconnectNetwork();
                                            }
                                        }
                                    }).ConfigureAwait(false);
                                }
                                else
                                {
                                    if (ClassMinerConfigObject.mining_enable_pow_mining_way)
                                    {
                                        ClassAlgoMining.DoPowShare(hashShare, CurrentBlockIndication, idThread, currentBlockDifficulty, calculCompute, firstNumber, secondNumber, calcul);
                                    }
                                }
                            }
                        }

                        // Test reverted
                        calcul = secondNumber.ToString("F0") + " " + mathOperator + " " +
                                 firstNumber.ToString("F0");
                        testCalculationObject = TestCalculation(secondNumber, firstNumber, mathOperator, idThread,
                            currentBlockDifficulty);
                        calculCompute = testCalculationObject.Item2;

                        if (testCalculationObject.Item1)
                        {
                            string encryptedShare = calcul;

                            encryptedShare = MakeEncryptedShare(encryptedShare, idThread);
                            if (encryptedShare != ClassAlgoErrorEnumeration.AlgoError)
                            {
                                string hashShare = ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);

                                TotalMiningHashrateRound[idThread]++;
                                if (!CanMining)
                                {
                                    return;
                                }

                                if (hashShare == CurrentBlockIndication)
                                {
                                    var compute = calculCompute;
                                    var calcul1 = calcul;
                                    Task.Factory.StartNew(async delegate
                                    {
                                        ClassConsole.WriteLine(
                                            "Exact share for unlock the block seems to be found, submit it: " +
                                            calcul1 + " and waiting confirmation..\n", 1);
#if DEBUG
                                            Debug.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", 1);
#endif
                                        if (!ClassMinerConfigObject.mining_enable_proxy)
                                        {
                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version +
                                                ClassConnectorSetting.PacketMiningSplitSeperator,
                                                CertificateConnection, false, true))
                                            {
                                                DisconnectNetwork();
                                            }
                                        }
                                        else
                                        {
                                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version, string.Empty,
                                                false, false))
                                            {
                                                DisconnectNetwork();
                                            }
                                        }
                                    }).ConfigureAwait(false);
                                }
                                else
                                {
                                    if (ClassMinerConfigObject.mining_enable_pow_mining_way)
                                    {
                                        ClassAlgoMining.DoPowShare(hashShare, CurrentBlockIndication, idThread, currentBlockDifficulty, calculCompute, secondNumber, firstNumber, calcul);
                                    }
                                }
                            }
                        }

                    }
                }
            }
        }



        /// <summary>
        /// Check if the cancellation task is done or not.
        /// </summary>
        /// <returns></returns>
        private static bool GetCancellationMiningTaskStatus()
        {
            try
            {
                if (CancellationTaskMining.IsCancellationRequested)
                {
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Test calculation, return the result and also if this one is valid for the current range.
        /// </summary>
        /// <param name="firstNumber"></param>
        /// <param name="secondNumber"></param>
        /// <param name="mathOperator"></param>
        /// <param name="idThread"></param>
        /// <param name="currentBlockDifficulty"></param>
        /// <returns></returns>
        public static Tuple<bool, decimal> TestCalculation(decimal firstNumber, decimal secondNumber,
            string mathOperator, int idThread, decimal currentBlockDifficulty)
        {
            if (firstNumber < secondNumber)
            {
                switch (mathOperator)
                {
                    case "-":
                    case "/":
                        return new Tuple<bool, decimal>(false, 0);
                }
            }

            decimal calculCompute =
                ClassUtility.ComputeCalculation(firstNumber, mathOperator, secondNumber);
            TotalMiningCalculationRound[idThread]++;


            if (calculCompute - Math.Round(calculCompute, 0) == 0
            ) // Check if the result contains decimal places, if yes ignore it. 
            {
                if (calculCompute >= 2 && calculCompute <= currentBlockDifficulty)
                {
                    return new Tuple<bool, decimal>(true, calculCompute);
                }
            }


            return new Tuple<bool, decimal>(false, calculCompute);
        }

        /// <summary>
        ///     Encrypt math calculation with the current mining method
        /// </summary>
        /// <param name="calculation"></param>
        /// <param name="idThread"></param>
        /// <returns></returns>
        public static string MakeEncryptedShare(string calculation, int idThread)
        {

            string encryptedShare = ClassUtility.StringToHex(calculation + CurrentBlockTimestampCreate);

            // Static XOR Encryption -> Key updated from the current mining method.
            encryptedShare = ClassAlgoMining.EncryptXorShare(encryptedShare, CurrentRoundXorKey.ToString());

            // Dynamic AES Encryption -> Size and Key's from the current mining method and the current block key encryption.
            for (int i = 0; i < CurrentRoundAesRound; i++)
            {
                encryptedShare = ClassAlgoMining.EncryptAesShare(encryptedShare, idThread);
            }

            // Static XOR Encryption -> Key from the current mining method
            encryptedShare = ClassAlgoMining.EncryptXorShare(encryptedShare, CurrentRoundXorKey.ToString());

            // Static AES Encryption -> Size and Key's from the current mining method.
            encryptedShare = ClassAlgoMining.EncryptAesShare(encryptedShare, idThread);


            // Generate SHA512 HASH for the share and return it.
            return ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);
        }

        /// <summary>
        ///     Calculate the hashrate of the solo miner.
        /// </summary>
        private static void CalculateHashrate()
        {
            if (!CalculateHashrateEnabled)
            {
                CalculateHashrateEnabled = true;
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
                                                TotalMiningCalculationRound[i] + " C/s", 4);
                                        }
                                        else
                                        {
                                            ClassConsole.WriteLine(
                                                "Encryption Speed Thread " + i + " : " + TotalMiningHashrateRound[i] +
                                                " H/s", 4);
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
                                            TotalBlockRefused + "]", 4);
                                    }
                                    else
                                    {
                                        ClassConsole.WriteLine(
                                            TotalHashrate + " H/s | ACCEPTED[" +
                                            TotalBlockAccepted + "] REFUSED[" +
                                            TotalBlockRefused + "]", 4);
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
                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                        ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ShareHashrate +
                                        ClassConnectorSetting.PacketContentSeperator + TotalHashrate, string.Empty,
                                        false, false))
                                    {
                                        DisconnectNetwork();
                                    }
                                }
                            }
                        }
                        catch
                        {
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