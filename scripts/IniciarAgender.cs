using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class IniciarAgender
{
    [STAThread]
    private static void Main()
    {
        string folder = AppDomain.CurrentDomain.BaseDirectory;
        string bat = Path.Combine(folder, "Iniciar-Agender.bat");

        if (!File.Exists(bat))
        {
            MessageBox.Show("Não encontrei o Iniciar-Agender.bat em " + folder, "Agender", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"\"" + bat + "\"\"")
        {
            WorkingDirectory = folder,
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }
}
