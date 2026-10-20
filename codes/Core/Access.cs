using System.Security.Cryptography;
using HairShop.Models;

namespace HairShop.Core
{
    public sealed class Area
    {
        public string Key { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Page { get; init; } = string.Empty;
        public string Extra { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public bool AdminOnly { get; init; }
        public bool NoEdit { get; init; }
        public bool Owned { get; init; }
    }

    public static class Access
    {
        public static readonly Area[] Areas =
        {
            new Area { Key = "bookings", Name = "Bookings", Page = "AdminBookings", Extra = "Cancel and no-show", Group = "Salon", Owned = true },
            new Area { Key = "checkout", Name = "Checkout", Page = "AdminCheckout", Extra = "Reopen a paid visit", Group = "Salon", Owned = true },
            new Area { Key = "guests", Name = "Guests", Page = "AdminGuests", Extra = "Delete a guest", Group = "Salon", Owned = true },
            new Area { Key = "tips", Name = "Tips report", Page = "AdminTips", Group = "Salon", NoEdit = true, Owned = true },
            new Area { Key = "services", Name = "Services", Page = "AdminServices", Group = "Setup" },
            new Area { Key = "stylists", Name = "Stylists", Page = "AdminStylists", Extra = "Working hours and days off", Group = "Setup" },
            new Area { Key = "pages", Name = "Pages", Page = "AdminPages", Group = "Website" },
            new Area { Key = "media", Name = "Media library", Page = "AdminMedia", Extra = "Delete files", Group = "Website" },
            new Area { Key = "messages", Name = "Messages", Page = "AdminMessages", Extra = "Delete messages", Group = "Website" },
            new Area { Key = "activity", Name = "Activity log", Page = "AdminActivity", Group = "Admin", NoEdit = true },
            new Area { Key = "settings", Name = "Settings", Page = "AdminSettings", Group = "Admin", AdminOnly = true },
            new Area { Key = "users", Name = "Users and rights", Page = "AdminUsers", Group = "Admin", AdminOnly = true }
        };

        private const string Pool = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

        public static Area? Find(string key)
        {
            return Areas.FirstOrDefault(a => a.Key == key);
        }

        public static string Clean(Area a, string? level)
        {
            if (a.AdminOnly)
            {
                return string.Empty;
            }
            string v = (level ?? string.Empty).ToUpperInvariant();
            if (!v.Contains('V'))
            {
                return string.Empty;
            }
            if (a.NoEdit || !v.Contains('E'))
            {
                return "V";
            }
            return a.Extra.Length > 0 && v.Contains('X') ? "VEX" : "VE";
        }

        public static Dictionary<string, string> CleanAll(Dictionary<string, string> perms)
        {
            Dictionary<string, string> outp = new Dictionary<string, string>();
            foreach (Area a in Areas)
            {
                string v = Clean(a, perms.TryGetValue(a.Key, out string? x) ? x : string.Empty);
                if (v.Length > 0)
                {
                    outp[a.Key] = v;
                }
            }
            return outp;
        }

        public static string Level(Staff u, string area)
        {
            Area? a = Find(area);
            if (a == null)
            {
                return string.Empty;
            }
            if (u.Admin)
            {
                return a.NoEdit ? "V" : a.Extra.Length > 0 ? "VEX" : "VE";
            }
            return Clean(a, u.Perms.TryGetValue(area, out string? v) ? v : string.Empty);
        }

        public static bool Can(Staff u, string area, char right)
        {
            return Level(u, area).Contains(right);
        }

        public static string OwnStylist(Staff u)
        {
            return !u.Admin && u.Own && u.StylistId.Length > 0 ? u.StylistId : string.Empty;
        }

        public static string NewPassword()
        {
            char[] c = new char[14];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = i == 4 || i == 9 ? '-' : Pool[RandomNumberGenerator.GetInt32(Pool.Length)];
            }
            return new string(c);
        }

        public static string HashPassword(string pass)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(pass, salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string pass, string stored)
        {
            string[] p = (stored ?? string.Empty).Split('$');
            if (p.Length != 4 || p[0] != "pbkdf2" || !int.TryParse(p[1], out int rounds))
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(p[2]);
                byte[] want = Convert.FromBase64String(p[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(pass ?? string.Empty, salt, rounds, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
