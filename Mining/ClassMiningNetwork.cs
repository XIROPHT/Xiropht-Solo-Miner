using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.Seed;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Connector_All.Utils;
using Xiropht_Solo_Miner.ConsoleMiner;
// ReSharper disable FunctionNeverReturns

namespace Xiropht_Solo_Miner.Mining
{
    public class ClassMiningBlockSplitEnumeration
    {
        private const string MiningBlockCharacterSeperator = "=";
        public const string MiningBlockCharacterSplit = "&";
        public const string MiningBlockMethodSplit = "#";
        public const string MiningBlockJobSplit = ";";

        public const string MiningBlockId = "ID" + MiningBlockCharacterSeperator;
        public const string MiningBlockHash = "HASH" + MiningBlockCharacterSeperator;
        public const string MiningBlockAlgorithm = "ALGORITHM" + MiningBlockCharacterSeperator;
        public const string MiningBlockSize = "SIZE" + MiningBlockCharacterSeperator;
        public const string MiningBlockMethod = "METHOD" + MiningBlockCharacterSeperator;
        public const string MiningBlockKey = "KEY" + MiningBlockCharacterSeperator;
        public const string MiningBlockJob = "JOB" + MiningBlockCharacterSeperator;
        public const string MiningBlockReward = "REWARD" + MiningBlockCharacterSeperator;
        public const string MiningBlockDifficulty = "DIFFICULTY" + MiningBlockCharacterSeperator;
        public const string MiningBlockTimestampCreate = "TIMESTAMP" + MiningBlockCharacterSeperator;
        public const string MiningBlockIndication = "INDICATION" + MiningBlockCharacterSeperator;
        public const string MiningBlockNetworkHashrate = "NETWORK_HASHRATE" + MiningBlockCharacterSeperator;
        public const string MiningBlockLifetime = "LIFETIME" + MiningBlockCharacterSeperator;
    }

    public class ClassMiningNetwork
    {
        private const int ThreadCheckNetworkInterval = 1 * 1000; // Check each 5 seconds.
        public static ClassSeedNodeConnector ObjectSeedNodeNetwork;
        public static string CertificateConnection;
        public static string MalformedPacket;
        public static CancellationTokenSource CancellationTaskNetwork;
        public static bool IsConnected;
        public static long LastPacketReceived;
        private static bool _checkConnectionStarted;
        private static bool _loginAccepted;
        private const int TimeoutPacketReceived = 60; // Max 60 seconds.


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
        ///     For mining method.
        /// </summary>
        public static List<string> ListeMiningMethodName = new List<string>();
        public static List<string> ListeMiningMethodContent = new List<string>();

