using Dapper;

namespace UPKPro.Data;

/// <summary>Isian data anggota peminjam (form gbanggota di FProposal).</summary>
public sealed record ProposalAnggota(
    DateTime TglMasuk,
    string Nama,
    string Sex,
    string TempatLhr,
    DateTime TglLhr,
    string Alamat,
    string Telp,
    string Desa,
    string Kecamatan,
    string Kabupaten,
    string Propinsi,
    string KodeKelompok,
    string NamaKelompok,
    string Nik,
    string NoKk,
    string Kategori,
    string Pekerjaan,
    string Produk,
    string Gakin,
    string GolDarah,
    string Nama1,
    string Alamat1,
    string Nik1,
    string Telp1);

/// <summary>Isian pengajuan kelompok (form gbkelompok di FProposal).</summary>
public sealed record ProposalKelompok(
    string NoKelompok,
    DateTime TglMasuk,
    string Nama,
    string Alamat,
    string Telp,
    string KodeDesa,
    string Desa,
    string Kecamatan,
    string Kabupaten,
    string Propinsi,
    string Ketua,
    string AlamatKetua,
    string Sekretaris,
    string AlamatSek,
    string Bendahara,
    string AlamatBend,
    string Usaha,
    string Jenis,
    string JenisPby,
    string Perkembangan,
    string Fungsi,
    string NoProposal,
    double Jumlah,
    double BungaPct,
    string Memo,
    string Status,
    string Lama,
    string Metode);

/// <summary>Info singkat proposal kelompok untuk pesan peringatan.</summary>
public sealed record ProposalInfo(string NoProposal, string Status1, string Nama);

/// <summary>
/// Query tulis/cek untuk tabel proposal_klp dan proposal_ag
/// (padanan mdproposal.qubah / mdutama.qubah di Unit_proposal.pas).
/// Query daftar untuk grid tetap di FProposal karena langsung mengisi BindingSource.
/// </summary>
public static class ProposalRepository
{
    public const string StatusInput = "INPUT";
    public const string StatusRevisi = "REVISI";
    public const string StatusLengkap = "LENGKAP";
    public const string StatusTerima = "TERIMA";

    // ---------------------------------------------------------------- nomor proposal

    /// <summary>
    /// Nomor proposal baru: yyyyMd-n, n = jumlah proposal hari ini + 1, dinaikkan sampai belum dipakai
    /// (Unit_proposal.pas:2206 bttambahClick). Format tanggal tanpa nol di depan seperti FloatToStr di Delphi.
    /// </summary>
    public static string NextNoProposal()
    {
        var today = DateTime.Today;
        var prefix = $"{today.Year}{today.Month}{today.Day}";

        using var conn = Database.Open();
        var count = conn.ExecuteScalar<int>(
            "SELECT count(*)::int FROM proposal_klp WHERE tglmasuk = @x", new { x = today });

        for (var n = count + 1; ; n++)
        {
            var no = $"{prefix}-{n}";
            var exists = conn.ExecuteScalar<bool>(
                "SELECT EXISTS (SELECT 1 FROM proposal_klp WHERE noproposal = @x)", new { x = no });
            if (!exists) return no;
        }
    }

