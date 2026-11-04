namespace UPKPro.Forms;

/// <summary>
/// Konfirmasi Pengguna
/// Port UI dari unit_utama.pas (TFUtama). Logika masih TODO.
/// </summary>
public partial class FUtama : Form
{
    public FUtama()
    {
        InitializeComponent();
        ApplyDelphiDefaults();
    }

    private void FormShow(object? sender, EventArgs e)
    {
        // TODO: port dari unit_utama.pas:299 (this.OnShow)
    }

    private void epassKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar != (char)Keys.Enter)
            return;
        e.Handled = true;

        // TODO: port autentikasi dari unit_utama.pas:605 (cek user di tabel pass + dekripsi sandi,
        // cek masa trial, atur tombol menu per level user).
#if DEBUG
        // Sementara (hanya build Debug): login dilewati agar navigasi UI bisa dicoba.
        OpenMainMenu();
#else
        MessageBox.Show(this, "Login belum diporting dari Delphi.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
#endif
    }

    private void OpenMainMenu()
    {
        Hide();
        DialogResult result;
        using (var main = new FUtama1())
            result = main.ShowDialog();

        if (result == DialogResult.Retry)
        {
            // Log Off: kembali ke login.
            epass.Text = "";
            Show();
            euser.Focus();
        }
        else
        {
            Close();
        }
    }

    private void euserKeyPress(object? sender, KeyPressEventArgs e)
    {
        // TODO: port dari unit_utama.pas:849 (euser.OnKeyPress)
    }

    private void tcekTimer(object? sender, EventArgs e)
    {
        // TODO: port dari unit_utama.pas:961 (tcek.OnTimer)
    }
}
