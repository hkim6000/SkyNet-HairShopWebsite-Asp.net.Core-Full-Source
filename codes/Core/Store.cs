using System.Text.Json;
using HairShop.Models;

namespace HairShop.Core
{
    public static class Store
    {
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim SessionGate = new SemaphoreSlim(1, 1);
        private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static DateTime configStamp = DateTime.MinValue;
        private static bool demoMode = true;

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string ConfigFile => Path.Combine(Root, "appConfig", "application.cfg");

        public static string InitialFile => Path.Combine(Root, "appConfig", "initial-admin.txt");

        private static string SessionFile => Path.Combine(Root, "temp", "admin", "sessions.json");

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim gate;
            private bool done;

            public Releaser(SemaphoreSlim g)
            {
                gate = g;
            }

            public void Dispose()
            {
                if (!done)
                {
                    done = true;
                    gate.Release();
                }
            }
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Releaser(Gate);
        }

        public static async Task<T> Read<T>(string file, Func<T> empty)
        {
            if (!File.Exists(file))
            {
                return empty();
            }
            try
            {
                string json = await File.ReadAllTextAsync(file);
                return JsonSerializer.Deserialize<T>(json, Opts) ?? empty();
            }
            catch (JsonException)
            {
                return empty();
            }
        }

        public static async Task Write<T>(string file, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? DataDir);
            string tmp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(value, Opts));
            File.Move(tmp, file, true);
        }

        private static string DataFile(string name)
        {
            return Path.Combine(DataDir, name + ".json");
        }

        public static async Task<SiteData> LoadSite()
        {
            SiteData site = await Read(DataFile("site"), () => new SiteData());
            Normalize(site);
            if (Blocks.Ensure(site))
            {
                await Write(DataFile("site"), site);
            }
            return site;
        }

        private static void Normalize(SiteData site)
        {
            while (site.Hours.Count < 7)
            {
                site.Hours.Add(new DayHours());
            }
            foreach (Stylist s in site.Stylists)
            {
                while (s.Week.Count < 7)
                {
                    s.Week.Add(new WorkDay());
                }
            }
            if (site.TipPresets.Count != 3)
            {
                site.TipPresets = new List<int> { 15, 18, 20 };
            }
        }

        public static Task SaveSite(SiteData site)
        {
            return Write(DataFile("site"), site);
        }

        public static Task<List<Guest>> LoadGuests()
        {
            return Read(DataFile("guests"), () => new List<Guest>());
        }

        public static Task SaveGuests(List<Guest> list)
        {
            return Write(DataFile("guests"), list);
        }

        public static Task<List<Booking>> LoadBookings()
        {
            return Read(DataFile("bookings"), () => new List<Booking>());
        }

        public static Task SaveBookings(List<Booking> list)
        {
            return Write(DataFile("bookings"), list);
        }

        public static Task<List<ContactMessage>> LoadMessages()
        {
            return Read(DataFile("messages"), () => new List<ContactMessage>());
        }

        public static Task SaveMessages(List<ContactMessage> list)
        {
            return Write(DataFile("messages"), list);
        }

        public static Task<List<Staff>> LoadStaff()
        {
            return Read(DataFile("staff"), () => new List<Staff>());
        }

        public static Task SaveStaff(List<Staff> list)
        {
            return Write(DataFile("staff"), list);
        }

        public static async Task Log(string userId, string action, string target, bool ok)
        {
            await LogGate.WaitAsync();
            try
            {
                List<ActivityEntry> list = await Read(DataFile("activity"), () => new List<ActivityEntry>());
                list.Insert(0, new ActivityEntry { When = DateTime.UtcNow, UserId = userId, Action = action, Target = target.Length > 160 ? target.Substring(0, 160) : target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write(DataFile("activity"), list);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static Task<List<ActivityEntry>> LoadLog()
        {
            return Read(DataFile("activity"), () => new List<ActivityEntry>());
        }

        public static async Task<List<Session>> Sessions(Func<List<Session>, bool>? change = null)
        {
            await SessionGate.WaitAsync();
            try
            {
                List<Session> list = await Read(SessionFile, () => new List<Session>());
                int before = list.Count;
                list.RemoveAll(s => s.Expires < DateTime.UtcNow);
                bool dirty = before != list.Count;
                if (change != null && change(list))
                {
                    dirty = true;
                }
                if (dirty)
                {
                    await Write(SessionFile, list);
                }
                return list;
            }
            finally
            {
                SessionGate.Release();
            }
        }

        public static bool Demo()
        {
            if (!File.Exists(ConfigFile))
            {
                return false;
            }
            DateTime stamp = File.GetLastWriteTimeUtc(ConfigFile);
            if (stamp != configStamp)
            {
                string line = File.ReadLines(ConfigFile).Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("app.app.runmode", StringComparison.Ordinal)) ?? string.Empty;
                string[] part = line.Split('|', 2);
                demoMode = part.Length == 2 && part[1].Trim() == "0";
                configStamp = stamp;
            }
            return demoMode;
        }

        public static async Task<Staff> DemoManager()
        {
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == "u-demo");
                if (u == null)
                {
                    u = new Staff { Id = "u-demo", Login = "demo", Name = "Demo manager", Title = "Manager", Admin = true, Active = true };
                    staff.Add(u);
                    await SaveStaff(staff);
                }
                return u;
            }
        }

        public static async Task EnsureFirstManager()
        {
            using (await Hold())
            {
                List<Staff> staff = await LoadStaff();
                if (staff.Any(u => u.Admin && u.Active && !u.MustChange && u.Hash != string.Empty && u.Id != "u-demo"))
                {
                    return;
                }
                if (File.Exists(InitialFile) && staff.Any(u => u.Admin && u.Active && u.MustChange))
                {
                    return;
                }
                string pass = Access.NewPassword();
                Staff? u = staff.FirstOrDefault(x => x.Login == "admin");
                if (u == null)
                {
                    u = new Staff { Id = "u-admin", Login = "admin", Name = "Administrator", Title = "Manager" };
                    staff.Insert(0, u);
                }
                u.Admin = true;
                u.Active = true;
                u.MustChange = true;
                u.Hash = Access.HashPassword(pass);
                await SaveStaff(staff);
                Directory.CreateDirectory(Path.GetDirectoryName(InitialFile) ?? Root);
                await File.WriteAllTextAsync(InitialFile, "user: admin" + Environment.NewLine + "password: " + pass + Environment.NewLine);
            }
        }

        public static string InitialPassword()
        {
            if (!File.Exists(InitialFile))
            {
                return string.Empty;
            }
            string line = File.ReadAllLines(InitialFile).FirstOrDefault(l => l.StartsWith("password: ", StringComparison.Ordinal)) ?? string.Empty;
            return line.Length > 10 ? line.Substring(10).Trim() : string.Empty;
        }

        public static void DropInitial()
        {
            if (File.Exists(InitialFile))
            {
                File.Delete(InitialFile);
            }
        }
    }
}
