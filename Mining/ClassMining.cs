using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xiropht_Connector_All.Mining;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Connector_All.Utils;
using Xiropht_Solo_Miner.Algo;
using Xiropht_Solo_Miner.Cache;
using Xiropht_Solo_Miner.ConsoleMiner;
using Xiropht_Solo_Miner.Utility;

namespace Xiropht_Solo_Miner.Mining
{
    public enum ClassMiningThreadPriority
    {
        ThreadPriorityLowest = 0,
        ThreadPriorityBelowNormal = 1,
        ThreadPriorityNormal = 2,
        ThreadPriorityAboveNormal = 3,
        ThreadPriorityHighest = 4
    }

    public class ClassMining
    {
        /// <summary>
        /// Tasks and cancellationToken.
        /// </summary>
        public static Task[] ThreadMining;
        public static CancellationTokenSource CancellationTaskMining;
        /// <summary>
        ///     Encryption informations and objects.
        /// </summary>
        private static byte[] _currentAesKeyBytes;
        private static byte[] _currentAesIvBytes;
        public static int CurrentRoundAesRound;
        public static int CurrentRoundAesSize;
        public static string CurrentRoundAesKey;
        public static int CurrentRoundXorKey;
        public static string CurrentRoundXorKeyStr;

        /// <summary>
        /// Initialize every mining objects necessary to use.
        /// </summary>
        public static void InitializeMiningObjects()
        {
            ThreadMining = new Task[Program.ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.Sha512ManagedMining = new SHA512Managed[Program.ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.CryptoTransformMining = new ICryptoTransform[Program.ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.AesManagedMining = new AesManaged[Program.ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.CryptoStreamMining = new CryptoStream[Program.ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.MemoryStreamMining = new MemoryStream[Program.ClassMinerConfigObject.mining_thread];
            ClassAlgoMining.TotalNonceMining = new int[Program.ClassMinerConfigObject.mining_thread];

        }

        /// <summary>
        /// Clear every mining objects necessary.
        /// </summary>
        public static void ClearMiningObjects()
        {
            Array.Clear(ThreadMining, 0, ThreadMining.Length);
            Array.Clear(ClassAlgoMining.Sha512ManagedMining, 0, ClassAlgoMining.Sha512ManagedMining.Length);
            Array.Clear(ClassAlgoMining.CryptoTransformMining, 0, ClassAlgoMining.CryptoTransformMining.Length);
            Array.Clear(ClassAlgoMining.AesManagedMining, 0, ClassAlgoMining.AesManagedMining.Length);
            Array.Clear(ClassAlgoMining.CryptoStreamMining, 0, ClassAlgoMining.CryptoStreamMining.Length);
            Array.Clear(ClassAlgoMining.MemoryStreamMining, 0, ClassAlgoMining.MemoryStreamMining.Length);
            Array.Clear(ClassAlgoMining.TotalNonceMining, 0, ClassAlgoMining.TotalNonceMining.Length);
        }

        /// <summary>
        ///     Initialize mining cache.
        /// </summary>
        public static void InitializeMiningCache()
        {
            ClassConsole.WriteLine("Be carefull, the mining cache feature is in beta and can use a lot of RAM, this function is not tested at 100% and need more features for probably provide more luck on mining.", ClassConsoleColorEnumeration.ConsoleTextColorRed);

            if (DictionaryCacheMining == null)
            {
                DictionaryCacheMining = new ClassMiningCache();
            }
            DictionaryCacheMining?.CleanMiningCache();
        }

        /// <summary>
        /// About Mining Cache feature.
        /// </summary>
        public static ClassMiningCache DictionaryCacheMining;

        /// <summary>
        ///     Stop mining.
        /// </summary>
        public static void StopMining()
        {
            ClassMiningStats.CanMining = false;
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
                                            //GC.SuppressFinalize(ThreadMining[i]);

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
                // Ignored.
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
                // Ignored.
            }
        }

        /// <summary>
        ///     Initialization of the mining thread executed.
        /// </summary>
        /// <param name="iThread"></param>
        public static async void InitializeMiningThread(int iThread)
        {

            if (Program.ClassMinerConfigObject.mining_enable_automatic_thread_affinity &&
                string.IsNullOrEmpty(Program.ClassMinerConfigObject.mining_manual_thread_affinity))
            {
                ClassUtilityAffinity.SetAffinity(iThread);
            }
            else
            {
                if (!string.IsNullOrEmpty(Program.ClassMinerConfigObject.mining_manual_thread_affinity))
                {
                    ClassUtilityAffinity.SetManualAffinity(Program.ClassMinerConfigObject.mining_manual_thread_affinity);
                }
            }

            using (var pdb = new PasswordDeriveBytes(ClassMiningNetwork.CurrentBlockKey, Encoding.UTF8.GetBytes(CurrentRoundAesKey)))
            {
                _currentAesKeyBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
                _currentAesIvBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
            }

            ClassAlgoMining.AesManagedMining[iThread] = new AesManaged()
            {
                BlockSize = CurrentRoundAesSize,
                KeySize = CurrentRoundAesSize,
                Key = _currentAesKeyBytes,
                IV = _currentAesIvBytes
            };
            ClassAlgoMining.CryptoTransformMining[iThread] =
                ClassAlgoMining.AesManagedMining[iThread].CreateEncryptor();

            int i1 = iThread + 1;
            var splitCurrentBlockJob = ClassMiningNetwork.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None);
            var minRange = decimal.Parse(splitCurrentBlockJob[0]);
            var maxRange = decimal.Parse(splitCurrentBlockJob[1]);

            var minProxyStart = maxRange - minRange;
            var incrementProxyRange = minProxyStart / Program.ClassMinerConfigObject.mining_thread;

            switch (Program.ClassMinerConfigObject.mining_thread_priority)
            {
                case (int)ClassMiningThreadPriority.ThreadPriorityLowest:
                    Thread.CurrentThread.Priority = ThreadPriority.Lowest;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                    break;
                case (int)ClassMiningThreadPriority.ThreadPriorityBelowNormal:
                    Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
                    break;
                case (int)ClassMiningThreadPriority.ThreadPriorityNormal:
                    Thread.CurrentThread.Priority = ThreadPriority.Normal;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                    break;
                case (int)ClassMiningThreadPriority.ThreadPriorityAboveNormal:
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case (int)ClassMiningThreadPriority.ThreadPriorityHighest:
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
                    break;
            }

            try
            {
                if (Program.ClassMinerConfigObject.mining_thread_spread_job)
                {
                    if (Program.ClassMinerConfigObject.mining_enable_proxy)
                    {
                        if (minRange > 0)
                        {
                            decimal minRangeTmp = minRange;
                            decimal maxRangeTmp = minRangeTmp + incrementProxyRange;

                            await StartMiningAsync(iThread, Math.Round(minRangeTmp), (Math.Round(maxRangeTmp)));

                        }
                        else
                        {
                            decimal minRangeTmp = Math.Round((maxRange / Program.ClassMinerConfigObject.mining_thread) * (i1 - 1), 0);
                            decimal maxRangeTmp = (Math.Round(((maxRange / Program.ClassMinerConfigObject.mining_thread) * i1)));
                            await StartMiningAsync(iThread, minRangeTmp, maxRangeTmp);
                        }
                    }
                    else
                    {
                        decimal minRangeTmp = Math.Round((maxRange / Program.ClassMinerConfigObject.mining_thread) * (i1 - 1));
                        decimal maxRangeTmp = (Math.Round(((maxRange / Program.ClassMinerConfigObject.mining_thread) * i1)));
                        await StartMiningAsync(iThread, minRangeTmp, maxRangeTmp);
                    }
                }
                else
                {
                    await StartMiningAsync(iThread, minRange, maxRange);
                }
            }
            catch
            {
                // Ignored, catch the exception once the task is cancelled.
            }
        }

        /// <summary>
        ///     Start mining.
        /// </summary>
        /// <param name="idThread"></param>
        /// <param name="minRange"></param>
        /// <param name="maxRange"></param>
        private static async Task StartMiningAsync(int idThread, decimal minRange, decimal maxRange)
        {
            if (minRange <= 1)
            {
                minRange = 2;
            }

            while (ClassMiningNetwork.ListeMiningMethodName.Count == 0)
            {
                ClassConsole.WriteLine("No method content received, waiting to receive them before..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                await Task.Delay(1000);
            }

            var currentBlockId = ClassMiningNetwork.CurrentBlockId;
            var currentBlockTimestamp = ClassMiningNetwork.CurrentBlockTimestampCreate;

            var currentBlockDifficulty = decimal.Parse(ClassMiningNetwork.CurrentBlockDifficulty);


            ClassConsole.WriteLine("Thread: " + idThread + " min range:" + minRange + " max range:" + maxRange + " | Host target: " + ClassMiningNetwork.ObjectSeedNodeNetwork.ReturnCurrentSeedNodeHost(), 1);

            ClassConsole.WriteLine("Current Mining Method: " + ClassMiningNetwork.CurrentBlockMethod + " = AES ROUND: " + CurrentRoundAesRound + " AES SIZE: " + CurrentRoundAesSize + " AES BYTE KEY: " + CurrentRoundAesKey + " XOR KEY: " + CurrentRoundXorKey, 1);


            decimal maxPowDifficultyShare = (currentBlockDifficulty * ClassPowSetting.MaxPercentBlockPowValueTarget) / 100;

            while (ClassMiningStats.CanMining)
            {
                if (!GetCancellationMiningTaskStatus())
                {
                    CancellationTaskMining.Token.ThrowIfCancellationRequested();

                    if (ThreadMining[idThread].Status == TaskStatus.Canceled)
                    {
                        break;
                    }

                    if (ClassMiningNetwork.CurrentBlockId != currentBlockId || currentBlockTimestamp != ClassMiningNetwork.CurrentBlockTimestampCreate)
                    {
                        using (var pdb = new PasswordDeriveBytes(ClassMiningNetwork.CurrentBlockKey,
                            Encoding.UTF8.GetBytes(CurrentRoundAesKey)))
                        {
                            _currentAesKeyBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
                            _currentAesIvBytes = pdb.GetBytes(CurrentRoundAesSize / 8);
                        }

                        ClassAlgoMining.AesManagedMining[idThread] = new AesManaged()
                        {
                            BlockSize = CurrentRoundAesSize,
                            KeySize = CurrentRoundAesSize,
                            Key = _currentAesKeyBytes,
                            IV = _currentAesIvBytes
                        };
                        ClassAlgoMining.CryptoTransformMining[idThread] =
                            ClassAlgoMining.AesManagedMining[idThread].CreateEncryptor();
                        currentBlockId = ClassMiningNetwork.CurrentBlockId;
                        currentBlockTimestamp = ClassMiningNetwork.CurrentBlockTimestampCreate;
                        currentBlockDifficulty = decimal.Parse(ClassMiningNetwork.CurrentBlockDifficulty);
                        maxPowDifficultyShare = (currentBlockDifficulty * ClassPowSetting.MaxPercentBlockPowValueTarget) / 100;
                        if (Program.ClassMinerConfigObject.mining_enable_cache)
                        {
                            ClearMiningCache();
                        }
                    }

                    try
                    {

                        MiningComputeProcess(idThread, minRange, maxRange, currentBlockDifficulty, maxPowDifficultyShare);

                    }
                    catch
                    {
                        // Ignored.
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


            while (ClassMiningStats.CanMining)
            {
                result = !rngGenerator ? ClassMiningMath.GenerateNumberMathCalculation(minRange, maxRange) : ClassMiningMath.GetRandomBetweenJob(minRange, maxRange);

                if (result < 0)
                {
                    result *= -1;
                }

                result = Math.Round(result);

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
        /// <param name="maxPowDifficultyShare"></param>
        private static void MiningComputeProcess(int idThread, decimal minRange, decimal maxRange, decimal currentBlockDifficulty, decimal maxPowDifficultyShare)
        {
            decimal firstNumber = GenerateRandomNumber(minRange, maxRange,
                ClassMiningMath.GetRandomBetween(1, 100) >= ClassMiningMath.GetRandomBetweenSize(1, 100));

            decimal secondNumber = GenerateRandomNumber(minRange, maxRange,
                ClassMiningMath.GetRandomBetween(1, 100) >= ClassMiningMath.GetRandomBetweenSize(1, 100));

            if (Program.ClassMinerConfigObject.mining_enable_cache) // Caching option enabled (don't test and encrypt already tested calculations cached).
            {

                #region Normal calculation test and encryption part.

                string mathCombinaison = firstNumber.ToString("F0") + ClassConnectorSetting.PacketContentSeperator + secondNumber.ToString("F0");

                if (!DictionaryCacheMining.CheckMathCombinaison(mathCombinaison))
                {

                    DictionaryCacheMining.InsertMathCombinaison(mathCombinaison, idThread);

                    for (var index = 0; index < ClassMiningMath.RandomOperatorCalculation.Length; index++)
                    {

                        if (index < ClassMiningMath.RandomOperatorCalculation.Length)
                        {
                            var mathOperator = ClassMiningMath.RandomOperatorCalculation[index];


                            string calcul = ClassMiningMath.BuildCalculationString(firstNumber, mathOperator, secondNumber);


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

                                    ClassMiningStats.TotalMiningHashrateRound[idThread]++;
                                    if (!ClassMiningStats.CanMining)
                                    {
                                        return;
                                    }

                                    if (hashShare == ClassMiningNetwork.CurrentBlockIndication)
                                    {
                                        var compute = calculCompute;
                                        var calcul1 = calcul;
                                        Task.Factory.StartNew(async delegate
                                        {
                                            ClassConsole.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#if DEBUG
                                            Debug.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#endif
                                            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                            {
                                                if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version +
                                                    ClassConnectorSetting.PacketMiningSplitSeperator,
                                                    ClassMiningNetwork.CertificateConnection, false, true))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }
                                            else
                                            {
                                                if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }
                                        }).ConfigureAwait(false);
                                    }
                                }
                            }
                        }

                    }

                }

                #endregion

                #region Reverted calculation test and encryption part.

                mathCombinaison = secondNumber.ToString("F0") + ClassConnectorSetting.PacketContentSeperator + firstNumber.ToString("F0");

                if (!DictionaryCacheMining.CheckMathCombinaison(mathCombinaison))
                {

                    DictionaryCacheMining.InsertMathCombinaison(mathCombinaison, idThread);

                    for (var index = 0; index < ClassMiningMath.RandomOperatorCalculation.Length; index++)
                    {
                        if (index < ClassMiningMath.RandomOperatorCalculation.Length)
                        {
                            var mathOperator = ClassMiningMath.RandomOperatorCalculation[index];

                            string calcul = ClassMiningMath.BuildCalculationString(secondNumber, mathOperator, firstNumber);
                            var testCalculationObject = TestCalculation(secondNumber, firstNumber, mathOperator, idThread, currentBlockDifficulty);
                            decimal calculCompute = testCalculationObject.Item2;

                            if (testCalculationObject.Item1)
                            {
                                string encryptedShare = calcul;

                                encryptedShare = MakeEncryptedShare(encryptedShare, idThread);
                                if (encryptedShare != ClassAlgoErrorEnumeration.AlgoError)
                                {
                                    string hashShare = ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);

                                    ClassMiningStats.TotalMiningHashrateRound[idThread]++;
                                    if (!ClassMiningStats.CanMining)
                                    {
                                        return;
                                    }

                                    if (hashShare == ClassMiningNetwork.CurrentBlockIndication)
                                    {
                                        var compute = calculCompute;
                                        var calcul1 = calcul;
                                        Task.Factory.StartNew(async delegate
                                        {
                                            ClassConsole.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#if DEBUG
                                            Debug.WriteLine(
                                                "Exact share for unlock the block seems to be found, submit it: " +
                                                calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#endif
                                            if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                            {
                                                if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version +
                                                    ClassConnectorSetting.PacketMiningSplitSeperator,
                                                    ClassMiningNetwork.CertificateConnection, false, true))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }
                                            else
                                            {
                                                if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                    ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                        .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                    encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                    compute.ToString("F0") +
                                                    ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                    ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                    ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }
                                        }).ConfigureAwait(false);
                                    }
                                }
                            }
                        }
                    }
                }

                #endregion
            }
            else // Caching option disabled (much faster).
            {
                for (var index = 0; index < ClassMiningMath.RandomOperatorCalculation.Length; index++)
                {
                    if (index < ClassMiningMath.RandomOperatorCalculation.Length)
                    {
                        var mathOperator = ClassMiningMath.RandomOperatorCalculation[index];

                        #region Normal calculation test and encryption part.

                        string calcul = ClassMiningMath.BuildCalculationString(firstNumber, mathOperator, secondNumber);

                        var testCalculationObject = TestCalculation(firstNumber, secondNumber, mathOperator, idThread, currentBlockDifficulty);

                        decimal calculCompute = testCalculationObject.Item2;

                        if (testCalculationObject.Item1)
                        {
                            string encryptedShare = calcul;

                            encryptedShare = MakeEncryptedShare(encryptedShare, idThread);

                            if (encryptedShare != ClassAlgoErrorEnumeration.AlgoError)
                            {
                                string hashShare = ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);

                                ClassMiningStats.TotalMiningHashrateRound[idThread]++;
                                if (!ClassMiningStats.CanMining)
                                {
                                    return;
                                }

                                if (hashShare == ClassMiningNetwork.CurrentBlockIndication)
                                {
                                    var compute = calculCompute;
                                    var calcul1 = calcul;
                                    Task.Factory.StartNew(async delegate
                                    {
                                        ClassConsole.WriteLine(
                                            "Exact share for unlock the block seems to be found, submit it: " +
                                            calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#if DEBUG
                                        Debug.WriteLine("Exact share for unlock the block seems to be found, submit it: " + calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#endif
                                        if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                        {
                                            if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version +
                                                ClassConnectorSetting.PacketMiningSplitSeperator,
                                                ClassMiningNetwork.CertificateConnection, false, true))
                                            {
                                                ClassMiningNetwork.DisconnectNetwork();
                                            }
                                        }
                                        else
                                        {
                                            if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                            {
                                                ClassMiningNetwork.DisconnectNetwork();
                                            }
                                        }
                                    }).ConfigureAwait(false);
                                }
                            }
                        }

                        #endregion

                        #region Reverted calculation test and encryption part.

                        calcul = ClassMiningMath.BuildCalculationString(secondNumber, mathOperator, firstNumber);

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

                                ClassMiningStats.TotalMiningHashrateRound[idThread]++;
                                if (!ClassMiningStats.CanMining)
                                {
                                    return;
                                }

                                if (hashShare == ClassMiningNetwork.CurrentBlockIndication)
                                {
                                    var compute = calculCompute;
                                    var calcul1 = calcul;
                                    Task.Factory.StartNew(async delegate
                                    {
                                        ClassConsole.WriteLine(
                                            "Exact share for unlock the block seems to be found, submit it: " +
                                            calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#if DEBUG
                                        Debug.WriteLine(
                                            "Exact share for unlock the block seems to be found, submit it: " +
                                            calcul1 + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
#endif
                                        if (!Program.ClassMinerConfigObject.mining_enable_proxy)
                                        {
                                            if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version +
                                                ClassConnectorSetting.PacketMiningSplitSeperator,
                                                ClassMiningNetwork.CertificateConnection, false, true))
                                            {
                                                ClassMiningNetwork.DisconnectNetwork();
                                            }
                                        }
                                        else
                                        {
                                            if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(
                                                ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration
                                                    .ReceiveJob + ClassConnectorSetting.PacketContentSeperator +
                                                encryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                compute.ToString("F0") +
                                                ClassConnectorSetting.PacketContentSeperator + calcul1 +
                                                ClassConnectorSetting.PacketContentSeperator + hashShare +
                                                ClassConnectorSetting.PacketContentSeperator + ClassMiningNetwork.CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                            {
                                                ClassMiningNetwork.DisconnectNetwork();
                                            }
                                        }
                                    }).ConfigureAwait(false);
                                }
                            }
                        }
                    
                        #endregion

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
        public static Tuple<bool, decimal> TestCalculation(decimal firstNumber, decimal secondNumber, string mathOperator, int idThread, decimal currentBlockDifficulty)
        {
            if (firstNumber < secondNumber)
            {
                switch (mathOperator)
                {
                    case ClassMiningMathOperatorEnumeration.MathOperatorLess:
                    case ClassMiningMathOperatorEnumeration.MathOperatorDividor:
                        return new Tuple<bool, decimal>(false, 0);
                }
            }

            decimal calculCompute = ClassMiningMath.ComputeCalculation(firstNumber, mathOperator, secondNumber);
            ClassMiningStats.TotalMiningCalculationRound[idThread]++;


            if (calculCompute - Math.Round(calculCompute) == 0) // Check if the result contains decimal places, if yes ignore it. 
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
            string encryptedShare = ClassUtility.StringToHex(calculation + ClassMiningNetwork.CurrentBlockTimestampCreate, true);

            // Static XOR Encryption -> Key updated from the current mining method.
            encryptedShare = ClassAlgoMining.EncryptXorShare(encryptedShare, CurrentRoundXorKeyStr);

            // Dynamic AES Encryption -> Size and Key's from the current mining method and the current block key encryption.
            for (int i = 0; i < CurrentRoundAesRound; i++)
            {
                encryptedShare = ClassAlgoMining.EncryptAesShare(encryptedShare, idThread, true);
            }

            // Static XOR Encryption -> Key from the current mining method
            encryptedShare = ClassAlgoMining.EncryptXorShare(encryptedShare, CurrentRoundXorKeyStr);

            // Static AES Encryption -> Size and Key's from the current mining method.
            encryptedShare = ClassAlgoMining.EncryptAesShare(encryptedShare, idThread, true);

            // Generate SHA512 HASH for the share and return it.
            return ClassAlgoMining.GenerateSha512FromString(encryptedShare, idThread);
        }

        /// <summary>
        ///     Clear Mining Cache
        /// </summary>
        public static void ClearMiningCache()
        {

            bool error = true;
            while (error)
            {
                try
                {
                    ClassConsole.WriteLine("Clear mining cache | total calculation cached: " + DictionaryCacheMining.Count.ToString("F0"), 5);
                    DictionaryCacheMining.CleanMiningCache();
                    error = false;
                }
                catch
                {
                    error = true;
                }
            }

        }
    }
}
