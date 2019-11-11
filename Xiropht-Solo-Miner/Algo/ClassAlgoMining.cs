using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xiropht_Connector_All.Mining;

namespace Xiropht_Solo_Miner.Algo
{

    public class ClassPowShareObject
    {
        public decimal PowDifficultyShare;

        public string PowShareHash;

        public string PowShareNonce;

        public string PowShareCalculation;

        public decimal PowShareResultCalculation;

        public string PowEncryptedShare;
    }

    public class ClassAlgoMining
    {
        public static ICryptoTransform[] CryptoTransformMining;

        public static AesManaged[] AesManagedMining;

        public static SHA512Managed[] Sha512ManagedMining;

        public static MemoryStream[] MemoryStreamMining;

        public static CryptoStream[] CryptoStreamMining;

        private static readonly char[] HexArrayList = "0123456789ABCDEF".ToCharArray();

        public static int[] TotalNonceMining;


        /// <summary>
        /// Encrypt the math calculation generated for the Exact Share Mining System.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="idThread"></param>
        /// <returns></returns>
        public static string EncryptAesShare(string text, int idThread)
        {
            if (MemoryStreamMining[idThread] == null)
            {
                MemoryStreamMining[idThread] = new MemoryStream();
            }

            if (CryptoStreamMining[idThread] == null)
            {
                CryptoStreamMining[idThread] = new CryptoStream(MemoryStreamMining[idThread],
                    CryptoTransformMining[idThread], CryptoStreamMode.Write);
            }

            #region Do mining work

            var textBytes = Encoding.UTF8.GetBytes(text);
            CryptoStreamMining[idThread].Write(textBytes, 0, textBytes.Length);

            #endregion

            #region Flush mining work process

            if (!CryptoStreamMining[idThread].HasFlushedFinalBlock)
            {
                CryptoStreamMining[idThread].FlushFinalBlock();
                CryptoStreamMining[idThread].Flush();
            }
 

            #endregion

            #region Translate Mining work

            byte[] resultByteShare = MemoryStreamMining[idThread].ToArray();
            string result = GetHexStringFromByteArray(resultByteShare, 0, resultByteShare.Length);

            #endregion


            #region Cleanup work
            CryptoStreamMining[idThread] = new CryptoStream(MemoryStreamMining[idThread], CryptoTransformMining[idThread], CryptoStreamMode.Write);
            MemoryStreamMining[idThread].SetLength(0);
            Array.Clear(resultByteShare, 0, resultByteShare.Length);
            Array.Clear(textBytes, 0, textBytes.Length);

            #endregion

            return result;
        }


