using System.Globalization;
using System.Text;
using System.Text.Json;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminTips : WebPage
    {
        private const string AreaKey = "tips";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Tips by stylist | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private (DateTime From, DateTime To, string Sty) Range(Ctx c, SiteData site)
        {
            DateTime from = Ui.ParseDay(GetDataValue("from")) ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime to = Ui.ParseDay(GetDataValue("to")) ?? DateTime.Today;
            if (to < from)
            {
                to = from;
            }
            string sty = Ui.Clip(GetDataValue("sty"), 40);
            if (!site.Stylists.Any(s => s.Id == sty))
            {
                sty = string.Empty;
            }
            string own = Access.OwnStylist(c.U);
            return (from, to, own.Length > 0 ? own : sty);
        }

        private static List<Booking> Paid(List<Booking> bookings, DateTime from, DateTime to, string sty)
        {
            return bookings.Where(b => b.Paid != null && b.Paid.When.ToLocalTime().Date >= from && b.Paid.When.ToLocalTime().Date <= to && (sty == string.Empty || b.StylistId == sty)).OrderBy(b => b.Paid!.When).ToList();
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
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("tp-main", NoAccess("the tips report"));
                return response;
            }
            (DateTime from, DateTime to, string sty) = Range(c, site);
            bool own = Access.OwnStylist(c.U).Length > 0;
            StringBuilder sb = new StringBuilder("<div class=\"tp-ph\"><div><h1>Tips by stylist</h1><p>Tips from paid visits, by the day they were paid. Percentages are of the service total before any discount.</p></div><button type=\"button\" class=\"tp-btn tp-btn-ghost\" onclick=\"AdminTipsJs.csv()\">Download CSV</button></div>");
            sb.Append("<div class=\"tp-tools\"><div class=\"tp-chips\"><button type=\"button\" class=\"tp-chip\" onclick=\"AdminTipsJs.preset('today')\">Today</button><button type=\"button\" class=\"tp-chip\" onclick=\"AdminTipsJs.preset('week')\">This week</button><button type=\"button\" class=\"tp-chip\" onclick=\"AdminTipsJs.preset('month')\">This month</button><button type=\"button\" class=\"tp-chip\" onclick=\"AdminTipsJs.preset('last')\">Last month</button></div><label class=\"tp-mini\">From <input type=\"date\" id=\"tp-from\" value=\"" + Ui.Iso(from) + "\" onchange=\"AdminTipsJs.go()\"></label><label class=\"tp-mini\">To <input type=\"date\" id=\"tp-to\" value=\"" + Ui.Iso(to) + "\" onchange=\"AdminTipsJs.go()\"></label>");
            if (!own)
            {
                sb.Append("<select id=\"tp-sty\" onchange=\"AdminTipsJs.go()\" aria-label=\"Stylist\"><option value=\"\">All stylists</option>" + string.Concat(site.Stylists.OrderBy(s => s.Order).Select(s => "<option value=\"" + Ui.Esc(s.Id) + "\">" + Ui.Esc(s.Name) + "</option>")) + "</select>");
            }
            sb.Append("</div><div id=\"tp-report\">" + ReportHtml(site, await Store.LoadBookings(), from, to, sty) + "</div>");
            response.SetElementContents("tp-main", sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Report()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "tips report", string.Empty, "You do not have access to the tips report.");
            }
            SiteData site = await Store.LoadSite();
            (DateTime from, DateTime to, string sty) = Range(c, site);
            response.SetElementContents("tp-report", ReportHtml(site, await Store.LoadBookings(), from, to, sty));
            return response;
        }

        private static string Pct(decimal tip, decimal basis)
        {
            return basis <= 0 ? "&ndash;" : (tip / basis * 100m).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static string ReportHtml(SiteData site, List<Booking> bookings, DateTime from, DateTime to, string sty)
        {
            List<Booking> rows = Paid(bookings, from, to, sty);
            StringBuilder sb = new StringBuilder("<p class=\"tp-hint tp-range\">" + Ui.Day(from) + (to != from ? " &ndash; " + Ui.Day(to) : string.Empty) + ", " + to.Year + " &middot; " + Ui.Count(rows.Count, "paid visit", "paid visits") + "</p>");
            if (rows.Count == 0)
            {
                return sb.Append("<div class=\"tp-panel tp-empty\">No paid visits in this period.</div>").ToString();
            }
            decimal tips = rows.Sum(b => b.Paid!.Tip);
            decimal basis = rows.Sum(b => b.Paid!.ServiceTotal);
            sb.Append("<div class=\"tp-stats\"><div class=\"tp-stat\"><b>" + Ui.Money(tips) + "</b><span>tips in total</span></div><div class=\"tp-stat\"><b>" + Ui.Money(rows.Where(b => b.Paid!.Method == "card").Sum(b => b.Paid!.Tip)) + "</b><span>tips by card</span></div><div class=\"tp-stat\"><b>" + Ui.Money(rows.Where(b => b.Paid!.Method == "cash").Sum(b => b.Paid!.Tip)) + "</b><span>tips in cash</span></div><div class=\"tp-stat\"><b>" + Pct(tips, basis) + "</b><span>average tip</span></div></div>");
            sb.Append("<div class=\"tp-wrapt\"><table class=\"tp-table\"><thead><tr><th>Stylist</th><th class=\"tp-num\">Paid visits</th><th class=\"tp-num\">Services</th><th class=\"tp-num\">Tips by card</th><th class=\"tp-num\">Tips in cash</th><th class=\"tp-num\">Tips total</th><th class=\"tp-num\">Average tip</th></tr></thead><tbody>");
            foreach (IGrouping<string, Booking> g in rows.GroupBy(b => b.StylistId).OrderByDescending(g => g.Sum(b => b.Paid!.Tip)))
            {
                string name = site.Stylists.FirstOrDefault(s => s.Id == g.Key)?.Name ?? "Former stylist";
                decimal t = g.Sum(b => b.Paid!.Tip);
                sb.Append("<tr><td><b>" + Ui.Esc(name) + "</b></td><td class=\"tp-num\">" + g.Count() + "</td><td class=\"tp-num\">" + Ui.Money(g.Sum(b => b.Paid!.ServiceTotal - b.Paid.Discount)) + "</td><td class=\"tp-num\">" + Ui.Money(g.Where(b => b.Paid!.Method == "card").Sum(b => b.Paid!.Tip)) + "</td><td class=\"tp-num\">" + Ui.Money(g.Where(b => b.Paid!.Method == "cash").Sum(b => b.Paid!.Tip)) + "</td><td class=\"tp-num\"><b>" + Ui.Money(t) + "</b></td><td class=\"tp-num\">" + Pct(t, g.Sum(b => b.Paid!.ServiceTotal)) + "</td></tr>");
            }
            sb.Append("</tbody><tfoot><tr><td>All</td><td class=\"tp-num\">" + rows.Count + "</td><td class=\"tp-num\">" + Ui.Money(rows.Sum(b => b.Paid!.ServiceTotal - b.Paid.Discount)) + "</td><td class=\"tp-num\">" + Ui.Money(rows.Where(b => b.Paid!.Method == "card").Sum(b => b.Paid!.Tip)) + "</td><td class=\"tp-num\">" + Ui.Money(rows.Where(b => b.Paid!.Method == "cash").Sum(b => b.Paid!.Tip)) + "</td><td class=\"tp-num\">" + Ui.Money(tips) + "</td><td class=\"tp-num\">" + Pct(tips, basis) + "</td></tr></tfoot></table></div>");
            sb.Append("<h2 class=\"tp-dh\">Each payment</h2><div class=\"tp-wrapt\"><table class=\"tp-table\"><thead><tr><th>Paid</th><th>Guest</th><th>Stylist</th><th class=\"tp-num\">Services</th><th class=\"tp-num\">Tip</th><th>By</th></tr></thead><tbody>");
            foreach (Booking b in rows.OrderByDescending(b => b.Paid!.When).Take(300))
            {
                string name = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
                sb.Append("<tr><td>" + Ui.Esc(Ui.When(b.Paid!.When)) + "</td><td><a class=\"tp-link\" href=\"AdminGuests?g=" + Ui.Esc(b.GuestId) + "\">" + Ui.Esc(b.Name) + "</a></td><td>" + Ui.Esc(name) + "</td><td class=\"tp-num\">" + Ui.Money(b.Paid.ServiceTotal) + "</td><td class=\"tp-num\">" + Ui.Money(b.Paid.Tip) + (b.Paid.TipMode is "none" or "custom" ? string.Empty : "<small>" + b.Paid.TipMode + "%</small>") + "</td><td>" + b.Paid.Method + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + (rows.Count > 300 ? "<p class=\"tp-hint\">Showing the latest 300. Download the CSV for all.</p>" : string.Empty));
            return sb.ToString();
        }

        private static string Cell(string v)
        {
            return v.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 || v.StartsWith('=') || v.StartsWith('+') || v.StartsWith('-') || v.StartsWith('@') ? "\"" + (v.StartsWith('=') || v.StartsWith('+') || v.StartsWith('-') || v.StartsWith('@') ? "'" : string.Empty) + v.Replace("\"", "\"\"") + "\"" : v;
        }

        public async Task<ApiResponse> Csv()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "tips csv", string.Empty, "You do not have access to the tips report.");
            }
            SiteData site = await Store.LoadSite();
            (DateTime from, DateTime to, string sty) = Range(c, site);
            List<Booking> rows = Paid(await Store.LoadBookings(), from, to, sty);
            StringBuilder sb = new StringBuilder("paid_at,reference,guest,stylist,services,discount,tip_mode,tip,total,method\n");
            foreach (Booking b in rows)
            {
                string name = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
                Payment p = b.Paid!;
                sb.Append(string.Join(",", new[] { p.When.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), b.Ref, Cell(b.Name), Cell(name), Ui.Num(p.ServiceTotal), Ui.Num(p.Discount), p.TipMode, Ui.Num(p.Tip), Ui.Num(p.Total), p.Method }) + "\n");
            }
            await Store.Log(c.U.Id, "download tips csv", Ui.Iso(from) + " to " + Ui.Iso(to), true);
            response.ExecuteScript("AdminTipsJs.download('tips-" + Ui.Iso(from) + "-to-" + Ui.Iso(to) + ".csv'," + JsonSerializer.Serialize(sb.ToString()).Replace("<", "\\u003C") + ");");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminTipsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminTipsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"tp-panel\"><h1>No access</h1><p class=\"tp-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("tp-brand", Ui.Esc(site.Brand));
            response.SetElementContents("tp-menu", await Auth.Menu("tp", "AdminTips", c));
            response.SetElementContents("tp-who", Auth.Who("tp", c));
        }

    }
}
