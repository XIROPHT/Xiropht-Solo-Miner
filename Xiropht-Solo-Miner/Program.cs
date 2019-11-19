using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xiropht_Connector_All.Setting;
using Xiropht_Solo_Miner.ConsoleMiner;
using Xiropht_Solo_Miner.Mining;
using Xiropht_Solo_Miner.Setting;
using Xiropht_Solo_Miner.Token;
using Xiropht_Solo_Miner.Utility;

// ReSharper disable FunctionNeverReturns

namespace Xiropht_Solo_Miner
{
    class Program
    {

        /// <summary>
        /// About configuration file.
        /// </summary>
        private static string ConfigFile = "\\config.json";
        private const string WalletCacheFile = "\\wallet-cache.xiro";
        private static Thread ThreadConsoleKey;
        public static ClassMinerConfig ClassMinerConfigObject;
        public static Dictionary<string, string> DictionaryWalletAddressCache = new Dictionary<string, string>();


        /// <summary>
        ///     Main
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            EnableUnexpectedExceptionHandler();
            Console.CancelKeyPress += Console_CancelKeyPress;
            Thread.CurrentThread.Name = Path.GetFileName(Environment.GetCommandLineArgs()[0]);
            ClassConsole.WriteLine("Xiropht Solo Miner - " + Assembly.GetExecutingAssembly().GetName().Version + "R", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);

            HandleArgumentStartup(args);
            LoadWalletAddressCache();
            InitializeMiner();
            EnableConsoleKeyCommand();
        }

        /// <summary>
        /// Enable unexpected exception handler on crash, save crash informations.
        /// </summary>
        private static void EnableUnexpectedExceptionHandler()
        {
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs args2)
            {
                var filePath = ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + "\\error_miner.txt");
                var exception = (Exception)args2.ExceptionObject;
                using (var writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine("Message :" + exception.Message + "<br/>" + Environment.NewLine + "StackTrace :" + exception.StackTrace + "" + Environment.NewLine + "Date :" + DateTime.Now);
                    writer.WriteLine(Environment.NewLine + "-----------------------------------------------------------------------------" + Environment.NewLine);
                }

                Trace.TraceError(exception.StackTrace);

                Environment.Exit(1);
            };
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
                                        ConfigFile = ClassUtility.ConvertPath(splitArgument[1]);
                                        ClassConsole.WriteLine("Enable using of custom config.json file from path: " + ClassUtility.ConvertPath(splitArgument[1]), 4);
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
                ConfigFile = ClassUtility.GetCurrentPathConfig(ConfigFile);
            }
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
        /// Initialization of the solo miner.
        /// </summary>
        private static void InitializeMiner()
        {
            if (File.Exists(ClassUtility.ConvertPath(ConfigFile)))
            {
                if (LoadConfig())
                {
                    ClassMining.InitializeMiningObjects();
                    ClassMiningStats.InitializeMiningStats();
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

            ClassMining.InitializeMiningObjects();


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

            ClassMiningStats.InitializeMiningStats();

            Console.WriteLine(
                "Select thread priority: 0 = Lowest, 1 = BelowNormal, 2 = Normal, 3 = AboveNormal, 4 = Highest [Default: 2]:");

            if (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_thread_priority))
            {
                ClassMinerConfigObject.mining_thread_priority = 2;
            }


            WriteMinerConfig();
            if (ClassMinerConfigObject.mining_enable_cache)
            {
                ClassMining.InitializeMiningCache();
            }

            ClassConsole.WriteLine("Start to connect to the network..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
            Task.Factory.StartNew(ClassMiningNetwork.StartConnectMinerAsync).ConfigureAwait(false);
        }

        /// <summary>
        ///     Write miner config file.
        /// </summary>
        private static void WriteMinerConfig()
        {
            ClassConsole.WriteLine("Save: " + ClassUtility.ConvertPath(ConfigFile), 1);
            File.Create(ClassUtility.ConvertPath(ConfigFile)).Close();
            using (StreamWriter writeConfig = new StreamWriter(ClassUtility.ConvertPath(ConfigFile))
            {
                AutoFlush = true
            })
            {
                writeConfig.Write(JsonConvert.SerializeObject(ClassMinerConfigObject, Formatting.Indented));
            }
        }

        /// <summary>
        ///     Load config file.
        /// </summary>
        /// <returns></returns>
        private static bool LoadConfig()
        {
            string configContent = string.Empty;

            using (StreamReader reader = new StreamReader(ClassUtility.ConvertPath(ConfigFile)))
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
                    ClassMining.InitializeMiningCache();
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
                ClassMining.InitializeMiningCache();
            }

            return true;
        }

        /// <summary>
        /// Load wallet address cache.
        /// </summary>
        private static void LoadWalletAddressCache()
        {
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)).Close();
            }
            else
            {
                ClassConsole.WriteLine("Loading wallet address cache..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                using (var reader =
                    new StreamReader(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
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
            if (!File.Exists(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                File.Create(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)).Close();
            }

            using (var writer = new StreamWriter(ClassUtility.ConvertPath(AppDomain.CurrentDomain.BaseDirectory + WalletCacheFile)))
            {
                writer.WriteLine(walletAddress);
            }

            ClassConsole.WriteLine("Save wallet address cache successfully done.", ClassConsoleColorEnumeration.ConsoleTextColorYellow);

        }

        /// <summary>
        /// Enable Console Key Command.
        /// </summary>
        private static void EnableConsoleKeyCommand()
        {
            ThreadConsoleKey = new Thread(delegate ()
            {
                ClassConsole.WriteLine("Command Line: " + ClassConsoleKeyCommandEnumeration.ConsoleCommandKeyHashrate + " -> show hashrate.", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                ClassConsole.WriteLine("Command Line: " + ClassConsoleKeyCommandEnumeration.ConsoleCommandKeyDifficulty + " -> show current difficulty.", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                ClassConsole.WriteLine("Command Line: " + ClassConsoleKeyCommandEnumeration.ConsoleCommandKeyRange + " -> show current range", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);

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
            ThreadConsoleKey.Start();
        }
    }
}