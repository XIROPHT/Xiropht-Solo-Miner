using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.SoloMining;
using Xiropht_Solo_Miner.ConsoleMiner;

namespace Xiropht_Solo_Miner.Mining
{
    public class ClassMiningStats
    {
        /// <summary>
        /// Const interval.
        /// </summary>
        private const int HashrateThreadIntervalShow = 60;

        /// <summary>
        ///  Mining Stats.
        /// </summary>
        public static int TotalBlockAccepted;
        public static int TotalBlockRefused;
        public static int TotalShareAccepted;
        public static int TotalShareInvalid;

        /// <summary>
        /// Mining Hashrate and Calculation Speed.
        /// </summary>
        public static List<int> TotalMiningHashrateRound = new List<int>();
        public static List<int> TotalMiningCalculationRound = new List<int>();
        public static float TotalHashrate;
        public static float TotalCalculation;


        /// <summary>
        /// For network.
        /// </summary>
        public static bool CanMining;
        private static bool _showHashrateEnabled;
        private static long _lastHashrateThreadShowed;

        /// <summary>
        /// Initialize mining stats objects.
        /// </summary>
        public static void InitializeMiningStats()
        {
            TotalMiningHashrateRound = new List<int>();
            TotalMiningCalculationRound = new List<int>();
            for (int i = 0; i < Program.ClassMinerConfigObject.mining_thread; i++)
            {
                if (i < Program.ClassMinerConfigObject.mining_thread)
                {
                    TotalMiningHashrateRound.Add(0);
                    TotalMiningCalculationRound.Add(0);
                }
            }
        }

        /// <summary>
        ///     Show the hashrate of the solo miner.
        /// </summary>
        public static void ShowHashrate()
        {
            if (!_showHashrateEnabled)
            {
                _showHashrateEnabled = true;
                _lastHashrateThreadShowed = DateTimeOffset.Now.ToUnixTimeSeconds();
                Task.Factory.StartNew(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            float totalRoundHashrate = 0;
                            float totalRoundCalculation = 0;

                            if (_lastHashrateThreadShowed + HashrateThreadIntervalShow <= DateTimeOffset.Now.ToUnixTimeSeconds())
                            {
                                _lastHashrateThreadShowed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                for (int i = 0; i < TotalMiningHashrateRound.Count; i++)
                                {
                                    if (i < TotalMiningHashrateRound.Count)
                                    {
                                        totalRoundHashrate += TotalMiningHashrateRound[i];
                                        totalRoundCalculation += TotalMiningCalculationRound[i];
                                        if (Program.ClassMinerConfigObject.mining_show_calculation_speed)
                                        {
                                            ClassConsole.WriteLine("Encryption Speed Thread " + i + " : " + TotalMiningHashrateRound[i] + " H/s | Calculation Speed Thread " + i + " : " + TotalMiningCalculationRound[i] + " C/s", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                        }
                                        else
                                        {
                                            ClassConsole.WriteLine("Encryption Speed Thread " + i + " : " + TotalMiningHashrateRound[i] + " H/s", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                        }
                                        TotalMiningCalculationRound[i] = 0;
                                        TotalMiningHashrateRound[i] = 0;
                                    }
                                }
                                if (Program.ClassMinerConfigObject.mining_show_calculation_speed)
                                {
                                    ClassConsole.WriteLine(totalRoundHashrate + " H/s | " + totalRoundCalculation + " C/s > ACCEPTED[" + TotalBlockAccepted + "] REFUSED[" + TotalBlockRefused + "]", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                }
                                else
                                {
                                    ClassConsole.WriteLine(totalRoundHashrate + " H/s | ACCEPTED[" + TotalBlockAccepted + "] REFUSED[" + TotalBlockRefused + "]", ClassConsoleColorEnumeration.ConsoleTextColorMagenta);
                                }
                            }
                            else
                            {
                                for (int i = 0; i < TotalMiningHashrateRound.Count; i++)
                                {
                                    if (i < TotalMiningHashrateRound.Count)
                                    {
                                        totalRoundHashrate += TotalMiningHashrateRound[i];
                                        totalRoundCalculation += TotalMiningCalculationRound[i];
                                        TotalMiningCalculationRound[i] = 0;
                                        TotalMiningHashrateRound[i] = 0;
                                    }
                                }
                            }

                            TotalHashrate = totalRoundHashrate;
                            TotalCalculation = totalRoundCalculation;

                            if (CanMining)
                            {
                                if (Program.ClassMinerConfigObject.mining_enable_proxy) // Share hashrate information to the proxy solo miner.
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
    }
}
