using System;
using System.Diagnostics;
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

        /// <summary>
        /// About Mining Cache feature.
        /// </summary>
        public static ClassMiningCache DictionaryCacheMining;

        /// <summary>
        ///     Stop mining.
        /// </summary>
        public static void StopMining()
        {
            Program.CanMining = false;
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
        public static void InitializeMiningThread(int iThread)
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

            using (var pdb = new PasswordDeriveBytes(Program.CurrentBlockKey,  Encoding.UTF8.GetBytes(CurrentRoundAesKey)))
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
            var splitCurrentBlockJob = Program.CurrentBlockJob.Split(new[] { ";" }, StringSplitOptions.None);
            var minRange = decimal.Parse(splitCurrentBlockJob[0]);
            var maxRange = decimal.Parse(splitCurrentBlockJob[1]);

            var minProxyStart = maxRange - minRange;
            var incrementProxyRange = minProxyStart / Program.ClassMinerConfigObject.mining_thread;

            switch (Program.ClassMinerConfigObject.mining_thread_priority)
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

            if (Program.ClassMinerConfigObject.mining_thread_spread_job)
            {
                if (Program.ClassMinerConfigObject.mining_enable_proxy)
                {
                    if (minRange > 0)
                    {
                        decimal minRangeTmp = minRange;
                        decimal maxRangeTmp = minRangeTmp + incrementProxyRange;
                        StartMiningAsync(iThread, Math.Round(minRangeTmp), (Math.Round(maxRangeTmp)));
                    }
                    else
                    {
                        decimal minRangeTmp = Math.Round((maxRange / Program.ClassMinerConfigObject.mining_thread) * (i1 - 1),
                            0);
                        decimal maxRangeTmp = (Math.Round(((maxRange / Program.ClassMinerConfigObject.mining_thread) * i1)));
                        StartMiningAsync(iThread, minRangeTmp, maxRangeTmp);
                    }
                }
                else
                {
                    decimal minRangeTmp = Math.Round((maxRange / Program.ClassMinerConfigObject.mining_thread) * (i1 - 1));
                    decimal maxRangeTmp = (Math.Round(((maxRange / Program.ClassMinerConfigObject.mining_thread) * i1)));
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

            while (Program.ListeMiningMethodName.Count == 0)
            {
                ClassConsole.WriteLine("No method content received, waiting to receive them before..", ClassConsoleColorEnumeration.ConsoleTextColorYellow);
                Thread.Sleep(1000);
            }

            var currentBlockId = Program.CurrentBlockId;
            var currentBlockTimestamp = Program.CurrentBlockTimestampCreate;

            var currentBlockDifficulty = decimal.Parse(Program.CurrentBlockDifficulty);


            ClassConsole.WriteLine(
                "Thread: " + idThread + " min range:" + minRange + " max range:" + maxRange + " | Host target: " +
                ClassMiningNetwork.ObjectSeedNodeNetwork.ReturnCurrentSeedNodeHost(), 1);

            ClassConsole.WriteLine(
                "Current Mining Method: " + Program.CurrentBlockMethod + " = AES ROUND: " + CurrentRoundAesRound +
                " AES SIZE: " + CurrentRoundAesSize + " AES BYTE KEY: " + CurrentRoundAesKey + " XOR KEY: " +
                CurrentRoundXorKey, 1);


            decimal maxPowDifficultyShare = (currentBlockDifficulty * ClassPowSetting.MaxPercentBlockPowValueTarget) / 100;

            while (Program.CanMining)
            {
                if (!GetCancellationMiningTaskStatus())
                {
                    CancellationTaskMining.Token.ThrowIfCancellationRequested();

                    if (ThreadMining[idThread].Status == TaskStatus.Canceled)
                    {
                        break;
                    }

                    if (Program.CurrentBlockId != currentBlockId || currentBlockTimestamp != Program.CurrentBlockTimestampCreate)
                    {
                        using (var pdb = new PasswordDeriveBytes(Program.CurrentBlockKey,
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
                        currentBlockId = Program.CurrentBlockId;
                        currentBlockTimestamp = Program.CurrentBlockTimestampCreate;
                        currentBlockDifficulty = decimal.Parse(Program.CurrentBlockDifficulty);
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


            while (Program.CanMining)
            {
                result = !rngGenerator ? ClassUtility.GenerateNumberMathCalculation(minRange, maxRange) : ClassUtility.GetRandomBetweenJob(minRange, maxRange);

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
                ClassUtility.GetRandomBetween(1, 100) >= ClassUtility.GetRandomBetweenSize(1, 100));

            decimal secondNumber = GenerateRandomNumber(minRange, maxRange,
                ClassUtility.GetRandomBetween(1, 100) >= ClassUtility.GetRandomBetweenSize(1, 100));

            if (Program.ClassMinerConfigObject.mining_enable_cache)
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

                                    Program.TotalMiningHashrateRound[idThread]++;
                                    if (!Program.CanMining)
                                    {
                                        return;
                                    }

                                    if (hashShare == Program.CurrentBlockIndication)
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
                                                    ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
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
                                                    ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }
                                        }).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        if (Program.ClassMinerConfigObject.mining_enable_pow_mining_way)
                                        {
                                            var powShare = ClassAlgoMining.DoPowShare(hashShare, Program.CurrentBlockIndication, idThread, currentBlockDifficulty, calculCompute, firstNumber, secondNumber, calcul, maxPowDifficultyShare);
#if DEBUG
                                            #region Test pow difficulty share value

                                            decimal difficultyTarget = 10000000;


                                            if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                            {
                                                Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                                powShare.PowDifficultyShare +
                                                                " who target block difficulty value: " + currentBlockDifficulty + "/" +
                                                                maxPowDifficultyShare + " with Nonce: " + powShare.PowShareNonce +
                                                                " found the block with the result: " + powShare.PowShareResultCalculation.ToString("F0") +
                                                                " calculation used: " + powShare.PowShareCalculation);
                                            }
                                            else if (powShare.PowDifficultyShare == difficultyTarget)
                                            {
                                                Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                                powShare.PowDifficultyShare + " with Nonce Hash: " + powShare.PowShareNonce +
                                                                " found with the math result: " + powShare.PowShareResultCalculation.ToString("F0") + " calculation used: " +
                                                                powShare.PowShareCalculation);
                                            }
                                            #endregion

#endif
                                            if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                            {
                                                Task.Factory.StartNew(async delegate
                                                {
                                                    ClassConsole.WriteLine(
                                                        "PoW share for unlock the block seems to be found, submit it hash: " +
                                                        powShare.PowShareHash + " | Nonce: " + powShare.PowShareNonce + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);

                                                    string packetShare = ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceivePowJob + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowDifficultyShare.ToString("F0") + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowEncryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareHash + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareNonce + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareCalculation + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareResultCalculation + ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId + ClassConnectorSetting.PacketContentSeperator + Assembly.GetExecutingAssembly().GetName().Version;
                                                    if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(packetShare, string.Empty))
                                                    {
                                                        ClassMiningNetwork.DisconnectNetwork();
                                                    }
                                                }).ConfigureAwait(false);
                                            }
                                        }
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

                                    Program.TotalMiningHashrateRound[idThread]++;
                                    if (!Program.CanMining)
                                    {
                                        return;
                                    }

                                    if (hashShare == Program.CurrentBlockIndication)
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
                                                    ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
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
                                                    ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
                                                    ClassConnectorSetting.PacketContentSeperator +
                                                    Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }
                                        }).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        if (Program.ClassMinerConfigObject.mining_enable_pow_mining_way)
                                        {
                                            var powShare = ClassAlgoMining.DoPowShare(hashShare, Program.CurrentBlockIndication, idThread, currentBlockDifficulty, calculCompute, secondNumber, firstNumber, calcul, maxPowDifficultyShare);
#if DEBUG
                                            #region Test pow difficulty share value

                                            decimal difficultyTarget = 10000000;


                                            if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                            {
                                                Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                                powShare.PowDifficultyShare +
                                                                " who target block difficulty value: " + currentBlockDifficulty + "/" +
                                                                maxPowDifficultyShare + " with Nonce: " + powShare.PowShareNonce +
                                                                " found the block with the result: " + powShare.PowShareResultCalculation.ToString("F0") +
                                                                " calculation used: " + powShare.PowShareCalculation);
                                            }
                                            else if (powShare.PowDifficultyShare == difficultyTarget)
                                            {
                                                Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                                powShare.PowDifficultyShare + " with Nonce Hash: " + powShare.PowShareNonce +
                                                                " found with the math result: " + powShare.PowShareResultCalculation.ToString("F0") + " calculation used: " +
                                                                powShare.PowShareCalculation);
                                            }
                                            #endregion

