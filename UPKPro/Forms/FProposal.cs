using System.Globalization;
using UPKPro.Data;

namespace UPKPro.Forms;

/// <summary>
/// Pengajuan Proposal Pinjaman
/// Port dari Unit_proposal.pas (TFProposal). Dataset/datasource Delphi ada di FProposal.Data.cs,
/// query tulis ada di Data/ProposalRepository.cs.
/// </summary>
public partial class FProposal : Form
{
    // Caption Panel9 dipakai Delphi sebagai penanda mode isian anggota.
    private const string ModeAnggotaBaru = "Isian Data Baru Anggota Peminjam";
    private const string ModeUbahAnggotaLama = "Ubah Data Anggota Lama";
    private const string ModeUbahAnggotaBaru = "Ubah Data Anggota Baru";
    private const string ModePilihAnggotaLama = "Pilih Data Anggota Lama";

    private const string SqlDafPinjamanAg = "SELECT * FROM proposal_ag WHERE noproposal = @x";
    private const string SqlSumPinjam =
        "SELECT sum(jumlahbaru) AS jumlah, sum(jumlahver) AS jumver, sum(jumlahreal) AS jumreal FROM proposal_ag WHERE noproposal = @x";
    private const string SqlJumPropAktif =
        "SELECT status1, count(status1) AS jumlah FROM proposal_klp WHERE status1 <> @x1 GROUP BY status1 ORDER BY status1";
    private const string SqlAnggotaKelompok = "SELECT * FROM anggota WHERE kodekelompok = @x1 ORDER BY nama";

    // Variabel global unit Delphi: sz (NIK lama yang diubah), ceknop1 / ceknop1x (NIK / KK awal anggota baru).
    private string sz = "", ceknop1 = "", ceknop1x = "";

    public FProposal()
    {
        InitializeComponent();
        SetupBindings();
        ApplyDelphiDefaults();
        BuildPetunjuk();
    }

    // ================================================================ helper query

    private void LoadDafPinjamanAg(string noProposal, bool orderByNik = false, bool quiet = false) =>
        Requery(bsDafPinjamanAg, SqlDafPinjamanAg + (orderByNik ? " ORDER BY nik" : ""), new { x = noProposal.Trim() }, quiet);

    private void LoadSumPinjam(string noProposal, bool quiet = false) =>
        Requery(bsSumPinjam, SqlSumPinjam, new { x = noProposal.Trim() }, quiet);

    private void LoadJumPropAktif() =>
        Requery(bsJumProp, SqlJumPropAktif, new { x1 = ProposalRepository.StatusTerima });

    /// <summary>Daftar proposal status INPUT/REVISI.</summary>
    private void LoadDafProposalInputRevisi(bool orderByTgl = true) =>
        Requery(bsDafProposal,
            "SELECT * FROM proposal_klp WHERE (status1 = @x1 OR status1 = @x2)" + (orderByTgl ? " ORDER BY tglmasuk" : ""),
            new { x1 = ProposalRepository.StatusInput, x2 = ProposalRepository.StatusRevisi });

    /// <summary>Anggota dari kelompok proposal yang sedang dipilih (qdafproposalno_kelompok).</summary>
    private void LoadAnggotaKelompokProposal(bool quiet = false) =>
        Requery(bsDafAnggota, SqlAnggotaKelompok, new { x1 = Str(Current(bsDafProposal), "no_kelompok") }, quiet);

    /// <summary>Pencarian database anggota (BitBtn6 / BitBtn17).</summary>
    private void CariAnggota(int index, string text)
    {
        var column = index switch
        {
            0 => "nik",
            1 => "lower(nama)",
            2 => "lower(namakelompok)",
            3 => "lower(desa)",
            _ => null,
        };
        if (column is null) return;
        var value = index == 0 ? text.Trim() : text.Trim().ToLower();
        Requery(bsDafAnggota, $"SELECT * FROM anggota WHERE {column} LIKE @x1 ORDER BY nama", new { x1 = value + "%" });
    }

    /// <summary>Cari kelompok yang mirip nama + desa isian (qcekklp).</summary>
    private void CekKelompok()
    {
        Requery(bsCekKlp, "SELECT * FROM kelompok WHERE lower(nama) LIKE @x1 AND lower(desa) LIKE @x2", new
        {
            x1 = enamakp.Text.Trim().ToLower() + "%",
            x2 = enamadesakp.Text.Trim().ToLower() + "%",
        });
    }

    /// <summary>Cari pinjaman belum lunas dengan nama kelompok + desa mirip (qcekpinjaman).</summary>
    private void CekPinjaman()
    {
        Requery(bsCekPinjaman, "SELECT * FROM pembiayaan WHERE upper(nama) LIKE @x1 AND upper(desa) LIKE @x2 AND status = @x3", new
        {
            x1 = enamakp.Text.Trim().ToUpper() + "%",
            x2 = enamadesakp.Text.Trim().ToUpper() + "%",
            x3 = "BL",
        });
    }

    private void LoadPembiayaanAg(string nik) =>
        Requery(bsPembiayaanAg, "SELECT * FROM pembiayaan_ag WHERE nik = @x ORDER BY status", new { x = nik }, quiet: true);

    // ================================================================ helper form anggota

    /// <summary>Isi form anggota dari baris anggota / proposal_ag (BitBtn1, 5, 12, 14).</summary>
    private void FillAnggotaForm(System.Data.DataRowView row)
    {
        enama.Text = Str(row, "nama");
        cbsex.Text = Str(row, "sex");
        etempatlhr.Text = Str(row, "tempat_lhr");
        etgllhr.Text = Str(row, "tgl_lhr");
        ealamat.Text = Str(row, "alamat");
        etelp.Text = Str(row, "telp");
        edesa.Text = Str(row, "desa");
        ekec.Text = Str(row, "kecamatan");
        ekab.Text = Str(row, "kabupaten");
        eprop.Text = Str(row, "propinsi");

        ekodekp.Text = Str(row, "kodekelompok");
        enamakp2.Text = Str(row, "namakelompok");
        enik.Text = Str(row, "nik");
        enokk.Text = Str(row, "nokk");
        ekategori.Text = Str(row, "kategori");
        epekerjaan.Text = Str(row, "pekerjaan");
        eproduk.Text = Str(row, "produk");
        egakin.Text = Str(row, "gakin");
        egol.Text = Str(row, "goldarah");
        enama1.Text = Str(row, "nama1");
        enik1.Text = Str(row, "nik1");
        ealamat1.Text = Str(row, "alamat1");
        etelp1.Text = Str(row, "telp1");

        edesa.BringToFront();
        enamakp2.BringToFront();
    }

