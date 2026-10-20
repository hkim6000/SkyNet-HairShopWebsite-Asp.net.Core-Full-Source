using System.Globalization;
using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminBookings : WebPage
    {
        private const string AreaKey = "bookings";
        private const int Size = 20;
        private const double Px = 1.6;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Bookings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private sealed class Filter
        {
            public string View { get; set; } = "day";
            public DateTime Day { get; set; } = DateTime.Today;
            public string Sty { get; set; } = string.Empty;
            public string Stat { get; set; } = string.Empty;
            public string Term { get; set; } = string.Empty;
            public DateTime From { get; set; } = DateTime.Today;
            public DateTime To { get; set; } = DateTime.Today.AddDays(30);
            public int Page { get; set; } = 1;
        }

        private Filter ReadFilter(Ctx c, SiteData site)
        {
            Filter f = new Filter();
            f.View = GetDataValue("view") == "list" ? "list" : "day";
            f.Day = Ui.ParseDay(GetDataValue("day")) ?? DateTime.Today;
            string sty = (GetDataValue("sty") ?? string.Empty).Trim();
            f.Sty = site.Stylists.Any(s => s.Id == sty) ? sty : string.Empty;
            string own = Access.OwnStylist(c.U);
            if (own.Length > 0)
            {
                f.Sty = own;
            }
            string stat = (GetDataValue("stat") ?? string.Empty).Trim();
            f.Stat = Diary.Statuses.Contains(stat) || stat == "unpaid" ? stat : string.Empty;
            f.Term = Ui.Clip(GetDataValue("term"), 40);
            f.From = Ui.ParseDay(GetDataValue("from")) ?? DateTime.Today;
            f.To = Ui.ParseDay(GetDataValue("to")) ?? f.From.AddDays(30);
            if (f.To < f.From)
            {
                f.To = f.From;
            }
            f.Page = Math.Max(1, Ui.Int(GetDataValue("pg"), 1));
            return f;
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
                response.SetElementContents("bo-main", NoAccess("bookings"));
                return response;
            }
            Filter f = ReadFilter(c, site);
            List<Booking> bookings = await Store.LoadBookings();
            response.SetElementContents("bo-main", MainHtml(c, site, f) );
            response.SetElementContents("bo-view", ViewHtml(c, site, bookings, f));
            response.ExecuteScript("AdminBookingsJs.loaded('" + f.View + "','" + Ui.Iso(f.Day) + "','" + Ui.Js(f.Sty) + "'," + (Access.OwnStylist(c.U).Length > 0 ? "true" : "false") + ");");
            return response;
        }

        public async Task<ApiResponse> View()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "view bookings", string.Empty, "You do not have access to bookings.");
            }
            SiteData site = await Store.LoadSite();
            Filter f = ReadFilter(c, site);
            List<Booking> bookings = await Store.LoadBookings();
            response.SetElementContents("bo-view", ViewHtml(c, site, bookings, f));
            response.ExecuteScript("AdminBookingsJs.viewed('" + f.View + "','" + Ui.Iso(f.Day) + "');");
            return response;
        }

        private static string Opt(string value, string label, string cur)
        {
            return "<option value=\"" + Ui.Esc(value) + "\"" + (value == cur ? " selected" : string.Empty) + ">" + Ui.Esc(label) + "</option>";
        }

        private static string MainHtml(Ctx c, SiteData site, Filter f)
        {
            bool own = Access.OwnStylist(c.U).Length > 0;
            StringBuilder sb = new StringBuilder("<div class=\"bo-ph\"><div><h1>Bookings</h1><p>" + (own ? "Your own bookings." : "Every visit, by day or as a list.") + "</p></div>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"bo-btn\" onclick=\"AdminBookingsJs.add('','','','')\">New booking</button>");
            }
            sb.Append("</div><div class=\"bo-tools\"><div class=\"bo-tabs\" role=\"tablist\"><button type=\"button\" class=\"bo-tab" + (f.View == "day" ? " bo-act" : string.Empty) + "\" id=\"bo-tday\" onclick=\"AdminBookingsJs.mode('day')\">Day</button><button type=\"button\" class=\"bo-tab" + (f.View == "list" ? " bo-act" : string.Empty) + "\" id=\"bo-tlist\" onclick=\"AdminBookingsJs.mode('list')\">List</button></div>");
            sb.Append("<div class=\"bo-dbar\" id=\"bo-dbar\"" + (f.View == "day" ? string.Empty : " hidden") + "><button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\" onclick=\"AdminBookingsJs.shift(-1)\" aria-label=\"Previous day\">&larr;</button><input type=\"date\" id=\"bo-day\" value=\"" + Ui.Iso(f.Day) + "\" onchange=\"AdminBookingsJs.go()\" aria-label=\"Day\"><button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\" onclick=\"AdminBookingsJs.shift(1)\" aria-label=\"Next day\">&rarr;</button><button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\" onclick=\"AdminBookingsJs.today()\">Today</button></div>");
            sb.Append("<div class=\"bo-lbar\" id=\"bo-lbar\"" + (f.View == "list" ? string.Empty : " hidden") + "><label class=\"bo-mini\">From <input type=\"date\" id=\"bo-from\" value=\"" + Ui.Iso(f.From) + "\" onchange=\"AdminBookingsJs.go()\"></label><label class=\"bo-mini\">To <input type=\"date\" id=\"bo-to\" value=\"" + Ui.Iso(f.To) + "\" onchange=\"AdminBookingsJs.go()\"></label><select id=\"bo-stat\" onchange=\"AdminBookingsJs.go()\" aria-label=\"Status\">" + Opt(string.Empty, "Any status", f.Stat) + string.Concat(Diary.Statuses.Select(s => Opt(s, Ui.StatusName(s), f.Stat))) + Opt("unpaid", "Done, not paid", f.Stat) + "</select><input type=\"search\" id=\"bo-term\" placeholder=\"Name, phone, email or reference\" maxlength=\"40\" oninput=\"AdminBookingsJs.typed()\" aria-label=\"Search bookings\"></div>");
            if (!own)
            {
                sb.Append("<select id=\"bo-sty\" onchange=\"AdminBookingsJs.go()\" aria-label=\"Stylist\">" + Opt(string.Empty, "All stylists", f.Sty) + string.Concat(site.Stylists.OrderBy(s => s.Order).Select(s => Opt(s.Id, s.Name + (s.Active ? string.Empty : " (inactive)"), f.Sty))) + "</select>");
            }
            sb.Append("</div><div id=\"bo-view\"></div>");
            return sb.ToString();
        }

        private static string ViewHtml(Ctx c, SiteData site, List<Booking> bookings, Filter f)
        {
            return f.View == "list" ? ListHtml(site, bookings, f) : DayHtml(c, site, bookings, f);
        }

        private static string DayHtml(Ctx c, SiteData site, List<Booking> bookings, Filter f)
        {
            DateTime day = f.Day;
            List<Stylist> cols = site.Stylists.Where(s => (f.Sty == string.Empty ? s.Active : s.Id == f.Sty)).OrderBy(s => s.Order).ToList();
            List<Booking> list = bookings.Where(b => b.Start.Date == day && Diary.Live(b) && cols.Any(s => s.Id == b.StylistId)).ToList();
            (int From, int To)? salon = Diary.SalonDay(site, day);
            int a = salon?.From ?? 9 * 60;
            int z = salon?.To ?? 18 * 60;
            foreach (Booking b in list)
            {
                a = Math.Min(a, (int)b.Start.TimeOfDay.TotalMinutes);
                z = Math.Max(z, (int)b.Start.TimeOfDay.TotalMinutes + b.Minutes);
            }
            a = a / 60 * 60;
            z = (z + 59) / 60 * 60;
            int height = (int)Math.Round((z - a) * Px);
            int cancelled = bookings.Count(b => b.Start.Date == day && !Diary.Live(b) && cols.Any(s => s.Id == b.StylistId));
            StringBuilder sb = new StringBuilder("<div class=\"bo-dayhead\"><h2>" + Ui.LongDay(day) + "</h2><span class=\"bo-hint\">" + (salon == null ? "The salon is closed this day. " : string.Empty) + Ui.Count(list.Count, "visit", "visits") + (cancelled > 0 ? " &middot; " + cancelled + " cancelled or no-show (see List)" : string.Empty) + "</span></div>");
            if (cols.Count == 0)
            {
                return sb.Append("<div class=\"bo-panel bo-empty\">No active stylists. Add one on the Stylists page.</div>").ToString();
            }
            bool canE = Access.Can(c.U, AreaKey, 'E');
            sb.Append("<div class=\"bo-dayw\"><div class=\"bo-day\" style=\"--n:" + cols.Count + "\"><div class=\"bo-gut\"><div class=\"bo-chead\"></div><div class=\"bo-cbody\" style=\"height:" + height + "px\">");
            for (int t = a; t < z; t += 30)
            {
                sb.Append("<span class=\"bo-glab" + (t % 60 == 0 ? string.Empty : " bo-half") + "\" style=\"top:" + Math.Round((t - a) * Px) + "px\">" + (t % 60 == 0 ? Ui.Time(t).Replace(":00", string.Empty) : string.Empty) + "</span>");
            }
            sb.Append("</div></div>");
            foreach (Stylist s in cols)
            {
                sb.Append("<div class=\"bo-col\"><div class=\"bo-chead\">" + (s.Photo.Length > 0 ? "<img src=\"" + Ui.Esc(s.Photo) + "\" alt=\"\" width=\"28\" height=\"28\">" : string.Empty) + "<b>" + Ui.Esc(s.Name.Split(' ')[0]) + "</b></div>");
                sb.Append("<div class=\"bo-cbody\" style=\"height:" + height + "px\"" + (canE ? " data-sty=\"" + Ui.Esc(s.Id) + "\" data-a=\"" + a + "\" onclick=\"AdminBookingsJs.pick(event,this)\"" : string.Empty) + ">");
                List<(int From, int To)> win = Diary.Windows(site, s, day);
                int cur = a;
                foreach ((int wa, int wb) in win.Concat(new[] { (z, z) }))
                {
                    if (wa > cur)
                    {
                        sb.Append("<i class=\"bo-off\" style=\"top:" + Math.Round((cur - a) * Px) + "px;height:" + Math.Round((wa - cur) * Px) + "px\"></i>");
                    }
                    cur = Math.Max(cur, wb);
                }
                for (int t = a + 60; t < z; t += 60)
                {
                    sb.Append("<i class=\"bo-hr\" style=\"top:" + Math.Round((t - a) * Px) + "px\"></i>");
                }
                foreach (Booking b in list.Where(x => x.StylistId == s.Id).OrderBy(x => x.Start))
                {
                    int st = (int)b.Start.TimeOfDay.TotalMinutes;
                    string paid = b.Paid != null ? " bo-ev-paid" : string.Empty;
                    sb.Append("<button type=\"button\" class=\"bo-ev bo-ev-" + b.Status + paid + "\" style=\"top:" + Math.Round((st - a) * Px) + "px;height:" + Math.Max(22, Math.Round(b.Minutes * Px) - 2) + "px\" onclick=\"event.stopPropagation();AdminBookingsJs.open('" + Ui.Js(b.Id) + "')\"><b>" + Ui.Time(b.Start) + " " + Ui.Esc(b.Name) + "</b><span>" + Ui.Esc(b.ServiceName) + "</span><em>" + (b.Paid != null ? "Paid" : Ui.StatusName(b.Status)) + "</em></button>");
                }
                sb.Append("</div></div>");
            }
            sb.Append("</div></div>");
            if (canE)
            {
                sb.Append("<p class=\"bo-hint bo-tip\">Click an empty spot in a column to book that stylist at that time. Grey areas are outside working hours.</p>");
            }
            return sb.ToString();
        }

        private static string ListHtml(SiteData site, List<Booking> bookings, Filter f)
        {
            IEnumerable<Booking> q = bookings.Where(b => b.Start.Date >= f.From && b.Start.Date <= f.To);
            if (f.Sty.Length > 0)
            {
                q = q.Where(b => b.StylistId == f.Sty);
            }
            if (f.Stat == "unpaid")
            {
                q = q.Where(b => b.Status == "done" && b.Paid == null);
            }
            else if (f.Stat.Length > 0)
            {
                q = q.Where(b => b.Status == f.Stat);
            }
            if (f.Term.Length > 0)
            {
                string d = Diary.Digits(f.Term);
                q = q.Where(b => Ui.Has(b.Name, f.Term) || Ui.Has(b.Email, f.Term) || Ui.Has(b.Ref, f.Term) || d.Length >= 3 && Diary.Digits(b.Phone).Contains(d));
            }
            List<Booking> rows = q.OrderBy(b => b.Start).ToList();
            int pages = Math.Max(1, (rows.Count + Size - 1) / Size);
            int page = Math.Min(f.Page, pages);
            if (rows.Count == 0)
            {
                return "<div class=\"bo-panel bo-empty\">No bookings match.</div>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"bo-wrapt\"><table class=\"bo-table\"><thead><tr><th>When</th><th>Guest</th><th>Service</th><th>Stylist</th><th>Status</th><th class=\"bo-num\">Paid</th></tr></thead><tbody>");
            foreach (Booking b in rows.Skip((page - 1) * Size).Take(Size))
            {
                string sty = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
                sb.Append("<tr class=\"bo-row\" onclick=\"AdminBookingsJs.open('" + Ui.Js(b.Id) + "')\"><td><b>" + Ui.Day(b.Start) + "</b><small>" + Ui.Time(b.Start) + " &middot; " + Ui.Esc(b.Ref) + "</small></td><td><a class=\"bo-link\" href=\"AdminGuests?g=" + Ui.Esc(b.GuestId) + "\" onclick=\"event.stopPropagation()\">" + Ui.Esc(b.Name) + "</a><small>" + Ui.Esc(b.Phone) + "</small></td><td>" + Ui.Esc(b.ServiceName) + "<small>" + Ui.Minutes(b.Minutes) + "</small></td><td>" + Ui.Esc(sty) + "</td><td>" + Ui.Status("bo", b.Status) + "</td><td class=\"bo-num\">" + (b.Paid != null ? Ui.Money(b.Paid.Total) : "&ndash;") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("bo", "AdminBookingsJs.page", page, pages, rows.Count, "booking", "bookings"));
            return sb.ToString();
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("bid"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open booking", id, "You do not have access to bookings.");
            }
            SiteData site = await Store.LoadSite();
            List<Booking> bookings = await Store.LoadBookings();
            Booking? b = bookings.FirstOrDefault(x => x.Id == id);
            string own = Access.OwnStylist(c.U);
            if (b == null || own.Length > 0 && b.StylistId != own)
            {
                response.ExecuteScript("AdminBookingsJs.toast('That booking was not found.');");
                return response;
            }
            List<Guest> guests = await Store.LoadGuests();
            response.SetElementContents("bo-mbody", CardHtml(c, site, guests, bookings, b));
            response.ExecuteScript("AdminBookingsJs.opened('" + Ui.Js(b.Ref + " · " + b.Name) + "');");
            return response;
        }

        private static string CardHtml(Ctx c, SiteData site, List<Guest> guests, List<Booking> bookings, Booking b)
        {
            Stylist? s = site.Stylists.FirstOrDefault(x => x.Id == b.StylistId);
            Guest? g = guests.FirstOrDefault(x => x.Id == b.GuestId);
            int visits = bookings.Count(x => x.GuestId == b.GuestId && x.Status == "done");
            StringBuilder sb = new StringBuilder("<div class=\"bo-card\"><div class=\"bo-cardtop\">" + Ui.Status("bo", b.Status) + (b.Paid != null ? "<span class=\"bo-pill bo-pill-go\">Paid " + Ui.Money(b.Paid.Total) + "</span>" : string.Empty) + "<span class=\"bo-hint\">" + Ui.Esc(b.Ref) + "</span></div>");
            sb.Append("<dl class=\"bo-kv\"><dt>When</dt><dd><b>" + Ui.LongDay(b.Start) + "</b><br>" + Ui.Time(b.Start) + " &ndash; " + Ui.Time(Diary.End(b)) + " (" + Ui.Minutes(b.Minutes) + ")</dd>");
            sb.Append("<dt>Service</dt><dd>" + Ui.Esc(b.ServiceName) + " &middot; " + Ui.Money(b.Price) + "</dd><dt>Stylist</dt><dd>" + Ui.Esc(s?.Name ?? "Unknown") + "</dd>");
            sb.Append("<dt>Guest</dt><dd><a class=\"bo-link\" href=\"AdminGuests?g=" + Ui.Esc(b.GuestId) + "\">" + Ui.Esc(b.Name) + "</a> &middot; " + Ui.Count(visits, "visit", "visits") + " done" + (g != null && g.Notes.Length > 0 ? "<br><span class=\"bo-gnote\">" + Ui.Esc(g.Notes) + "</span>" : string.Empty) + "</dd>");
            sb.Append("<dt>Phone</dt><dd><a class=\"bo-link\" href=\"tel:" + Ui.Esc(Diary.Digits(b.Phone)) + "\">" + Ui.Esc(b.Phone) + "</a></dd>" + (b.Email.Length > 0 ? "<dt>Email</dt><dd><a class=\"bo-link\" href=\"mailto:" + Ui.Esc(b.Email) + "\">" + Ui.Esc(b.Email) + "</a></dd>" : string.Empty));
            if (b.Note.Length > 0)
            {
                sb.Append("<dt>Note</dt><dd>" + Ui.Esc(b.Note) + "</dd>");
            }
            sb.Append("<dt>Booked</dt><dd>" + Ui.Esc(Ui.When(b.Created)) + " &middot; " + (b.Source == "online" ? "online" : b.Source == "walkin" ? "walk-in" : "by phone") + "</dd>");
            if (b.Arrived != null)
            {
                sb.Append("<dt>Arrived</dt><dd>" + Ui.Esc(Ui.When(b.Arrived.Value)) + "</dd>");
            }
            if (b.Finished != null)
            {
                sb.Append("<dt>Finished</dt><dd>" + Ui.Esc(Ui.When(b.Finished.Value)) + "</dd>");
            }
            if (b.Reason.Length > 0)
            {
                sb.Append("<dt>Reason</dt><dd>" + Ui.Esc(b.Reason) + "</dd>");
            }
            if (b.Paid != null)
            {
                sb.Append("<dt>Payment</dt><dd>Services " + Ui.Money(b.Paid.ServiceTotal) + (b.Paid.Discount > 0 ? " &minus; discount " + Ui.Money(b.Paid.Discount) : string.Empty) + " + tip " + Ui.Money(b.Paid.Tip) + " = <b>" + Ui.Money(b.Paid.Total) + "</b> by " + b.Paid.Method + "</dd>");
            }
            sb.Append("</dl><div class=\"bo-acts\">");
            string id = Ui.Js(b.Id);
            string who = Ui.Js(b.Name);
            bool e = Access.Can(c.U, AreaKey, 'E');
            bool x = Access.Can(c.U, AreaKey, 'X');
            if (b.Paid == null)
            {
                if (b.Status == "booked" && e)
                {
                    sb.Append("<button type=\"button\" class=\"bo-btn\" onclick=\"AdminBookingsJs.act('" + id + "','arrive','" + who + "')\">Check in</button>");
                }
                if (b.Status == "arrived" && e)
                {
                    sb.Append("<button type=\"button\" class=\"bo-btn\" onclick=\"AdminBookingsJs.act('" + id + "','done','" + who + "')\">Mark done</button><button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.act('" + id + "','undo','" + who + "')\">Undo check-in</button>");
                }
                if (b.Status == "done")
                {
                    if (Access.Can(c.U, "checkout", 'E'))
                    {
                        sb.Append("<a class=\"bo-btn\" href=\"AdminCheckout?b=" + Ui.Esc(b.Id) + "\">Check out</a>");
                    }
                    if (e)
                    {
                        sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.act('" + id + "','back','" + who + "')\">Back to in the chair</button>");
                    }
                }
                if (b.Status is "booked" or "arrived" && e)
                {
                    sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.edit('" + id + "')\">Change</button>");
                }
                if (b.Status == "booked" && x)
                {
                    sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.act('" + id + "','noshow','" + who + "')\">No-show</button>");
                }
                if (b.Status is "booked" or "arrived" && x)
                {
                    sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-danger bo-push\" onclick=\"AdminBookingsJs.act('" + id + "','cancel','" + who + "')\">Cancel booking</button>");
                }
                if (b.Status is "noshow" or "cancelled" && x)
                {
                    sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.act('" + id + "','restore','" + who + "')\">Restore booking</button>");
                }
            }
            else if (Access.Can(c.U, "checkout", 'V'))
            {
                sb.Append("<a class=\"bo-btn bo-btn-ghost\" href=\"AdminCheckout?b=" + Ui.Esc(b.Id) + "\">See receipt</a>");
            }
            if (e)
            {
                sb.Append("<button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.add('" + Ui.Js(b.GuestId) + "','','','')\">Book again</button>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Act()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("bid"), 40);
            string act = (GetDataValue("act") ?? string.Empty).Trim();
            string why = Ui.Clip(GetDataValue("why"), 300);
            char need = act is "noshow" or "cancel" or "restore" ? 'X' : 'E';
            if (!Access.Can(c.U, AreaKey, need))
            {
                return await Refuse(response, c, "booking " + act, id, need == 'X' ? "You are not allowed to cancel, restore or mark no-shows." : "You can look at bookings but not change them.");
            }
            SiteData site = await Store.LoadSite();
            string own = Access.OwnStylist(c.U);
            string msg = string.Empty;
            Booking? done = null;
            using (await Store.Hold())
            {
                List<Booking> bookings = await Store.LoadBookings();
                Booking? b = bookings.FirstOrDefault(x => x.Id == id);
                if (b == null || own.Length > 0 && b.StylistId != own)
                {
                    msg = "That booking was not found.";
                }
                else if (b.Paid != null)
                {
                    msg = "This visit is paid. Reopen it in Checkout first.";
                }
                else
                {
                    string from = b.Status;
                    string to = (act, from) switch
                    {
                        ("arrive", "booked") => "arrived",
                        ("undo", "arrived") => "booked",
                        ("done", "arrived") => "done",
                        ("back", "done") => "arrived",
                        ("noshow", "booked") => "noshow",
                        ("cancel", "booked") => "cancelled",
                        ("cancel", "arrived") => "cancelled",
                        ("restore", "noshow") => "booked",
                        ("restore", "cancelled") => "booked",
                        _ => string.Empty
                    };
                    if (to.Length == 0)
                    {
                        msg = "That step is not possible from " + Ui.StatusName(from) + ".";
                    }
                    else if (act == "restore")
                    {
                        Stylist? s = site.Stylists.FirstOrDefault(x => x.Id == b.StylistId);
                        SalonService? svc = site.Services.FirstOrDefault(x => x.Id == b.ServiceId);
                        string p = Diary.Problem(site, bookings, s, svc, b.Start, DateTime.Now, true, b.Id);
                        if (p.Length > 0)
                        {
                            msg = "Cannot restore: " + p + " Make a new booking instead.";
                        }
                    }
                    if (msg.Length == 0)
                    {
                        b.Status = to;
                        if (to == "arrived" && from == "booked")
                        {
                            b.Arrived = DateTime.UtcNow;
                        }
                        if (to == "booked")
                        {
                            b.Arrived = null;
                            b.Reason = string.Empty;
                        }
                        if (to == "done")
                        {
                            b.Finished = DateTime.UtcNow;
                        }
                        if (to == "arrived" && from == "done")
                        {
                            b.Finished = null;
                        }
                        if (to is "cancelled" or "noshow")
                        {
                            b.Reason = why;
                        }
                        await Store.SaveBookings(bookings);
                        done = b;
                    }
                }
            }
            if (done == null)
            {
                await Store.Log(c.U.Id, "booking " + act, id, false);
                response.ExecuteScript("AdminBookingsJs.toast('" + Ui.Js(msg) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "booking " + act, done.Ref + " · " + done.Name, true);
            List<Booking> all = await Store.LoadBookings();
            List<Guest> guests = await Store.LoadGuests();
            response.SetElementContents("bo-mbody", CardHtml(c, site, guests, all, done));
            response.SetElementContents("bo-view", ViewHtml(c, site, all, ReadFilter(c, site)));
            response.SetElementContents("bo-menu", await Auth.Menu("bo", "AdminBookings", c));
            string label = act switch { "arrive" => "Checked in", "done" => "Marked done. Ready to check out.", "noshow" => "Marked as no-show", "cancel" => "Booking cancelled", "restore" => "Booking restored", _ => "Updated" };
            response.ExecuteScript("AdminBookingsJs.clean();AdminBookingsJs.toast('" + Ui.Js(label) + "');");
            return response;
        }

        private static string StyOpts(SiteData site, string svc, string cur)
        {
            List<Stylist> list = svc.Length > 0 ? Diary.For(site, svc) : site.Stylists.Where(s => s.Active).OrderBy(s => s.Order).ToList();
            return "<option value=\"\">Choose a stylist</option>" + string.Concat(list.Select(s => Opt(s.Id, s.Name, cur)));
        }

        private static string TimeOpts(SiteData site, List<Booking> bookings, string svcId, string styId, DateTime? day, string ignore, int keep)
        {
            SalonService? svc = site.Services.FirstOrDefault(x => x.Id == svcId);
            Stylist? s = site.Stylists.FirstOrDefault(x => x.Id == styId);
            if (svc == null || s == null || day == null || !Diary.Does(s, svc.Id))
            {
                return "<option value=\"\">Choose service, stylist and day first</option>";
            }
            List<int> free = Diary.Free(site, bookings, s, svc, day.Value, DateTime.Now, -15, ignore);
            if (free.Count == 0)
            {
                return "<option value=\"\">No free times this day</option>";
            }
            return "<option value=\"\">Choose a time (" + free.Count + " free)</option>" + string.Concat(free.Select(t => Opt(Diary.Hhmm(t), Ui.Time(t), keep >= 0 ? Diary.Hhmm(keep) : string.Empty)));
        }

        private static string StySelect(SiteData site, string svc, string sty, bool locked)
        {
            return "<select id=\"bo-f-sty\" data-k=\"bsty\" onchange=\"AdminBookingsJs.slots()\"" + (locked ? " disabled" : string.Empty) + ">" + StyOpts(site, svc, sty) + "</select>";
        }

        private static string TimeSelect(SiteData site, List<Booking> bookings, string svcId, string styId, DateTime? day, string ignore, int keep)
        {
            return "<select id=\"bo-f-at\" data-k=\"bat\">" + TimeOpts(site, bookings, svcId, styId, day, ignore, keep) + "</select>";
        }

        public async Task<ApiResponse> Form()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "open booking form", string.Empty, "You can look at bookings but not add or change them.");
            }
            SiteData site = await Store.LoadSite();
            List<Booking> bookings = await Store.LoadBookings();
            List<Guest> guests = await Store.LoadGuests();
            string own = Access.OwnStylist(c.U);
            Booking? b = bookings.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("bid"), 40));
            if (b != null && (b.Paid != null || !(b.Status is "booked" or "arrived") || own.Length > 0 && b.StylistId != own))
            {
                response.ExecuteScript("AdminBookingsJs.toast('This booking cannot be changed.');");
                return response;
            }
            Guest? g = b != null ? guests.FirstOrDefault(x => x.Id == b.GuestId) : guests.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("gid"), 40));
            string sty = b?.StylistId ?? (own.Length > 0 ? own : Ui.Clip(GetDataValue("fsty"), 40));
            DateTime day = b?.Start.Date ?? Ui.ParseDay(GetDataValue("fday")) ?? DateTime.Today;
            int at = b != null ? (int)b.Start.TimeOfDay.TotalMinutes : Diary.Min(GetDataValue("fat"));
            string svc = b?.ServiceId ?? string.Empty;
            StringBuilder sb = new StringBuilder("<form class=\"bo-stack\" id=\"bo-form\" onsubmit=\"return AdminBookingsJs.save('" + Ui.Js(b?.Id ?? string.Empty) + "')\" novalidate>");
            sb.Append("<input type=\"hidden\" id=\"bo-f-gid\" data-k=\"gid\" value=\"" + Ui.Esc(g?.Id ?? string.Empty) + "\">");
            if (b == null)
            {
                sb.Append("<div class=\"bo-gpick\" id=\"bo-gpick\"" + (g != null ? " hidden" : string.Empty) + "><label class=\"bo-field\" for=\"bo-f-find\"><span>Find a guest</span><input type=\"search\" id=\"bo-f-find\" maxlength=\"40\" placeholder=\"Type a name, phone or email\" oninput=\"AdminBookingsJs.find(this.value)\" autocomplete=\"off\"><small>Or fill in a new guest below.</small></label><div class=\"bo-sugg\" id=\"bo-sugg\"></div></div>");
            }
            sb.Append("<div class=\"bo-gsel\" id=\"bo-gsel\"" + (g == null ? " hidden" : string.Empty) + ">" + (g == null ? string.Empty : GuestChip(g, b == null)) + "</div>");
            sb.Append("<div class=\"bo-grid3\" id=\"bo-gnew\"" + (g != null ? " hidden" : string.Empty) + "><label class=\"bo-field\"><span>Name</span><input type=\"text\" id=\"bo-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(b?.Name ?? string.Empty) + "\"><em class=\"bo-err\" id=\"bo-e-name\"></em></label><label class=\"bo-field\"><span>Phone</span><input type=\"tel\" id=\"bo-f-phone\" data-k=\"phone\" maxlength=\"24\" value=\"" + Ui.Esc(b?.Phone ?? string.Empty) + "\"><em class=\"bo-err\" id=\"bo-e-phone\"></em></label><label class=\"bo-field\"><span>Email (optional)</span><input type=\"email\" id=\"bo-f-email\" data-k=\"email\" maxlength=\"80\" value=\"" + Ui.Esc(b?.Email ?? string.Empty) + "\"><em class=\"bo-err\" id=\"bo-e-email\"></em></label></div>");
            sb.Append("<div class=\"bo-grid2\"><label class=\"bo-field\"><span>Service</span><select id=\"bo-f-svc\" data-k=\"svc\" onchange=\"AdminBookingsJs.slots()\"><option value=\"\">Choose a service</option>");
            foreach (Category cat in site.Categories)
            {
                List<SalonService> list = site.Services.Where(x => x.Category == cat.Key && (x.Active || x.Id == svc)).OrderBy(x => x.Order).ToList();
                if (list.Count > 0)
                {
                    sb.Append("<optgroup label=\"" + Ui.Esc(cat.Label) + "\">" + string.Concat(list.Select(x => Opt(x.Id, x.Name + " · " + Ui.Minutes(x.Minutes) + " · " + Ui.Money(x.Price), svc))) + "</optgroup>");
                }
            }
            sb.Append("</select><em class=\"bo-err\" id=\"bo-e-svc\"></em></label>");
            sb.Append("<label class=\"bo-field\"><span>Stylist</span><span class=\"bo-selw\" id=\"bo-f-styw\">" + StySelect(site, svc, sty, own.Length > 0) + "</span><em class=\"bo-err\" id=\"bo-e-sty\"></em></label></div>");
            sb.Append("<div class=\"bo-grid2\"><label class=\"bo-field\"><span>Day</span><input type=\"date\" id=\"bo-f-day\" data-k=\"bday\" value=\"" + Ui.Iso(day) + "\" min=\"" + Ui.Iso(DateTime.Today) + "\" onchange=\"AdminBookingsJs.slots()\"></label>");
            sb.Append("<label class=\"bo-field\"><span>Time</span><span class=\"bo-selw\" id=\"bo-f-atw\">" + TimeSelect(site, bookings, svc, sty, day, b?.Id ?? string.Empty, at) + "</span><em class=\"bo-err\" id=\"bo-e-at\"></em></label></div>");
            sb.Append("<div class=\"bo-grid2\"><label class=\"bo-field\"><span>How was it booked?</span><select id=\"bo-f-src\" data-k=\"src\">" + Opt("phone", "By phone", b?.Source ?? "phone") + Opt("walkin", "Walk-in", b?.Source ?? "phone") + Opt("online", "Online", b?.Source ?? "phone") + "</select></label>");
            sb.Append("<label class=\"bo-field\"><span>Note (optional)</span><input type=\"text\" id=\"bo-f-note\" data-k=\"note\" maxlength=\"300\" value=\"" + Ui.Esc(b?.Note ?? string.Empty) + "\"><small>No medical details.</small></label></div>");
            sb.Append("<em class=\"bo-err\" id=\"bo-e-form\" role=\"alert\"></em><div class=\"bo-acts\"><button type=\"submit\" class=\"bo-btn\">" + (b == null ? "Save booking" : "Save changes") + "</button><button type=\"button\" class=\"bo-btn bo-btn-ghost\" onclick=\"AdminBookingsJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("bo-mbody", sb.ToString());
            response.ExecuteScript("AdminBookingsJs.opened('" + (b == null ? "New booking" : "Change " + Ui.Js(b.Ref)) + "');");
            return response;
        }

        private static string GuestChip(Guest g, bool change)
        {
            return "<div class=\"bo-gchip\"><span class=\"bo-gav\">" + Ui.Esc(Ui.Initials(g.Name)) + "</span><div><b>" + Ui.Esc(g.Name) + "</b><small>" + Ui.Esc(g.Phone) + (g.Email.Length > 0 ? " &middot; " + Ui.Esc(g.Email) : string.Empty) + "</small></div>" + (change ? "<button type=\"button\" class=\"bo-btn bo-btn-ghost bo-btn-sm\" onclick=\"AdminBookingsJs.unpick()\">Change guest</button>" : string.Empty) + "</div>";
        }

        public async Task<ApiResponse> Slots()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<Booking> bookings = await Store.LoadBookings();
            string svc = Ui.Clip(GetDataValue("svc"), 40);
            string own = Access.OwnStylist(c.U);
            string sty = own.Length > 0 ? own : Ui.Clip(GetDataValue("bsty"), 40);
            if (svc.Length > 0 && sty.Length > 0 && !site.Stylists.Any(s => s.Id == sty && Diary.Does(s, svc)))
            {
                sty = string.Empty;
            }
            if (own.Length == 0)
            {
                response.SetElementContents("bo-f-styw", StySelect(site, svc, sty, false));
            }
            response.SetElementContents("bo-f-atw", TimeSelect(site, bookings, svc, sty, Ui.ParseDay(GetDataValue("bday")), Ui.Clip(GetDataValue("bid"), 40), Diary.Min(GetDataValue("bat"))));
            return response;
        }

        public async Task<ApiResponse> Find()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            string term = Ui.Clip(GetDataValue("term"), 40);
            List<Guest> guests = await Store.LoadGuests();
            string d = Diary.Digits(term);
            List<Guest> hits = term.Length < 2 ? new List<Guest>() : guests.Where(g => Ui.Has(g.Name, term) || Ui.Has(g.Email, term) || d.Length >= 3 && Diary.Digits(g.Phone).Contains(d)).OrderBy(g => g.Name).Take(8).ToList();
            StringBuilder sb = new StringBuilder();
            foreach (Guest g in hits)
            {
                sb.Append("<button type=\"button\" class=\"bo-sg\" onclick=\"AdminBookingsJs.choose('" + Ui.Js(g.Id) + "')\"><b>" + Ui.Esc(g.Name) + "</b><small>" + Ui.Esc(g.Phone) + (g.Email.Length > 0 ? " &middot; " + Ui.Esc(g.Email) : string.Empty) + "</small></button>");
            }
            response.SetElementContents("bo-sugg", term.Length < 2 ? string.Empty : sb.Length == 0 ? "<p class=\"bo-hint bo-sgnone\">No guest matches. Fill in the new guest below.</p>" : sb.ToString());
            return response;
        }

        public async Task<ApiResponse> Choose()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return response;
            }
            Guest? g = (await Store.LoadGuests()).FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("gid"), 40));
            if (g == null)
            {
                return response;
            }
            response.SetElementContents("bo-gsel", GuestChip(g, true));
            response.ExecuteScript("AdminBookingsJs.chosen('" + Ui.Js(g.Id) + "');");
            return response;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string bid = Ui.Clip(GetDataValue("bid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save booking", bid, "You can look at bookings but not add or change them.");
            }
            SiteData site = await Store.LoadSite();
            string own = Access.OwnStylist(c.U);
            string gid = Ui.Clip(GetDataValue("gid"), 40);
            string name = Ui.Clip(GetDataValue("name"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 100);
            string note = Ui.Clip(GetDataValue("note"), 300);
            string src = GetDataValue("src") is "walkin" or "online" ? GetDataValue("src") : "phone";
            SalonService? svc = site.Services.FirstOrDefault(x => x.Id == Ui.Clip(GetDataValue("svc"), 40));
            string styId = own.Length > 0 ? own : Ui.Clip(GetDataValue("bsty"), 40);
            Stylist? sty = site.Stylists.FirstOrDefault(x => x.Id == styId);
            DateTime? day = Ui.ParseDay(GetDataValue("bday"));
            int at = Diary.Min(GetDataValue("bat"));
            Dictionary<string, string> err = new Dictionary<string, string>();
            if (gid.Length == 0 && bid.Length == 0)
            {
                if (name.Length < 2 || name.Length > 60)
                {
                    err["name"] = "Enter the guest's name.";
                }
                if (!Diary.IsPhone(phone))
                {
                    err["phone"] = "Enter a phone number.";
                }
                if (email.Length > 0 && !Diary.IsEmail(email))
                {
                    err["email"] = "Check the email address.";
                }
            }
            if (svc == null)
            {
                err["svc"] = "Choose a service.";
            }
            if (sty == null)
            {
                err["sty"] = "Choose a stylist.";
            }
            if (day == null || at < 0)
            {
                err["at"] = "Choose a day and a time.";
            }
            foreach (string k in new[] { "name", "phone", "email", "svc", "sty", "at" })
            {
                response.SetElementContents("bo-e-" + k, err.TryGetValue(k, out string? m) ? m : string.Empty);
            }
            response.SetElementContents("bo-e-form", string.Empty);
            if (err.Count > 0)
            {
                return response;
            }
            string problem = string.Empty;
            Booking? made = null;
            using (await Store.Hold())
            {
                List<Booking> bookings = await Store.LoadBookings();
                List<Guest> guests = await Store.LoadGuests();
                DateTime start = day!.Value.AddMinutes(at);
                Booking? b = bid.Length > 0 ? bookings.FirstOrDefault(x => x.Id == bid) : null;
                if (bid.Length > 0 && (b == null || b.Paid != null || !(b.Status is "booked" or "arrived") || own.Length > 0 && b.StylistId != own))
                {
                    problem = "This booking can no longer be changed.";
                }
                else
                {
                    problem = Diary.Problem(site, bookings, sty, svc, start, DateTime.Now, true, b?.Id ?? string.Empty);
                }
                Guest? g = null;
                if (problem.Length == 0)
                {
                    if (b != null)
                    {
                        g = guests.FirstOrDefault(x => x.Id == b.GuestId);
                    }
                    else if (gid.Length > 0)
                    {
                        g = guests.FirstOrDefault(x => x.Id == gid);
                        if (g == null)
                        {
                            problem = "That guest was not found. Search again.";
                        }
                    }
                    else
                    {
                        g = Diary.Upsert(guests, name, phone, email, c.U.Name);
                    }
                }
                if (problem.Length == 0)
                {
                    if (b == null)
                    {
                        b = new Booking { Id = Diary.NewId("b-"), Ref = Diary.NewRef(bookings), Status = "booked", Created = DateTime.UtcNow, By = c.U.Id };
                        bookings.Add(b);
                    }
                    if (g != null)
                    {
                        b.GuestId = g.Id;
                        b.Name = g.Name;
                        b.Phone = g.Phone;
                        b.Email = g.Email;
                    }
                    b.ServiceId = svc!.Id;
                    b.ServiceName = svc.Name;
                    b.Minutes = svc.Minutes;
                    b.Price = svc.Price;
                    b.StylistId = sty!.Id;
                    b.Start = start;
                    b.Note = note;
                    b.Source = src;
                    await Store.SaveGuests(guests);
                    await Store.SaveBookings(bookings);
                    made = b;
                }
            }
            if (made == null)
            {
                await Store.Log(c.U.Id, "save booking", bid, false);
                response.SetElementContents("bo-e-form", Ui.Esc(problem));
                return response;
            }
            await Store.Log(c.U.Id, bid.Length > 0 ? "change booking" : "add booking", made.Ref + " · " + made.Name + " · " + Ui.Day(made.Start) + " " + Ui.Time(made.Start), true);
            List<Booking> all = await Store.LoadBookings();
            Filter f = ReadFilter(c, site);
            if (f.View == "day")
            {
                f.Day = made.Start.Date;
            }
            response.SetElementContents("bo-view", ViewHtml(c, site, all, f));
            List<Guest> gl = await Store.LoadGuests();
            response.SetElementContents("bo-mbody", CardHtml(c, site, gl, all, made));
            response.ExecuteScript("AdminBookingsJs.clean();AdminBookingsJs.opened('" + Ui.Js(made.Ref + " · " + made.Name) + "');AdminBookingsJs.viewed('" + f.View + "','" + Ui.Iso(f.Day) + "');AdminBookingsJs.toast('" + (bid.Length > 0 ? "Booking changed" : "Booking saved") + "');");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminBookingsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminBookingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"bo-panel\"><h1>No access</h1><p class=\"bo-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("bo-brand", Ui.Esc(site.Brand));
            response.SetElementContents("bo-menu", await Auth.Menu("bo", "AdminBookings", c));
            response.SetElementContents("bo-who", Auth.Who("bo", c));
        }

    }
}