    public static bool ProposalExists(string noProposal)
    {
        using var conn = Database.Open();
        return conn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM proposal_klp WHERE noproposal = @x)", new { x = noProposal.Trim() });
    }

    // ---------------------------------------------------------------- cek NIK / KK

    public static bool AnggotaNikExists(string nik) => AnggotaExists("nik", nik);

    public static bool AnggotaNoKkExists(string noKk) => AnggotaExists("nokk", noKk);

    private static bool AnggotaExists(string column, string value)
    {
        using var conn = Database.Open();
        return conn.ExecuteScalar<bool>(
            $"SELECT EXISTS (SELECT 1 FROM anggota WHERE lower({column}) = lower(@x))", new { x = value });
    }

    /// <summary>Pengajuan lain yang memuat NIK ini (proposal_ag -> proposal_klp), atau null.</summary>
    public static ProposalInfo? FindPengajuanByNik(string nik) => FindPengajuan("nik", nik);

    /// <summary>Pengajuan lain yang memuat No KK ini (proposal_ag -> proposal_klp), atau null.</summary>
    public static ProposalInfo? FindPengajuanByNoKk(string noKk) => FindPengajuan("nokk", noKk);

    private static ProposalInfo? FindPengajuan(string column, string value)
    {
        using var conn = Database.Open();
        var noProposal = conn.QueryFirstOrDefault<string>(
            $"SELECT noproposal FROM proposal_ag WHERE lower({column}) = lower(@x)", new { x = value });
        if (noProposal is null) return null;

        return GetProposalInfo(conn, noProposal) ?? new ProposalInfo(noProposal.Trim(), "", "");
    }

    /// <summary>Status1 dan noproposal dari proposal_ag untuk NIK ini (Unit_proposal.pas:566).</summary>
    public static (string Status1, string NoProposal)? GetAnggotaProposal(string nik)
    {
        using var conn = Database.Open();
        var rows = conn.Query<(string? Status1, string? NoProposal)>(
            "SELECT status1, noproposal FROM proposal_ag WHERE trim(nik) = @x LIMIT 1", new { x = nik.Trim() }).AsList();
        if (rows.Count == 0) return null;
        var r = rows[0];
        return ((r.Status1 ?? "").Trim(), (r.NoProposal ?? "").Trim());
    }

    public static ProposalInfo? GetProposalInfo(string noProposal)
    {
        using var conn = Database.Open();
        return GetProposalInfo(conn, noProposal);
    }

    private static ProposalInfo? GetProposalInfo(Npgsql.NpgsqlConnection conn, string noProposal)
    {
        var rows = conn.Query<(string? NoProposal, string? Status1, string? Nama)>(
            "SELECT noproposal, status1, nama FROM proposal_klp WHERE trim(noproposal) = @x LIMIT 1",
            new { x = noProposal.Trim() }).AsList();
        if (rows.Count == 0) return null;
        var r = rows[0];
        return new ProposalInfo((r.NoProposal ?? "").Trim(), (r.Status1 ?? "").Trim(), (r.Nama ?? "").Trim());
    }

    // ---------------------------------------------------------------- proposal_klp

    /// <summary>Simpan pengajuan kelompok baru (Unit_proposal.pas:1942 btsimpanClick).</summary>
    public static void InsertKelompok(ProposalKelompok k)
    {
        const string sql = """
            INSERT INTO proposal_klp (no_kelompok, tglmasuk, nama, alamat, telp, kodedesa, desa, kecamatan, kabupaten,
                propinsi, ketua, alamatketua, sekretaris, alamatsek, bendahara, alamatbend, usaha, jenis, jenispby,
                perkembangan, fungsi, noproposal, jumlah, jumlahver, jumlahreal, bungapct, memo, memod, status, status1,
                lama, metode)
            VALUES (@NoKelompok, @TglMasuk, @Nama, @Alamat, @Telp, @KodeDesa, upper(@Desa), upper(@Kecamatan),
                upper(@Kabupaten), upper(@Propinsi), @Ketua, @AlamatKetua, @Sekretaris, @AlamatSek, @Bendahara,
                @AlamatBend, @Usaha, @Jenis, @JenisPby, @Perkembangan, @Fungsi, trim(@NoProposal), @Jumlah, 0, 0,
                @BungaPct, @Memo, '', @Status, 'INPUT', @Lama, @Metode)
            """;

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute(sql, k with { TglMasuk = k.TglMasuk.Date }, tx);
        tx.Commit();
    }

    /// <summary>Kirim ke Bag. Verifikasi: status INPUT/REVISI -> LENGKAP (Unit_proposal.pas:801 BitBtn19Click).</summary>
    public static void KirimKeVerifikasi(string noProposal)
    {
        var p = new { x1 = StatusLengkap, x2 = noProposal, x3 = StatusInput, x4 = StatusRevisi };

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute("UPDATE proposal_klp SET status1 = @x1 WHERE noproposal = @x2 AND (status1 = @x3 OR status1 = @x4)", p, tx);
        conn.Execute("UPDATE proposal_ag SET status1 = @x1 WHERE noproposal = @x2 AND (status1 = @x3 OR status1 = @x4)", p, tx);
        tx.Commit();
    }

    /// <summary>Hapus proposal kelompok beserta anggotanya, hanya status INPUT/REVISI (Unit_proposal.pas:1105).</summary>
    public static void DeleteProposal(string noProposal)
    {
        var p = new { x1 = noProposal, x3 = StatusInput, x4 = StatusRevisi };

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute("DELETE FROM proposal_klp WHERE noproposal = @x1 AND (status1 = @x3 OR status1 = @x4)", p, tx);
        conn.Execute("DELETE FROM proposal_ag WHERE noproposal = @x1 AND (status1 = @x3 OR status1 = @x4)", p, tx);
        tx.Commit();
    }

    // ---------------------------------------------------------------- proposal_ag

    /// <summary>
    /// Tambah anggota peminjam ke proposal (Unit_proposal.pas:560 btpilih / 1546 btsimpanag).
    /// <paramref name="replaceNik"/> menghapus dulu baris NIK yang sama (anggota lama dengan status TERIMA).
    /// </summary>
    public static void InsertAnggota(ProposalAnggota a, string status, string jenisPby, string noProposal, bool replaceNik = false)
    {
        const string sql = """
            INSERT INTO proposal_ag (tglmasuk, no_anggota, nama, sex, tempat_lhr, tgl_lhr, alamat, telp, desa, kecamatan,
                kabupaten, propinsi, status, status1, jenispby, kodekelompok, namakelompok, nik, nokk, kategori, pekerjaan,
                produk, gakin, goldarah, noproposal, nama1, alamat1, nik1, telp1, jumlahlama, jumlahbaru, jumlahver, jumlahreal)
            VALUES (@TglMasuk, '-', @Nama, @Sex, @TempatLhr, @TglLhr, @Alamat, @Telp, upper(@Desa), upper(@Kecamatan),
                upper(@Kabupaten), upper(@Propinsi), @Status, 'INPUT', @JenisPby, '-', @NamaKelompok, trim(@Nik), @NoKk,
                @Kategori, @Pekerjaan, @Produk, @Gakin, @GolDarah, trim(@NoProposal), @Nama1, @Alamat1, @Nik1, @Telp1,
                0, 0, 0, 0)
            """;

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        if (replaceNik)
            conn.Execute("DELETE FROM proposal_ag WHERE trim(nik) = @x", new { x = a.Nik.Trim() }, tx);
        conn.Execute(sql, ToParams(a, new { Status = status, JenisPby = jenisPby.Trim(), NoProposal = noProposal }), tx);
        tx.Commit();
    }

    /// <summary>Ubah data anggota baru yang sudah ada di proposal (Unit_proposal.pas:1546, "Ubah Data Anggota Baru").</summary>
    public static void UpdateAnggotaBaru(ProposalAnggota a, string jenisPby, string oldNik, string noProposal)
    {
        const string sql = """
            UPDATE proposal_ag SET tglmasuk = @TglMasuk, no_anggota = '-', nama = @Nama, sex = @Sex, tempat_lhr = @TempatLhr,
                tgl_lhr = @TglLhr, alamat = @Alamat, telp = @Telp, desa = upper(@Desa), kecamatan = upper(@Kecamatan),
                kabupaten = upper(@Kabupaten), propinsi = upper(@Propinsi), status = 'BARU', jenispby = @JenisPby,
                kodekelompok = '-', namakelompok = @NamaKelompok, nik = @Nik, nokk = @NoKk, kategori = @Kategori,
                pekerjaan = @Pekerjaan, produk = @Produk, gakin = @Gakin, goldarah = @GolDarah, nama1 = @Nama1,
                alamat1 = @Alamat1, nik1 = @Nik1, telp1 = @Telp1
            WHERE nik = @OldNik AND noproposal = @NoProposal
            """;

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute(sql, ToParams(a, new { JenisPby = jenisPby.Trim(), OldNik = oldNik, NoProposal = noProposal.Trim() }), tx);
        tx.Commit();
    }

    /// <summary>Ubah memo + pinjaman lama/baru satu anggota (Unit_proposal.pas:937 / 1018).</summary>
    public static void UpdateMemoJumlah(string nik, string noProposal, string memo, double jumlahLama, double jumlahBaru)
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute(
            "UPDATE proposal_ag SET memo = @memo, jumlahlama = @lama, jumlahbaru = @baru WHERE nik = @nik AND noproposal = @nop",
            new { memo, lama = jumlahLama, baru = jumlahBaru, nik = nik.Trim(), nop = noProposal.Trim() }, tx);
        tx.Commit();
    }

    /// <summary>Hapus satu anggota dari proposal, hanya status INPUT/REVISI (Unit_proposal.pas:1207).</summary>
    public static void DeleteAnggota(string noProposal, string nik)
    {
        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute(
            "DELETE FROM proposal_ag WHERE noproposal = @x1 AND nik = @x2 AND (status1 = @x3 OR status1 = @x4)",
            new { x1 = noProposal.Trim(), x2 = nik.Trim(), x3 = StatusInput, x4 = StatusRevisi }, tx);
        tx.Commit();
    }

    // ---------------------------------------------------------------- anggota

    /// <summary>Ubah data di master anggota (Unit_proposal.pas:1546, "Ubah Data Anggota Lama").</summary>
    public static void UpdateAnggotaLama(ProposalAnggota a, string oldNik)
    {
        const string sql = """
            UPDATE anggota SET tglmasuk = @TglMasuk, no_anggota = '-', nama = @Nama, sex = @Sex, tempat_lhr = @TempatLhr,
                tgl_lhr = @TglLhr, alamat = @Alamat, telp = @Telp, desa = upper(@Desa), kecamatan = upper(@Kecamatan),
                kabupaten = upper(@Kabupaten), propinsi = upper(@Propinsi), status = 1, kodekelompok = @KodeKelompok,
                namakelompok = @NamaKelompok, nik = @Nik, nokk = @NoKk, kategori = @Kategori, pekerjaan = @Pekerjaan,
                produk = @Produk, gakin = @Gakin, goldarah = @GolDarah, nama1 = @Nama1, alamat1 = @Alamat1,
                nik1 = @Nik1, telp1 = @Telp1
            WHERE nik = @OldNik
            """;

        using var conn = Database.Open();
        using var tx = conn.BeginTransaction();
        conn.Execute(sql, ToParams(a, new { OldNik = oldNik }), tx);
        tx.Commit();
    }

    private static DynamicParameters ToParams(ProposalAnggota a, object extra)
    {
        var p = new DynamicParameters(a with { TglMasuk = a.TglMasuk.Date, TglLhr = a.TglLhr.Date });
        p.AddDynamicParams(extra);
        return p;
    }
}