    /// <summary>Baca form anggota; null bila tanggal lahir tidak valid (Delphi: strtodate gagal -> exception).</summary>
    private ProposalAnggota? ReadAnggotaForm()
    {
        if (!DateTime.TryParseExact(etgllhr.Text, "dd/MM/yyyy", Id, DateTimeStyles.None, out var tglLhr))
        {
            Info("Tanggal lahir tidak valid (format dd/mm/yyyy).");
            etgllhr.Focus();
            return null;
        }

        return new ProposalAnggota(
            TglMasuk: etglmasuk.Value,
            Nama: enama.Text,
            Sex: cbsex.Text,
            TempatLhr: etempatlhr.Text,
            TglLhr: tglLhr,
            Alamat: ealamat.Text,
            Telp: etelp.Text,
            Desa: edesa.Text,
            Kecamatan: ekec.Text,
            Kabupaten: ekab.Text,
            Propinsi: eprop.Text,
            KodeKelompok: ekodekp.Text,
            NamaKelompok: enamakp2.Text,
            Nik: enik.Text,
            NoKk: enokk.Text,
            Kategori: ekategori.Text,
            Pekerjaan: epekerjaan.Text,
            Produk: eproduk.Text,
            Gakin: egakin.Text,
            GolDarah: egol.Text,
            Nama1: enama1.Text,
            Alamat1: ealamat1.Text,
            Nik1: enik1.Text,
            Telp1: etelp1.Text);
    }

    private string KonfirmasiAnggotaText(string judul) =>
        judul + "\n" +
        "Nama           : " + enama.Text + "\n" +
        "Alamat         : " + ealamat.Text + "\n" +
        "NIK            : " + enik.Text + "\n" +
        "NoKK           : " + enokk.Text + "\n" +
        "Kode Kelompok  : " + ekodekp.Text + "\n" +
        "Nama Kelompok  : " + enamakp1.Text + "\n" +
        "Kategori Usaha : " + ekategori.Text + "\n" +
        "Pekerjaan      : " + epekerjaan.Text + "\n" +
        "Produk         : " + eproduk.Text + "\n" +
        "Kategori Gakin : " + egakin.Text;

    /// <summary>Peringatan "sudah terdaftar pada pengajuan lain" (dipakai cek NIK / KK).</summary>
    private void WarnPengajuanLain(string apa, ProposalInfo p) =>
        Warn("Perhatian :\n" + apa + " sudah terdaftar pada pengajuan lain\n" +
             "No Proposal   : " + p.NoProposal + " Status :" + p.Status1 + "\n" +
             "Nama Kelompok : " + p.Nama);

    /// <summary>Cek NIK / KK di master anggota dan di proposal lain. True bila boleh lanjut.</summary>
    private bool CekNikKk(bool cekProposalNik = true, bool cekProposalKk = true, string kkLabel = "No. KK")
    {
        if (ProposalRepository.AnggotaNikExists(enik.Text))
        {
            Warn("Perhatian :\nNIK sudah terdaftar pada database anggota\nPeriksa ulang data pengajuan anggota baru");
            return false;
        }
        if (cekProposalNik && ProposalRepository.FindPengajuanByNik(enik.Text) is { } p1)
        {
            WarnPengajuanLain("NIK", p1);
            return false;
        }
        if (ProposalRepository.AnggotaNoKkExists(enokk.Text))
        {
            Warn("Perhatian :\n" + kkLabel + " sudah terdaftar pada database anggota\nPeriksa ulang data pengajuan anggota baru");
            return false;
        }
        if (cekProposalKk && ProposalRepository.FindPengajuanByNoKk(enokk.Text) is { } p2)
        {
            WarnPengajuanLain(kkLabel == "No. KK" ? "NoKK" : kkLabel, p2);
            return false;
        }
        return true;
    }

    // ================================================================ tab Pengajuan: kelompok

    private void BitBtn10Click(object? sender, EventArgs e)
    {
        if (enamakp.Text == "") return;
        CekPinjaman();
    }

    private void BitBtn9Click(object? sender, EventArgs e)
    {
        if (enamakp.Text == "") return;
        CekKelompok();
    }

    private void btcek1Click(object? sender, EventArgs e)
    {
        if (enamakp.Text == "") return;
        Panel6.Visible = true;
        CekKelompok();
    }

    private void btcek2Click(object? sender, EventArgs e)
    {
        if (enamakp.Text == "") return;

        Requery(bsDafProposal, "SELECT * FROM proposal_klp WHERE nama = @x1 AND desa = @x2 AND status <> @x3", new
        {
            x1 = enamakp.Text.Trim(),
            x2 = enamadesacek.Text.Trim(),
            x3 = "REALISASI",
        });
        if (!IsEmpty(bsDafProposal))
            Info("Nama kelompok yang sama pada desa yang dipilih sudah ada di daftar tunggu pengajuan!\n\nPeriksa ulang proposal pengajuan");

        CekPinjaman();
        if (!IsEmpty(bsCekPinjaman))
        {
            Panel6.Visible = true;
            pagecek.SelectedIndex = 1;
            Info("Pinjaman dengan nama kelompok yang hampir sama pada desa yang dipilih ditemukan..!\n\nPeriksa ulang proposal pengajuan");
        }
    }

    private void BitBtn8Click(object? sender, EventArgs e)
    {
        Requery(bsDafProposal, "SELECT * FROM proposal_klp WHERE trim(noproposal) = @x1 AND status <> @x2",
            new { x1 = enoprop.Text.Trim(), x2 = "REALISASI" });
        if (!IsEmpty(bsDafProposal))
        {
            Info("No NIK masih memiliki pengajuan belum terealisasi (daftar tunggu)\n\nPeriksa ulang proposal pengajuan");
            return;
        }

        System.Data.DataTable ceknonik;
        try
        {
            ceknonik = Database.QueryTable("SELECT * FROM pembiayaan_ag WHERE trim(nik) = @x1 AND status = @x2",
                new { x1 = enoprop.Text.Trim(), x2 = "BL" });
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }

        if (ceknonik.Rows.Count > 0)
        {
            var r = ceknonik.DefaultView[0];
            Info("No NIK masih memiliki pinjaman belum lunas\n\n" +
                 "No Pinjaman   : " + Str(r, "no_pembiayaan") + "\n" +
                 "No Kelompok   : " + Str(r, "kodekelompok") + "\n" +
                 "Nama Kelompok : " + Str(r, "namakelompok") + "\n" +
                 "Jumlah        : " + Rupiah(Num(r, "jumlah")) + "\n\n" +
                 "Periksa ulang proposal pengajuan");
        }
    }

    private void BitBtn13Click(object? sender, EventArgs e)
    {
        Requery(bsDafProposal, "SELECT * FROM proposal_klp WHERE status1 <> @x1 ORDER BY status1, tglmasuk",
            new { x1 = ProposalRepository.StatusTerima });
        LoadJumPropAktif();
    }