#endif
                                            if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                            {
                                                Task.Factory.StartNew(async delegate
                                                {
                                                    ClassConsole.WriteLine(
                                                        "PoW share for unlock the block seems to be found, submit it hash: " +
                                                        powShare.PowShareHash + " | Nonce: " + powShare.PowShareNonce + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
                                                    string packetShare = ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceivePowJob + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowDifficultyShare.ToString("F0") + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowEncryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareHash + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareNonce + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareCalculation + ClassConnectorSetting.PacketContentSeperator +
                                                                     powShare.PowShareResultCalculation + ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId + ClassConnectorSetting.PacketContentSeperator + Assembly.GetExecutingAssembly().GetName().Version;
                                                    if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(packetShare, string.Empty))
                                                    {
                                                        ClassMiningNetwork.DisconnectNetwork();
                                                    }
                                                }).ConfigureAwait(false);
                                            }
                                        }
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

                                Program.TotalMiningHashrateRound[idThread]++;
                                if (!Program.CanMining)
                                {
                                    return;
                                }

                                if (hashShare == Program.CurrentBlockIndication)
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
                                                ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
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
                                                ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                            {
                                                ClassMiningNetwork.DisconnectNetwork();
                                            }
                                        }
                                    }).ConfigureAwait(false);
                                }
                                else
                                {
                                    if (Program.ClassMinerConfigObject.mining_enable_pow_mining_way)
                                    {
                                        var powShare = ClassAlgoMining.DoPowShare(hashShare, Program.CurrentBlockIndication, idThread, currentBlockDifficulty, calculCompute, firstNumber, secondNumber, calcul, maxPowDifficultyShare);
#if DEBUG
                                        #region Test pow difficulty share value

                                        decimal difficultyTarget = 10000000;


                                        if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                        {
                                            Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                            powShare.PowDifficultyShare +
                                                            " who target block difficulty value: " + currentBlockDifficulty + "/" +
                                                            maxPowDifficultyShare + " with Nonce: " + powShare.PowShareNonce +
                                                            " found the block with the result: " + powShare.PowShareResultCalculation.ToString("F0") +
                                                            " calculation used: " + powShare.PowShareCalculation);
                                        }
                                        else if (powShare.PowDifficultyShare == difficultyTarget)
                                        {
                                            Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                            powShare.PowDifficultyShare + " with Nonce Hash: " + powShare.PowShareNonce +
                                                            " found with the math result: " + powShare.PowShareResultCalculation.ToString("F0") + " calculation used: " +
                                                            powShare.PowShareCalculation);
                                        }
                                        #endregion

