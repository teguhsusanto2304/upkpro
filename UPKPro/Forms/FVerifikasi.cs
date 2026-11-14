using System.Data;
using System.Drawing;
using System.Globalization;
using Dapper;
using UPKPro.Data;

namespace UPKPro.Forms;

/// <summary>
/// Verifikasi dan Realisasi Pinjaman
/// Port dari unit_verifikasi.pas (TFVerifikasi).
/// </summary>
public partial class FVerifikasi : Form
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");
    private static readonly string[] StatusVerifikasi =
        ["LENGKAP", "VERIFIKASI", "REALISASI", "RE_VERIFIKASI", "RE_AKAD"];
    private bool _formattingAmount;
    private bool _updatingIncomeFigures;

    private readonly BindingSource bsProposal = new();
    private readonly BindingSource bsAnggotaProposal = new();
    private readonly BindingSource bsJumlahProposal = new();
    private readonly BindingSource bsJumlahAnggota = new();
    private readonly BindingSource bsKelompok = new();
    private readonly BindingSource bsAnggota = new();
    private readonly BindingSource bsRiwayatPembiayaan = new();
    private readonly BindingSource bsPembiayaanAg = new();
    private readonly BindingSource bsPembiayaanBatal = new();
    private readonly BindingSource bsPinjamanDesa = new();
    private readonly BindingSource bsPinjamanDesaDetail = new();
    private readonly BindingSource bsAkad = new();
    private readonly BindingSource bsAkadAnggota = new();
    private readonly BindingSource bsJenisPby = new();
    private readonly Dictionary<BindingSource, List<(Control Control, string Field)>> fieldBindings = [];
    private bool _refreshingFields;

    public FVerifikasi()
    {
        InitializeComponent();
        SetupBindings();
        bsProposal.CurrentChanged += (_, _) =>
        {
            RefreshFields(bsProposal);
            if (!_refreshingFields)
                LoadAnggotaProposal();
        };
        bsAnggota.CurrentChanged += (_, _) => LoadMemberLoans();
        bsKelompok.CurrentChanged += (_, _) => LoadGroupLoans();
        bsPembiayaanBatal.CurrentChanged += (_, _) => LoadCancellationDetails();
        ApplyDelphiDefaults();
    }

    private void LoadMemberLoans()
    {
        var nik = CurrentValue(bsAnggota, "nik");
        if (nik.Length == 0)
        {
            bsPembiayaanAg.DataSource = null;
            return;
        }

        Requery(bsPembiayaanAg,
            "SELECT * FROM pembiayaan_ag WHERE nik = @nik ORDER BY no_pembiayaan",
            new { nik });
    }

    private void LoadGroupLoans()
    {
        var noKelompok = CurrentValue(bsKelompok, "no_kelompok");
        if (noKelompok.Length == 0)
        {
            bsRiwayatPembiayaan.DataSource = null;
            return;
        }

        Requery(bsRiwayatPembiayaan,
            "SELECT * FROM pembiayaan WHERE no_anggota = @noKelompok ORDER BY tglkredit DESC",
            new { noKelompok });
    }

    private void LoadCancellationDetails()
    {
        if (bsPembiayaanBatal.Current is not DataRowView row)
            return;

        etglreal.Value = ReadDate(row, "tglrealisasi", etglreal.Value);
        etglkreditx.Value = ReadDate(row, "tglkredit", etglkreditx.Value);
        etgljttempox.Value = ReadDate(row, "tgljatuhtempo", etgljttempox.Value);
        elamax.Text = FieldText(row, "lama", "0");
        epokokx.Text = FieldText(row, "pokokrp", "0");
        ebungapctbatal.Text = FieldText(row, "bungapct", "0");
        emetodebatal.SelectedIndex = int.TryParse(FieldText(row, "metode", "0"), out var method) ? method - 1 : -1;
        elamaulang.Text = "0";
        epokokxChange(this, EventArgs.Empty);
    }

    private static DateTime ReadDate(DataRowView row, string field, DateTime fallback)
    {
        if (!row.Row.Table.Columns.Contains(field) || row[field] is DBNull)
            return fallback;
        var value = Convert.ToDateTime(row[field], CultureInfo.InvariantCulture);
        return value >= DateTimePicker.MinimumDateTime && value <= DateTimePicker.MaximumDateTime
            ? value
            : fallback;
    }

    private static DateTime MaturityDate(DateTime start, string monthText, DateTime fallback)
    {
        if (!double.TryParse(monthText, NumberStyles.Number, Id, out var months) ||
            months < 0 || !double.IsFinite(months))
            return fallback;

        var days = months * (365d / 12d);
        if (days > (DateTimePicker.MaximumDateTime - start).TotalDays)
            return fallback;
        return start.AddDays(days);
    }

    private void UpdateLoanDistribution()
    {
        switch (rgsbpinjaman.ItemIndex)
        {
            case 0:
                dbsbpinjaman.Visible = true;
                dbtahunpinjam.Visible = false;
                Requery(bsPinjamanDesa,
                    "SELECT desa, count(desa) AS jumlah FROM pembiayaan WHERE status = @status GROUP BY desa",
                    new { status = "BL" });
                break;
            case 1:
                if (!int.TryParse(etahun.Text, out var year))
                {
                    Info("Tahun pinjaman tidak valid.");
                    return;
                }
                dbsbpinjaman.Visible = true;
                dbtahunpinjam.Visible = false;
                Requery(bsPinjamanDesa,
                    "SELECT desa, count(desa) AS jumlah FROM pembiayaan WHERE tahun = @year GROUP BY desa",
                    new { year });
                break;
            case 2:
                dbsbpinjaman.Visible = false;
                dbtahunpinjam.Visible = true;
                Requery(bsPinjamanDesaDetail,
                    "SELECT tahun, count(tahun) AS jumlah FROM pembiayaan WHERE desa = @desa GROUP BY tahun ORDER BY tahun",
                    new { desa = dbnamadesa.Text.Trim() });
                break;
            case 3:
                dbsbpinjaman.Visible = false;
                dbtahunpinjam.Visible = true;
                Requery(bsPinjamanDesaDetail,
                    "SELECT tahun, sum(pokokrp) AS jumlah FROM pembiayaan WHERE desa = @desa GROUP BY tahun ORDER BY tahun",
                    new { desa = dbnamadesa.Text.Trim() });
                break;
        }
    }

    private void SetupBindings()
    {
        BindGrid(bsProposal, DBGrid6, DBGrid1);
        BindGrid(bsAnggotaProposal, dbdafpinjaman, DBGrid2);
        BindGrid(bsJumlahProposal, DBGrid9);
        BindGrid(bsKelompok, gridcari);
        BindGrid(bsAnggota, DBGrid4);
        BindGrid(bsRiwayatPembiayaan, DBGrid3);
        BindGrid(bsPembiayaanAg, DBGrid7, DBGrid10);
        BindGrid(bsPembiayaanBatal, dbgridx);
        BindGrid(bsPinjamanDesa, DBGrid5);
        BindGrid(bsPinjamanDesaDetail, DBGrid8);

        BindField(dbjenispby, bsProposal, "jenispby");
        BindField(DBEdit3, bsProposal, "ketua");
        BindField(DBEdit9, bsProposal, "desa");
        BindField(dbnamakp2, bsProposal, "nama");
        BindField(noprop2, bsProposal, "noproposal");
        BindField(DBEdit12, bsProposal, "jumlah");
        BindField(dbstatus2, bsProposal, "status1");
        BindField(dbmetode, bsProposal, "metode");
        BindField(dblama, bsProposal, "lama");
        BindField(dbbungapct, bsProposal, "bungapct");
        BindField(DBEdit24, bsProposal, "nama");
        BindField(DBEdit25, bsProposal, "noproposal");
        BindField(DBEdit1, bsProposal, "ketua");
        BindField(dbdesa, bsProposal, "desa");
        BindField(dbkodedesa, bsProposal, "kodedesa");
        BindField(dbnama, bsProposal, "nama");
        BindField(enoindukkp1, bsProposal, "no_kelompok");
        BindField(noprop3, bsProposal, "noproposal");
        BindField(dbmetode1, bsProposal, "metode");
        BindField(dbjenispby1, bsProposal, "jenispby");
        BindField(dbjeniskp, bsProposal, "status");
        BindField(DBEdit11, bsProposal, "jumlah");
        BindField(dblama1, bsProposal, "lama");
        BindField(dbbungar, bsProposal, "bungapct");
        BindField(enoindukkp, bsProposal, "no_kelompok");

        BindField(dbnik1, bsAnggotaProposal, "nik");
        BindField(DBEdit5, bsAnggotaProposal, "nama");
        BindField(DBEdit10, bsAnggotaProposal, "alamat");
        BindField(DBEdit6, bsAnggotaProposal, "jumlahlama");
        BindField(DBEdit7, bsAnggotaProposal, "jumlahbaru");
        BindField(DBMemo1, bsAnggotaProposal, "memo");
        BindField(dbnikcari, bsAnggotaProposal, "nik");
        BindField(DBEdit22, bsAnggotaProposal, "nama");

        BindField(DBEdit29, bsJumlahAnggota, "jumlah");
        BindField(DBEdit30, bsJumlahAnggota, "jumver");
        BindField(DBEdit31, bsJumlahAnggota, "jumreal");
        BindField(dbjumpin, bsJumlahAnggota, "jumpin");
        BindField(DBEdit19, bsJenisPby, "namapby");

        BindField(DBEdit34, bsKelompok, "no_kelompok");
        BindField(DBEdit20, bsKelompok, "nama");
        BindField(DBEdit32, bsAnggota, "nik");
        BindField(DBEdit33, bsAnggota, "nama");
        BindField(DBEdit23, bsRiwayatPembiayaan, "no_pembiayaan");
        BindField(DBEdit21, bsPembiayaanBatal, "no_pembiayaan");
        BindField(DBEdit26, bsPembiayaanBatal, "status");
        BindField(DBEdit27, bsPembiayaanBatal, "jenispby");
        BindField(DBEdit28, bsPembiayaanBatal, "nama");

        BindField(DBMemo2, bsAkad, "memo1");
        BindField(DBMemo3, bsAkad, "memo2");
        BindField(DBMemo4, bsAkad, "memo3");
        BindField(DBMemo5, bsAkad, "memo4");
        BindField(DBMemo6, bsAkad, "memo5");
        BindField(DBMemo7, bsAkad, "memo6");
        BindField(DBMemo8, bsAkad, "memo7");
        BindField(DBMemo9, bsAkad, "memo8");
        BindField(DBMemo10, bsAkadAnggota, "memo1");
        BindField(DBMemo11, bsAkadAnggota, "memo2");
        BindField(DBMemo12, bsAkadAnggota, "memo3");
        BindField(DBMemo15, bsAkadAnggota, "memo6");
        BindField(DBMemo16, bsAkadAnggota, "memo7");
        BindField(DBMemo17, bsAkadAnggota, "memo8");
    }

    private static void BindGrid(BindingSource source, params DataGridView[] grids)
    {
        foreach (var grid in grids)
        {
            grid.AutoGenerateColumns = false;
            grid.DataSource = source;
        }
    }

    private void BindField(TextBox box, BindingSource source, string field)
    {
        box.ReadOnly = true;
        AddFieldBinding(box, source, field);
    }

    private void BindField(UPKPro.Controls.CaptionPanel box, BindingSource source, string field)
    {
        AddFieldBinding(box, source, field);
    }

    private void AddFieldBinding(Control control, BindingSource source, string field)
    {
        if (!fieldBindings.TryGetValue(source, out var fields))
        {
            fields = [];
            fieldBindings.Add(source, fields);
            source.CurrentChanged += (_, _) => RefreshFields(source);
            source.ListChanged += (_, e) =>
            {
                if (e.ListChangedType == System.ComponentModel.ListChangedType.Reset)
                    RefreshFields(source);
            };
        }
        fields.Add((control, field));
    }

    private void RefreshFields(BindingSource source)
    {
        if (_refreshingFields || !fieldBindings.TryGetValue(source, out var fields))
            return;

        _refreshingFields = true;
        try
        {
            var row = source.Current as DataRowView;
            foreach (var (control, field) in fields)
            {
                var value = row is null || !row.Row.Table.Columns.Contains(field) || row[field] is DBNull
                    ? ""
                    : FormatValue(row[field]);
                if (control is TextBox textBox)
                    textBox.Text = value;
                else if (control is UPKPro.Controls.CaptionPanel captionPanel)
                    captionPanel.Text = value;
            }
        }
        finally
        {
            _refreshingFields = false;
        }
    }

    private static string FormatValue(object value) => value switch
    {
        DateTime date => date.ToString("dd/MM/yyyy", Id),
        decimal or double or float => ((IFormattable)value).ToString("#,0.##", Id),
        _ => value.ToString()?.Trim() ?? "",
    };

    private bool Requery(BindingSource source, string sql, object? parameters = null)
    {
        try
        {
            source.DataSource = Database.QueryTable(sql, parameters);
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
    }

    private void LoadProposalList()
    {
        Requery(bsProposal,
            "SELECT * FROM proposal_klp WHERE status1 IN @statuses ORDER BY tglmasuk, status1",
            new { statuses = StatusVerifikasi });

        Requery(bsJumlahProposal,
            "SELECT status1, count(status1) AS jumlah FROM proposal_klp WHERE status1 <> @status GROUP BY status1 ORDER BY status1",
            new { status = "TERIMA" });

        LoadProposalTotals();
    }

    private void LoadRealizationQueue()
    {
        Requery(bsProposal,
            "SELECT * FROM proposal_klp WHERE status1 IN @statuses ORDER BY tglmasuk",
            new { statuses = new[] { "REALISASI", "AKAD", "RE_AKAD" } });
        Requery(bsJumlahProposal,
            "SELECT status1, count(status1) AS jumlah FROM proposal_klp WHERE status1 <> @status GROUP BY status1 ORDER BY status1",
            new { status = "TERIMA" });
        LoadProposalTotals();
    }

    private void LoadAnggotaProposal()
    {
        var noProposal = CurrentValue(bsProposal, "noproposal");
        if (noProposal.Length == 0)
        {
            bsAnggotaProposal.DataSource = null;
            bsJumlahAnggota.DataSource = null;
            return;
        }

        Requery(bsAnggotaProposal,
            "SELECT * FROM proposal_ag WHERE noproposal = @noProposal ORDER BY nik",
            new { noProposal });
        LoadProposalTotals();
    }

    private void LoadProposalTotals()
    {
        var noProposal = CurrentValue(bsProposal, "noproposal");
        if (noProposal.Length == 0)
        {
            bsJumlahAnggota.DataSource = null;
            return;
        }

        Requery(bsJumlahAnggota,
            """
            SELECT COALESCE(sum(jumlahbaru), 0) AS jumlah,
                   COALESCE(sum(jumlahver), 0) AS jumver,
                   COALESCE(sum(jumlahreal), 0) AS jumreal,
                   COALESCE(sum(jumlahreal), 0) AS jumpin
            FROM proposal_ag
            WHERE noproposal = @noProposal
            """,
            new { noProposal });
    }

    private static string CurrentValue(BindingSource source, string field)
    {
        if (source.Current is not DataRowView row || !row.Row.Table.Columns.Contains(field) || row[field] is DBNull)
            return "";
        return row[field]?.ToString()?.Trim() ?? "";
    }

    private double CurrentNumber(BindingSource source, string field)
    {
        if (source.Current is not DataRowView row || !row.Row.Table.Columns.Contains(field) || row[field] is DBNull)
            return 0;
        return Convert.ToDouble(row[field], CultureInfo.InvariantCulture);
    }

    private void ShowError(Exception ex) =>
        MessageBox.Show(this, "Error Message : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void Info(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private bool ChangeProposalStatus(string newStatus, string successMessage, params string[] oldStatuses)
    {
        var noProposal = CurrentValue(bsProposal, "noproposal");
        var nama = CurrentValue(bsProposal, "nama");
        if (noProposal.Length == 0)
        {
            Info("Pilih proposal terlebih dahulu.");
            return false;
        }

        try
        {
            using var connection = Database.Open();
            using var transaction = connection.BeginTransaction();
            var affected = connection.Execute(
                "UPDATE proposal_klp SET status1 = @newStatus WHERE noproposal = @noProposal AND status1 IN @oldStatuses",
                new { newStatus, noProposal, oldStatuses }, transaction);
            if (affected != 1)
            {
                transaction.Rollback();
                Info("Status proposal telah berubah. Muat ulang daftar sebelum melanjutkan.");
                LoadProposalList();
                return false;
            }

            connection.Execute(
                "UPDATE proposal_ag SET status1 = @newStatus WHERE noproposal = @noProposal AND status1 IN @oldStatuses",
                new { newStatus, noProposal, oldStatuses }, transaction);
            transaction.Commit();

            LoadProposalList();
            LocateProposal(noProposal);
            Info($"Status Proposal No: {noProposal} a.n Kelompok: {nama}\n{successMessage}");
            return true;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return false;
        }
    }

    private void LocateProposal(string noProposal)
    {
        for (var index = 0; index < bsProposal.Count; index++)
        {
            if (bsProposal[index] is DataRowView row &&
                string.Equals(row["noproposal"]?.ToString()?.Trim(), noProposal, StringComparison.OrdinalIgnoreCase))
            {
                bsProposal.Position = index;
                return;
            }
        }
    }

    private static bool TryParseAmount(string text, out double value) =>
        double.TryParse(text.Replace(".", ""), NumberStyles.Number, Id, out value);

    private void FormatAmount(TextBox box)
    {
        if (_formattingAmount || !TryParseAmount(box.Text, out var value))
            return;

        var digitsAfterCaret = box.Text[Math.Min(box.SelectionStart, box.Text.Length)..].Count(char.IsDigit);
        var formatted = value.ToString("N0", Id);
        if (formatted == box.Text)
            return;

        _formattingAmount = true;
        try
        {
            box.Text = formatted;
            var caret = formatted.Length;
            for (var seen = 0; caret > 0 && seen < digitsAfterCaret; caret--)
                if (char.IsDigit(formatted[caret - 1]))
                    seen++;
            box.SelectionStart = caret;
        }
        finally
        {
            _formattingAmount = false;
        }
    }

    private void UpdateIncomeFigures()
    {
        if (_updatingIncomeFigures)
            return;

        _updatingIncomeFigures = true;
        try
        {
            FormatAmount(epkotor);
            FormatAmount(ebiaya);
            var gross = TryParseAmount(epkotor.Text, out var parsedGross) ? parsedGross : 0;
            var cost = TryParseAmount(ebiaya.Text, out var parsedCost) ? parsedCost : 0;
            epbersih.Text = (gross - cost).ToString("N0", Id);
            UpdateRpcFigures();
        }
        finally
        {
            _updatingIncomeFigures = false;
        }
    }

    private void UpdateRpcFigures()
    {
        FormatAmount(epbersih);
        var net = TryParseAmount(epbersih.Text, out var parsedNet) ? parsedNet : 0;
        var factor = double.TryParse(erpcpct.Text, NumberStyles.Number, Id, out var parsedFactor) ? parsedFactor : 0;
        var months = double.TryParse(ejw.Text, NumberStyles.Number, Id, out var parsedMonths) ? parsedMonths : 0;
        var rpc = Math.Round(net * factor / 100, MidpointRounding.ToEven);
        erpc.Text = rpc.ToString("N0", Id);
        eplafon.Text = (rpc * months).ToString("N0", Id);
    }

    private void CalculateInstallment(
        TextBox principalBox,
        TextBox monthsBox,
        TextBox rateBox,
        ComboBox methodBox,
        UPKPro.Controls.CaptionPanel principalTotal,
        UPKPro.Controls.CaptionPanel interestTotal,
        UPKPro.Controls.CaptionPanel principalPerInstallment,
        UPKPro.Controls.CaptionPanel interestPerInstallment,
        UPKPro.Controls.CaptionPanel installmentTotal)
    {
        FormatAmount(principalBox);
        if (!TryParseAmount(principalBox.Text, out var principal) ||
            !double.TryParse(monthsBox.Text, NumberStyles.Number, Id, out var months) ||
            !double.TryParse(rateBox.Text, NumberStyles.Number, Id, out var annualRate) ||
            months <= 0 || annualRate < 0)
        {
            ClearInstallmentFigures(principalTotal, interestTotal, principalPerInstallment, interestPerInstallment, installmentTotal);
            return;
        }

        var interval = methodBox.SelectedIndex switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            5 => 6,
            6 => 10,
            7 => 12,
            _ => 0,
        };
        if (interval == 0 || months % interval != 0)
        {
            ClearInstallmentFigures(principalTotal, interestTotal, principalPerInstallment, interestPerInstallment, installmentTotal);
            return;
        }

        var totalInterest = Math.Round(principal * (annualRate / 12 / 100) * months, MidpointRounding.ToEven);
        var installmentCount = months / interval;
        var principalEach = Math.Round(principal / installmentCount, MidpointRounding.ToEven);
        var interestEach = Math.Round(totalInterest / installmentCount, MidpointRounding.ToEven);
        principalTotal.Text = principal.ToString("N0", Id);
        interestTotal.Text = totalInterest.ToString("N0", Id);
        principalPerInstallment.Text = principalEach.ToString("N0", Id);
        interestPerInstallment.Text = interestEach.ToString("N0", Id);
        installmentTotal.Text = (principalEach + interestEach).ToString("N0", Id);
    }

    private static void ClearInstallmentFigures(params UPKPro.Controls.CaptionPanel[] panels)
    {
        foreach (var panel in panels)
            panel.Text = "0";
    }

    private static bool IsEnter(KeyPressEventArgs e)
    {
        if (e.KeyChar != '\r')
            return false;
        e.Handled = true;
        return true;
    }

    private void BitBtn10Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Berkas gambar|*.jpg;*.jpeg;*.png;*.bmp;*.gif|Semua berkas|*.*",
            InitialDirectory = AppContext.BaseDirectory,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            SetPhoto(dialog.FileName);
    }

    private void BitBtn11Click(object? sender, EventArgs e)
    {
        if (Image1.Image is null)
        {
            Info("Tidak ada foto yang dapat disimpan.");
            return;
        }

        using var dialog = new SaveFileDialog
        {
            FileName = CurrentValue(bsAnggotaProposal, "nik") + "_",
            Filter = "JPEG image|*.jpg|PNG image|*.png|Bitmap image|*.bmp",
            InitialDirectory = AppContext.BaseDirectory,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var format = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
        {
            ".png" => System.Drawing.Imaging.ImageFormat.Png,
            ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
            _ => System.Drawing.Imaging.ImageFormat.Jpeg,
        };
        Image1.Image.Save(dialog.FileName, format);
    }

    private void BitBtn12Click(object? sender, EventArgs e)
    {
        var directory = db1.SelectedNode?.FullPath;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            directory = AppContext.BaseDirectory;

        try
        {
            ListBox1.Items.Clear();
            ListBox1.Items.AddRange(Directory.GetFiles(directory, dbnikcari.Text.Trim() + "*.*"));
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void BitBtn13Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:860 (BitBtn13.OnClick)
    }

    private void BitBtn14Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:909 (BitBtn14.OnClick)
    }

    private void BitBtn15Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1033 (BitBtn15.OnClick)
    }

    private void BitBtn16Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1085 (BitBtn16.OnClick)
    }

    private void BitBtn17Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1139 (BitBtn17.OnClick)
    }

    private void BitBtn18Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1226 (BitBtn18.OnClick)
    }

    private void BitBtn19Click(object? sender, EventArgs e)
    {
        bteprop.Text = "Edit";
        panel10.Visible = false;
    }

    private void BitBtn1Click(object? sender, EventArgs e)
    {
        SetVerificationEditMode(false);
        dbdafpinjaman.Enabled = true;
        ememover.Clear();
        ec1.Text = ec2.Text = ec3.Text = ec4.Text = ec5.Text = "0";
        epkotor.Text = ebiaya.Text = epbersih.Text = erpcpct.Text = erpc.Text = "0";
        emetode.SelectedIndex = 0;
        ebungapct.Text = "18";
        ejw.Text = elama.Text = "12";
        eplafon.Text = epokok.Text = "0";
        ClearInstallmentFigures(epanelpokok, epanelbunga, epanelapokok, epanelabunga, epanelatotal);
    }

    private void BitBtn20Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1383 (BitBtn20.OnClick, BitBtn24.OnClick)
    }

    private void BitBtn21Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1396 (BitBtn21.OnClick)
    }

    private void BitBtn25Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1458 (BitBtn25.OnClick)
    }

    private void BitBtn26Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1676 (BitBtn26.OnClick)
    }

    private void BitBtn27Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1767 (BitBtn27.OnClick)
    }

    private void BitBtn28Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1817 (BitBtn28.OnClick)
    }

    private void BitBtn29Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1933 (BitBtn29.OnClick)
    }

    private void btedtClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:1983 (btedt.OnClick)
    }

    private void BitBtn2Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2013 (BitBtn2.OnClick, BitBtn23.OnClick)
    }

    private void BitBtn30Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2137 (BitBtn30.OnClick)
    }

    private void BitBtn31Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2188 (BitBtn31.OnClick)
    }

    private void BitBtn32Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2238 (BitBtn32.OnClick)
    }

    private void BitBtn3Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2291 (BitBtn3.OnClick)
    }

    private void BitBtn4Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2401 (BitBtn4.OnClick, BitBtn22.OnClick)
    }

    private void BitBtn5Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2520 (BitBtn5.OnClick)
    }

    private void BitBtn6Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2806 (BitBtn6.OnClick)
    }

    private void BitBtn7Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2859 (BitBtn7.OnClick)
    }

    private void BitBtn8Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:2864 (BitBtn8.OnClick)
    }

    private void BitBtn9Click(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:3001 (BitBtn9.OnClick)
    }

    private void btbatalrClick(object? sender, EventArgs e)
    {
        enopby.Text = "";
        enoanggota.Text = "";
        enama.Text = "";
        edesa.Text = "";
        ekodedesa.Text = "";
        ebungapct1r.Text = "18";
        elama1r.Text = "12";
        emetode1r.SelectedIndex = 0;
        epokokr.Text = "0";
        gbval.Enabled = false;
    }

    private void btsimpanrClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:3038 (btsimpanr.OnClick)
    }

    private void btrefreshrClick(object? sender, EventArgs e)
    {
        LoadRealizationQueue();
    }

    private void btnokpClick(object? sender, EventArgs e)
    {
        var villageCode = dbkodedesa.Text.Trim();
        if (villageCode.Length == 0)
        {
            Info("Kode desa belum tersedia untuk membuat nomor kelompok.");
            return;
        }

        try
        {
            var count = Database.QuerySingleOrDefault<int>(
                "SELECT count(*)::int FROM kelompok WHERE no_kelompok LIKE @prefix",
                new { prefix = villageCode + "%" });
            var next = count + 1;
            var suffix = next < 10 ? $".00{next}" : next < 100 ? $".0{next}" : $".{next}";
            var groupNumber = villageCode + suffix;
            var exists = Database.QuerySingleOrDefault<bool>(
                "SELECT EXISTS (SELECT 1 FROM kelompok WHERE no_kelompok = @groupNumber)",
                new { groupNumber });
            enoindukkp.Text = exists ? groupNumber + "1" : groupNumber;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void btnrcbatalClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:3527 (btnrcbatal.OnClick)
    }

    private void btnrcsaldoClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:3602 (btnrcsaldo.OnClick)
    }

    private void btpinjamanClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:3665 (btpinjaman.OnClick)
    }

    private void btubahverClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:3731 (btubahver.OnClick)
    }

    private void btrevisi1Click(object? sender, EventArgs e)
    {
        if (dbstatus2.Text.Trim() != "VERIFIKASI")
        {
            Info($"Status: {dbstatus2.Text}\nHanya proposal berstatus VERIFIKASI yang dapat dikirim kembali untuk verifikasi.");
            return;
        }

        ChangeProposalStatus(
            "RE_VERIFIKASI",
            "Status diubah menjadi RE_VERIFIKASI dan dikirim kembali ke Bagian Verifikasi Pinjaman.",
            "VERIFIKASI");
    }

    private void btrevisiClick(object? sender, EventArgs e)
    {
        ChangeProposalStatus(
            "REVISI",
            "Status diubah menjadi REVISI dan dikirim kembali ke Bagian Pengajuan Pinjaman.",
            "LENGKAP", "RE_VERIFIKASI");
    }

    private void SpeedButton2Click(object? sender, EventArgs e)
    {
        panel10.Visible = false;
    }

    private void btepropClick(object? sender, EventArgs e)
    {
        if (bteprop.Text == "Edit")
        {
            if (bsProposal.Current is not DataRowView row)
            {
                Info("Pilih proposal terlebih dahulu.");
                return;
            }

            ealamat.Text = FieldText(row, "alamat", "");
            eketua.Text = FieldText(row, "ketua", "");
            eaketua.Text = FieldText(row, "alamatketua", "");
            esek.Text = FieldText(row, "sekretaris", "");
            easek.Text = FieldText(row, "alamatsek", "");
            ebend.Text = FieldText(row, "bendahara", "");
            eabend.Text = FieldText(row, "alamatbend", "");
            ejumlah.Text = FieldText(row, "jumlah", "0");
            ebungapct.Text = FieldText(row, "bungapct", "0");
            elama.Text = FieldText(row, "lama", "12");
            emetode.SelectedIndex = int.TryParse(FieldText(row, "metode", "1"), out var method)
                ? Math.Clamp(method - 1, 0, emetode.Items.Count - 1)
                : 0;
            cbjenis.SelectedValue = FieldText(row, "jenispby", "");
            gbx.Enabled = true;
            foreach (var box in new[] { ealamat, eketua, eaketua, esek, easek, ebend, eabend, ejumlah })
                box.ReadOnly = false;
            bteprop.Text = "Simpan";
            panel10.Visible = true;
            Info("Perubahan pengajuan akan mengatur ulang nilai verifikasi/realisasi dan status menjadi LENGKAP.");
            return;
        }

        var noProposal = CurrentValue(bsProposal, "noproposal");
        if (noProposal.Length == 0 ||
            emetode.SelectedIndex < 0 ||
            !int.TryParse(elama.Text, NumberStyles.Integer, Id, out var term) ||
            term <= 0 ||
            !double.TryParse(ebungapct.Text, NumberStyles.Number, Id, out var rate) ||
            cbjenis.Text.Trim().Length == 0)
        {
            Info("Jenis pinjaman, bunga, jangka waktu, atau metode belum valid.");
            return;
        }

        var interval = emetode.SelectedIndex switch
        {
            0 => 1,
            1 => 2,
            2 => 3,
            3 => 4,
            4 => 5,
            5 => 6,
            6 => 10,
            _ => 0,
        };
        if (interval == 0 || term % interval != 0)
        {
            Info("Jangka waktu tidak sesuai dengan metode angsuran yang dipilih.");
            return;
        }

        try
        {
            var parameters = new
            {
                noProposal,
                rate,
                term,
                method = emetode.SelectedIndex + 1,
                jenis = cbjenis.Text.Trim(),
                alamat = ealamat.Text,
                ketua = eketua.Text,
                alamatKetua = eaketua.Text,
                sekretaris = esek.Text,
                alamatSek = easek.Text,
                bendahara = ebend.Text,
                alamatBend = eabend.Text,
                jumlah = ReadAmount(ejumlah),
            };
            using var connection = Database.Open();
            using var transaction = connection.BeginTransaction();
            var affected = connection.Execute(
                """
                UPDATE proposal_klp
                SET bungapct=@rate, lama=@term, metode=@method, jenispby=@jenis, alamat=@alamat,
                    ketua=@ketua, alamatketua=@alamatKetua, sekretaris=@sekretaris, alamatsek=@alamatSek,
                    bendahara=@bendahara, alamatbend=@alamatBend, jumlah=@jumlah, status1='LENGKAP'
                WHERE noproposal=@noProposal
                """,
                parameters,
                transaction);
            if (affected != 1)
                throw new InvalidOperationException("Proposal tidak ditemukan atau telah berubah.");

            connection.Execute(
                """
                UPDATE proposal_ag
                SET status1='LENGKAP', bungapct=@rate, lama=@term, metode=@method, jenispby=@jenis,
                    jumlahver=0, jumlahreal=0, pokokrp=0, pokokrp1=0, bungarp=0, bungarp1=0,
                    angspokok=0, angspokok1=0, angsbunga=0, angsbunga1=0
                WHERE noproposal=@noProposal
                """,
                parameters,
                transaction);
            transaction.Commit();

            foreach (var box in new[] { ealamat, eketua, eaketua, esek, easek, ebend, eabend, ejumlah })
                box.ReadOnly = true;
            gbx.Enabled = false;
            bteprop.Text = "Edit";
            panel10.Visible = false;
            LoadProposalList();
            LocateProposal(noProposal);
            Info("Perubahan properti pinjaman berhasil disimpan.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void btrealisasiClick(object? sender, EventArgs e)
    {
        if (CurrentNumber(bsJumlahAnggota, "jumreal") == 0)
        {
            Info("Verifikasi dari Tim Pemutus Pinjaman belum dilakukan. Transaksi dibatalkan.");
            return;
        }

        ChangeProposalStatus(
            "REALISASI",
            "Status diubah menjadi REALISASI dan dikirimkan ke Bagian Realisasi Pinjaman.",
            "VERIFIKASI", "RE_AKAD");
    }

    private void btverifikasiClick(object? sender, EventArgs e)
    {
        if (CurrentNumber(bsJumlahAnggota, "jumver") == 0)
        {
            Info("Verifikasi pinjaman anggota belum diposting. Transaksi dibatalkan.");
            return;
        }

        ChangeProposalStatus(
            "VERIFIKASI",
            "Status diubah menjadi VERIFIKASI dan dikirimkan ke Tim Pemutus Pinjaman.",
            "LENGKAP", "RE_VERIFIKASI");
    }

    private void cbkepaladusunClick(object? sender, EventArgs e)
    {
        cbnamadusun.Checked = cbkepaladusun.Checked;
    }

    private void cbnamadusunClick(object? sender, EventArgs e)
    {
        cbkepaladusun.Checked = cbnamadusun.Checked;
    }

    private void btsimpanxClick(object? sender, EventArgs e)
    {
        if (btsimpanx.Text == "Edit")
        {
            dbdafpinjaman.Enabled = false;
            gb5c.Enabled = true;
            gbrpc.Enabled = true;
            gbpinjaman.Enabled = true;
            ememover.ReadOnly = false;
            btsimpanx.Text = "Simpan";
            return;
        }

        var noProposal = CurrentValue(bsProposal, "noproposal");
        var nik = CurrentValue(bsAnggotaProposal, "nik");
        var status = CurrentValue(bsProposal, "status1");
        if (noProposal.Length == 0 || nik.Length == 0)
        {
            Info("Pilih anggota peminjam sebelum menyimpan hasil verifikasi.");
            return;
        }

        var isVerification = status is "LENGKAP" or "RE_VERIFIKASI";
        var isRealization = status is "VERIFIKASI" or "REALISASI" or "RE_AKAD";
        if (!isVerification && !isRealization)
        {
            Info($"Perubahan tidak dapat disimpan untuk status {status}.");
            return;
        }

        try
        {
            var method = emetode.SelectedIndex + 1;
            if (method <= 0 ||
                !double.TryParse(elama1.Text, NumberStyles.Number, Id, out var term) ||
                term <= 0 ||
                !double.TryParse(ebungapct1.Text, NumberStyles.Number, Id, out var annualRate))
                throw new InvalidOperationException("Jangka waktu, metode, atau bunga pinjaman tidak valid.");

            var amount = ReadAmount(epokok);
            var update = isVerification
                ? """
                  UPDATE proposal_ag SET c1=@c1, c2=@c2, c3=@c3, c4=@c4, c5=@c5,
                      pkotor=@gross, biaya=@cost, pbersih=@net, rpcpct=@rpcPct, rpc=@rpc,
                      plafon=@ceiling, jumlahver=@amount, memover=@memo, pokokrp=@amount,
                      bungapct=@rate, bungarp=@interest, lama=@term, angspokok=@principalInstallment,
                      angsbunga=@interestInstallment, metode=@method
                  WHERE nik=@nik AND noproposal=@noProposal
                  """
                : """
                  UPDATE proposal_ag SET c1=@c1, c2=@c2, c3=@c3, c4=@c4, c5=@c5,
                      pkotor=@gross, biaya=@cost, pbersih=@net, rpcpct=@rpcPct, rpc=@rpc,
                      plafon=@ceiling, jumlahreal=@amount, memover=@memo, pokokrp1=@amount,
                      bungapct=@rate, bungarp1=@interest, lama=@term, angspokok1=@principalInstallment,
                      angsbunga1=@interestInstallment, metode=@method
                  WHERE nik=@nik AND noproposal=@noProposal
                  """;

            using var connection = Database.Open();
            using var transaction = connection.BeginTransaction();
            var affected = connection.Execute(update, new
            {
                c1 = ec1.Text,
                c2 = ec2.Text,
                c3 = ec3.Text,
                c4 = ec4.Text,
                c5 = ec5.Text,
                gross = ReadAmount(epkotor),
                cost = ReadAmount(ebiaya),
                net = ReadAmount(epbersih),
                rpcPct = ReadAmount(erpcpct),
                rpc = ReadAmount(erpc),
                ceiling = ReadAmount(eplafon),
                amount,
                memo = ememover.Text,
                rate = Math.Round(annualRate / 12 * 100, MidpointRounding.ToEven),
                interest = ReadAmount(epanelbunga),
                term,
                principalInstallment = ReadAmount(epanelapokok),
                interestInstallment = ReadAmount(epanelabunga),
                method,
                nik,
                noProposal,
            }, transaction);
            if (affected != 1)
                throw new InvalidOperationException("Data anggota tidak ditemukan atau telah berubah. Muat ulang data sebelum menyimpan.");
            transaction.Commit();

            Requery(bsAnggotaProposal,
                "SELECT * FROM proposal_ag WHERE noproposal = @noProposal ORDER BY nik",
                new { noProposal });
            LoadProposalTotals();
            SetVerificationEditMode(false);
            panel15.Visible = false;
            Info("Rekomendasi dan perhitungan analisa pinjaman telah disimpan.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private static double ReadAmount(TextBox box)
    {
        if (!TryParseAmount(box.Text, out var amount))
            throw new InvalidOperationException($"Nilai pada {box.Name} tidak valid.");
        return amount;
    }

    private static double ReadAmount(UPKPro.Controls.CaptionPanel panel)
    {
        if (!TryParseAmount(panel.Text, out var amount))
            throw new InvalidOperationException($"Nilai pada {panel.Name} tidak valid.");
        return amount;
    }

    private void SetVerificationEditMode(bool editing)
    {
        dbdafpinjaman.Enabled = !editing;
        gb5c.Enabled = editing;
        gbrpc.Enabled = editing;
        gbpinjaman.Enabled = editing;
        ememover.ReadOnly = !editing;
        btsimpanx.Text = editing ? "Simpan" : "Edit";
    }

    private void dbbungapctChange(object? sender, EventArgs e)
    {
        Edit4.Text = dbbungapct.Text;
    }

    private void dbdafpinjamanDblClick(object? sender, EventArgs e)
    {
        if (bsAnggotaProposal.Current is not DataRowView row)
            return;

        if (CurrentValue(bsProposal, "status1") is "INPUT" or "REVISI")
        {
            Info($"Status Data: {CurrentValue(bsProposal, "status1")}\nPerubahan hanya bisa dilakukan oleh Bagian Pengajuan.");
            return;
        }

        if (CurrentValue(bsProposal, "status1") is "REALISASI" or "VERIFIKASI" or "RE_AKAD" &&
            !btrealisasi.Enabled)
        {
            Info("Perubahan hanya bisa dilakukan oleh Bagian Realisasi/Tim Pemutus Pinjaman.");
            return;
        }

        panel15.Visible = true;
        SetVerificationEditMode(false);
        ec1.Text = FieldText(row, "c1", "0");
        ec2.Text = FieldText(row, "c2", "0");
        ec3.Text = FieldText(row, "c3", "0");
        ec4.Text = FieldText(row, "c4", "0");
        ec5.Text = FieldText(row, "c5", "0");
        epkotor.Text = FieldText(row, "pkotor", "0");
        ebiaya.Text = FieldText(row, "biaya", "0");
        epbersih.Text = FieldText(row, "pbersih", "0");
        erpcpct.Text = FieldText(row, "rpcpct", "0");
        erpc.Text = FieldText(row, "rpc", "0");
        eplafon.Text = FieldText(row, "plafon", "0");
        ebungapct1.Text = dbbungapct.Text;
        elama1.Text = dblama.Text;
        ejw.Text = dblama.Text;
        emetode1.SelectedIndex = int.TryParse(dbmetode.Text, out var method) ? method - 1 : -1;
        var amountField = CurrentValue(bsProposal, "status1") is "VERIFIKASI" or "REALISASI" or "RE_AKAD"
            ? "jumlahreal"
            : "jumlahver";
        epokok.Text = FieldText(row, amountField, "0");
        ememover.Text = FieldText(row, "memover", "");
    }

    private static string FieldText(DataRowView row, string field, string fallback)
    {
        if (!row.Row.Table.Columns.Contains(field) || row[field] is DBNull)
            return fallback;
        return FormatValue(row[field]);
    }

    private void DBEdit21Change(object? sender, EventArgs e)
    {
        if (!_refreshingFields)
            LoadCancellationDetails();
    }

    private void DBEdit23Change(object? sender, EventArgs e)
    {
        // The Delphi handler contained only disabled report code.
    }

    private void DBEdit32Change(object? sender, EventArgs e)
    {
        if (_refreshingFields || DBEdit32.Text.Trim().Length == 0)
            return;
        LoadMemberLoans();
    }

    private void DBEdit34Change(object? sender, EventArgs e)
    {
        if (_refreshingFields || DBEdit34.Text.Trim().Length == 0)
            return;
        LoadGroupLoans();
    }

    private void DBGrid1CellClick(object? sender, DataGridViewCellEventArgs e)
    {
    }

    private void DBGrid3CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;
        var noPembiayaan = CurrentValue(bsRiwayatPembiayaan, "no_pembiayaan");
        if (noPembiayaan.Length > 0)
            Requery(bsPembiayaanAg,
                "SELECT * FROM pembiayaan_ag WHERE no_pembiayaan = @noPembiayaan ORDER BY nik",
                new { noPembiayaan });
    }

    private void DBGrid4CellClick(object? sender, DataGridViewCellEventArgs e)
    {
    }

    private void DBGrid5CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        var desa = CurrentValue(bsPinjamanDesa, "desa");
        if (desa.Length == 0)
            return;

        if (rgsbpinjaman.ItemIndex == 0)
        {
            Requery(bsPinjamanDesaDetail,
                "SELECT * FROM pembiayaan WHERE status = @status AND desa = @desa ORDER BY tglkredit",
                new { status = "BL", desa });
        }
        else if (int.TryParse(etahun.Text, out var year))
        {
            Requery(bsPinjamanDesaDetail,
                "SELECT * FROM pembiayaan WHERE tahun = @year AND desa = @desa ORDER BY tglkredit",
                new { year, desa });
        }
    }

    private void DBGrid9CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;
        var status = CurrentValue(bsJumlahProposal, "status1");
        if (status.Length > 0)
            Requery(bsProposal,
                "SELECT * FROM proposal_klp WHERE status1 = @status ORDER BY tglmasuk",
                new { status });
    }

    private void dbnamadesaClick(object? sender, EventArgs e)
    {
        UpdateLoanDistribution();
    }

    private void dbnik1Change(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:4599 (dbnik1.OnChange)
    }

    private void dbbungarChange(object? sender, EventArgs e)
    {
        ebungar.Text = dbbungar.Text;
    }

    private void ebiayaChange(object? sender, EventArgs e)
    {
        UpdateIncomeFigures();
    }

    private void ebiayaKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            erpcpct.Focus();
    }

    private void ebungapctChange(object? sender, EventArgs e)
    {
        // The Delphi handler intentionally left this field's change callback inert.
    }

    private void ebungapctKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            elama.Focus();
    }

    private void ec1KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            ec2.Focus();
    }

    private void ec2KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            ec3.Focus();
    }

    private void ec3KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            ec4.Focus();
    }

    private void ec4KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            ec5.Focus();
    }

    private void ec5KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            epkotor.Focus();
    }

    private void ecari1Change(object? sender, EventArgs e)
    {
        if (ecari1.Text.Length == 0)
            return;

        var (column, order) = rgpilih1.ItemIndex switch
        {
            0 => ("lower(nik)", "nik"),
            1 => ("lower(nama)", "nama"),
            _ => (null, null),
        };
        if (column is null)
            return;

        Requery(bsAnggota,
            $"SELECT * FROM anggota WHERE {column} LIKE @value ORDER BY {order}",
            new { value = ecari1.Text.Trim().ToLowerInvariant() + "%" });
    }

    private void ecariChange(object? sender, EventArgs e)
    {
        if (ecari.Text.Length == 0)
            return;

        var (column, order) = rgcari.ItemIndex switch
        {
            0 => ("lower(no_kelompok)", "no_kelompok"),
            1 => ("lower(nama)", "nama"),
            _ => (null, null),
        };
        if (column is null)
            return;

        Requery(bsKelompok,
            $"SELECT * FROM kelompok WHERE {column} LIKE @value ORDER BY {order}",
            new { value = ecari.Text.Trim().ToLowerInvariant() + "%" });
    }

    private void ecaridafChange(object? sender, EventArgs e)
    {
        if (rgdaf2.ItemIndex is < 0 or > 2)
            return;

        var (column, value) = rgdaf2.ItemIndex switch
        {
            0 => ("noproposal", ecaridaf.Text.Trim()),
            1 => ("lower(nama)", ecaridaf.Text.Trim().ToLowerInvariant()),
            _ => ("lower(desa)", ecaridaf.Text.Trim().ToLowerInvariant()),
        };
        Requery(bsProposal,
            $"SELECT * FROM proposal_klp WHERE {column} LIKE @value ORDER BY tglmasuk",
            new { value = value + "%" });
        LoadProposalTotals();
        Requery(bsJumlahProposal,
            "SELECT status1, count(status1) AS jumlah FROM proposal_klp WHERE status1 <> @status GROUP BY status1 ORDER BY status1",
            new { status = "TERIMA" });
    }

    private void ecaripby2xChange(object? sender, EventArgs e)
    {
        var criteria = ecaripby1x.Text switch
        {
            "Nama" => ("lower(nama)", "nama"),
            "No Kelompok" => ("lower(no_anggota)", "no_anggota"),
            _ => (null, null),
        };
        if (criteria.Item1 is null)
            return;

        Requery(bsPembiayaanBatal,
            $"SELECT * FROM pembiayaan WHERE status = @status AND {criteria.Item1} LIKE @value ORDER BY tanggal",
            new { status = "BL", value = ecaripby2x.Text.Trim().ToLowerInvariant() + "%" });
    }

    private void ecaripbyxChange(object? sender, EventArgs e)
    {
        ecaripby1x.Text = ecaripbyx.Text switch
        {
            "1" => "Nama",
            "2" => "No Kelompok",
            _ => "",
        };
    }

    private void ejumlahChange(object? sender, EventArgs e)
    {
        FormatAmount(ejumlah);
    }

    private void elama1rChange(object? sender, EventArgs e)
    {
        epokokrChange(sender, e);
        etgljttempo.Value = MaturityDate(etglkredit.Value, elama1r.Text, etgljttempo.Value);
    }

    private void elamaKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            emetode.Focus();
    }

    private void enoindukkp1Change(object? sender, EventArgs e)
    {
        enoindukkp.Text = enoindukkp1.Text;
    }

    private void epbersihChange(object? sender, EventArgs e)
    {
        if (!_updatingIncomeFigures)
            UpdateRpcFigures();
    }

    private void epbersihKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            erpcpct.Focus();
    }

    private void epkotorChange(object? sender, EventArgs e)
    {
        UpdateIncomeFigures();
    }

    private void epkotorKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            ebiaya.Focus();
    }

    private void epokokChange(object? sender, EventArgs e)
    {
        CalculateInstallment(epokok, elama1, ebungapct1, emetode1,
            epanelpokok, epanelbunga, epanelapokok, epanelabunga, epanelatotal);
    }

    private void epokokrChange(object? sender, EventArgs e)
    {
        CalculateInstallment(epokokr, elama1r, ebungapct1r, emetode1r,
            epanelpokokr, epanelbungar, epanelapokokr, epanelabungar, epanelatotalr);
    }

    private void epokokxChange(object? sender, EventArgs e)
    {
        CalculateInstallment(epokokx, elamax, ebungapctbatal, emetodebatal,
            epanelpokokx, epanelbungax, epanelapokokx, epanelabungax, epanelatotalx);
    }

    private void erpcChange(object? sender, EventArgs e)
    {
        FormatAmount(erpc);
    }

    private void erpcpctChange(object? sender, EventArgs e)
    {
        UpdateRpcFigures();
    }

    private void erpcpctKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (IsEnter(e))
            epokok.Focus();
    }

    private void etglkreditChange(object? sender, EventArgs e)
    {
        etgljttempo.Value = MaturityDate(etglkredit.Value, elama1r.Text, etgljttempo.Value);
    }

    private void FormClose(object? sender, FormClosedEventArgs e)
    {
        bsProposal.Dispose();
        bsAnggotaProposal.Dispose();
        bsJumlahProposal.Dispose();
        bsJumlahAnggota.Dispose();
        bsKelompok.Dispose();
        bsAnggota.Dispose();
        bsRiwayatPembiayaan.Dispose();
        bsPembiayaanAg.Dispose();
        bsPembiayaanBatal.Dispose();
        bsPinjamanDesa.Dispose();
        bsPinjamanDesaDetail.Dispose();
        bsAkad.Dispose();
        bsAkadAnggota.Dispose();
        bsJenisPby.Dispose();
        Image1.Image?.Dispose();
        Image1.Image = null;
    }

    private void FormShow(object? sender, EventArgs e)
    {
        spc1.InitialDirectory = AppContext.BaseDirectory;
        try
        {
            bsJenisPby.DataSource = Database.QueryTable("SELECT kode, namapby FROM jenispby ORDER BY kode");
            cbjenis.DisplayMember = "kode";
            cbjenis.ValueMember = "kode";
            cbjenis.DataSource = bsJenisPby;
            cbjenis.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ListBox1Click(object? sender, EventArgs e)
    {
        if (ListBox1.SelectedItem is string path && File.Exists(path))
            SetPhoto(path);
    }

    private void SetPhoto(string path)
    {
        try
        {
            using var source = Image.FromFile(path);
            var image = new Bitmap(source);
            var previous = Image1.Image;
            Image1.Image = image;
            previous?.Dispose();
            Panel20.Text = path;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void noprop2Change(object? sender, EventArgs e)
    {
        if (!_refreshingFields)
            LoadAnggotaProposal();
    }

    private void noprop3Change(object? sender, EventArgs e)
    {
        if (_refreshingFields || noprop3.Text.Trim().Length == 0)
            return;

        LoadAnggotaProposal();
    }

    private void rgsbpinjamanClick(object? sender, EventArgs e)
    {
        UpdateLoanDistribution();
    }

    private void SpeedButton3Click(object? sender, EventArgs e)
    {
        BitBtn1Click(sender, e);
        panel15.Visible = false;
    }

    private void bthitungClick(object? sender, EventArgs e)
    {
        if (bsProposal.Current is not DataRowView proposal)
        {
            Info("Pilih proposal yang akan direalisasikan.");
            return;
        }

        btbatalrClick(sender, e);
        enopby.Text = FieldText(proposal, "no_pembiayaan", "");
        enoanggota.Text = FieldText(proposal, "no_kelompok", "");
        enama.Text = FieldText(proposal, "nama", "");
        edesa.Text = FieldText(proposal, "desa", "");
        ekodedesa.Text = FieldText(proposal, "kodedesa", "");
        ebungapct1r.Text = FieldText(proposal, "bungapct", "18");
        elama1r.Text = FieldText(proposal, "lama", "12");
        emetode1r.SelectedIndex = int.TryParse(FieldText(proposal, "metode", "1"), out var method)
            ? Math.Clamp(method - 1, 0, emetode1r.Items.Count - 1)
            : 0;
        epokokr.Text = FieldText(proposal, "pokokrp", CurrentValue(bsJumlahAnggota, "jumpin"));
        gbval.Enabled = true;
        etglrealisasi.Value = DateTime.Today;
        etglkredit.Value = DateTime.Today;
        etgljttempo.Value = MaturityDate(etglkredit.Value, elama1r.Text, etgljttempo.Value);
    }

    private void btjadwalClick(object? sender, EventArgs e)
    {
        // TODO: port dari unit_verifikasi.pas:5880 (btjadwal.OnClick)
    }

    private void SpeedButton9Click(object? sender, EventArgs e)
    {
        panelkuitansi.Visible = false;
    }

    private void tabrealisasiShow(object? sender, EventArgs e)
    {
        btrefreshrClick(sender, e);
        etglrealisasi.Value = DateTime.Today;
        etglkredit.Value = DateTime.Today;
    }

    private void TabSheet5Show(object? sender, EventArgs e)
    {
        Requery(bsKelompok, "SELECT * FROM kelompok ORDER BY no_kelompok LIMIT 25");
    }

    private void TabSheet6Show(object? sender, EventArgs e)
    {
        LoadMemberLoans();
    }

    private void TabSheet8Show(object? sender, EventArgs e)
    {
        etahun.Text = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
        try
        {
            dbnamadesa.DataSource = Database.QueryTable("SELECT DISTINCT desa FROM pembiayaan ORDER BY desa");
            dbnamadesa.DisplayMember = "desa";
            dbnamadesa.ValueMember = "desa";
            dbnamadesa.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void tabakadanggotaShow(object? sender, EventArgs e)
    {
        Requery(bsAkadAnggota, "SELECT * FROM akadanggota");
    }

    private void tabverifikasiShow(object? sender, EventArgs e)
    {
        LoadProposalList();
    }

    private void UpDown7Click(object? sender, EventArgs e)
    {
        rgsbpinjaman.ItemIndex = 1;
        UpdateLoanDistribution();
    }

    private void tabakadShow(object? sender, EventArgs e)
    {
        Requery(bsAkad, "SELECT * FROM akad");
    }

    private void tabbatalShow(object? sender, EventArgs e)
    {
        Requery(bsPembiayaanBatal,
            "SELECT * FROM pembiayaan WHERE status = @status ORDER BY tanggal",
            new { status = "BL" });
    }
}