    /// <summary>Pilih ketua/sekretaris/bendahara dari anggota kelompok (BitBtn16, BitBtn7, BitBtn18).</summary>
    private void PilihPengurus(string jabatan)
    {
        Panel6.Visible = true;
        datap.Text = jabatan;
        pagecek.SelectedIndex = 2;
        Requery(bsDafAnggota, SqlAnggotaKelompok, new { x1 = enoindukkp.Text.Trim() });
    }

    private void BitBtn16Click(object? sender, EventArgs e) => PilihPengurus("ketua");

    private void BitBtn7Click(object? sender, EventArgs e) => PilihPengurus("sek");

    private void BitBtn18Click(object? sender, EventArgs e) => PilihPengurus("bend");

    private void BitBtn17Click(object? sender, EventArgs e)
    {
        if (ecari1.Text == "") return;
        // Delphi memakai rgcari (bukan rgcari1) untuk pilihan "Desa"; di sini konsisten memakai rgcari1.
        CariAnggota(rgcari1.ItemIndex, ecari1.Text);
    }

    private void btbatalClick(object? sender, EventArgs e)
    {
        CloseQuery(bsDafProposal);

        rgklp.ItemIndex = -1;
        enoprop.Enabled = false;
        enamakp.Enabled = false;
        enamadesacek.Enabled = false;
        enamadesacek1.Enabled = false;
        rgklp.Enabled = false;

        btcek1.Enabled = false;
        btcek2.Enabled = false;
        Panel12.Visible = true;
        Panel6.Visible = true;
        btsimpan.Enabled = false;
        bttambah.Enabled = true;

        enoindukkp.Text = "";
        etglmasukkp.Value = DateTime.Now;

        enamakp.Text = "";

        ealamatkp.Text = "";
        etelpkp.Text = "";
        enamadesakp.Text = "";
        enamakeckp.Text = "";
        enamakabkp.Text = "";
        enamapropkp.Text = "";
        enoprop.Text = "";

        eketuakp.Text = "";
        ealamatketuakp.Text = "";
        esekkp.Text = "";
        ealamatsekkp.Text = "";
        ebendkp.Text = "";
        ealamatbendkp.Text = "";
        SelectFirst(eusahaklp, ejenisklp, ekembangklp, efungsiklp, emetode);
        ejumlah.Text = "0";
        elama.Text = "12";
        Memo1.Clear();

        // Delphi memfilter kolom "status" (LAMA/BARU) sehingga daftar selalu kosong; maksudnya status1.
        LoadDafProposalInputRevisi(orderByTgl: false);
    }

    private static void SelectFirst(params ComboBox[] combos)
    {
        foreach (var c in combos)
            if (c.Items.Count > 0) c.SelectedIndex = 0;
    }

    private void bttambahClick(object? sender, EventArgs e)
    {
        btbatalClick(sender, e);
        enoprop.Enabled = true;
        rgklp.Enabled = true;

        try
        {
            enoprop.Text = ProposalRepository.NextNoProposal();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }

        enoprop.Focus();
        bttambah.Enabled = false;
        btsimpan.Enabled = true;
    }

    private void btsimpanClick(object? sender, EventArgs e)
    {
        try
        {
            // Nomor sudah dipakai user lain -> buat nomor baru (Delphi: label balikm).
            if (ProposalRepository.ProposalExists(enoprop.Text))
                enoprop.Text = ProposalRepository.NextNoProposal();
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }

        const string metodeTidakSesuai =
            "Pemilihan Jangka Waktu dan Metode Angsuran tidak sesuai\n" +
            "Lakukan pemilihan dengan benar. Jangka waktu pinjaman harus habis dibagi dengan metode angsuran";

        if (!int.TryParse(elama.Text.Trim(), out var lama) || lama <= 0)
        {
            Info("Jangka waktu pinjaman tidak valid.");
            elama.Focus();
            return;
        }
        var metode = emetode.SelectedIndex;
        if (metode < 0)
        {
            Info("Metode angsuran belum dipilih.");
            emetode.Focus();
            return;
        }
        // Index 6 = Selapan Hari (35 hari).
        if ((metode != 6 && lama % (metode + 1) > 0) || (metode == 6 && lama * 10 % 12 > 0))
        {
            Info(metodeTidakSesuai);
            return;
        }

        if (rgklp.ItemIndex == -1)
        {
            Info("Pilihan sebagai Kelompok Lama atau Baru belum dipilih..!");
            return;
        }
        if (rgklp.ItemIndex == 0 && enoindukkp.Text == "")
        {
            Info("Isian No Kelompok masih kosong\n" +
                 "Pilihan kelompok : KELOMPOK LAMA, seharusnya sudah memiliki No Kelompok\n" +
                 "Pilih ulang kelompok dari Daftar Kelompok yang ada (Baca Petunjuk)");
            return;
        }
        if (rgklp.ItemIndex == 1 && enoindukkp.Text != "-")
        {
            Info("Pilihan kelompok : KELOMPOK BARU, seharusnya belum memiliki No Kelompok\n" +
                 "Nilai default untuk isian No Kelompok adalah \"-\"");
            return;
        }

        (TextBox Box, string Nama)[] wajib =
        [
            (eketuakp, "ketua kelompok"),
            (ealamatketuakp, "alamat ketua kelompok"),
            (esekkp, "sekretaris kelompok"),
            (ealamatsekkp, "alamat sekretaris kelompok"),
            (ebendkp, "bendahara kelompok"),
            (ealamatbendkp, "alamat bendahara kelompok"),
        ];
        foreach (var (box, nama) in wajib)
        {
            if (box.Text == "")
            {
                Info($"Kolom isian {nama} belum diisi");
                return;
            }
        }

        if (ejumlah.Text is "0" or "" || !TryParseRupiah(ejumlah.Text, out var jumlah) || jumlah == 0)
        {
            Info("Isian jumlah pengajuan pinjaman masih kosong!");
            return;
        }
        if (cbjenispby.Text == "")
        {
            Info("Jenis pinjaman belum dipilih!");
            return;
        }
        if (enamakp.Text == "" || enoprop.Text == "" || enamadesakp.Text == "")
        {
            Info("Data pengajuan tidak lengkap..!");
            return;
        }
        if (ebungapct.Text == "")
        {
            Info("Bunga pinjaman per tahun belum diisi..!");
            return;
        }
        ebungapct.Text = ClearKoma(ebungapct.Text);
        if (!double.TryParse(ebungapct.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var bunga))
        {
            Info("Bunga pinjaman per tahun tidak valid..!");
            return;
        }

        if (!Confirm("Anda yakin menyimpan pengajuan kredit sbb:\n" +
                     "No Pengajuan   : " + enoprop.Text + "\n" +
                     "Nama           : " + enamakp.Text + "\n" +
                     "Alamat         : " + ealamatkp.Text + "\n" +
                     "Desa           : " + enamadesakp.Text + "\n" +
                     "Ketua : " + eketuakp.Text + "\n" +
                     "Sekretaris : " + esekkp.Text + "\n" +
                     "Bendahara   : " + ebendkp.Text))
            return;

        var k = new ProposalKelompok(
            NoKelompok: enoindukkp.Text,
            TglMasuk: etglmasukkp.Value,
            Nama: enamakp.Text,
            Alamat: ealamatkp.Text,
            Telp: etelpkp.Text,
            KodeDesa: ekodedesa.Text,
            Desa: enamadesakp.Text,
            Kecamatan: enamakeckp.Text,
            Kabupaten: enamakabkp.Text,
            Propinsi: enamapropkp.Text,
            Ketua: eketuakp.Text,
            AlamatKetua: ealamatketuakp.Text,
            Sekretaris: esekkp.Text,
            AlamatSek: ealamatsekkp.Text,
            Bendahara: ebendkp.Text,
            AlamatBend: ealamatbendkp.Text,
            Usaha: eusahaklp.Text,
            Jenis: ejenisklp.Text,
            JenisPby: cbjenispby.Text,
            Perkembangan: ekembangklp.Text,
            Fungsi: efungsiklp.Text,
            NoProposal: enoprop.Text,
            Jumlah: jumlah,
            BungaPct: bunga,
            Memo: Memo1.Text,
            Status: rgklp.ItemIndex == 0 ? "LAMA" : "BARU",
            Lama: elama.Text.Trim(),
            Metode: (metode + 1).ToString(CultureInfo.InvariantCulture));

        try
        {
            ProposalRepository.InsertKelompok(k);
        }
        catch (Exception ex)
        {
            ShowError(ex, "Transaksi dibatalkan");
            btbatalClick(sender, e);
            BitBtn13Click(sender, e);
            return;
        }

        btsimpan.Enabled = false;
        btbatalClick(sender, e);

        Requery(bsDafProposal, "SELECT * FROM proposal_klp WHERE status1 = @x1 ORDER BY status1, tglmasuk",
            new { x1 = ProposalRepository.StatusInput });
        LoadJumPropAktif();

        Info("Transaksi sudah disimpan dengan baik");
    }

