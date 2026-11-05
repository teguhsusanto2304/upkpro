using UPKPro.Data;

namespace UPKPro.Security;

/// <summary>
/// User yang sedang login (di Delphi dibaca langsung dari mdutama.tbpass, mis. "Petugas: tbpassnama").
/// </summary>
public static class Session
{
    public static PassUser? CurrentUser { get; private set; }

    public static void SignIn(PassUser user) => CurrentUser = user;

    public static void SignOut() => CurrentUser = null;
}
