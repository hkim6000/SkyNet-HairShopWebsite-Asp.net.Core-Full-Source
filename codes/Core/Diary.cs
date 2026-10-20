using System.Globalization;
using System.Security.Cryptography;
using HairShop.Models;

namespace HairShop.Core
{
    public sealed class Quote
    {
        public decimal ServiceTotal { get; init; }
        public decimal Discount { get; init; }
        public decimal Tip { get; init; }
        public decimal Total { get; init; }
        public string TipMode { get; init; } = "none";
        public string Error { get; init; } = string.Empty;
    }

    public static class Diary
    {
        private const string RefLetters = "ACDEFGHJKLMNPQRTUVWXY34679";

        public static readonly string[] Statuses = { "booked", "arrived", "done", "noshow", "cancelled" };

        public static bool Live(Booking b)
        {
            return b.Status is "booked" or "arrived" or "done";
        }

        public static int Dow(DateTime d)
        {
            return ((int)d.DayOfWeek + 6) % 7;
        }

        public static int Min(string? hhmm)
        {
            if (!TimeSpan.TryParseExact((hhmm ?? string.Empty).Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out TimeSpan t) || t.TotalMinutes >= 24 * 60)
            {
                return -1;
            }
            return (int)t.TotalMinutes;
        }

        public static string Hhmm(int m)
        {
            return (m / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (m % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        public static DateTime End(Booking b)
        {
            return b.Start.AddMinutes(b.Minutes);
        }

        public static (int From, int To)? SalonDay(SiteData site, DateTime day)
        {
            DayHours h = site.Hours[Dow(day)];
            int a = Min(h.From);
            int b = Min(h.To);
            return h.Open && a >= 0 && b > a ? (a, b) : null;
        }

        public static bool IsOff(Stylist s, DateTime day)
        {
            foreach (TimeOff o in s.Off)
            {
                DateTime? a = Ui.ParseDay(o.From);
                DateTime? b = Ui.ParseDay(o.To.Length > 0 ? o.To : o.From);
                if (a != null && b != null && day.Date >= a.Value && day.Date <= b.Value)
                {
                    return true;
                }
            }
            return false;
        }

        public static List<(int From, int To)> Windows(SiteData site, Stylist s, DateTime day)
        {
            List<(int, int)> list = new List<(int, int)>();
            (int From, int To)? salon = SalonDay(site, day);
            if (salon == null || !s.Active || IsOff(s, day))
            {
                return list;
            }
            WorkDay w = s.Week[Dow(day)];
            int a = Math.Max(Min(w.From), salon.Value.From);
            int b = Math.Min(Min(w.To), salon.Value.To);
            if (!w.On || Min(w.From) < 0 || Min(w.To) < 0 || b <= a)
            {
                return list;
            }
            int ba = Min(w.BreakFrom);
            int bb = Min(w.BreakTo);
            if (ba > a && bb > ba && bb < b)
            {
                list.Add((a, ba));
                list.Add((bb, b));
            }
            else
            {
                list.Add((a, b));
            }
            return list;
        }

        public static bool Does(Stylist s, string serviceId)
        {
            return s.Active && s.Services.Contains(serviceId);
        }

        public static List<Stylist> For(SiteData site, string serviceId)
        {
            return site.Stylists.Where(s => Does(s, serviceId)).OrderBy(s => s.Order).ToList();
        }

        public static List<int> Free(SiteData site, List<Booking> bookings, Stylist s, SalonService svc, DateTime day, DateTime now, int lead, string ignoreId)
        {
            List<int> free = new List<int>();
            int step = site.SlotStep is 10 or 15 or 20 or 30 ? site.SlotStep : 15;
            int buf = Math.Clamp(site.Buffer, 0, 60);
            int dur = Math.Max(5, svc.Minutes);
            int earliest;
            if (day.Date < now.Date)
            {
                return free;
            }
            earliest = day.Date == now.Date ? (int)Math.Ceiling(now.TimeOfDay.TotalMinutes) + lead : 0;
            List<(int A, int B)> busy = bookings.Where(b => Live(b) && b.StylistId == s.Id && b.Start.Date == day.Date && b.Id != ignoreId).Select(b => ((int)b.Start.TimeOfDay.TotalMinutes, (int)b.Start.TimeOfDay.TotalMinutes + b.Minutes)).ToList();
            foreach ((int a, int b) in Windows(site, s, day))
            {
                for (int t = a; t + dur <= b; t += step)
                {
                    if (t < earliest)
                    {
                        continue;
                    }
                    int start = t;
                    if (busy.Any(x => start < x.B + buf && x.A < start + dur + buf))
                    {
                        continue;
                    }
                    free.Add(t);
                }
            }
            return free;
        }

        public static string Problem(SiteData site, List<Booking> bookings, Stylist? s, SalonService? svc, DateTime start, DateTime now, bool staff, string ignoreId)
        {
            if (svc == null || !svc.Active && !staff)
            {
                return "Choose a service.";
            }
            if (s == null || !s.Active)
            {
                return "Choose a stylist.";
            }
            if (!Does(s, svc.Id))
            {
                return s.Name + " does not offer " + svc.Name + ".";
            }
            if (!staff && start.Date > now.Date.AddDays(Math.Clamp(site.HorizonDays, 1, 365)))
            {
                return "Online booking opens " + site.HorizonDays + " days ahead.";
            }
            if (SalonDay(site, start) == null)
            {
                return "The salon is closed on " + Ui.Day(start) + ".";
            }
            if (Windows(site, s, start).Count == 0)
            {
                return s.Name + " is not working on " + Ui.Day(start) + ".";
            }
            int t = (int)start.TimeOfDay.TotalMinutes;
            int lead = staff ? -15 : Math.Clamp(site.LeadMinutes, 0, 2880);
            if (!Free(site, bookings, s, svc, start.Date, now, lead, ignoreId).Contains(t))
            {
                bool inHours = Windows(site, s, start).Any(w => t >= w.From && t + svc.Minutes <= w.To);
                if (!inHours)
                {
                    return Ui.Time(start) + " is outside " + s.Name + "'s working hours.";
                }
                if (start < now.AddMinutes(lead))
                {
                    return "That time has already passed. Choose a later time.";
                }
                return "That time was just taken. Please choose another time.";
            }
            return string.Empty;
        }

        public static List<Booking> Clashes(SiteData site, List<Booking> bookings, Stylist s)
        {
            List<Booking> outp = new List<Booking>();
            foreach (Booking b in bookings.Where(x => x.StylistId == s.Id && x.Status == "booked" && x.Start >= DateTime.Now).OrderBy(x => x.Start))
            {
                int t = (int)b.Start.TimeOfDay.TotalMinutes;
                if (!Windows(site, s, b.Start).Any(w => t >= w.From && t + b.Minutes <= w.To))
                {
                    outp.Add(b);
                }
            }
            return outp;
        }

        public static string NewRef(List<Booking> bookings)
        {
            for (int k = 0; k < 50; k++)
            {
                char[] c = new char[5];
                for (int i = 0; i < c.Length; i++)
                {
                    c[i] = RefLetters[RandomNumberGenerator.GetInt32(RefLetters.Length)];
                }
                string r = "HS-" + new string(c);
                if (!bookings.Any(b => b.Ref == r))
                {
                    return r;
                }
            }
            return "HS-" + DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture).Substring(8);
        }

        public static string NewId(string prefix)
        {
            return prefix + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        }

        public static string Digits(string? phone)
        {
            return new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        public static bool IsPhone(string v)
        {
            int n = Digits(v).Length;
            return n >= 7 && n <= 15 && v.Length <= 24 && v.All(ch => char.IsDigit(ch) || " +-().".Contains(ch));
        }

        public static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        public static Guest? Match(List<Guest> guests, string phone, string email)
        {
            string d = Digits(phone);
            string e = email.Trim().ToLowerInvariant();
            Guest? g = d.Length >= 7 ? guests.FirstOrDefault(x => Digits(x.Phone) == d) : null;
            if (g == null && e.Length > 0)
            {
                g = guests.FirstOrDefault(x => x.Email.Trim().ToLowerInvariant() == e);
            }
            return g;
        }

        public static Guest Upsert(List<Guest> guests, string name, string phone, string email, string by)
        {
            Guest? g = Match(guests, phone, email);
            if (g == null)
            {
                g = new Guest { Id = NewId("g-"), Name = name, Phone = phone, Email = email, Created = DateTime.UtcNow, By = by };
                guests.Add(g);
                return g;
            }
            if (g.Phone.Length == 0 && phone.Length > 0)
            {
                g.Phone = phone;
            }
            if (g.Email.Length == 0 && email.Length > 0)
            {
                g.Email = email;
            }
            if (g.Name.Length == 0)
            {
                g.Name = name;
            }
            return g;
        }

        public static Quote Price(SiteData site, List<PayLine> lines, string? discount, string? tipMode, string? tipAmount)
        {
            decimal total = lines.Sum(l => l.Price);
            decimal disc = 0;
            string dv = (discount ?? string.Empty).Trim();
            if (dv.Length > 0)
            {
                decimal? d = Ui.Dec(dv);
                if (d == null || d < 0)
                {
                    return new Quote { ServiceTotal = total, Error = "The discount must be an amount like 10 or 12.50." };
                }
                if (d > total)
                {
                    return new Quote { ServiceTotal = total, Error = "The discount cannot be more than the service total." };
                }
                disc = Math.Round(d.Value, 2);
            }
            string mode = (tipMode ?? "none").Trim();
            decimal tip = 0;
            if (mode == "custom")
            {
                decimal? t = Ui.Dec(tipAmount);
                if (t == null || t < 0 || t > 1000)
                {
                    return new Quote { ServiceTotal = total, Discount = disc, TipMode = mode, Error = "Enter a tip between $0 and $1,000." };
                }
                tip = Math.Round(t.Value, 2);
            }
            else if (int.TryParse(mode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pct) && site.TipPresets.Contains(pct))
            {
                tip = Math.Round(total * pct / 100m, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                mode = "none";
            }
            return new Quote { ServiceTotal = total, Discount = disc, Tip = tip, TipMode = mode, Total = total - disc + tip };
        }
    }
}
