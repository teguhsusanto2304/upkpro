using System.ComponentModel;
using System.Data;
using System.Globalization;
using UPKPro.Data;

namespace UPKPro.Forms;

/// <summary>
/// Pengganti dataset + TDataSource dari data module Delphi (Unit_mdproposal / unit_mdutama) untuk FProposal.
/// Setiap BindingSource mewakili satu TADOQuery; grid dan "DBEdit" (TextBox) diikat ke sini sehingga
/// perpindahan baris memicu TextChanged seperti OnChange pada TDBEdit.
/// </summary>
public partial class FProposal
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    // mdproposal
    private readonly BindingSource bsCekKlp = new();          // qcekklp
    private readonly BindingSource bsCekPinjaman = new();     // qcekpinjaman
    private readonly BindingSource bsDafAnggota = new();      // qdafanggota
    private readonly BindingSource bsDafProposal = new();     // qdafproposal
    private readonly BindingSource bsJumProp = new();         // qjumprop
    private readonly BindingSource bsDafPinjamanAg = new();   // qdafpinjaman_ag
    private readonly BindingSource bsSumPinjam = new();       // qsumpinjam
    // mdutama
    private readonly BindingSource bsPembiayaanAg = new();    // qpembiayaan_ag
    private readonly BindingSource bsDesa = new();            // tbdesa
    private readonly BindingSource bsJenisPby = new();        // tbjenispby
    private readonly BindingSource bsKelompok = new();        // tbkelompok

    private readonly Dictionary<BindingSource, List<(TextBox Box, string Field, string? Format)>> _fieldBindings = [];

    private void SetupBindings()
    {
        BindGrid(bsCekKlp, DBGrid5);
        BindGrid(bsCekPinjaman, DBGrid7);
        BindGrid(bsDafAnggota, DBGrid3, DBGrid1);
        BindGrid(bsJumProp, DBGrid8, DBGrid12, DBGrid13);
        BindGrid(bsDafProposal, DBGrid14, DBGrid6, DBGrid4);
        BindGrid(bsDafPinjamanAg, DBGrid2, DBGrid9);
        BindGrid(bsPembiayaanAg, DBGrid10, DBGrid11);

        // dsqdafproposal
        BindField(noprop1, bsDafProposal, "noproposal");
        BindField(dbnamakp, bsDafProposal, "nama");
        BindField(DBEdit4, bsDafProposal, "ketua");
        BindField(DBEdit5, bsDafProposal, "desa");
        BindField(DBEdit6, bsDafProposal, "jumlah");
        BindField(dbstatus1, bsDafProposal, "status1");
        BindField(DBEdit8, bsDafProposal, "alamat");
        BindField(dbjenispby, bsDafProposal, "jenispby");
        BindField(DBEdit2, bsDafProposal, "alamat");
        BindField(DBEdit3, bsDafProposal, "ketua");
        BindField(DBEdit9, bsDafProposal, "desa");
        BindField(dbnamakp2, bsDafProposal, "nama");
        BindField(noprop2, bsDafProposal, "noproposal");
        BindField(DBEdit12, bsDafProposal, "jumlah");
        BindField(dbstatus2, bsDafProposal, "status1");
        // dsqdafpinjaman_ag
        BindField(dbnik2, bsDafPinjamanAg, "nik");
        BindField(DBEdit11, bsDafPinjamanAg, "nama");
        BindField(DBEdit7, bsDafPinjamanAg, "nama");
        BindField(dbnik1, bsDafPinjamanAg, "nik");
        // dsqsumpinjam
        BindField(DBEdit13, bsSumPinjam, "jumlah");
        BindField(DBEdit10, bsSumPinjam, "jumlah");
        // mdutama.dstbdesa
        BindField(enamakeckp, bsDesa, "namakec");
        BindField(enamakabkp, bsDesa, "namakab");
        BindField(enamapropkp, bsDesa, "namaprop");
        BindField(ekec, bsDesa, "namakec");
        BindField(ekab, bsDesa, "namakab");
        BindField(eprop, bsDesa, "namaprop");
        // mdutama.dstbjenispby
        BindField(DBEdit1, bsJenisPby, "namapby");
        BindField(ebungapctxx, bsJenisPby, "bunga", "0.##");
        // mdutama.dstbkelompok
        BindField(ekodekp1, bsKelompok, "no_kelompok");
    }

    /// <summary>Isi tabel lookup yang di Delphi sudah dibuka oleh mdutama saat aplikasi mulai.</summary>
    private void LoadLookups()
    {
        Requery(bsDesa, "SELECT * FROM dafdesa ORDER BY kodedesa");
        Requery(bsJenisPby, "SELECT * FROM jenispby");
        Requery(bsKelompok, "SELECT * FROM kelompok");

        BindLookup(enamadesacek1, bsDesa, "namadesa");
        BindLookup(ekodedesax, bsDesa, "namadesa");
        BindLookup(ekodedesa1x, bsDesa, "namadesa");
        BindLookup(cbjenispby, bsJenisPby, "kode");
        BindLookup(enamakp1, bsKelompok, "nama");
    }

    private static void BindLookup(ComboBox combo, BindingSource bs, string displayMember)
    {
        if (combo.DataSource is null)
        {
            combo.DisplayMember = displayMember;
            combo.DataSource = bs;
        }
        // TDBLookupComboBox tanpa DataSource tampil kosong sampai user memilih.
        combo.SelectedIndex = -1;
    }

    private void BindGrid(BindingSource bs, params DataGridView[] grids)
    {
        foreach (var grid in grids)
        {
            grid.DataSource = bs;
            foreach (DataGridViewColumn col in grid.Columns)
            {
                switch (col.DataPropertyName)
                {
                    case "tglmasuk" or "tglkredit" or "tgljatuhtempo" or "tgl_lhr":
                        col.DefaultCellStyle.Format = "dd/MM/yyyy";
                        break;
                    case "jumlah" or "jumlahlama" or "jumlahbaru" or "pokokrp" or "saldopokok_bunga":
                        col.DefaultCellStyle.Format = "#,0.##";
                        col.DefaultCellStyle.FormatProvider = Id;
                        col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                        break;
                }
            }
        }
    }

    /// <summary>Padanan TDBEdit (DataSource + DataField). Hanya baca: perubahan disimpan lewat query eksplisit.</summary>
    private void BindField(TextBox box, BindingSource bs, string field, string? format = null)
    {
        if (!_fieldBindings.TryGetValue(bs, out var list))
        {
            list = [];
            _fieldBindings[bs] = list;
            bs.CurrentChanged += (_, _) => RefreshFields(bs);
            bs.ListChanged += (_, e) =>
            {
                if (e.ListChangedType == ListChangedType.Reset) RefreshFields(bs);
            };
        }
        list.Add((box, field, format));
        box.ReadOnly = true;
    }

    private void RefreshFields(BindingSource bs)
    {
        var row = Current(bs);
        foreach (var (box, field, format) in _fieldBindings[bs])
            box.Text = Str(row, field, format);
    }

    /// <summary>Padanan Close + SQL.Add + Parameters + Open pada TADOQuery.</summary>
    private bool Requery(BindingSource bs, string sql, object? param = null, bool quiet = false)
    {
        try
        {
            bs.DataSource = Database.QueryTable(sql, param);
            return true;
        }
        catch (Exception ex)
        {
            if (!quiet) ShowError(ex);
            return false;
        }
    }

    /// <summary>Padanan TADOQuery.Close.</summary>
    private static void CloseQuery(BindingSource bs) => bs.DataSource = null;

    private static bool IsEmpty(BindingSource bs) => Current(bs) is null;

    private static DataRowView? Current(BindingSource bs) =>
        bs.DataSource is null || bs.Count == 0 ? null : bs.Current as DataRowView;

    private static DataRowView? Selected(ComboBox combo) => combo.SelectedItem as DataRowView;

    /// <summary>Nilai field sebagai teks (null -> ""), tanggal dd/MM/yyyy, angka gaya Indonesia.</summary>
    private static string Str(DataRowView? row, string field, string? format = null)
    {
        if (row is null || !row.Row.Table.Columns.Contains(field)) return "";
        return row[field] switch
        {
            null or DBNull => "",
            DateTime d => d.ToString(format ?? "dd/MM/yyyy", Id),
            DateOnly d => d.ToString(format ?? "dd/MM/yyyy", Id),
            double or float or decimal or int or long or short => ((IFormattable)row[field]).ToString(format ?? "#,0.##", Id),
            var v => v.ToString()?.Trim() ?? "",
        };
    }

    private static double Num(DataRowView? row, string field)
    {
        if (row is null || !row.Row.Table.Columns.Contains(field)) return 0;
        return row[field] is null or DBNull ? 0 : Convert.ToDouble(row[field], CultureInfo.InvariantCulture);
    }

    /// <summary>Padanan TADOQuery.Locate(field, value, []).</summary>
    private static void Locate(BindingSource bs, string field, string value)
    {
        for (var i = 0; i < bs.Count; i++)
        {
            if (bs[i] is DataRowView r && Str(r, field) == value.Trim())
            {
                bs.Position = i;
                return;
            }
        }
    }

    // ---------------------------------------------------------------- format angka (Unit_proposal.pas:420-455)

    /// <summary>Padanan formatRupiah: format ulang isi TextBox dengan pemisah ribuan titik.</summary>
    private static void FormatRupiah(TextBox box)
    {
        var text = box.Text;
        var digitsAfterCaret = text[Math.Min(box.SelectionStart, text.Length)..].Count(char.IsDigit);

        var value = double.TryParse(ClearDot(text), NumberStyles.Number, Id, out var v) ? v : 0;
        var formatted = value.ToString("N0", Id);
        if (formatted == text) return;
        box.Text = formatted;

        // Kembalikan caret ke posisi digit yang sama dihitung dari kanan.
        var caret = formatted.Length;
        for (var seen = 0; caret > 0 && seen < digitsAfterCaret; caret--)
            if (char.IsDigit(formatted[caret - 1])) seen++;
        box.SelectionStart = caret;
    }

    private static string ClearDot(string s) => s.Replace(".", "");

    private static string ClearKoma(string s) => s.Replace(",", ".");

    /// <summary>Teks berformat rupiah ("1.500.000") ke angka; padanan StrToCurr(cleardot(..)).</summary>
    private static bool TryParseRupiah(string text, out double value) =>
        double.TryParse(ClearDot(text), NumberStyles.Number, Id, out value);

    private static string Rupiah(double value) => value.ToString("N0", Id);

    // ---------------------------------------------------------------- dialog

    private void Info(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void Warn(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private bool Confirm(string message, bool defaultCancel = false) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            defaultCancel ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1) == DialogResult.Yes;

    private void ShowError(Exception ex, string suffix = "") =>
        MessageBox.Show(this, "Error Message : " + ex.Message + (suffix.Length > 0 ? "\n" + suffix : ""),
            Text, MessageBoxButtons.OK, MessageBoxIcon.Error);

    /// <summary>Padanan "if not (key=chr(13)) then exit": true bila Enter (dan menahan bunyi beep).</summary>
    private static bool IsEnter(KeyPressEventArgs e)
    {
        if (e.KeyChar != '\r') return false;
        e.Handled = true;
        return true;
    }
}
