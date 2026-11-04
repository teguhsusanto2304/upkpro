using UPKPro.Forms;

namespace UPKPro
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            // Alur seperti upk_pro.dpr: splash (FrmSp) -> login (FUtama) -> menu utama (FUtama1).
            using (var splash = new FrmSp())
            {
                splash.Show();
                splash.Update();
                var until = DateTime.Now.AddSeconds(1.5);
                while (DateTime.Now < until)
                {
                    Application.DoEvents();
                    Thread.Sleep(15);
                }
                splash.Close();
            }

            Application.Run(new FUtama());
        }
    }
}
