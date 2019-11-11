using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xiropht_Connector_All.RPC;
using Xiropht_Connector_All.Setting;
using Xiropht_Connector_All.Utils;

namespace Xiropht_Solo_Miner.Token
{
    public class ClassTokenNetwork
    {
        public const string PacketNotExist = "not_exist";
        public const string PacketResult = "result";

        /// <summary>
        /// Check if the wallet address exist on the network.
        /// </summary>
        /// <param name="walletAddress"></param>
        /// <returns></returns>
        public static async Task<bool> CheckWalletAddressExistAsync(string walletAddress)
        {

            if (Program.DictionaryWalletAddressCache.ContainsKey(walletAddress))
            {
                return true;
            }
            Dictionary<string, int> listOfSeedNodesSpeed = new Dictionary<string, int>();
            foreach (var seedNode in ClassConnectorSetting.SeedNodeIp)
            {

                try
                {
                    int seedNodeResponseTime = -1;
                    Task taskCheckSeedNode = Task.Run(() => seedNodeResponseTime = CheckPing.CheckPingHost(seedNode.Key, true));
                    taskCheckSeedNode.Wait(ClassConnectorSetting.MaxPingDelay);
                    if (seedNodeResponseTime == -1)
                    {
                        seedNodeResponseTime = ClassConnectorSetting.MaxSeedNodeTimeoutConnect;
                    }
                    listOfSeedNodesSpeed.Add(seedNode.Key, seedNodeResponseTime);

                }
                catch
                {
                    listOfSeedNodesSpeed.Add(seedNode.Key, ClassConnectorSetting.MaxSeedNodeTimeoutConnect); // Max delay.
                }

            }

            listOfSeedNodesSpeed = listOfSeedNodesSpeed.OrderBy(u => u.Value).ToDictionary(z => z.Key, y => y.Value);

            foreach (var seedNode in listOfSeedNodesSpeed)
            {
                try
                {
                    string randomSeedNode = seedNode.Key;
                    string request = ClassConnectorSettingEnumeration.WalletTokenType + ClassConnectorSetting.PacketContentSeperator + ClassRpcWalletCommand.TokenCheckWalletAddressExist + ClassConnectorSetting.PacketContentSeperator + walletAddress;
                    string result = await ProceedHttpRequest("http://" + randomSeedNode + ":" + ClassConnectorSetting.SeedNodeTokenPort + "/", request);
                    if (result != string.Empty && result != PacketNotExist)
                    {
                        JObject resultJson = JObject.Parse(result);
                        if (resultJson.ContainsKey(PacketResult))
                        {
                            string resultCheckWalletAddress = resultJson[PacketResult].ToString();
                            if (resultCheckWalletAddress.Contains(ClassConnectorSetting.PacketContentSeperator))
                            {
                                var splitResultCheckWalletAddress = resultCheckWalletAddress.Split(new[] { ClassConnectorSetting.PacketContentSeperator }, StringSplitOptions.None);

                                if (splitResultCheckWalletAddress[0] == ClassRpcWalletCommand.SendTokenCheckWalletAddressValid)
                                {
                                    Program.SaveWalletAddressCache(walletAddress);
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignored.
                }
            }
            return false;
        }

        /// <summary>
        /// Proceed http get request who target the token network.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="requestString"></param>
        /// <returns></returns>
        private static async Task<string> ProceedHttpRequest(string url, string requestString)
        {
            string result = string.Empty;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url + requestString);
            request.AutomaticDecompression = DecompressionMethods.GZip;
            request.ServicePoint.Expect100Continue = false;
            request.KeepAlive = false;
            request.Timeout = 10000;
            request.UserAgent = ClassConnectorSetting.CoinName + "Solo Miner - " + Assembly.GetExecutingAssembly().GetName().Version + "R";
            using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            result = await reader.ReadToEndAsync();
                        }
                    }
                }
            }

            return result;
        }
    }
}
