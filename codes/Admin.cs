using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class Admin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Today | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        public async Task<ApiResponse> Load()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            await Shell(response, c, site);
            response.SetElementContents("db-main", await MainHtml(c, site));
            return response;
        }

        private static async Task<string> MainHtml(Ctx c, SiteData site)
        {
            DateTime now = DateTime.Now;
            string own = Access.OwnStylist(c.U);
            List<Booking> all = (await Store.LoadBookings()).Where(b => own == string.Empty || b.StylistId == own).ToList();
            List<Booking> today = all.Where(b => b.Start.Date == now.Date).OrderBy(b => b.Start).ToList();
            bool canB = Access.Can(c.U, "bookings", 'V');
            bool canC = Access.Can(c.U, "checkout", 'V');
            string hello = now.Hour < 12 ? "Good morning" : now.Hour < 17 ? "Good afternoon" : "Good evening";
            StringBuilder sb = new StringBuilder("<div class=\"db-ph\"><div><h1>" + hello + ", " + Ui.Esc(c.U.Name.Split(' ')[0]) + "</h1><p>" + Ui.LongDay(now) + (own.Length > 0 ? " &middot; your own bookings" : string.Empty) + "</p></div>");
            if (Access.Can(c.U, "bookings", 'E'))
            {
                sb.Append("<a class=\"db-btn\" href=\"AdminBookings?new=1\">New booking</a>");
            }
            sb.Append("</div>");
            if (canB || canC)
            {
                List<Booking> paid = all.Where(b => b.Paid != null && b.Paid.When.ToLocalTime().Date == now.Date).ToList();
                int live = today.Count(b => Diary.Live(b));
                sb.Append("<div class=\"db-stats\">");
                sb.Append(Stat(live.ToString(), "Visits today", "AdminBookings"));
                sb.Append(Stat(today.Count(b => b.Status == "arrived").ToString(), "In the salon now", "AdminBookings"));
                sb.Append(Stat(all.Count(b => b.Status == "done" && b.Paid == null).ToString(), "Waiting to pay", canC ? "AdminCheckout" : string.Empty));
                sb.Append(Stat(Ui.Money(paid.Sum(p => p.Paid!.ServiceTotal - p.Paid.Discount)), "Services paid today &middot; tips " + Ui.Money(paid.Sum(p => p.Paid!.Tip)), canC ? "AdminCheckout" : string.Empty));
                sb.Append("</div>");
            }
            sb.Append("<div class=\"db-cols\">");
            if (canB)
            {
                List<Booking> next = today.Where(b => b.Status is "booked" or "arrived" or "done" && (b.Status != "done" || b.Paid == null)).ToList();
                sb.Append("<section class=\"db-panel\"><div class=\"db-sh\"><h2>Today&rsquo;s visits</h2><a class=\"db-link\" href=\"AdminBookings\">Day view</a></div>");
                if (next.Count == 0)
                {
                    sb.Append("<p class=\"db-empty\">No visits left today.</p>");
                }
                else
                {
                    sb.Append("<ul class=\"db-agenda\">");
                    foreach (Booking b in next)
                    {
                        string sty = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
                        sb.Append("<li><time>" + Ui.Time(b.Start) + "</time><a href=\"AdminBookings?d=" + Ui.Iso(b.Start) + "&amp;b=" + Ui.Esc(b.Id) + "\"><b>" + Ui.Esc(b.Name) + "</b><small>" + Ui.Esc(b.ServiceName) + " &middot; " + Ui.Esc(sty) + "</small></a>" + Ui.Status("db", b.Status) + "</li>");
                    }
                    sb.Append("</ul>");
                }
                sb.Append("</section>");
            }
            sb.Append("<section class=\"db-panel\"><h2>Shortcuts</h2><div class=\"db-short\">");
            foreach (Area a in Access.Areas.Where(a => Access.Can(c.U, a.Key, 'V')))
            {
                sb.Append("<a href=\"" + a.Page + "\">" + Ui.Esc(a.Name) + " <span aria-hidden=\"true\">&rarr;</span></a>");
            }
            sb.Append("<a href=\"Home\" target=\"_blank\" rel=\"noopener\">View the website <span aria-hidden=\"true\">&nearr;</span></a></div>");
            if (Access.Can(c.U, "messages", 'V'))
            {
                int unread = (await Store.LoadMessages()).Count(m => !m.Read);
                sb.Append("<p class=\"db-hint db-mt\">" + (unread == 0 ? "No unread messages." : "<a class=\"db-link\" href=\"AdminMessages\">" + Ui.Count(unread, "unread message", "unread messages") + "</a>") + "</p>");
            }
            sb.Append("</section></div>");
            return sb.ToString();
        }

        private static string Stat(string value, string label, string href)
        {
            string inner = "<b>" + Ui.Esc(value) + "</b><span>" + label + "</span>";
            return href.Length > 0 ? "<a class=\"db-stat\" href=\"" + href + "\">" + inner + "</a>" : "<div class=\"db-stat\">" + inner + "</div>";
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"db-panel\"><h1>No access</h1><p class=\"db-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("db-brand", Ui.Esc(site.Brand));
            response.SetElementContents("db-menu", await Auth.Menu("db", "Admin", c));
            response.SetElementContents("db-who", Auth.Who("db", c));
        }

    }
}
