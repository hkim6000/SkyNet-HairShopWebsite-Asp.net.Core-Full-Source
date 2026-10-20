using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using HairShop.Models;

namespace HairShop.Core
{
    public sealed class Ctx
    {
        public Staff U { get; init; } = new Staff();
        public string Token { get; init; } = string.Empty;
    }

    public static class Auth
    {
        private static readonly ConcurrentDictionary<string, List<DateTime>> Misses = new ConcurrentDictionary<string, List<DateTime>>();

        public static bool Paused(string login)
        {
            if (!Misses.TryGetValue(login.ToLowerInvariant(), out List<DateTime>? list))
            {
                return false;
            }
            lock (list)
            {
                list.RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Miss(string login)
        {
            List<DateTime> list = Misses.GetOrAdd(login.ToLowerInvariant(), _ => new List<DateTime>());
            lock (list)
            {
                list.Add(DateTime.UtcNow);
            }
        }

        public static void Clear(string login)
        {
            Misses.TryRemove(login.ToLowerInvariant(), out _);
        }

        public static async Task<string> Open(Staff u)
        {
            string tok = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            await Store.Sessions(list =>
            {
                list.Add(new Session { Token = tok, UserId = u.Id, Expires = DateTime.UtcNow.AddHours(8) });
                return true;
            });
            return tok;
        }

        public static async Task Close(string tok)
        {
            await Store.Sessions(list => list.RemoveAll(s => s.Token == tok) > 0);
        }

        public static async Task CloseUser(string userId, string keep)
        {
            await Store.Sessions(list => list.RemoveAll(s => s.UserId == userId && s.Token != keep) > 0);
        }

        public static async Task<Ctx?> Check(string? token)
        {
            string tok = (token ?? string.Empty).Trim();
            if (tok.Length != 32)
            {
                return null;
            }
            Session? found = null;
            await Store.Sessions(list =>
            {
                found = list.FirstOrDefault(s => s.Token == tok);
                if (found != null && found.Expires < DateTime.UtcNow.AddHours(8).AddMinutes(-5))
                {
                    found.Expires = DateTime.UtcNow.AddHours(8);
                    return true;
                }
                return false;
            });
            if (found == null)
            {
                return null;
            }
            List<Staff> staff = await Store.LoadStaff();
            Staff? u = staff.FirstOrDefault(x => x.Id == found.UserId);
            if (u == null || !u.Active || u.MustChange)
            {
                return null;
            }
            if (u.Id == "u-demo" && !Store.Demo())
            {
                await Close(tok);
                return null;
            }
            return new Ctx { U = u, Token = tok };
        }

        public static async Task<string> Menu(string px, string page, Ctx c)
        {
            int unread = 0;
            int waiting = 0;
            int inChair = 0;
            if (Access.Can(c.U, "messages", 'V'))
            {
                unread = (await Store.LoadMessages()).Count(m => !m.Read);
            }
            if (Access.Can(c.U, "checkout", 'V') || Access.Can(c.U, "bookings", 'V'))
            {
                string own = Access.OwnStylist(c.U);
                List<Booking> bookings = await Store.LoadBookings();
                waiting = bookings.Count(b => b.Status == "done" && b.Paid == null && (own == string.Empty || b.StylistId == own));
                inChair = bookings.Count(b => b.Status == "arrived" && b.Start.Date == DateTime.Today && (own == string.Empty || b.StylistId == own));
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(Item(px, "Admin", "Today", page, 0));
            string group = string.Empty;
            foreach (Area a in Access.Areas)
            {
                if (!Access.Can(c.U, a.Key, 'V'))
                {
                    continue;
                }
                if (a.Group != group)
                {
                    group = a.Group;
                    sb.Append("<span class=\"" + px + "-mg\">" + Ui.Esc(group) + "</span>");
                }
                int badge = a.Key == "messages" ? unread : a.Key == "checkout" ? waiting : a.Key == "bookings" ? inChair : 0;
                sb.Append(Item(px, a.Page, a.Name, page, badge));
            }
            sb.Append("<span class=\"" + px + "-mg\">You</span>");
            if (!Access.Can(c.U, "activity", 'V'))
            {
                sb.Append(Item(px, "AdminActivity", "My activity", page, 0));
            }
            sb.Append(Item(px, "AdminAccount", "My account", page, 0));
            return sb.ToString();
        }

        private static string Item(string px, string href, string label, string page, int badge)
        {
            return "<a class=\"" + px + "-mi" + (href == page ? " " + px + "-on" : string.Empty) + "\" href=\"" + href + "\"" + (href == page ? " aria-current=\"page\"" : string.Empty) + "><span>" + Ui.Esc(label) + "</span>" + (badge > 0 ? "<i class=\"" + px + "-badge\">" + badge + "</i>" : string.Empty) + "</a>";
        }

        public static string Who(string px, Ctx c)
        {
            string role = c.U.Admin ? "Manager" : c.U.Title.Length > 0 ? c.U.Title : "Staff";
            return "<b>" + Ui.Esc(c.U.Name) + "</b><small>" + Ui.Esc(role) + (Store.Demo() ? " &middot; Demo" : string.Empty) + "</small>";
        }
    }
}