        /// <summary>
        /// Do PoW share from Exact Share Mining System.
        /// </summary>
        /// <param name="share"></param>
        /// <param name="hashTarget"></param>
        /// <param name="idThread"></param>
        /// <param name="currentBlockDifficulty"></param>
        /// <param name="result"></param>
        /// <param name="firstNumber"></param>
        /// <param name="secondNumber"></param>
        /// <param name="calculation"></param>
        /// <param name="maxPowDifficultyShare"></param>
        public static ClassPowShareObject DoPowShare(string share, string hashTarget, int idThread, decimal currentBlockDifficulty, decimal result, decimal firstNumber, decimal secondNumber, string calculation, decimal maxPowDifficultyShare)
        {

            if (TotalNonceMining[idThread] >= ClassPowSetting.MaxNonceValue)
            {
                TotalNonceMining[idThread] = 0;
            }

            #region Initialize PoW.

            byte[] mergedShareArray = new byte[ClassPowSetting.MergedPowShareSize];

            byte[] byteShareArray = Encoding.ASCII.GetBytes(share);

            byte[] byteTargetShareArray = Encoding.ASCII.GetBytes(Program.CurrentBlockIndication);

            byte[] nonceHashingArray = new byte[ClassPowSetting.NonceShareSize];

            #endregion

            #region Edit hash target with nonce.

            byteTargetShareArray[ClassPowSetting.OffsetTargetShareNonceByteIndex1] =
                (byte) TotalNonceMining[idThread];
            byteTargetShareArray[ClassPowSetting.OffsetTargetShareNonceByteIndex2] =
                (byte) (TotalNonceMining[idThread] >> ClassPowSetting.TargetShareNonceValueShift1);
            byteTargetShareArray[ClassPowSetting.OffsetTargetShareNonceByteIndex3] =
                (byte) (TotalNonceMining[idThread] >> ClassPowSetting.TargetShareNonceValueShift2);
            byteTargetShareArray[ClassPowSetting.OffsetTargetShareNonceByteIndex4] =
                (byte) (TotalNonceMining[idThread] >> ClassPowSetting.TargetShareNonceValueShift3);


            #endregion

            #region Merge share done with share target.

            Array.Copy(byteShareArray, 0, mergedShareArray, ClassPowSetting.AmountByteShareIndex,
                ClassPowSetting.AmountByteShareSize);
            Array.Copy(byteTargetShareArray, 0, mergedShareArray, ClassPowSetting.AmountByteShareTargetIndex,
                ClassPowSetting.AmountByteShareTargetSize);

            #endregion

            #region Encrypt and Compute merged share.


            CryptoStreamMining[idThread].Write(mergedShareArray, 0, mergedShareArray.Length);


            if (!CryptoStreamMining[idThread].HasFlushedFinalBlock)
            {
                CryptoStreamMining[idThread].FlushFinalBlock();
                CryptoStreamMining[idThread].Flush();
            }

            mergedShareArray = MemoryStreamMining[idThread].ToArray();
            MemoryStreamMining[idThread].SetLength(0);
            CryptoStreamMining[idThread] = new CryptoStream(MemoryStreamMining[idThread], CryptoTransformMining[idThread], CryptoStreamMode.Write);


            #endregion

            #region Create nonce array from total nonce produced.

            nonceHashingArray[ClassPowSetting.OffsetNonceByteIndex1] = (byte) TotalNonceMining[idThread];
            nonceHashingArray[ClassPowSetting.OffsetNonceByteIndex2] = (byte) (TotalNonceMining[idThread] >> ClassPowSetting.NonceValueShift1);
            nonceHashingArray[ClassPowSetting.OffsetNonceByteIndex3] = (byte) (TotalNonceMining[idThread] >> ClassPowSetting.NonceValueShift2);
            nonceHashingArray[ClassPowSetting.OffsetNonceByteIndex4] = (byte) (TotalNonceMining[idThread] >> ClassPowSetting.NonceValueShift3);

            #endregion

            #region Generate Pow Share Hash and Nonce Share Hash

            var powShareHash = ByteArrayToHexString(mergedShareArray).ToLower();

            var nonceShareHash = ByteArrayToHexString(nonceHashingArray).ToLower();

            #endregion


            #region Calculate percentage of value of the PoW Share with the share target value.


            decimal difficultyShareValue = 0;

            // From result of the math calculation.
            for (int i = ClassPowSetting.PowDifficultyShareFromResultStartIndex;
                i < ClassPowSetting.PowDifficultyShareFromResultEndIndex;
                i++)
            {
                if (i < ClassPowSetting.PowDifficultyShareFromResultEndIndex)
                {
                    decimal bytePowShareValue = powShareHash[i];
                    decimal bytePowTargetShareValue = hashTarget[i];
                    if (bytePowShareValue != 0 && bytePowTargetShareValue != 0)
                    {
                        if (bytePowShareValue == bytePowTargetShareValue)
                        {
                            difficultyShareValue += result;
                        }
                        else if (bytePowShareValue < bytePowTargetShareValue)
                        {
                            difficultyShareValue +=
                                (result * ((bytePowShareValue / bytePowTargetShareValue) * 100)) / 100;
                        }
                        else if (bytePowShareValue > bytePowTargetShareValue)
                        {
                            difficultyShareValue -=
                                (result * ((bytePowTargetShareValue / bytePowShareValue) * 100)) / 100;
                        }
                    }
                }
            }

            // From the first number of the math calculation.
            for (int i = ClassPowSetting.PowDifficultyShareFromFirstNumberStartIndex;
                i < ClassPowSetting.PowDifficultyShareFromFirstNumberEndIndex;
                i++)
            {
                if (i < ClassPowSetting.PowDifficultyShareFromFirstNumberEndIndex)
                {
                    decimal bytePowShareValue = powShareHash[i];
                    decimal bytePowTargetShareValue = hashTarget[i];
                    if (bytePowShareValue != 0 && bytePowTargetShareValue != 0)
                    {
                        if (bytePowShareValue == bytePowTargetShareValue)
                        {
                            difficultyShareValue += firstNumber;
                        }
                        else if (bytePowShareValue < bytePowTargetShareValue)
                        {
                            difficultyShareValue +=
                                (firstNumber * ((bytePowShareValue / bytePowTargetShareValue) * 100)) / 100;
                        }
                        else if (bytePowShareValue > bytePowTargetShareValue)
                        {
                            difficultyShareValue -=
                                (firstNumber * ((bytePowTargetShareValue / bytePowShareValue) * 100)) / 100;
                        }
                    }
                }
            }

            // From the second number of the math calculation.
            for (int i = ClassPowSetting.PowDifficultyShareFromSecondNumberStartIndex;
                i < ClassPowSetting.PowDifficultyShareFromSecondtNumberEndIndex;
                i++)
            {
                if (i < ClassPowSetting.PowDifficultyShareFromSecondtNumberEndIndex)
                {
                    decimal bytePowShareValue = powShareHash[i];
                    decimal bytePowTargetShareValue = hashTarget[i];
                    if (bytePowShareValue != 0 && bytePowTargetShareValue != 0)
                    {
                        if (bytePowShareValue == bytePowTargetShareValue)
                        {
                            difficultyShareValue += secondNumber;
                        }
                        else if (bytePowShareValue < bytePowTargetShareValue)
                        {
                            difficultyShareValue +=
                                (secondNumber * ((bytePowShareValue / bytePowTargetShareValue) * 100)) / 100;
                        }
                        else if (bytePowShareValue > bytePowTargetShareValue)
                        {
                            difficultyShareValue -=
                                (secondNumber * ((bytePowTargetShareValue / bytePowShareValue) * 100)) / 100;
                        }
                    }
                }
            }


            difficultyShareValue = Math.Round(difficultyShareValue, 0);


            #endregion






            TotalNonceMining[idThread]++;

            #region Cleanup work

            Array.Clear(nonceHashingArray, 0, nonceHashingArray.Length);
            Array.Clear(mergedShareArray, 0, mergedShareArray.Length);
            Array.Clear(byteShareArray, 0, byteShareArray.Length);
            Array.Clear(byteTargetShareArray, 0, byteTargetShareArray.Length);


            #endregion

            return new ClassPowShareObject()
            {
                PowDifficultyShare = difficultyShareValue,
                PowShareHash = powShareHash,
                PowShareNonce = nonceShareHash,
                PowShareCalculation = calculation,
                PowShareResultCalculation = result,
                PowEncryptedShare = share
            };
        }

