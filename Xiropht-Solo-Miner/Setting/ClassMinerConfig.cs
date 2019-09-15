namespace Xiropht_Solo_Miner.Setting
{
    public class ClassMinerConfig
    {
        public string mining_wallet_address = string.Empty;
        public int mining_thread;
        public int mining_thread_priority = 2;
        public bool mining_enable_cache = false;
        public bool mining_thread_spread_job;
        public bool mining_enable_automatic_thread_affinity;
        public string mining_manual_thread_affinity = string.Empty;
        public bool mining_enable_proxy;
        public int mining_proxy_port;
        public string mining_proxy_host = string.Empty;
        public int mining_percent_difficulty_start;
        public int mining_percent_difficulty_end;
        public bool mining_show_calculation_speed = false;
        public bool mining_enable_pow_mining_way = false;
    }
}
