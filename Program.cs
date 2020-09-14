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
        private static string _configFile = "\\config.json";
        private const string WalletCacheFile = "\\wallet-cache.xiro";
        private const string AcceptChoose = "y";
        public static ClassMinerConfig ClassMinerConfigObject;
        public static Dictionary<string, string> DictionaryWalletAddressValidCache = new Dictionary<string, string>();


        /// <summary>
        /// Threads
        /// </summary>
        private static Thread _threadConsoleKey;




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

            WriteAppConfigContent();

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
                                        _configFile = ClassUtility.ConvertPath(splitArgument[1]);
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
                _configFile = ClassUtility.GetCurrentPathConfig(_configFile);
            }
        }

        /// <summary>
        /// Write optimized app config arguments.
        /// </summary>
        private static void WriteAppConfigContent()
        {
            string appConfigFilePath = ClassUtility.ConvertPath(Process.GetCurrentProcess().MainModule.FileName + ".config");

            bool needRestart = !File.Exists(appConfigFilePath);

            using (StreamWriter writer = new StreamWriter(appConfigFilePath))
            {
                writer.Write(ClassStartupArgumentEnumeration.AppConfigContent);
            }

            if (needRestart)
            {
                ClassConsole.WriteLine("The application configuration file has been saved, please restart your miner to take in count the optimized arguments of configuration.", ClassConsoleColorEnumeration.ConsoleTextCyan);
                ClassConsole.WriteLine("Press a key to exit.");
                Console.ReadLine();
                Process.GetCurrentProcess().Kill(); 
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
            ClassConsole.WriteLine("Closing miner.");
            Process.GetCurrentProcess().Kill();
        }

        /// <summary>
        /// Initialization of the solo miner.
        /// </summary>
        private static void InitializeMiner()
        {
            ClassUtility.Lookup32 = ClassUtility.CreateLookup32();
            if (File.Exists(ClassUtility.ConvertPath(_configFile)))
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
                        if (choose.ToLower() == AcceptChoose)
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
            while (choose == null)
            {
                ClassConsole.WriteLine("Your input seems invalid or empty, do you want to use a proxy instead seed node? [Y/N]", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                choose = Console.ReadLine();
            }
            if (choose.ToLower() == AcceptChoose)
            {
                ClassConsole.WriteLine("Please, write your wallet address or a worker name to start your solo mining: ");
                ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();

                ClassConsole.WriteLine("Write the IP/HOST of your mining proxy: ");
                ClassMinerConfigObject.mining_proxy_host = Console.ReadLine();

                ClassConsole.WriteLine("Write the port of your mining proxy: ");
                while (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_proxy_port))
                {
                    ClassConsole.WriteLine("This is not a port number, please try again: ", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                    ClassConsole.WriteLine("Write the port of your mining proxy: ");
                }

                ClassConsole.WriteLine("Do you want select a mining range percentage of difficulty? [Y/N]");
                choose = Console.ReadLine();
                while (choose == null)
                {
                    ClassConsole.WriteLine("Your input seems invalid or empty, do you want select a mining range percentage of difficulty? [Y/N]", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                    choose = Console.ReadLine();
                }
                if (choose.ToLower() == AcceptChoose)
                {
                    ClassConsole.WriteLine("Select the start percentage range of difficulty [0 to 100]:");
                    while (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_percent_difficulty_start))
                    {
                        ClassConsole.WriteLine("This is not a number, please try again: ", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                        ClassConsole.WriteLine("Select the start percentage range of difficulty [0 to 100]:");
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_start > 100)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_start = 100;
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_start < 0)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_start = 0;
                    }

                    ClassConsole.WriteLine("Select the end percentage range of difficulty [" + ClassMinerConfigObject.mining_percent_difficulty_start + " to 100]: ");
                    while (!int.TryParse(Console.ReadLine(), out ClassMinerConfigObject.mining_percent_difficulty_end))
                    {
                        ClassConsole.WriteLine("This is not a number, please try again: ", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                        ClassConsole.WriteLine("Select the end percentage range of difficulty [" + ClassMinerConfigObject.mining_percent_difficulty_start + " to 100]: ");
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_end < 1)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_end = 1;
                    }
                    else if (ClassMinerConfigObject.mining_percent_difficulty_end > 100)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_end = 100;
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_start > ClassMinerConfigObject.mining_percent_difficulty_end)
                    {
                        ClassMinerConfigObject.mining_percent_difficulty_start -= (ClassMinerConfigObject.mining_percent_difficulty_start - ClassMinerConfigObject.mining_percent_difficulty_end);
                    }
                    else
                    {
                        if (ClassMinerConfigObject.mining_percent_difficulty_start == ClassMinerConfigObject.mining_percent_difficulty_end)
                        {
                            ClassMinerConfigObject.mining_percent_difficulty_start--;
                        }
                    }

                    if (ClassMinerConfigObject.mining_percent_difficulty_end <
                        ClassMinerConfigObject.mining_percent_difficulty_start)
                    {
                        var tmpPercentStart = ClassMinerConfigObject.mining_percent_difficulty_start;
                        ClassMinerConfigObject.mining_percent_difficulty_start = ClassMinerConfigObject.mining_percent_difficulty_end;
                        ClassMinerConfigObject.mining_percent_difficulty_end = tmpPercentStart;
                    }
                }

                ClassMinerConfigObject.mining_enable_proxy = true;
            }
            else
            {
                ClassConsole.WriteLine("Please, write your wallet address to start your solo mining: ");
                ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                ClassMinerConfigObject.mining_wallet_address = ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);

                while (ClassMinerConfigObject.mining_wallet_address.Length < ClassConnectorSetting.MinWalletAddressSize || ClassMinerConfigObject.mining_wallet_address.Length > ClassConnectorSetting.MaxWalletAddressSize)
                {
                    ClassConsole.WriteLine("Invalid wallet address - Please, write your wallet address to start your solo mining: ", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                    ClassConsole.WriteLine("Please, write your wallet address to start your solo mining: ");
                    ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                    ClassMinerConfigObject.mining_wallet_address = ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);
                }

                ClassConsole.WriteLine("Check your wallet address..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                bool checkWalletAddress = ClassTokenNetwork.CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;

                while (!checkWalletAddress)
                {
                    ClassConsole.WriteLine("Invalid wallet address - Please, write your wallet address to start your solo mining: ", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                    ClassConsole.WriteLine("Please, write your wallet address to start your solo mining: ");
                    ClassMinerConfigObject.mining_wallet_address = Console.ReadLine();
                    ClassMinerConfigObject.mining_wallet_address = ClassUtility.RemoveSpecialCharacters(ClassMinerConfigObject.mining_wallet_address);

                    ClassConsole.WriteLine("Check your wallet address..", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                    checkWalletAddress = ClassTokenNetwork.CheckWalletAddressExistAsync(ClassMinerConfigObject.mining_wallet_address).Result;
                }

                ClassConsole.WriteLine("Wallet address: " + ClassMinerConfigObject.mining_wallet_address + " is valid.", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
            }

            ClassConsole.WriteLine("How many threads do you want to run? Number of cores detected: " +  Environment.ProcessorCount);

            var tmp = Console.ReadLine();
            if (!int.TryParse(tmp, out ClassMinerConfigObject.mining_thread))
            {
                ClassMinerConfigObject.mining_thread = Environment.ProcessorCount;
            }

            ClassMining.InitializeMiningObjects();


            ClassConsole.WriteLine("Do you want share job range per thread ? [Y/N]");
            choose = Console.ReadLine();
            while (choose == null)
            {
                ClassConsole.WriteLine("Your input seems to be empty or invalid, do you want share job range per thread ? [Y/N]", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                choose = Console.ReadLine();
            }
            if (choose.ToLower() == AcceptChoose)
            {
                ClassMinerConfigObject.mining_thread_spread_job = true;
            }

            ClassMiningStats.InitializeMiningStats();

            ClassConsole.WriteLine("Select thread priority: 0 = Lowest, 1 = BelowNormal, 2 = Normal, 3 = AboveNormal, 4 = Highest [Default: 2]:");

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
            ClassConsole.WriteLine("Save: " + ClassUtility.ConvertPath(_configFile), 1);
            File.Create(ClassUtility.ConvertPath(_configFile)).Close();
            using (StreamWriter writeConfig = new StreamWriter(ClassUtility.ConvertPath(_configFile))
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

            using (StreamReader reader = new StreamReader(ClassUtility.ConvertPath(_configFile)))
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
                    ClassConsole.WriteLine(
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
                    !configContent.Contains("mining_show_calculation_speed"))
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
                !configContent.Contains("mining_show_calculation_speed"))
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
                            if (!DictionaryWalletAddressValidCache.ContainsKey(line))
                            {
                                DictionaryWalletAddressValidCache.Add(line, string.Empty);
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
            _threadConsoleKey = new Thread(delegate ()
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
            _threadConsoleKey.Start();
        }
    }
}