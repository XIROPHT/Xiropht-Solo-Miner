using System;
using System.Security.Cryptography;
using System.Text;

namespace Xiropht_Solo_Miner.Mining
{
    public class ClassMiningMathOperatorEnumeration
    {
        public const string MathOperatorPlus = "+";
        public const string MathOperatorMultiplicator = "*";
        public const string MathOperatorModulo = "%";
        public const string MathOperatorLess = "-";
        public const string MathOperatorDividor = "/";
    }

    public class ClassMiningMath
    {

        public static readonly string[] RandomOperatorCalculation = { ClassMiningMathOperatorEnumeration.MathOperatorPlus, ClassMiningMathOperatorEnumeration.MathOperatorMultiplicator, ClassMiningMathOperatorEnumeration.MathOperatorModulo, ClassMiningMathOperatorEnumeration.MathOperatorLess, ClassMiningMathOperatorEnumeration.MathOperatorDividor };

        private static readonly char[] RandomNumberCalculation = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };


        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngSize;
        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngInteger;
        [ThreadStatic] private static RNGCryptoServiceProvider _generatorRngJob;
        [ThreadStatic] private static StringBuilder _numberBuilder;


        #region RNG Random Number Generator

        /// <summary>
        ///     Get a random number in integer size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static int GetRandomBetweenSize(int minimumValue, int maximumValue)
        {

            if (_generatorRngSize == null)
            {
                _generatorRngSize = new RNGCryptoServiceProvider();
            }

            byte[] randomByteSize = new byte[1];

            _generatorRngSize.GetBytes(randomByteSize);

            var asciiValueOfRandomCharacter = Convert.ToDecimal(randomByteSize[0]);

            var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

            var range = maximumValue - minimumValue + 1;

            var randomValueInRange = Math.Floor(multiplier * range);


            return (int)(minimumValue + randomValueInRange);

        }

        /// <summary>
        ///     Get a random number in integer size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static int GetRandomBetween(int minimumValue, int maximumValue)
        {

            if (_generatorRngInteger == null)
            {
                _generatorRngInteger = new RNGCryptoServiceProvider();
            }

            byte[] randomByteSize = new byte[sizeof(int)];

            _generatorRngInteger.GetBytes(randomByteSize);

            var asciiValueOfRandomCharacter =
                Convert.ToDecimal(randomByteSize[GetRandomBetweenSize(0, randomByteSize.Length - 1)]);

            var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

            var range = maximumValue - minimumValue + 1;

            var randomValueInRange = Math.Floor(multiplier * range);


            return (int)(minimumValue + randomValueInRange);

        }

        /// <summary>
        /// Get a random number in float size.
        /// </summary>
        /// <param name="minimumValue"></param>
        /// <param name="maximumValue"></param>
        /// <returns></returns>
        public static decimal GetRandomBetweenJob(decimal minimumValue, decimal maximumValue)
        {

            if (_generatorRngJob == null)
            {
                _generatorRngJob = new RNGCryptoServiceProvider();
            }

            byte[] randomByteSize = new byte[sizeof(decimal)];

            _generatorRngJob.GetBytes(randomByteSize);

            var asciiValueOfRandomCharacter =
                Convert.ToDecimal(randomByteSize[GetRandomBetweenSize(0, randomByteSize.Length - 1)]);

            var multiplier = Math.Max(0, asciiValueOfRandomCharacter / 255m - 0.00000000001m);

            var range = maximumValue - minimumValue + 1;

            var randomValueInRange = Math.Floor(multiplier * range);


            return (minimumValue + randomValueInRange);

        }

        #endregion

        #region Random Number Generator by random index combined with RNG Random Number generator functions

        /// <summary>
        /// Return a number for complete a math calculation text.
        /// </summary>
        /// <returns></returns>
        public static decimal GenerateNumberMathCalculation(decimal minRange, decimal maxRange)
        {
            if (_numberBuilder == null)
            {
                _numberBuilder = new StringBuilder();
            }
            else
            {
                _numberBuilder.Clear();
            }

            int randomSize = GetRandomBetweenSize(minRange.ToString("F0").Length, GetRandomBetweenJob(minRange, maxRange).ToString("F0").Length);
            int counter = 0;

            bool cleanGenerator = false;
            while (ClassMiningStats.CanMining)
            {


                _numberBuilder.Append(
                    RandomNumberCalculation[GetRandomBetween(0, RandomNumberCalculation.Length - 1)]);


                counter++;
                if (_numberBuilder[0] == RandomNumberCalculation[0])
                {
                    cleanGenerator = true;
                }

                if (!cleanGenerator)
                {
                    if (counter == randomSize)
                    {
                        if (decimal.TryParse(_numberBuilder.ToString(), out var number))
                        {

                            return number;

                        }

                        cleanGenerator = true;
                    }
                }

                if (cleanGenerator)
                {
                    _numberBuilder.Clear();
                    counter = 0;
                    cleanGenerator = false;

                }
            }

            if (decimal.TryParse(_numberBuilder.ToString(), out var numberResult))
            {
                return numberResult;
            }

            return 0;
        }


        #endregion

        /// <summary>
        /// Build calculation string
        /// </summary>
        /// <param name="firstNumber"></param>
        /// <param name="mathOperator"></param>
        /// <param name="secondNumber"></param>
        /// <returns></returns>
        public static string BuildCalculationString(decimal firstNumber, string mathOperator, decimal secondNumber)
        {
            return firstNumber.ToString("F0") + " " + mathOperator + " " + secondNumber.ToString("F0");
        }

        /// <summary>
        /// Return result from a math calculation.
        /// </summary>
        /// <param name="firstNumber"></param>
        /// <param name="operatorCalculation"></param>
        /// <param name="secondNumber"></param>
        /// <returns></returns>
        public static decimal ComputeCalculation(decimal firstNumber, string operatorCalculation, decimal secondNumber)
        {
            decimal number1 = firstNumber;
            decimal number2 = secondNumber;
            if (number1 == 0 || number2 == 0)
            {
                return 0;
            }

            try
            {
                switch (operatorCalculation)
                {
                    case ClassMiningMathOperatorEnumeration.MathOperatorPlus:
                        return number1 + number2;
                    case ClassMiningMathOperatorEnumeration.MathOperatorLess:
                        return number1 - number2;
                    case ClassMiningMathOperatorEnumeration.MathOperatorMultiplicator:
                        return number1 * number2;
                    case ClassMiningMathOperatorEnumeration.MathOperatorModulo:
                        return number1 % number2;
                    case ClassMiningMathOperatorEnumeration.MathOperatorDividor:
                        return number1 / number2;
                }
            }
            catch
            {
                // Ignored.
            }

            return 0;
        }
    }
}
