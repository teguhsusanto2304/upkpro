namespace UPKPro.Forms;

/// <summary>
/// FUtama1
/// Port UI dari Unit_utama1.pas (TFUtama1). Logika masih TODO.
/// </summary>
public partial class FUtama1 : Form
{
    public FUtama1()
    {
        InitializeComponent();
        ApplyDelphiDefaults();
    }

    /// <summary>
    /// Membuka form modul secara modal. Di Delphi menu utama di-disable (futama1.Enabled := false)
    /// lalu diaktifkan lagi saat form modul ditutup; ShowDialog memberi perilaku yang sama.
    /// </summary>
    private void OpenModule<T>(Action<T>? setup = null) where T : Form, new()
    {
        using var form = new T();
        setup?.Invoke(form);
        form.ShowDialog(this);
    }

    private void btpegawaiClick(object? sender, EventArgs e)
    {
        // TODO: port cek lisensi/level user dari Unit_utama1.pas:535 (btpegawai.OnClick)
        OpenModule<FAnggota>();
    }

    private void btpesanhClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:556 (btpesanh.OnClick)
    }

    private void btproposalClick(object? sender, EventArgs e)
    {
        // TODO: port cek lisensi/level user dari Unit_utama1.pas:634 (btproposal.OnClick)
        OpenModule<FProposal>();
    }

    private void btkeuClick(object? sender, EventArgs e)
    {
        // TODO: port cek lisensi/level user dari Unit_utama1.pas:657 (btkeu.OnClick)
        OpenModule<FPembiayaan>();
    }

    private void btsetsmsClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:840 (btsetsms.OnClick)
    }

    private void btsettingClick(object? sender, EventArgs e)
    {
        // TODO: port cek level user dari Unit_utama1.pas:854 (btsetting.OnClick)
        OpenModule<F_Rekening>();
    }

    private void Button3Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:876 (Button3.OnClick)
    }

    private void dbdesaClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1011 (dbdesa.OnClick)
    }

    private void DBGrid3CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1018 (DBGrid3.OnCellClick)
    }

    private void DBGrid4CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1025 (DBGrid4.OnCellClick)
    }

    private void DBGrid5DblClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1032 (DBGrid5.OnDblClick)
    }

    private void enotujuanKeyPress(object? sender, KeyPressEventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1046 (enotujuan.OnKeyPress)
    }

    private void btsegarClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1081 (btsegar.OnClick)
    }

    private void etimerChange(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1190 (etimer.OnChange)
    }

    private void FormClose(object? sender, FormClosedEventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1200 (this.OnClose)
    }

    private void FormShow(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1205 (this.OnShow)
    }

    private void Memo1Change(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1323 (Memo1.OnChange)
    }

    private void Memo2Change(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1398 (Memo2.OnChange)
    }

    private void rghminClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1463 (rghmin.OnClick)
    }

    private void SpeedButton13Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1578 (SpeedButton13.OnClick)
    }

    private void btarsipClick(object? sender, EventArgs e)
    {
        // TODO: port cek level user dari Unit_utama1.pas:1585 (btarsip.OnClick)
        OpenModule<FLok>();
    }

    private void btlaporanClick(object? sender, EventArgs e)
    {
        // Log Off: kembali ke form login (Unit_utama1.pas:1606).
        DialogResult = DialogResult.Retry;
        Close();
    }

    private void btopensmsClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1625 (btopensms.OnClick)
    }

    private void SpeedButton6Click(object? sender, EventArgs e)
    {
        // Keluar (Unit_utama1.pas:1721: Application.Terminate).
        DialogResult = DialogResult.Abort;
        Close();
    }

    private void btbackupClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1726 (btbackup.OnClick)
    }

    private void btperguliranClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1777. Di Delphi, edisi "UE" membuka FVerifikasi tab Realisasi,
        // edisi "SE" membuka FPembiayaan; tab juga disembunyikan menurut level user.
        OpenModule<FVerifikasi>(f => f.pagerealisasi.SelectedIndex = 1);
    }

    private void btverifikasiClick(object? sender, EventArgs e)
    {
        // TODO: port cek lisensi/level user dari Unit_utama1.pas:1841 (btverifikasi.OnClick)
        OpenModule<FVerifikasi>();
    }

    private void btlapClick(object? sender, EventArgs e)
    {
        // TODO: port cek lisensi/level user dari Unit_utama1.pas:1915 (btlap.OnClick)
        OpenModule<FKeuangan>();
    }

    private void Timer1Timer(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1937 (Timer1.OnTimer)
    }

    private void timerbalasTimer(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1942 (timerbalas.OnTimer)
    }

    private void timerhapusTimer(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:1980 (timerhapus.OnTimer)
    }

    private void timerinboxTimer(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2007 (timerinbox.OnTimer)
    }

    private void timerkirimmasalTimer(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2116 (timerkirimmasal.OnTimer)
    }

    private void timerkirimTimer(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2151 (timerkirim.OnTimer)
    }

    private void BitBtn10Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2177 (BitBtn10.OnClick)
    }

    private void BitBtn11Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2207 (BitBtn11.OnClick)
    }

    private void BitBtn12Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2243 (BitBtn12.OnClick)
    }

    private void BitBtn13Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2263 (BitBtn13.OnClick)
    }

    private void BitBtn14Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2285 (BitBtn14.OnClick)
    }

    private void BitBtn15Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2318 (BitBtn15.OnClick)
    }

    private void BitBtn16Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2343 (BitBtn16.OnClick)
    }

    private void BitBtn17Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2385 (BitBtn17.OnClick)
    }

    private void BitBtn18Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2398 (BitBtn18.OnClick)
    }

    private void BitBtn19Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2404 (BitBtn19.OnClick)
    }

    private void btcekClick(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2426 (btcek.OnClick)
    }

    private void BitBtn1Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2454 (BitBtn1.OnClick)
    }

    private void BitBtn2Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2465 (BitBtn2.OnClick)
    }

    private void BitBtn3Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2477 (BitBtn3.OnClick)
    }

    private void BitBtn4Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2495 (BitBtn4.OnClick)
    }

    private void BitBtn5Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2605 (BitBtn5.OnClick)
    }

    private void BitBtn6Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2650 (BitBtn6.OnClick)
    }

    private void BitBtn7Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2670 (BitBtn7.OnClick)
    }

    private void BitBtn8Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2689 (BitBtn8.OnClick)
    }

    private void BitBtn9Click(object? sender, EventArgs e)
    {
        // TODO: port dari Unit_utama1.pas:2709 (BitBtn9.OnClick)
    }

    private void btblmClick(object? sender, EventArgs e)
    {
        // TODO: port cek level user dari Unit_utama1.pas:2732 (btblm.OnClick)
        OpenModule<Fblm>();
    }
}