#endif
                                        if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                        {
                                            Task.Factory.StartNew(async delegate
                                            {
                                                ClassConsole.WriteLine(
                                                    "PoW share for unlock the block seems to be found, submit it hash: " +
                                                    powShare.PowShareHash + " | Nonce: " + powShare.PowShareNonce + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
                                                string packetShare = ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceivePowJob + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowDifficultyShare.ToString("F0") + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowEncryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareHash + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareNonce + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareCalculation + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareResultCalculation + ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId + ClassConnectorSetting.PacketContentSeperator + Assembly.GetExecutingAssembly().GetName().Version;
                                                if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(packetShare, string.Empty))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }).ConfigureAwait(false);
                                        }
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

                                Program.TotalMiningHashrateRound[idThread]++;
                                if (!Program.CanMining)
                                {
                                    return;
                                }

                                if (hashShare == Program.CurrentBlockIndication)
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
                                                ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
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
                                                ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId +
                                                ClassConnectorSetting.PacketContentSeperator +
                                                Assembly.GetExecutingAssembly().GetName().Version, string.Empty))
                                            {
                                                ClassMiningNetwork.DisconnectNetwork();
                                            }
                                        }
                                    }).ConfigureAwait(false);
                                }
                                else
                                {
                                    if (Program.ClassMinerConfigObject.mining_enable_pow_mining_way)
                                    {
                                        var powShare = ClassAlgoMining.DoPowShare(hashShare, Program.CurrentBlockIndication, idThread, currentBlockDifficulty, calculCompute, secondNumber, firstNumber, calcul, maxPowDifficultyShare);
#if DEBUG
                                        #region Test pow difficulty share value

                                        decimal difficultyTarget = 10000000;


                                        if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                        {
                                            Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                            powShare.PowDifficultyShare +
                                                            " who target block difficulty value: " + currentBlockDifficulty + "/" +
                                                            maxPowDifficultyShare + " with Nonce: " + powShare.PowShareNonce +
                                                            " found the block with the result: " + powShare.PowShareResultCalculation.ToString("F0") +
                                                            " calculation used: " + powShare.PowShareCalculation);
                                        }
                                        else if (powShare.PowDifficultyShare == difficultyTarget)
                                        {
                                            Debug.WriteLine("Xiropht PoW hash test: " + powShare.PowShareHash + " with difficulty value of: " +
                                                            powShare.PowDifficultyShare + " with Nonce Hash: " + powShare.PowShareNonce +
                                                            " found with the math result: " + powShare.PowShareResultCalculation.ToString("F0") + " calculation used: " +
                                                            powShare.PowShareCalculation);
                                        }
                                        #endregion

#endif
                                        if (powShare.PowDifficultyShare >= currentBlockDifficulty && powShare.PowDifficultyShare <= maxPowDifficultyShare)
                                        {
                                            Task.Factory.StartNew(async delegate
                                            {
                                                ClassConsole.WriteLine(
                                                    "PoW share for unlock the block seems to be found, submit it hash: " +
                                                    powShare.PowShareHash + " | Nonce: " + powShare.PowShareNonce + " and waiting confirmation..\n", ClassConsoleColorEnumeration.ConsoleTextColorGreen);
                                                string packetShare = ClassSoloMiningPacketEnumeration.SoloMiningSendPacketEnumeration.ReceivePowJob + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowDifficultyShare.ToString("F0") + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowEncryptedShare + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareHash + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareNonce + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareCalculation + ClassConnectorSetting.PacketContentSeperator +
                                                                 powShare.PowShareResultCalculation + ClassConnectorSetting.PacketContentSeperator + Program.CurrentBlockId + ClassConnectorSetting.PacketContentSeperator + Assembly.GetExecutingAssembly().GetName().Version;
                                                if (!await ClassMiningNetwork.ObjectSeedNodeNetwork.SendPacketToSeedNodeAsync(packetShare, string.Empty))
                                                {
                                                    ClassMiningNetwork.DisconnectNetwork();
                                                }
                                            }).ConfigureAwait(false);
                                        }
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
            Program.TotalMiningCalculationRound[idThread]++;


            if (calculCompute - Math.Round(calculCompute) == 0
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

            string encryptedShare = ClassUtility.StringToHex(calculation + Program.CurrentBlockTimestampCreate);

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
        ///     Clear Mining Cache
        /// </summary>
        public static void ClearMiningCache()
        {

            bool error = true;
            while (error)
            {
                try
                {

                    ClassConsole.WriteLine("Clear mining cache | total calculation cached: " + ClassMining.DictionaryCacheMining.Count.ToString("F0"), 5);
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
