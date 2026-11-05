namespace UPKPro.Security;

/// <summary>
/// Port cipher dari aplikasi Delphi agar data lama (kolom pass.sandi, info.trd, dll.) tetap terbaca.
/// <list type="bullet">
/// <item><see cref="Encrypt"/> = <c>eee()</c> (Unit_utama1.pas:388 / U_SetupRekening.pas:348)</item>
/// <item><see cref="Decrypt"/> = <c>dxyz()</c> / <c>ddd()</c> (unit_utama.pas:172 / :223) — keduanya identik</item>
/// </list>
/// Catatan: ini obfuscation, bukan enkripsi yang aman. Dipertahankan hanya demi kompatibilitas data.
/// </summary>
public static class LegacyCipher
{
    /// <summary>Kunci yang dipakai Delphi untuk kolom pass.sandi.</summary>
    public const string PasswordKey = "YjOWbtf6RM52Zh4wq";

    private const string Codes64 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>Padanan Delphi <c>eee(Data, SecurityString, MinV, MaxV)</c>.</summary>
    public static string Encrypt(string data, string key = PasswordKey, int minV = 0, int maxV = 5)
    {
        if (minV > maxV) (minV, maxV) = (maxV, minV);
        if (minV < 0) minV = 0;
        if (maxV > 100) maxV = 100;
        if (!IsValidKey(key)) return "";

        // Karakter "sampah" = Codes64 tanpa karakter kunci; diabaikan saat dekripsi.
        var noise = new string(Codes64.Where(c => !key.Contains(c)).ToArray());

        var ss = key;
        var body = new System.Text.StringBuilder(data.Length * 2);
        foreach (var ch in data)
        {
            body.Append(ss[ch % 13]);
            ss = RotateRight(ss);
            body.Append(ss[ch / 13]);
            ss = RotateRight(ss);
        }

        var rnd = Random.Shared;
        var result = new System.Text.StringBuilder();
        result.Append(RandomString(noise, rnd.Next(maxV - minV) + minV + 1));
        foreach (var c in body.ToString())
        {
            result.Append(c);
            result.Append(RandomString(noise, rnd.Next(maxV - minV) + minV));
        }
        return result.ToString();
    }

    /// <summary>
    /// Padanan Delphi <c>dxyz()</c>/<c>ddd()</c>. Mengembalikan <c>null</c> jika data/kunci tidak valid
    /// (Delphi mengembalikan #1).
    /// </summary>
    public static string? Decrypt(string data, string key = PasswordKey)
    {
        if (!IsValidKey(key)) return null;

        var filtered = new string(data.Where(key.Contains).ToArray());
        if (filtered.Length % 2 != 0) return null;

        var ss = key;
        var result = new System.Text.StringBuilder(filtered.Length / 2);
        for (var i = 0; i < filtered.Length / 2; i++)
        {
            var x = ss.IndexOf(filtered[i * 2]);
            if (x < 0) return null;
            ss = RotateRight(ss);
            var x2 = ss.IndexOf(filtered[i * 2 + 1]);
            if (x2 < 0) return null;
            ss = RotateRight(ss);
            result.Append((char)(x + x2 * 13));
        }
        return result.ToString();
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length < 16) return false;
        for (var i = 0; i < key.Length; i++)
        {
            if (key.IndexOf(key[i], i + 1) >= 0) return false; // karakter kunci harus unik
            if (!Codes64.Contains(key[i])) return false;
        }
        return true;
    }

    // Delphi: ss := Copy(ss, Length(ss), 1) + Copy(ss, 1, Length(ss) - 1)
    private static string RotateRight(string s) => s[^1] + s[..^1];

    // Delphi: MakeRNDString — ambil karakter acak tanpa pengulangan.
    private static string RandomString(string chars, int count)
    {
        var pool = new List<char>(chars);
        var sb = new System.Text.StringBuilder(count);
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var x = Random.Shared.Next(pool.Count);
            sb.Append(pool[x]);
            pool.RemoveAt(x);
        }
        return sb.ToString();
    }
}
