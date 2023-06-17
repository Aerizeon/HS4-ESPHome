namespace HSPI_ESPHomeNative
{
    internal class Program
    {
        internal static HSPI _plugin;
        static void Main(string[] args)
        {
            _plugin = new HSPI();
            _plugin.Connect(args);
        }
    }
}