    private void rgklpClick(object? sender, EventArgs e)
    {
        if (rgklp.ItemIndex == 0)
        {
            if (enoindukkp.Text == "-")
            {
                if (!Confirm("Apabila Kelompok Lama anda pilih, prosedur akan diulang dari awal"))
                {
                    rgklp.ItemIndex = 1;
                    return;
                }
                btbatalClick(sender, e);
            }
            else
            {
                enamakp.Enabled = true;
                enamadesacek.Enabled = true;
                enamadesacek1.Enabled = true;
                enamadesacek1.Focus();
            }
        }
        else if (rgklp.ItemIndex == 1)
        {
            if (enoindukkp.Text != "-" && enoindukkp.Text != "")
            {
                if (!Confirm("Apabila Kelompok Baru anda pilih, prosedur akan diulang dari awal"))
                {
                    rgklp.ItemIndex = 0;
                    return;
                }
                btbatalClick(sender, e);
            }
            else
            {
                enoindukkp.Text = "-";
                enamakp.Enabled = true;
                enamadesacek.Enabled = true;
                enamadesacek1.Enabled = true;
                enamadesacek1.Focus();
            }
        }
    }

    private void DBGrid5DblClick(object? sender, EventArgs e)
    {
        if (bttambah.Enabled) return;

        if (rgklp.ItemIndex == 1)
        {
            Info("Kategori kelompok pilihan anda : KELOMPOK BARU.\n" +
                 "Apabila ingin menggunakan Daftar Kelompok yang ada pilih Kategori ke : KELOMPOK LAMA");
            btbatalClick(sender, e);
            return;
        }

        var klp = Current(bsCekKlp);
        if (klp is null) return;

        Requery(bsDafProposal, "SELECT * FROM proposal_klp WHERE no_kelompok = @x1 AND desa = @x2 AND status1 <> @x3", new
        {
            x1 = Str(klp, "no_kelompok"),
            x2 = enamadesacek.Text.Trim(),
            x3 = ProposalRepository.StatusTerima,
        });
        if (!IsEmpty(bsDafProposal))
        {
            Info("Nomor dan Nama kelompok yang anda pilih sudah ada di daftar tunggu pengajuan!\n\nPeriksa ulang proposal pengajuan");
            return;
        }

        if (!Confirm("Anda yakin memilih dari Daftar Kelompok sbb :\n" +
                     "No Kelompok    : " + Str(klp, "no_kelompok") + "\n" +
                     "Kelompok       : " + Str(klp, "nama")))
            return;

        rgklp.ItemIndex = 0;
        ealamatkp.Focus();
        enamadesakp.BringToFront();
        enoindukkp.Text = Str(klp, "no_kelompok");

        enamakp.Text = Str(klp, "nama");
        ealamatkp.Text = Str(klp, "alamat");
        etelpkp.Text = Str(klp, "telp");
        enamadesakp.Text = Str(klp, "desa");
        ekodedesa.Text = Str(klp, "kodedesa");

        enamakeckp.Text = Str(klp, "kecamatan");
        enamakabkp.Text = Str(klp, "kabupaten");
        enamapropkp.Text = Str(klp, "propinsi");

        eketuakp.Text = Str(klp, "ketua");
        ealamatketuakp.Text = Str(klp, "alamatketua");
        esekkp.Text = Str(klp, "sekretaris");
        ealamatsekkp.Text = Str(klp, "alamatsek");
        ebendkp.Text = Str(klp, "bendahara");
        ealamatbendkp.Text = Str(klp, "alamatbend");
        eusahaklp.Text = Str(klp, "usaha");
        ejenisklp.Text = Str(klp, "jenis");
        ekembangklp.Text = Str(klp, "perkembangan");
        efungsiklp.Text = Str(klp, "fungsi");
        rgklp.ItemIndex = 0;
        Panel6.Visible = false;
    }

    private void DBGrid3DblClick(object? sender, EventArgs e)
    {
        var ag = Current(bsDafAnggota);
        if (ag is null) return;

        var (nama, alamat) = datap.Text switch
        {
            "ketua" => (eketuakp, ealamatketuakp),
            "sek" => (esekkp, ealamatsekkp),
            "bend" => (ebendkp, ealamatbendkp),
            _ => (null, null),
        };
        if (nama is null || alamat is null) return;

        nama.Text = Str(ag, "nama");
        alamat.Text = Str(ag, "alamat");
        Panel6.Visible = false;
    }

