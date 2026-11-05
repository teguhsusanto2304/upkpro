using Dapper;
using UPKPro.Security;

namespace UPKPro.Data;

/// <summary>Satu baris tabel <c>pass</c> (nama char(17), level char(10), sandi char(255) terenkripsi).</summary>
public sealed record PassUser(string Nama, string Level);

/// <summary>
/// Akses tabel <c>pass</c> (padanan mdutama.tbpass / tbpass1 di Delphi).
/// Kolom bertipe char (fixed) sehingga selalu di-trim saat dibandingkan.
/// </summary>
public static class UserRepository
{
    /// <summary>Level yang dikenal aplikasi Delphi (unit_utama.pas:747-823).</summary>
    public static readonly string[] Levels = ["Admin", "Keuangan", "Kasir", "Proposal", "Verifikasi", "Realisasi"];

    /// <summary>Padanan <c>tbpass.Locate('nama', euser.Text, [])</c> (unit_utama.pas:944).</summary>
    public static bool Exists(string nama)
    {
        using var conn = Database.Open();
        return conn.ExecuteScalar<bool>(
            "SELECT EXISTS (SELECT 1 FROM pass WHERE trim(nama) = @nama)",
            new { nama = nama.Trim() });
    }

    /// <summary>
    /// Cek nama + sandi (unit_utama.pas:733). Sandi di DB terenkripsi dengan <c>eee()</c> dan
    /// karena berisi karakter acak, perbandingan dilakukan setelah didekripsi di sisi aplikasi.
    /// </summary>
    public static PassUser? Authenticate(string nama, string password)
    {
        if (string.IsNullOrEmpty(password)) return null;

        using var conn = Database.Open();
        var rows = conn.Query<(string Nama, string? Level, string? Sandi)>(
            "SELECT trim(nama) AS nama, trim(level) AS level, trim(sandi) AS sandi FROM pass WHERE trim(nama) = @nama",
            new { nama = nama.Trim() });

        foreach (var row in rows)
        {
            if (row.Sandi is not null && LegacyCipher.Decrypt(row.Sandi) == password)
                return new PassUser(row.Nama, row.Level ?? "");
        }
        return null;
    }

    /// <summary>
    /// Tambah user baru ke tabel <c>pass</c> (padanan tf_rekening.bitbtn14click, U_SetupRekening.pas:1778).
    /// Sandi dienkripsi dengan cipher Delphi agar tetap bisa dipakai login dari aplikasi lama.
    /// </summary>
    public static int Insert(string nama, string level, string password)
    {
        if (string.IsNullOrWhiteSpace(nama)) throw new ArgumentException("Nama user wajib diisi.", nameof(nama));
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("Sandi wajib diisi.", nameof(password));
        if (nama.Trim().Length > 17) throw new ArgumentException("Nama user maksimal 17 karakter.", nameof(nama));
        if (level.Trim().Length > 10) throw new ArgumentException("Level maksimal 10 karakter.", nameof(level));

        const string sql = """
            INSERT INTO pass (nama, level, sandi)
            VALUES (@nama, @level, @sandi)
            """;

        using var conn = Database.Open();
        return conn.Execute(sql, new
        {
            nama = nama.Trim(),
            level = level.Trim(),
            sandi = LegacyCipher.Encrypt(password),
        });
    }
}
