namespace Updater
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            var appId = AppHelper.ParseAppId(args);
            if (string.IsNullOrEmpty(appId))
            {
                MessageBox.Show(
                    "用法：Updater.exe --app=<软件ID>",
                    "参数错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            var offline = args.Any(a => a.Equals("--offline", StringComparison.OrdinalIgnoreCase));

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new UpdaterContext(appId, offline));
        }
    }
}