    private void DBGrid12CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        Requery(bsDafProposal, "SELECT * FROM proposal_klp WHERE status1 LIKE @x1 ORDER BY tglmasuk",
            new { x1 = Str(Current(bsJumProp), "status1") });
    }

    private void DBGrid13CellClick(object? sender, DataGridViewCellEventArgs e) => DBGrid12CellClick(sender, e);

    private void ekodedesaxClick(object? sender, EventArgs e)
    {
        var desa = Selected(ekodedesax);
        ekodedesax.BringToFront();
        ekodedesa.Text = Str(desa, "kodedesa");
        enamadesakp.Text = Str(desa, "namadesa");
    }

    private void enamadesacek1Click(object? sender, EventArgs e)
    {
        if (rgklp.ItemIndex == -1)
        {
            Info("Kategori kelompok belum dipilih");
            return;
        }

        var desa = Selected(enamadesacek1);
        enamadesacek.BringToFront();
        enamadesacek.Text = Str(desa, "namadesa");
        ekodedesa.Text = Str(desa, "kodedesa");
        enamadesakp.Text = Str(desa, "namadesa");
        Panel12.Visible = false;
        // Panel6 (daftar cek kelompok) menutupi kolom isian. Hanya perlu untuk KELOMPOK LAMA
        // (memilih dari daftar); untuk KELOMPOK BARU langsung tampilkan kolom isian.
        Panel6.Visible = rgklp.ItemIndex == 0;
        enamakp.Focus();
    }

    private void enamakpChange(object? sender, EventArgs e)
    {
        pagecek.SelectedIndex = 0;
        btcek1Click(sender, e);
    }

    private void ejumlahChange(object? sender, EventArgs e) => FormatRupiah(ejumlah);

    private void ebungapctxxChange(object? sender, EventArgs e) => ebungapct.Text = ebungapctxx.Text;

    private void cbjenispbyClick(object? sender, EventArgs e) => emetode.Focus();

    // ================================================================ tab Detil: anggota peminjam

    private void BitBtn11Click(object? sender, EventArgs e) => LoadDafPinjamanAg(noprop1.Text, orderByNik: true);

    private void BitBtn12Click(object? sender, EventArgs e)
    {
        var ag = Current(bsDafPinjamanAg);
        if (ag is null) return;

        if (Str(ag, "status") == "LAMA")
        {
            Info("Data terpilih adalah data anggota lama,\n" +
                 "anda tidak dapat melakukan perubahan melalui menu ini.\n" +
                 "Untuk melakukan perubahan, lakukan sbb :\n" +
                 "1. Hapus data dari Daftar Anggota Peminjam\n" +
                 "2. Lakukan perubahan melalui menu ubah pada Pencarian Database Anggota\n" +
                 "3. Setelah perubahan anda lakukan, masukkan kembali ke Daftar Anggota Peminjam");
            return;
        }

        FillAnggotaForm(ag);
        sz = Str(ag, "nik");
        ceknop1 = sz;
        ceknop1x = Str(ag, "nokk");

        Panel8.Visible = true;
        Panel9.Text = ModeUbahAnggotaBaru;
        btpilih.Visible = false;
        btsimpanag.Visible = true;
        gbanggota.Enabled = true;
        BitBtn1.Visible = false;
    }

    /// <summary>Pilih anggota lama dari Pencarian Database Anggota (BitBtn14, juga dobel klik DBGrid1).</summary>
    private void BitBtn14Click(object? sender, EventArgs e)
    {
        var ag = Current(bsDafAnggota);
        if (ag is null) return;

        FillAnggotaForm(ag);

        Panel8.Visible = true;
        Panel9.Text = ModePilihAnggotaLama;
        btpilih.Visible = true;
        btsimpanag.Visible = false;
        gbanggota.Enabled = false;
        BitBtn1.Visible = true;
    }

    /// <summary>Ubah data anggota lama (BitBtn1 di panel isian, BitBtn5 di tab detil).</summary>
    private void UbahAnggotaLama()
    {
        var ag = Current(bsDafAnggota);
        if (ag is null) return;

        FillAnggotaForm(ag);

        Panel8.Visible = true;
        Panel9.Text = ModeUbahAnggotaLama;
        sz = enik.Text;
        btpilih.Visible = false;
        btsimpanag.Visible = true;
        gbanggota.Enabled = true;
    }

    private void BitBtn1Click(object? sender, EventArgs e)
    {
        UbahAnggotaLama();
        BitBtn1.Visible = false;
    }

    private void BitBtn5Click(object? sender, EventArgs e) => UbahAnggotaLama();

    private void btpilihClick(object? sender, EventArgs e)
    {
        bool replaceNik;
        try
        {
            var existing = ProposalRepository.GetAnggotaProposal(enik.Text);
            if (existing is { } cur && cur.Status1 != ProposalRepository.StatusTerima)
            {
                var p = ProposalRepository.GetProposalInfo(cur.NoProposal);
                Info("Perhatian :\nAnggota masih tercatat di pengajuan kelompok lain\n" +
                     "No Pengajuan  :" + (p?.NoProposal ?? cur.NoProposal) + " Status : " + (p?.Status1 ?? cur.Status1) + "\n" +
                     "Nama Kelompok :" + (p?.Nama ?? ""));
                return;
            }
            // Status TERIMA: baris proposal_ag lama dihapus lalu diganti (dalam satu transaksi).
            replaceNik = existing is not null;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }

        if (enama1.Text == "" || ealamat1.Text == "" || enik1.Text == "")
        {
            Info("Data Suami/ Istri belum dimasukkan");
            return;
        }

        if (ReadAnggotaForm() is not { } a) return;

        try
        {
            ProposalRepository.InsertAnggota(a, "LAMA", dbjenispby.Text, noprop1.Text, replaceNik);
        }
        catch (Exception ex)
        {
            ShowError(ex, "transaksi dibatalkan");
            return;
        }

        LoadDafPinjamanAg(noprop1.Text, orderByNik: true);
        btbatalagClick(sender, e);
        LoadAnggotaKelompokProposal();
        Info("Transaksi telah disimpan dengan baik");
    }

    private void btsimpanagClick(object? sender, EventArgs e)
    {
        try
        {
            switch (Panel9.Text)
            {
                case ModeAnggotaBaru:
                    SimpanAnggotaBaru();
                    break;
                case ModeUbahAnggotaLama:
                    SimpanUbahAnggotaLama();
                    break;
                case ModeUbahAnggotaBaru:
                    SimpanUbahAnggotaBaru();
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex, "Transaksi dibatalkan");
        }
    }

    private void SimpanAnggotaBaru()
    {
        if (!CekNikKk()) return;
        if (ReadAnggotaForm() is not { } a) return;

        ProposalRepository.InsertAnggota(a, "BARU", dbjenispby.Text, noprop1.Text);

        LoadDafPinjamanAg(noprop1.Text);
        btbatalagClick(this, EventArgs.Empty);
        Info("Penambahan telah disimpan dengan baik");
    }

    private void SimpanUbahAnggotaLama()
    {
        if (!Confirm(KonfirmasiAnggotaText("Anda yakin menyimpan perubahan data anggota sbb :"), defaultCancel: true))
            return;
        if (ReadAnggotaForm() is not { } a) return;

        ProposalRepository.UpdateAnggotaLama(a, sz);

        BitBtn6Click(this, EventArgs.Empty);
        Locate(bsDafAnggota, "nik", enik.Text);
        BitBtn1.Visible = true;
        BitBtn1.BringToFront();

        Info("Perubahan sudah disimpan dengan baik");
        BitBtn14Click(this, EventArgs.Empty);
    }

    private void SimpanUbahAnggotaBaru()
    {
        if (!Confirm(KonfirmasiAnggotaText("Anda yakin menyimpan perubahan data anggota baru sbb :"), defaultCancel: true))
            return;

        // NIK/KK yang tidak berubah boleh tetap ada di proposal ini sendiri.
        if (!CekNikKk(cekProposalNik: ceknop1 != enik.Text.Trim(),
                      cekProposalKk: ceknop1x != enokk.Text.Trim(),
                      kkLabel: "No KK"))
            return;
        if (ReadAnggotaForm() is not { } a) return;

        ProposalRepository.UpdateAnggotaBaru(a, dbjenispby.Text, sz, noprop1.Text);

        BitBtn11Click(this, EventArgs.Empty);
        btbatalagClick(this, EventArgs.Empty);
        Info("Perubahan sudah disimpan dengan baik");
    }

    private void btbatalagClick(object? sender, EventArgs e)
    {
        etglmasuk.Value = DateTime.Now;
        enama.Text = "";
        if (cbsex.Items.Count > 0) cbsex.SelectedIndex = 0;
        etempatlhr.Text = "";
        etgllhr.Text = "";
        ealamat.Text = "";
        edesa.Text = "";
        ekec.Text = "";
        etelp.Text = "";
        ekab.Text = "";
        eprop.Text = "";
        egol.Text = "-";

        enik.Text = "";
        enokk.Text = "";
        ekategori.Text = "";
        epekerjaan.Text = "";
        eproduk.Text = "";
        enamakp2.Text = "";
        enamakp2.BringToFront();

        enama1.Text = "";
        ealamat1.Text = "";
        enik1.Text = "";
        etelp1.Text = "";
        Panel8.Visible = false;
    }

    private void BitBtn2Click(object? sender, EventArgs e)
    {
        btbatalagClick(sender, e);
        Panel8.Visible = true;
        Panel9.Text = ModeAnggotaBaru;
        btpilih.Visible = false;
        btsimpanag.Visible = true;
        gbanggota.Enabled = true;
        enamakp2.Text = dbnamakp.Text.Trim();
        BitBtn1.Visible = false;
    }

    private void BitBtn3Click(object? sender, EventArgs e)
    {
        LoadDafProposalInputRevisi(orderByTgl: false);

        Requery(bsJumProp,
            "SELECT status1, count(status1) AS jumlah FROM proposal_klp WHERE status1 <> @x1 AND (status1 = @x2 OR status1 = @x3) GROUP BY status1 ORDER BY status1",
            new { x1 = ProposalRepository.StatusTerima, x2 = ProposalRepository.StatusInput, x3 = ProposalRepository.StatusRevisi });

        LoadAnggotaKelompokProposal(quiet: true);
    }

    private void BitBtn4Click(object? sender, EventArgs e)
    {
        var ag = Current(bsDafPinjamanAg);
        if (ag is null) return;

        if (!Confirm("Anda yakin menghapus pengajuan kredit sbb:\n" +
                     "No Pengajuan   : " + Str(ag, "noproposal") + "\n" +
                     "Kelompok       : " + Str(ag, "namakelompok") + "\n" +
                     "Nama           : " + Str(ag, "nama") + "\n" +
                     "NIK            : " + Str(ag, "nik")))
            return;

        try
        {
            ProposalRepository.DeleteAnggota(Str(ag, "noproposal"), Str(ag, "nik"));
        }
        catch
        {
            Info("Data tidak dapat dihapus. Status : LENGKAP/VERIFIKASI/REALISASI");
            return;
        }

        LoadDafPinjamanAg(noprop1.Text);
    }

    private void BitBtn6Click(object? sender, EventArgs e) => CariAnggota(rgcari.ItemIndex, ecari.Text);

    private void ecariChange(object? sender, EventArgs e) => BitBtn6Click(sender, e);

    private void ecari1Change(object? sender, EventArgs e) => BitBtn17Click(sender, e);

    private void ecaridafChange(object? sender, EventArgs e)
    {
        var column = rgdaf2.ItemIndex switch
        {
            0 => "noproposal",
            1 => "lower(nama)",
            2 => "lower(desa)",
            _ => null,
        };
        if (column is null) return;

        var value = rgdaf2.ItemIndex == 0 ? ecaridaf.Text.Trim() : ecaridaf.Text.Trim().ToLower();
        Requery(bsDafProposal, $"SELECT * FROM proposal_klp WHERE {column} LIKE @x2 ORDER BY tglmasuk", new { x2 = value + "%" });
    }

    private void BitBtn24Click(object? sender, EventArgs e)
    {
        if (!Confirm("Anda yakin menghapus pengajuan kredit sbb:\n" +
                     "No Pengajuan   : " + noprop1.Text + "\n" +
                     "Kelompok       : " + dbnamakp.Text))
            return;

        try
        {
            ProposalRepository.DeleteProposal(noprop1.Text.Trim());
            Info("Data sudah dihapus..!");
        }
        catch
        {
            Info("Data tidak dapat dihapus. Status : LENGKAP/VERIFIKASI/REALISASI");
            return;
        }

        LoadDafProposalInputRevisi(orderByTgl: false);
        LoadDafPinjamanAg(noprop1.Text);
    }

    private void DBGrid1DblClick(object? sender, EventArgs e) => BitBtn14Click(sender, e);

    private void DBGrid6CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        LoadAnggotaKelompokProposal();
    }

    private void DBGrid6DblClick(object? sender, EventArgs e) => LoadAnggotaKelompokProposal();

    private void noprop1Change(object? sender, EventArgs e)
    {
        LoadDafPinjamanAg(noprop1.Text, orderByNik: true, quiet: true);
        LoadSumPinjam(noprop1.Text, quiet: true);
    }

    private void dbstatus1Change(object? sender, EventArgs e)
    {
        // Di Delphi isinya dikomentari (Unit_proposal.pas:2511).
    }

    // ---------------------------------------------------------------- catatan anggota (Panel17, tab detil)

    private void ShowCatatan(TextBox lama, TextBox baru, TextBox memo, Button edit)
    {
        var ag = Current(bsDafPinjamanAg);
        edit.Text = "Edit";
        memo.ReadOnly = true;
        lama.ReadOnly = true;
        baru.ReadOnly = true;
        lama.Text = Num(ag, "jumlahlama").ToString(Id);
        baru.Text = Num(ag, "jumlahbaru").ToString(Id);
        memo.Text = Str(ag, "memo");
    }

    private void DBGrid2DblClick(object? sender, EventArgs e)
    {
        Panel17.Visible = true;
        ShowCatatan(eplama, epbaru, Memo4, BitBtn22);
    }

    private void dbnik2Change(object? sender, EventArgs e)
    {
        ShowCatatan(eplama, epbaru, Memo4, BitBtn22);
        LoadPembiayaanAg(dbnik2.Text);
    }

    private void dbnik1Change(object? sender, EventArgs e)
    {
        ShowCatatan(eplamax, epbarux, Memo3, BitBtn21);
        LoadPembiayaanAg(dbnik1.Text);
    }

    /// <summary>Tombol Edit/Simpan catatan anggota (BitBtn22 di tab detil, BitBtn21 di tab kirim).</summary>
    private void EditSimpanCatatan(string status, TextBox lama, TextBox baru, TextBox memo, Button edit,
        Control panel, string nik, string noProposal, bool orderByNik)
    {
        if (status is not (ProposalRepository.StatusInput or ProposalRepository.StatusRevisi))
        {
            Info("Status Data: " + status + "\nPerubahan hanya bisa dilakukan oleh Bag.Verifikasi");
            return;
        }

        if (edit.Text == "Edit")
        {
            memo.ReadOnly = false;
            lama.ReadOnly = false;
            baru.ReadOnly = false;
            edit.Text = "Simpan";
            return;
        }

        if (edit.Text != "Simpan") return;

        void Selesai()
        {
            memo.ReadOnly = true;
            lama.ReadOnly = true;
            baru.ReadOnly = true;
            edit.Text = "Edit";
            panel.Visible = false;
        }

        if (!TryParseRupiah(lama.Text, out var nLama) || !TryParseRupiah(baru.Text, out var nBaru))
        {
            Info("Jumlah pinjaman tidak valid");
            return;
        }

        try
        {
            ProposalRepository.UpdateMemoJumlah(nik, noProposal, memo.Text, nLama, nBaru);
        }
        catch (Exception ex)
        {
            ShowError(ex, "Transaksi dibatalkan");
            Selesai();
            return;
        }

        LoadDafPinjamanAg(noProposal, orderByNik);
        LoadSumPinjam(noProposal);
        Selesai();
        Info("Transaksi sudah disimpan dengan baik");
    }

    private void BitBtn22Click(object? sender, EventArgs e) =>
        EditSimpanCatatan(dbstatus1.Text, eplama, epbaru, Memo4, BitBtn22, Panel17, dbnik2.Text, noprop1.Text, orderByNik: true);

    private void eplamaChange(object? sender, EventArgs e) => FormatRupiah(eplama);

    private void epbaruChange(object? sender, EventArgs e) => FormatRupiah(epbaru);

    // ================================================================ tab Kirim

    private void BitBtn21Click(object? sender, EventArgs e) =>
        EditSimpanCatatan(dbstatus2.Text, eplamax, epbarux, Memo3, BitBtn21, Panel15, dbnik1.Text, noprop2.Text, orderByNik: false);

    private void eplamaxChange(object? sender, EventArgs e) => FormatRupiah(eplamax);

    private void epbaruxChange(object? sender, EventArgs e) => FormatRupiah(epbarux);

    private void BitBtn19Click(object? sender, EventArgs e)
    {
        if (dbstatus2.Text is not (ProposalRepository.StatusInput or ProposalRepository.StatusRevisi))
        {
            Info("Status Proposal :" + dbstatus2.Text + "\nTidak dapat diubah menjadi status LENGKAP");
            return;
        }

        var noprop = noprop2.Text.Trim();
        var namakp = dbnamakp2.Text.Trim();

        try
        {
            ProposalRepository.KirimKeVerifikasi(noprop);
        }
        catch (Exception ex)
        {
            ShowError(ex, "Transaksi dibatalkan");
            return;
        }

        Info("Status Proposal No: " + noprop + " a.n  Kelompok : " + namakp + "\n" +
             "Telah diubah menjadi Status :LENGKAP dan dikirimkan ke Bag. Verifikasi");

        LoadDafProposalInputRevisi();
        LoadJumPropAktif();
    }

    private void noprop2Change(object? sender, EventArgs e)
    {
        LoadDafPinjamanAg(noprop2.Text, quiet: true);
        LoadSumPinjam(noprop2.Text, quiet: true);
    }

    private void dbstatus2Change(object? sender, EventArgs e) =>
        DBGrid9.ReadOnly = dbstatus2.Text is not (ProposalRepository.StatusInput or ProposalRepository.StatusRevisi);

    private void DBGrid9CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        // Di Delphi isinya dikomentari (Unit_proposal.pas:2444); catatan dibuka lewat dbnik1Change.
    }

    // ================================================================ data anggota: lookup desa & kelompok

    private void ekodedesa1xClick(object? sender, EventArgs e)
    {
        var desa = Selected(ekodedesa1x);
        Requery(bsKelompok, "SELECT * FROM kelompok WHERE trim(kodedesa) = @x ORDER BY nama", new { x = Str(desa, "kodedesa") });
        enamakp1.SelectedIndex = -1;

        edesa.BringToFront();
        edesa.Text = Str(desa, "namadesa");
        edesa.Focus();
    }

    private void enamakp1Click(object? sender, EventArgs e)
    {
        switch (Panel9.Text)
        {
            case ModeUbahAnggotaLama:
                enamakp1.BringToFront();
                ekodekp1.Text = Str(Selected(enamakp1), "no_kelompok");
                enamakp2.Text = enamakp1.Text;
                enamakp2.Focus();
                break;
            case ModeUbahAnggotaBaru:
            case ModeAnggotaBaru:
                Info("Data tidak bisa diubah, masih pada pengajuan pinjaman baru");
                break;
        }
    }

    private void cbsexClick(object? sender, EventArgs e) => egol.Focus();

    // ================================================================ cek NIK / KK saat Enter

    private void enikKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!IsEnter(e)) return;
        try
        {
            if (ProposalRepository.AnggotaNikExists(enik.Text))
            {
                Warn("Perhatian : NIK sudah terdaftar pada database anggota\nPeriksa ulang data pengajuan anggota baru");
                return;
            }
            if (ProposalRepository.FindPengajuanByNik(enik.Text) is { } p)
            {
                Warn("Perhatian : NIK sudah terdaftar pada pengajuan lain\n" +
                     "No Proposal   : " + p.NoProposal + " Status :" + p.Status1 + "\n" +
                     "Nama Kelompok : " + p.Nama);
                return;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }
        enokk.Focus();
    }

    private void enokkKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!IsEnter(e)) return;
        try
        {
            if (ProposalRepository.AnggotaNoKkExists(enokk.Text))
            {
                Warn("Perhatian : No KK sudah terdaftar pada database anggota\nPeriksa ulang data pengajuan anggota baru");
                return;
            }
            if (ProposalRepository.FindPengajuanByNoKk(enokk.Text) is { } p)
            {
                Warn("Perhatian : No KK sudah terdaftar pada pengajuan lain\n" +
                     "No Proposal   : " + p.NoProposal + " Status :" + p.Status1 + "\n" +
                     "Nama Kelompok : " + p.Nama);
                return;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }
        enama.Focus();
    }

    private void enopropKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!IsEnter(e)) return;
        enamakp.Focus();
        BitBtn8Click(sender, e);
    }

    private void ecariKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!IsEnter(e)) return;
        BitBtn6Click(sender, e);
    }

    // ================================================================ navigasi Enter antar isian

    private static void EnterTo(KeyPressEventArgs e, Control next)
    {
        if (IsEnter(e)) next.Focus();
    }

    private void enamakpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, enamadesacek1);

    private void ealamatkpKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!IsEnter(e)) return;
        if (rgklp.ItemIndex == 1) Panel6.Visible = false;
        etelpkp.Focus();
    }

    private void eketuakpKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!IsEnter(e)) return;
        if (rgklp.ItemIndex == 1) Panel6.Visible = false;
        ealamatketuakp.Focus();
    }

    private void etelpkpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ejenisklp);
    private void ejenisklpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, eusahaklp);
    private void eusahaklpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ekembangklp);
    private void ekembangklpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, efungsiklp);
    private void efungsiklpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, eketuakp);
    private void ealamatketuakpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, esekkp);
    private void esekkpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ealamatsekkp);
    private void ealamatsekkpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ebendkp);
    private void ebendkpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ealamatbendkp);
    private void ealamatbendkpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ejumlah);
    private void ejumlahKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, elama);
    private void elamaKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, cbjenispby);
    private void cbjenispbyKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ebungapct);
    private void ebungapctKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, emetode);
    private void emetodeKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, Memo1);

    private void enamaKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, cbsex);
    private void cbsexKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, egol);
    private void egolKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, etempatlhr);
    private void etempatlhrKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, etgllhr);
    private void etgllhrKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ealamat);
    private void ealamatKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, edesa);
    private void ekodedesa1xKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, edesa);
    private void edesaKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, etelp);
    private void etelpKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, enamakp2);
    private void enamakp1KeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, enamakp2);
    private void enamakp2KeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, enama1);
    private void enama1KeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ealamat1);
    private void ealamat1KeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, enik1);
    private void enik1KeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, etelp1);
    private void etelp1KeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, egakin);
    private void egakinKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, ekategori);
    private void ekategoriKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, epekerjaan);
    private void epekerjaanKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, eproduk);
    private void eplamaKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, epbaru);
    private void epbaruKeyPress(object? sender, KeyPressEventArgs e) => EnterTo(e, Memo4);

    // ================================================================ tombol tutup panel

    private void SpeedButton1Click(object? sender, EventArgs e) => Panel8.Visible = false;
    private void SpeedButton2Click(object? sender, EventArgs e) => Panel17.Visible = false;
    private void SpeedButton3Click(object? sender, EventArgs e) => Panel15.Visible = false;
    private void SpeedButton8Click(object? sender, EventArgs e) => Panel6.Visible = false;

    // ================================================================ form & tab

    private void FormClose(object? sender, FormClosedEventArgs e)
    {
        // Delphi: futama1.Enabled := true. Tidak perlu karena FUtama1 membuka form ini dengan ShowDialog.
    }

    private void FormCreate(object? sender, EventArgs e)
    {
        // Delphi: kosong (DecimalSeparator dikomentari).
    }

    private void FormShow(object? sender, EventArgs e)
    {
        // Lookup desa / jenis pinjaman / kelompok (di Delphi dibuka mdutama saat start).
        // qakad tidak dipakai kontrol mana pun di form ini, jadi tidak dimuat.
        LoadLookups();

        etglmasuk.Value = DateTime.Now;
        enama.Text = "";
        if (cbsex.Items.Count > 0) cbsex.SelectedIndex = 0;
        etempatlhr.Text = "";
        etgllhr.Text = "";
        ealamat.Text = "";
        edesa.Text = "";
        ekec.Text = "";
        etelp.Text = "";
        ekab.Text = "";
        eprop.Text = "";
        egol.Text = "-";

        btsimpan.Enabled = false;
        bttambah.Enabled = true;

        enoindukkp.Text = "";
        etglmasukkp.Value = DateTime.Now;

        enamakp.Text = "";

        ealamatkp.Text = "";
        etelpkp.Text = "";
        enamadesakp.Text = "";
        enamakeckp.Text = "";
        enamakabkp.Text = "";
        enamapropkp.Text = "";

        eketuakp.Text = "";
        ealamatketuakp.Text = "";
        esekkp.Text = "";
        ealamatsekkp.Text = "";
        ebendkp.Text = "";
        ealamatbendkp.Text = "";
        SelectFirst(eusahaklp, ejenisklp, ekembangklp, efungsiklp);

        enik.Text = "";
        enokk.Text = "";
        ekategori.Text = "";
        epekerjaan.Text = "";
        eproduk.Text = "";
    }

    private void tabpengajuanShow(object? sender, EventArgs e) => BitBtn13Click(sender, e);

    private void tabdetilShow(object? sender, EventArgs e)
    {
        BitBtn3Click(sender, e);
        CloseQuery(bsDafAnggota);
    }

    private void tabkirimShow(object? sender, EventArgs e)
    {
        LoadDafProposalInputRevisi();
        LoadJumPropAktif();
    }

    private void tbskelompokShow(object? sender, EventArgs e)
    {
        // Di Delphi isinya dikomentari (Unit_proposal.pas:3263).
    }

    private void tbspinjamanShow(object? sender, EventArgs e)
    {
        // Di Delphi isinya dikomentari (Unit_proposal.pas:3275).
    }

    private void gbkelompok_Enter(object sender, EventArgs e)
    {
    }

    private void DBGrid10_CellContentClick(object sender, DataGridViewCellEventArgs e)
    {

    }

    private void Shape2_Click(object sender, EventArgs e)
    {

    }
}