        /// <summary>
        /// Convert a byte array to hex string like Bitconverter class.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static string GetHexStringFromByteArray(byte[] value, int startIndex, int length)
        {
            int newSize = length * 3;
            char[] hexCharArray = new char[newSize];
            int currentIndex;
            for (currentIndex = 0; currentIndex < newSize; currentIndex += 3)
            {
                byte currentByte = value[startIndex++];
                hexCharArray[currentIndex] = GetHexValue(currentByte / 0x10);
                hexCharArray[currentIndex + 1] = GetHexValue(currentByte % 0x10);
                hexCharArray[currentIndex + 2] = '-';
            }
            return new string(hexCharArray, 0, hexCharArray.Length - 1);
        }

        /// <summary>
        /// Get Hex value from char index value.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private static char GetHexValue(int i)
        {
            if (i < 10)
            {
                return (char)(i + 0x30);
            }
            return (char)((i - 10) + 0x41);
        }

        /// <summary>
        /// Encrypt share with XOR.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string EncryptXorShare(string text, string key)
        {
            char[] resultXor = new char[text.Length];

            for (int c = 0; c < text.Length; c++)
            {
                resultXor[c] = (char)(text[c] ^ (uint)key[c % key.Length]);
            }
            return new string(resultXor, 0, resultXor.Length);
        }

        /// <summary>
        /// Generate a sha512 hash
        /// </summary>
        /// <param name="input"></param>
        /// <param name="idThread"></param>
        /// <returns></returns>
        public static string GenerateSha512FromString(string input, int idThread)
        {
            if (Sha512ManagedMining[idThread] == null)
            {
                Sha512ManagedMining[idThread] = new SHA512Managed();
            }

            var bytes = Encoding.UTF8.GetBytes(input);

           return ByteArrayToHexString(Sha512ManagedMining[idThread].ComputeHash(bytes));

        }


        /// <summary>
        /// Convert a byte array into a hex string
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string ByteArrayToHexString(byte[] bytes)
        {
            char[] hexChars = new char[(bytes.Length * 2)];
            for (int j = 0; j < bytes.Length; j++)
            {
                int v = bytes[j] & 255;
                hexChars[j * 2] = HexArrayList[(int)((uint)v >> 4)];
                hexChars[(j * 2) + 1] = HexArrayList[v & 15];
            }
            return new string(hexChars);
        }

    }
}