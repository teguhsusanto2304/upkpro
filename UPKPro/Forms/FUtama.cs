using Npgsql;
using UPKPro.Data;
using UPKPro.Security;

namespace UPKPro.Forms;

/// <summary>
/// Konfirmasi Pengguna
/// Port dari unit_utama.pas (TFUtama).
/// Alur: euser [Enter] -> cek nama di tabel pass -> epass [Enter] -> cek sandi -> buka FUtama1.
/// Belum diporting: cek dongle Rockey / register key (info.trd) dan masa trial.
/// </summary>
public partial class FUtama : Form
{
    private const string PesanAwal = "Masukkan user name dan password anda";

    public FUtama()
    {
        InitializeComponent();
        ApplyDelphiDefaults();
    }

    private void FormShow(object? sender, EventArgs e)
    {
        // unit_utama.pas:299 — bagian register key/dongle belum diporting; di sini cukup cek koneksi.
        ResetForm();
        if (!Database.TestConnection(out var error))
        {
            MessageBox.Show(this, "Tidak dapat terhubung ke database:\n" + error, Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void euserKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar != (char)Keys.Enter)
            return;
        e.Handled = true;

        // unit_utama.pas:849
        var nama = euser.Text.Trim();
        if (nama.Length == 0)
            return;

        if (!TryDb(() => UserRepository.Exists(nama), out var ada))
            return;

        if (ada)
        {
            pesan.Text = "Username diterima,..masukkan Password Anda";
            epass.Focus();
        }
        else
        {
            MessageBox.Show(this, "Username tidak terdaftar", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            pesan.Text = "Masukkan Username dan Password Anda";
            euser.SelectAll();
            euser.Focus();
        }
    }

    private void epassKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (e.KeyChar != (char)Keys.Enter)
            return;
        e.Handled = true;

        // unit_utama.pas:605
        if (!TryDb(() => UserRepository.Authenticate(euser.Text, epass.Text), out var user))
            return;

        if (user is null)
        {
            MessageBox.Show(this, "User dan Password tidak terdaftar", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            epass.Text = "";
            epass.Focus();
            return;
        }

        Session.SignIn(user);
        OpenMainMenu();
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
            Session.SignOut();
            ResetForm();
            Show();
            euser.Focus();
        }
        else
        {
            Close();
        }
    }

    private void ResetForm()
    {
        euser.Text = "";
        epass.Text = "";
        pesan.Text = PesanAwal;
    }

    /// <summary>Jalankan akses DB; tampilkan pesan bila gagal (padanan try..except di Delphi).</summary>
    private bool TryDb<T>(Func<T> action, out T result)
    {
        try
        {
            result = action();
            return true;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            result = default!;
            MessageBox.Show(this, "Gagal mengakses database:\n" + ex.Message, Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void tcekTimer(object? sender, EventArgs e)
    {
        // unit_utama.pas:961
        Application.Exit();
    }
}