        /// <summary>
        ///     Start to connect the miner to the network
        /// </summary>
        public static async Task<bool> StartConnectMinerAsync()
        {
            CertificateConnection = ClassUtils.GenerateCertificate();
            MalformedPacket = string.Empty;


            ObjectSeedNodeNetwork?.DisconnectToSeed();


            ObjectSeedNodeNetwork = new ClassSeedNodeConnector();

            CancellationTaskNetwork = new CancellationTokenSource();

            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
            {
                while (!await ObjectSeedNodeNetwork.StartConnectToSeedAsync(string.Empty))
                {
                    ClassConsole.WriteLine("Can't connect to the network, retry in 5 seconds..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                    await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                }
            }
            else
            {
                while (!await ObjectSeedNodeNetwork.StartConnectToSeedAsync(Program.ClassMinerConfigObject.mining_proxy_host,
                    Program.ClassMinerConfigObject.mining_proxy_port))
                {
                    ClassConsole.WriteLine("Can't connect to the proxy, retry in 5 seconds..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                    await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                }
            }

            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
            {
                ClassConsole.WriteLine("Miner connected to the network, generate certificate connection..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
            }

            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
            {
                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(CertificateConnection, string.Empty))
                {
                    IsConnected = false;
                    return false;
                }
            }

            LastPacketReceived = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
            {

                ClassConsole.WriteLine("Send wallet address for login your solo miner..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                    ClassConnectorSettingEnumeration.MinerLoginType + ClassConnectorSetting.PacketContentSeperator +
                    Program.ClassMinerConfigObject.mining_wallet_address + ClassConnectorSetting.PacketSplitSeperator,
                    CertificateConnection, false, true))
                {
                    IsConnected = false;
                    return false;
                }
            }
            else
            {
                ClassConsole.WriteLine("Send wallet address for login your solo miner..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                    ClassConnectorSettingEnumeration.MinerLoginType + ClassConnectorSetting.PacketContentSeperator +
                    Program.ClassMinerConfigObject.mining_wallet_address + ClassConnectorSetting.PacketContentSeperator +
                    Program.ClassMinerConfigObject.mining_percent_difficulty_end +
                    ClassConnectorSetting.PacketContentSeperator +
                    Program.ClassMinerConfigObject.mining_percent_difficulty_start +
                    ClassConnectorSetting.PacketContentSeperator +
                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                {
                    IsConnected = false;
                    return false;
                }
            }

            IsConnected = true;
            ListenNetwork();
            if (!_checkConnectionStarted)
            {
                _checkConnectionStarted = true;
                CheckNetwork();
            }

            return true;
        }

        /// <summary>
        ///     Check the connection of the miner to the network.
        /// </summary>
        private static void CheckNetwork()
        {
            Task.Factory.StartNew(async delegate
            {
                ClassConsole.WriteLine("Check connection enabled.", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                await Task.Delay(ThreadCheckNetworkInterval);
                while (true)
                {
                    try
                    {
                        if (!IsConnected || !_loginAccepted || !ObjectSeedNodeNetwork.ReturnStatus() ||
                            LastPacketReceived + TimeoutPacketReceived < DateTimeOffset.Now.ToUnixTimeSeconds())
                        {
                            ClassConsole.WriteLine("Miner connection lost or aborted, retry to connect..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                            ClassMining.StopMining();
                            CleanNetworkBlockInformations();
                            DisconnectNetwork();
                            while (!await StartConnectMinerAsync())
                            {
                                ClassConsole.WriteLine("Can't connect to the proxy, retry in 5 seconds..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                            }
                        }
                    }
                    catch
                    {
                        ClassConsole.WriteLine("Miner connection lost or aborted, retry to connect..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                        ClassMining.StopMining();
                        CleanNetworkBlockInformations();
                        DisconnectNetwork();
                        while (!await StartConnectMinerAsync())
                        {
                            ClassConsole.WriteLine("Can't connect to the proxy, retry in 5 seconds..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                            await Task.Delay(ClassConnectorSetting.MaxTimeoutConnect);
                        }
                    }

                    await Task.Delay(ThreadCheckNetworkInterval);
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
        }

        /// <summary>
        /// Clean network block informations.
        /// </summary>
        private static void CleanNetworkBlockInformations()
        {
            CurrentBlockId = string.Empty;
            CurrentBlockHash = string.Empty;
        }

        /// <summary>
        ///     Force disconnect the miner.
        /// </summary>
        public static void DisconnectNetwork()
        {
            IsConnected = false;
            _loginAccepted = false;

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
                // Ignored.
            }

            try
            {
                ObjectSeedNodeNetwork?.DisconnectToSeed();
            }
            catch
            {
                // Ignored.
            }

            ClassMining.StopMining();
        }

        /// <summary>
        ///     Listen packet received from blockchain.
        /// </summary>
        private static void ListenNetwork()
        {
            try
            {
                Task.Factory.StartNew(async delegate
                {
                    while (true)
                    {
                        try
                        {
                            CancellationTaskNetwork.Token.ThrowIfCancellationRequested();

                            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                            {
                                string packet = await ObjectSeedNodeNetwork.ReceivePacketFromSeedNodeAsync(CertificateConnection, false, true);
                                if (packet == ClassSeedNodeStatus.SeedError)
                                {
                                    ClassConsole.WriteLine("Network error received. Waiting network checker..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
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

                                    var splitPacket = packet.Split(new[] { ClassConnectorSetting.PacketSplitSeperator }, StringSplitOptions.None);
                                    foreach (var packetEach in splitPacket)
                                    {
                                        if (!string.IsNullOrEmpty(packetEach))
                                        {
                                            if (packetEach.Length > 1)
                                            {
                                                var packetRequest = packetEach.Replace(ClassConnectorSetting.PacketSplitSeperator,"");
                                                if (packetRequest == ClassSeedNodeStatus.SeedError)
                                                {
                                                    ClassConsole.WriteLine("Network error received. Waiting network checker..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                                    DisconnectNetwork();
                                                    break;
                                                }

                                                if (packetRequest != ClassSeedNodeStatus.SeedNone && packetRequest != ClassSeedNodeStatus.SeedError)
                                                {
                                                    await HandlePacketMiningAsync(packetRequest);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    if (MalformedPacket != null && (MalformedPacket.Length < int.MaxValue - 1 || (long)(MalformedPacket.Length + packet.Length) < int.MaxValue - 1))
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
                                string packet = await ObjectSeedNodeNetwork.ReceivePacketFromSeedNodeAsync(string.Empty);
                                if (packet == ClassSeedNodeStatus.SeedError)
                                {
                                    ClassConsole.WriteLine("Network error received. Waiting network checker..", ClassConsoleColorEnumeration.ConsoleTextColorRed);
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
                }, CancellationTaskNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
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
                var splitPacket = packet.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);
                switch (splitPacket[0])
                {
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendLoginAccepted:
                        ClassMiningStats.ShowHashrate();
                        ClassConsole.WriteLine("Miner login accepted, start to mine..", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
                        _loginAccepted = true;
                        MiningProcessingRequest();
                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendListBlockMethod:
                        var methodList = splitPacket[1];
                        try
                        {
                            await Task.Factory.StartNew(async delegate
                            {
                                if (methodList.Contains(ClassMiningBlockSplitEnumeration.MiningBlockMethodSplit))
                                {
                                    var splitMethodList = methodList.Split(new[] { ClassMiningBlockSplitEnumeration.MiningBlockMethodSplit }, StringSplitOptions.None);
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

                                                if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                                {
                                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod +
                                                        ClassConnectorSetting.PacketContentSeperator +
                                                        methodName + ClassConnectorSetting.PacketMiningSplitSeperator,
                                                        CertificateConnection, false, true))
                                                    {
                                                        DisconnectNetwork();
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod +
                                                        ClassConnectorSetting.PacketContentSeperator +
                                                        methodName,
                                                        string.Empty))
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

                                                if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                                {
                                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod +
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
                                                    if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod +
                                                        ClassConnectorSetting.PacketContentSeperator +
                                                        methodName,
                                                        string.Empty))
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

                                    if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                    {
                                        if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod +
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
                                        if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskContentBlockMethod +
                                            ClassConnectorSetting.PacketContentSeperator + methodList,
                                            string.Empty))
                                        {
                                            DisconnectNetwork();
                                        }
                                    }
                                }

                            }, CancellationTaskNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Catch the exception once the task is cancelled.
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

                        var splitBlockContent = splitPacket[1].Split(new[] { ClassMiningBlockSplitEnumeration.MiningBlockCharacterSplit }, StringSplitOptions.None);

                        string currentBlockId = splitBlockContent[0].Replace(ClassMiningBlockSplitEnumeration.MiningBlockId, "");
                        string currentBlockHash = splitBlockContent[1].Replace(ClassMiningBlockSplitEnumeration.MiningBlockHash, "");

                        if (CurrentBlockId != currentBlockId || CurrentBlockHash != currentBlockHash)
                        {

                            try
                            {

                                if (CurrentBlockId == splitBlockContent[0].Replace(ClassMiningBlockSplitEnumeration.MiningBlockId, ""))
                                {
                                    ClassConsole.WriteLine("Current Block ID: " + CurrentBlockId + " has been renewed.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                }
                                else
                                {
                                    ClassConsole.WriteLine("New Block ID to mine " + currentBlockId, ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                                }

                                CurrentBlockId = currentBlockId;
                                CurrentBlockHash = currentBlockHash;
                                CurrentBlockAlgorithm = splitBlockContent[2].Replace(ClassMiningBlockSplitEnumeration.MiningBlockAlgorithm, "");
                                CurrentBlockSize = splitBlockContent[3].Replace(ClassMiningBlockSplitEnumeration.MiningBlockSize, "");
                                CurrentBlockMethod = splitBlockContent[4].Replace(ClassMiningBlockSplitEnumeration.MiningBlockMethod, "");
                                CurrentBlockKey = splitBlockContent[5].Replace(ClassMiningBlockSplitEnumeration.MiningBlockKey, "");
                                CurrentBlockJob = splitBlockContent[6].Replace(ClassMiningBlockSplitEnumeration.MiningBlockJob, "");
                                CurrentBlockReward = splitBlockContent[7].Replace(ClassMiningBlockSplitEnumeration.MiningBlockReward, "");
                                CurrentBlockDifficulty = splitBlockContent[8].Replace(ClassMiningBlockSplitEnumeration.MiningBlockDifficulty, "");
                                CurrentBlockTimestampCreate = splitBlockContent[9].Replace(ClassMiningBlockSplitEnumeration.MiningBlockTimestampCreate, "");
                                CurrentBlockIndication = splitBlockContent[10].Replace(ClassMiningBlockSplitEnumeration.MiningBlockIndication, "");
                                CurrentBlockNetworkHashrate = splitBlockContent[11].Replace(ClassMiningBlockSplitEnumeration.MiningBlockNetworkHashrate, "");
                                CurrentBlockLifetime = splitBlockContent[12].Replace(ClassMiningBlockSplitEnumeration.MiningBlockLifetime, "");



                                ClassMining.StopMining();
                                if (Program.ClassMinerConfigObject.mining_enable_cache)
                                {
                                    ClassMining.ClearMiningCache();
                                }
                                ClassMining.ClearMiningObjects();
                                ClassMining.InitializeMiningObjects();


                                ClassMiningStats.CanMining = true;
                                var splitCurrentBlockJob = CurrentBlockJob.Split(new[] { ClassMiningBlockSplitEnumeration.MiningBlockJobSplit }, StringSplitOptions.None);
                                var minRange = decimal.Parse(splitCurrentBlockJob[0]);
                                var maxRange = decimal.Parse(splitCurrentBlockJob[1]);


                                if (Program.ClassMinerConfigObject.mining_enable_proxy)
                                {
                                    ClassConsole.WriteLine("Job range received from proxy: " + minRange.ToString("F0") + ClassConnectorSetting.PacketContentSeperator + maxRange.ToString("F0") + "", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
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

                                var splitMethod = ListeMiningMethodContent[idMethod].Split(new[] { ClassMiningBlockSplitEnumeration.MiningBlockMethodSplit }, StringSplitOptions.None);

                                ClassMining.CurrentRoundAesRound = int.Parse(splitMethod[0]);
                                ClassMining.CurrentRoundAesSize = int.Parse(splitMethod[1]);
                                ClassMining.CurrentRoundAesKey = splitMethod[2];
                                ClassMining.CurrentRoundXorKey = int.Parse(splitMethod[3]);
                                ClassMining.CurrentRoundXorKeyStr = splitMethod[3];


                                for (int i = 0; i < Program.ClassMinerConfigObject.mining_thread; i++)
                                {
                                    if (i < Program.ClassMinerConfigObject.mining_thread)
                                    {
                                        int iThread = i;
                                        try
                                        {
                                            
                                            ClassMining.ThreadMining[i] = new Task(() => ClassMining.InitializeMiningThread(iThread), ClassMining.CancellationTaskMining.Token);
                                            var i1 = i;
                                            await Task.Factory.StartNew(() =>
                                            {
                                                ClassMining.ThreadMining[i1].Start();
                                            }, ClassMining.CancellationTaskMining.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
                                        }
                                        catch
                                        {
                                            // Catch the exception, once the task is cancelled.
                                        }
                                    }
                                }
                            }
                            catch (Exception error)
                            {
                                ClassConsole.WriteLine("Block template not completly received, stop mining and ask again the blocktemplate | Exception: " + error.Message, ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                                ClassMining.StopMining();
                            }
                        }

                        break;
                    case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.SendJobStatus:
                        switch (splitPacket[1])
                        {
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareUnlock:
                                ClassMiningStats.TotalBlockAccepted++;
                                ClassConsole.WriteLine("Block accepted, stop mining, wait new block.", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareWrong:
                                ClassMiningStats.TotalBlockRefused++;
                                ClassConsole.WriteLine("Block not accepted, stop mining, wait new block.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareAleady:
                                if (CurrentBlockId == splitPacket[1])
                                {
                                    ClassConsole.WriteLine(splitPacket[1] + " Orphaned, someone already got it, stop mining, wait new block.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                }
                                else
                                {
                                    ClassConsole.WriteLine(splitPacket[1] + " Orphaned, someone already get it.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                }

                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareNotExist:
                                ClassConsole.WriteLine("Block mined does not exist, stop mining, wait new block.", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareGood:
                                ClassMiningStats.TotalShareAccepted++;
                                break;
                            case ClassSoloMiningPacketEnumeration.SoloMiningRecvPacketEnumeration.ShareBad:
                                ClassConsole.WriteLine(
                                    "Block not accepted, someone already got it or your share is invalid.", ClassConsoleColorEnumeration.ConsoleTextColorRed);
                                ClassMiningStats.TotalShareInvalid++;
                                break;
                        }

                        break;
                }
            }
            catch
            {
                // Ignored.
            }
        }

        /// <summary>
        ///     Ask new mining method and current blocktemplate automaticaly.
        /// </summary>
        private static void MiningProcessingRequest()
        {
            try
            {
                Task.Factory.StartNew(async delegate
                {
                    if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                    {
                        while (IsConnected)
                        {
                            CancellationTaskNetwork.Token.ThrowIfCancellationRequested();

                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskListBlockMethod + ClassConnectorSetting.PacketMiningSplitSeperator,
                                CertificateConnection, false, true))
                            {
                                DisconnectNetwork();
                                break;
                            }

                            await Task.Delay(1000);
                            if (ListeMiningMethodContent.Count > 0)
                            {
                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskCurrentBlockMining + ClassConnectorSetting.PacketMiningSplitSeperator, CertificateConnection, false,
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
                            if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskListBlockMethod, string.Empty))
                            {
                                DisconnectNetwork();
                            }

                            await Task.Delay(1000);

                            if (ListeMiningMethodContent.Count > 0)
                            {
                                if (!await ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceiveAskCurrentBlockMining, string.Empty))
                                {
                                    DisconnectNetwork();
                                }
                            }

                            await Task.Delay(100);
                        }
                    }
                }, CancellationTaskNetwork.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).ConfigureAwait(false);
            }
            catch
            {
                // Catch the exception once the task is cancelled.
            }
        }


    }
}
