namespace Xiropht_Solo_Miner.Setting
{
    public class ClassStartupArgumentEnumeration
    {
        public const string ArgumentCharacterSplitter = "=";
        public const string ConfigFileArgument = "--config-file";
        public const string AppConfigContent = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<configuration>\r\n    <startup> \r\n        <supportedRuntime version=\"v4.0\" sku=\".NETFramework,Version=v4.6.1\"/>\r\n    </startup>\r\n  <runtime>\r\n    <gcAllowVeryLargeObjects enabled=\"true\"/>\r\n    <gcServer enabled=\"true\"/>\r\n    <GCHeapCount enabled=\"10\"/>\r\n    <GCNoAffinitize enabled=\"true\"/>\r\n  </runtime>\r\n</configuration>\r\n";
    }
}
