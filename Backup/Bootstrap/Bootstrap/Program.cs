namespace Bootstrap
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            var appArg =
                args.FirstOrDefault(a => a.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
                ?? "--app=softwaremanager";

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new BootstrapContext(appArg));
        }
    }
}
