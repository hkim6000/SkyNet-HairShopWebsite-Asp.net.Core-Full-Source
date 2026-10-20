using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class Book : WebPage
    {
        private const int StripDays = 14;
        private string preSvc = string.Empty;
        private string preSty = string.Empty;
        private List<Booking> diary = new List<Booking>();

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            List<MediaItem> media = await Media.Load();
            diary = await Store.LoadBookings();
            SalonService? s = Svc(site, QueryValue("s"));
            preSvc = s?.Id ?? string.Empty;
            string p = (QueryValue("p") ?? string.Empty).Trim();
            if (site.Stylists.Any(x => x.Active && x.Id == p && (s == null || Diary.Does(x, s.Id))))
            {
                preSty = p;
            }
            Prepare(site, media, "book", "Book online", "Book a cut, color or blow-dry at " + site.Brand + ". Choose a stylist and a free time. No deposit; pay at the salon.");
        }

        private static SalonService? Svc(SiteData site, string? id)
        {
            string v = (id ?? string.Empty).Trim();
            return site.Services.FirstOrDefault(x => x.Active && x.Id == v && Diary.For(site, x.Id).Count > 0);
        }

        private static Stylist? Sty(SiteData site, SalonService? svc, string? id)
        {
            string v = (id ?? string.Empty).Trim();
            return svc == null ? null : site.Stylists.FirstOrDefault(x => x.Id == v && Diary.Does(x, svc.Id));
        }

        private static List<Stylist> Pool(SiteData site, SalonService svc, string sty)
        {
            return sty == "any" ? Diary.For(site, svc.Id) : site.Stylists.Where(x => x.Id == sty && Diary.Does(x, svc.Id)).ToList();
        }

        private static List<int> Times(SiteData site, List<Booking> bookings, SalonService svc, string sty, DateTime day)
        {
            DateTime now = DateTime.Now;
            int lead = Math.Clamp(site.LeadMinutes, 0, 2880);
            return Pool(site, svc, sty).SelectMany(x => Diary.Free(site, bookings, x, svc, day, now, lead, string.Empty)).Distinct().OrderBy(t => t).ToList();
        }

        private static DateTime LastDay(SiteData site)
        {
            return DateTime.Today.AddDays(Math.Clamp(site.HorizonDays, 1, 365));
        }

        public async Task<ApiResponse> Plan()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            List<Booking> bookings = await Store.LoadBookings();
            SalonService? svc = Svc(site, GetDataValue("svc"));
            string styKey = (GetDataValue("sty") ?? string.Empty).Trim();
            string sty = svc == null ? string.Empty : styKey == "any" ? "any" : Sty(site, svc, styKey)?.Id ?? string.Empty;
            DateTime? day = Ui.ParseDay(GetDataValue("day"));
            if (day != null && (day < DateTime.Today || day > LastDay(site)))
            {
                day = null;
            }
            int at = Diary.Min(GetDataValue("at"));
            if (svc == null || sty == string.Empty || day == null || !Times(site, bookings, svc, sty, day.Value).Contains(at))
            {
                at = -1;
            }
            Fill(response, site, bookings, svc, sty, day, at);
            response.ExecuteScript("BookJs.planned('" + Ui.Js(svc?.Id ?? string.Empty) + "','" + Ui.Js(sty) + "','" + (day == null ? string.Empty : Ui.Iso(day.Value)) + "','" + (at < 0 ? string.Empty : Diary.Hhmm(at)) + "');");
            return response;
        }

        private static void Fill(ApiResponse response, SiteData site, List<Booking> bookings, SalonService? svc, string sty, DateTime? day, int at)
        {
            response.SetElementContents("bk-svcs", SvcHtml(site, svc?.Id ?? string.Empty));
            response.SetElementContents("bk-stys", StyHtml(site, svc, sty));
            response.SetElementContents("bk-days", DaysHtml(site, bookings, svc, sty, day));
            response.SetElementContents("bk-times", TimesHtml(site, bookings, svc, sty, day, at));
            response.SetElementContents("bk-sum", SumHtml(site, svc, sty, day, at));
        }

        public async Task<ApiResponse> Reserve()
        {
            ApiResponse response = new ApiResponse();
            SiteData site = await Store.LoadSite();
            string name = Ui.Clip(GetDataValue("name"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 100);
            string note = (GetDataValue("note") ?? string.Empty).Trim();
            Dictionary<string, string> err = new Dictionary<string, string>();
            if (name.Length < 2 || name.Length > 60)
            {
                err["name"] = "Please enter your name.";
            }
            if (!Diary.IsPhone(phone))
            {
                err["phone"] = "Please enter a phone number we can call, like (555) 014-2290.";
            }
            if (email.Length > 0 && !Diary.IsEmail(email))
            {
                err["email"] = "That email address does not look right.";
            }
            if (note.Length > 300)
            {
                err["note"] = "Please keep the note under 300 characters.";
            }
            SalonService? svc = Svc(site, GetDataValue("svc"));
            string styKey = (GetDataValue("sty") ?? string.Empty).Trim();
            DateTime? day = Ui.ParseDay(GetDataValue("day"));
            int at = Diary.Min(GetDataValue("at"));
            string slotErr = string.Empty;
            if (svc == null)
            {
                slotErr = "Choose a service first.";
            }
            else if (styKey != "any" && Sty(site, svc, styKey) == null)
            {
                slotErr = "Choose a stylist.";
            }
            else if (day == null || at < 0)
            {
                slotErr = "Choose a day and a time.";
            }
            foreach (string k in new[] { "name", "phone", "email", "note" })
            {
                response.SetElementContents("bk-e-" + k, err.TryGetValue(k, out string? m) ? m : string.Empty);
            }
            response.SetElementContents("bk-e-slot", slotErr);
            if (err.Count > 0 || slotErr.Length > 0)
            {
                response.ExecuteScript("BookJs.invalid();");
                return response;
            }
            Booking? made = null;
            string problem = string.Empty;
            List<Booking> after;
            using (await Store.Hold())
            {
                List<Booking> bookings = await Store.LoadBookings();
                DateTime start = day!.Value.AddMinutes(at);
                DateTime now = DateTime.Now;
                Stylist? who = null;
                if (styKey == "any")
                {
                    who = Diary.For(site, svc!.Id).Where(x => Diary.Problem(site, bookings, x, svc, start, now, false, string.Empty).Length == 0).OrderBy(x => bookings.Count(b => Diary.Live(b) && b.StylistId == x.Id && b.Start.Date == start.Date)).ThenBy(x => x.Order).FirstOrDefault();
                    problem = who == null ? "That time was just taken. Please choose another time." : string.Empty;
                }
                else
                {
                    who = Sty(site, svc, styKey);
                    problem = Diary.Problem(site, bookings, who, svc, start, now, false, string.Empty);
                }
                if (problem.Length == 0 && who != null)
                {
                    List<Guest> guests = await Store.LoadGuests();
                    Guest g = Diary.Upsert(guests, name, phone, email, "online");
                    made = new Booking
                    {
                        Id = Diary.NewId("b-"),
                        Ref = Diary.NewRef(bookings),
                        GuestId = g.Id,
                        Name = name,
                        Phone = phone,
                        Email = email,
                        ServiceId = svc!.Id,
                        ServiceName = svc.Name,
                        Minutes = svc.Minutes,
                        Price = svc.Price,
                        StylistId = who.Id,
                        Start = start,
                        Status = "booked",
                        Note = note,
                        Source = "online",
                        Created = DateTime.UtcNow,
                        By = "online"
                    };
                    bookings.Add(made);
                    await Store.SaveGuests(guests);
                    await Store.SaveBookings(bookings);
                }
                after = bookings;
            }
            if (made == null)
            {
                response.SetElementContents("bk-e-slot", Ui.Esc(problem));
                Fill(response, site, after, svc, styKey == "any" ? "any" : styKey, day, -1);
                response.ExecuteScript("BookJs.planned('" + Ui.Js(svc!.Id) + "','" + Ui.Js(styKey) + "','" + Ui.Iso(day!.Value) + "','');BookJs.invalid();");
                return response;
            }
            Stylist st = site.Stylists.First(x => x.Id == made.StylistId);
            StringBuilder sb = new StringBuilder("<div class=\"bk-okbox\"><span class=\"bk-okic\" aria-hidden=\"true\"><svg viewBox=\"0 0 24 24\" width=\"30\" height=\"30\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg></span><h2 tabindex=\"-1\" id=\"bk-okh\">You are booked in, " + Ui.Esc(name.Split(' ')[0]) + "</h2>");
            sb.Append("<p class=\"bk-okref\">Booking reference <b>" + Ui.Esc(made.Ref) + "</b></p><dl class=\"bk-okdl\"><dt>When</dt><dd>" + Ui.LongDay(made.Start) + " at " + Ui.Time(made.Start) + "</dd><dt>Service</dt><dd>" + Ui.Esc(made.ServiceName) + " &middot; " + Ui.Minutes(made.Minutes) + "</dd><dt>Stylist</dt><dd>" + Ui.Esc(st.Name) + "</dd><dt>Price</dt><dd>" + (svc!.Price == 0 ? "Free" : (svc.From ? "from " : string.Empty) + Ui.Price(svc.Price)) + "</dd><dt>Where</dt><dd>" + Ui.Esc(site.Street) + ", " + Ui.Esc(site.City) + "</dd></dl>");
            sb.Append("<p>Please arrive five minutes early. You pay at the salon after your visit, by card or cash. To change or cancel, call <a href=\"" + Ui.Esc(Tel(site.Phone)) + "\">" + Ui.Esc(site.Phone) + "</a>.</p><p class=\"bk-oknote\">Take a screenshot or note your reference; we do not send an email.</p><div class=\"bk-okacts\"><a class=\"bk-btn\" href=\"Book\">Book another visit</a><a class=\"bk-btn bk-btn-ghost\" href=\"Home\">Back to home</a></div></div>");
            response.SetElementContents("bk-done", sb.ToString());
            response.ExecuteScript("BookJs.booked();");
            return response;
        }

        private static string PriceText(SalonService s)
        {
            return s.Price == 0 ? "Free" : (s.From ? "from " : string.Empty) + Ui.Price(s.Price);
        }

        private static string SvcHtml(SiteData site, string cur)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Category c in site.Categories)
            {
                List<SalonService> list = site.Services.Where(s => s.Active && s.Category == c.Key && Diary.For(site, s.Id).Count > 0).OrderBy(s => s.Order).ToList();
                if (list.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"bk-sgroup\"><h3>" + Ui.Esc(c.Label) + "</h3><div class=\"bk-opts\">");
                foreach (SalonService s in list)
                {
                    bool on = s.Id == cur;
                    sb.Append("<button type=\"button\" class=\"bk-opt" + (on ? " bk-sel" : string.Empty) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"BookJs.svc('" + Ui.Js(s.Id) + "')\"><b>" + Ui.Esc(s.Name) + "</b><span>" + Ui.Minutes(s.Minutes) + " &middot; " + PriceText(s) + "</span></button>");
                }
                sb.Append("</div></div>");
            }
            return sb.Length == 0 ? "<p class=\"bk-none\">Online booking is not open yet. Please call us.</p>" : sb.ToString();
        }

        private static string StyHtml(SiteData site, SalonService? svc, string cur)
        {
            if (svc == null)
            {
                return "<p class=\"bk-wait\">Choose a service first.</p>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"bk-people\">");
            sb.Append("<button type=\"button\" class=\"bk-who" + (cur == "any" ? " bk-sel" : string.Empty) + "\" aria-pressed=\"" + (cur == "any" ? "true" : "false") + "\" onclick=\"BookJs.sty('any')\"><span class=\"bk-anyic\" aria-hidden=\"true\">?</span><b>Any stylist</b><small>First free</small></button>");
            foreach (Stylist s in Diary.For(site, svc.Id))
            {
                bool on = s.Id == cur;
                sb.Append("<button type=\"button\" class=\"bk-who" + (on ? " bk-sel" : string.Empty) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"BookJs.sty('" + Ui.Js(s.Id) + "')\">" + (s.Photo.Length > 0 ? "<img src=\"" + Ui.Esc(s.Photo) + "\" alt=\"\" width=\"64\" height=\"64\" loading=\"lazy\">" : "<span class=\"bk-anyic\">" + Ui.Esc(Ui.Initials(s.Name)) + "</span>") + "<b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Role) + "</small></button>");
            }
            return sb.Append("</div>").ToString();
        }

        private static string DaysHtml(SiteData site, List<Booking> bookings, SalonService? svc, string sty, DateTime? day)
        {
            if (svc == null || sty == string.Empty)
            {
                return "<p class=\"bk-wait\">Choose a service and a stylist first.</p>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"bk-strip\">");
            DateTime last = LastDay(site);
            for (int i = 0; i < StripDays; i++)
            {
                DateTime d = DateTime.Today.AddDays(i);
                if (d > last)
                {
                    break;
                }
                int n = Times(site, bookings, svc, sty, d).Count;
                bool on = day == d;
                sb.Append("<button type=\"button\" class=\"bk-day" + (on ? " bk-sel" : string.Empty) + "\"" + (n == 0 ? " disabled" : string.Empty) + " aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"BookJs.day('" + Ui.Iso(d) + "')\" aria-label=\"" + Ui.LongDay(d) + (n == 0 ? ", fully booked or closed" : ", " + Ui.Count(n, "free time", "free times")) + "\"><small>" + (i == 0 ? "Today" : i == 1 ? "Tomorrow" : d.ToString("ddd", Ui.Us)) + "</small><b>" + d.Day + "</b><small>" + d.ToString("MMM", Ui.Us) + "</small><i>" + (n == 0 ? "Full" : n + " free") + "</i></button>");
            }
            sb.Append("</div><label class=\"bk-later\"><span>Later date</span><input type=\"date\" id=\"bk-later\" min=\"" + Ui.Iso(DateTime.Today) + "\" max=\"" + Ui.Iso(last) + "\" value=\"" + (day != null && day >= DateTime.Today.AddDays(StripDays) ? Ui.Iso(day.Value) : string.Empty) + "\" onchange=\"BookJs.day(this.value)\"></label>");
            return sb.ToString();
        }

        private static string TimesHtml(SiteData site, List<Booking> bookings, SalonService? svc, string sty, DateTime? day, int at)
        {
            if (svc == null || sty == string.Empty || day == null)
            {
                return "<p class=\"bk-wait\">Pick a day to see the free times.</p>";
            }
            List<int> times = Times(site, bookings, svc, sty, day.Value);
            if (times.Count == 0)
            {
                return "<p class=\"bk-wait\">No free times on " + Ui.Day(day.Value) + ". Try another day" + (sty != "any" ? " or Any stylist" : string.Empty) + ".</p>";
            }
            StringBuilder sb = new StringBuilder("<p class=\"bk-dayname\">" + Ui.LongDay(day.Value) + "</p>");
            foreach ((string label, int a, int b) in new[] { ("Morning", 0, 720), ("Afternoon", 720, 1020), ("Evening", 1020, 1440) })
            {
                List<int> part = times.Where(t => t >= a && t < b).ToList();
                if (part.Count == 0)
                {
                    continue;
                }
                sb.Append("<div class=\"bk-tgroup\"><h3>" + label + "</h3><div class=\"bk-tlist\">");
                foreach (int t in part)
                {
                    bool on = t == at;
                    sb.Append("<button type=\"button\" class=\"bk-time" + (on ? " bk-sel" : string.Empty) + "\" aria-pressed=\"" + (on ? "true" : "false") + "\" onclick=\"BookJs.time('" + Diary.Hhmm(t) + "')\">" + Ui.Time(t) + "</button>");
                }
                sb.Append("</div></div>");
            }
            return sb.ToString();
        }

        private static string SumHtml(SiteData site, SalonService? svc, string sty, DateTime? day, int at)
        {
            string who = sty == "any" ? "Any stylist" : site.Stylists.FirstOrDefault(x => x.Id == sty)?.Name ?? string.Empty;
            StringBuilder sb = new StringBuilder("<h2>Your visit</h2><dl class=\"bk-sumdl\">");
            sb.Append("<dt>Service</dt><dd>" + (svc == null ? "<em>Not chosen</em>" : Ui.Esc(svc.Name) + "<small>" + Ui.Minutes(svc.Minutes) + "</small>") + "</dd>");
            sb.Append("<dt>Stylist</dt><dd>" + (who.Length == 0 ? "<em>Not chosen</em>" : Ui.Esc(who)) + "</dd>");
            sb.Append("<dt>When</dt><dd>" + (day == null ? "<em>Not chosen</em>" : Ui.Day(day.Value) + (at >= 0 ? " at " + Ui.Time(at) : string.Empty)) + "</dd>");
            sb.Append("</dl><div class=\"bk-sumtot\"><span>Price</span><b>" + (svc == null ? "&ndash;" : PriceText(svc)) + "</b></div><p class=\"bk-sumnote\">No deposit. Pay at the salon by card or cash after your visit.</p>");
            return sb.ToString();
        }

        private string DataPart(SiteData site, Block b, bool first)
        {
            if (b.Source != "bookform")
            {
                return string.Empty;
            }
            SalonService? svc = Svc(site, preSvc);
            string sty = preSty;
            StringBuilder sb = new StringBuilder(Head(site, b, true, first ? "h1" : "h2"));
            sb.Append("<div class=\"bk-book\" id=\"bk-book\" data-svc=\"" + Ui.Esc(svc?.Id ?? string.Empty) + "\" data-sty=\"" + Ui.Esc(sty) + "\"><div class=\"bk-flow\">");
            sb.Append("<section class=\"bk-step\" id=\"bk-s1\"><h2><i>1</i>Choose a service</h2><div id=\"bk-svcs\">" + SvcHtml(site, svc?.Id ?? string.Empty) + "</div></section>");
            sb.Append("<section class=\"bk-step\" id=\"bk-s2\"><h2><i>2</i>Choose a stylist</h2><div id=\"bk-stys\">" + StyHtml(site, svc, sty) + "</div></section>");
            sb.Append("<section class=\"bk-step\" id=\"bk-s3\"><h2><i>3</i>Pick a day</h2><div id=\"bk-days\">" + DaysHtml(site, diary, svc, sty, null) + "</div></section>");
            sb.Append("<section class=\"bk-step\" id=\"bk-s4\"><h2><i>4</i>Pick a time</h2><div id=\"bk-times\">" + TimesHtml(site, diary, svc, sty, null, -1) + "</div></section>");
            sb.Append("<section class=\"bk-step\" id=\"bk-s5\"><h2><i>5</i>Your details</h2><form class=\"bk-form\" onsubmit=\"return BookJs.send()\" novalidate>");
            sb.Append("<div class=\"bk-two\"><label class=\"bk-field\"><span>Name</span><input type=\"text\" id=\"bk-name\" maxlength=\"60\" autocomplete=\"name\"><em class=\"bk-err\" id=\"bk-e-name\" role=\"alert\"></em></label>");
            sb.Append("<label class=\"bk-field\"><span>Phone</span><input type=\"tel\" id=\"bk-phone\" maxlength=\"24\" autocomplete=\"tel\"><em class=\"bk-err\" id=\"bk-e-phone\" role=\"alert\"></em></label></div>");
            sb.Append("<label class=\"bk-field\"><span>Email (optional)</span><input type=\"email\" id=\"bk-email\" maxlength=\"80\" autocomplete=\"email\"><em class=\"bk-err\" id=\"bk-e-email\" role=\"alert\"></em></label>");
            sb.Append("<label class=\"bk-field\"><span>Anything we should know? (optional)</span><textarea id=\"bk-note\" maxlength=\"300\" rows=\"3\" placeholder=\"For example: first visit, bringing a photo, sensitive scalp\"></textarea><small>Please do not share medical details.</small><em class=\"bk-err\" id=\"bk-e-note\" role=\"alert\"></em></label>");
            sb.Append("<em class=\"bk-err bk-slot\" id=\"bk-e-slot\" role=\"alert\"></em><button type=\"submit\" class=\"bk-btn bk-go\">Book this time</button><p class=\"bk-fine\">We use your details only for this booking and your visit history at the salon.</p></form></section>");
            sb.Append("</div><aside class=\"bk-sum\" id=\"bk-sum\" aria-live=\"polite\">" + SumHtml(site, svc, sty, null, -1) + "</aside></div><div class=\"bk-done\" id=\"bk-done\" aria-live=\"polite\"></div>");
            return Sec(b, sb.ToString());
        }

        private void Prepare(SiteData site, List<MediaItem> media, string key, string title, string desc)
        {
            HtmlDoc.SetTitle(title + " | " + site.Brand);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", desc);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_foot}", FootHtml(site))
                .Replace("{plhd_blocks}", PageHtml(site, media, key));
        }

        private static string HourText(DayHours h)
        {
            int a = Diary.Min(h.From);
            int b = Diary.Min(h.To);
            return h.Open && a >= 0 && b > a ? Ui.Time(a) + " &ndash; " + Ui.Time(b) : "Closed";
        }

        private static string HoursRows(SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 7; i++)
            {
                sb.Append("<li><b>" + Ui.DayNames[i].Substring(0, 3) + "</b><span>" + HourText(site.Hours[i]) + "</span></li>");
            }
            return sb.ToString();
        }

        private static string Tel(string phone)
        {
            return "tel:" + new string(phone.Where(ch => char.IsDigit(ch) || ch == '+').ToArray());
        }

        private static string FootHtml(SiteData site)
        {
            StringBuilder sb = new StringBuilder("<div class=\"bk-foot-in\"><div><a class=\"bk-logo\" href=\"Home\"><svg viewBox=\"0 0 40 40\" width=\"34\" height=\"34\" aria-hidden=\"true\"><rect width=\"40\" height=\"40\" rx=\"11\" fill=\"#231A17\"/><path d=\"M14 31c-1-7 13-7 13-14 0-4-3-7-7.5-7\" fill=\"none\" stroke=\"#E8C9A9\" stroke-width=\"3.2\" stroke-linecap=\"round\"/><path d=\"M20 32c0-5 9-5.5 9.5-11\" fill=\"none\" stroke=\"#C2553A\" stroke-width=\"2.6\" stroke-linecap=\"round\"/></svg><span>" + Ui.Esc(site.Brand) + "</span></a><p>" + Ui.Esc(site.Tagline) + "</p></div>");
            sb.Append("<div><h3>Visit</h3><ul><li>" + Ui.Esc(site.Street) + "</li><li>" + Ui.Esc(site.City) + "</li><li><a href=\"" + Ui.Esc(Tel(site.Phone)) + "\">" + Ui.Esc(site.Phone) + "</a></li><li><a href=\"mailto:" + Ui.Esc(site.Email) + "\">" + Ui.Esc(site.Email) + "</a></li></ul></div>");
            sb.Append("<div><h3>Hours</h3><ul class=\"bk-fhours\">" + HoursRows(site) + "</ul></div>");
            sb.Append("<div><h3>Salon</h3><ul><li><a href=\"Services\">Services &amp; prices</a></li><li><a href=\"Stylists\">Stylists</a></li><li><a href=\"Gallery\">Gallery</a></li><li><a href=\"Book\">Book online</a></li><li><a href=\"Contact\">Contact</a></li><li><a href=\"AdminLogin\">Staff sign in</a></li></ul></div></div>");
            sb.Append("<div class=\"bk-foot-b\"><span>&copy; " + DateTime.Today.Year + " " + Ui.Esc(site.Brand) + "</span><span>Pay at the salon by card or cash. No deposits.</span></div>");
            return sb.ToString();
        }

        private static string Tok(SiteData site, string v)
        {
            return v.Replace("{name}", site.Brand);
        }

        private static string TokH(SiteData site, string v)
        {
            return v.Replace("{name}", Ui.Esc(site.Brand));
        }

        private static string Lines(string v)
        {
            return Ui.Esc(v).Replace("\n", "<br>");
        }

        private static string Icon(string k)
        {
            string a = "<svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.8\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">";
            return k switch
            {
                "scissors" => a + "<circle cx=\"6.5\" cy=\"17.5\" r=\"2.8\"/><circle cx=\"17.5\" cy=\"17.5\" r=\"2.8\"/><path d=\"M8.6 15.6L18 4M15.4 15.6L6 4\"/></svg>",
                "drop" => a + "<path d=\"M12 3.5s6 6.4 6 10.6a6 6 0 0 1-12 0C6 9.9 12 3.5 12 3.5z\"/><path d=\"M9.5 14.5a2.6 2.6 0 0 0 2.5 2.5\"/></svg>",
                "spark" => a + "<path d=\"M12 3v5M12 16v5M3 12h5M16 12h5M6.5 6.5l2.6 2.6M14.9 14.9l2.6 2.6M17.5 6.5l-2.6 2.6M9.1 14.9l-2.6 2.6\"/></svg>",
                "leaf" => a + "<path d=\"M5 19c0-8 5-13 14-13-0 9-5 14-14 13zM5 19c3-4 6-7 10-9\"/></svg>",
                "cup" => a + "<path d=\"M4.5 9h12v5a5 5 0 0 1-5 5h-2a5 5 0 0 1-5-5zM16.5 10.5h1.5a2.5 2.5 0 0 1 0 5h-1.8M8 3.5c0 1.5 1.5 1.5 1.5 3M12 3.5c0 1.5 1.5 1.5 1.5 3\"/></svg>",
                "clock" => a + "<circle cx=\"12\" cy=\"12\" r=\"8.5\"/><path d=\"M12 7.5V12l3 2\"/></svg>",
                "pin" => a + "<path d=\"M12 21s-6.5-6-6.5-11a6.5 6.5 0 0 1 13 0c0 5-6.5 11-6.5 11z\"/><circle cx=\"12\" cy=\"10\" r=\"2.4\"/></svg>",
                "cal" => a + "<rect x=\"3.5\" y=\"5\" width=\"17\" height=\"15\" rx=\"2\"/><path d=\"M3.5 10h17M8 3v4M16 3v4\"/></svg>",
                "heart" => a + "<path d=\"M12 20s-7.5-4.6-7.5-10.2A4.3 4.3 0 0 1 12 7.3a4.3 4.3 0 0 1 7.5 2.5C19.5 15.4 12 20 12 20z\"/></svg>",
                "star" => a + "<path d=\"M12 3.5l2.6 5.4 5.9.8-4.3 4.1 1 5.8L12 16.9l-5.2 2.7 1-5.8-4.3-4.1 5.9-.8z\"/></svg>",
                "users" => a + "<circle cx=\"9\" cy=\"8\" r=\"3.2\"/><path d=\"M3 19c0-3.3 2.7-5.5 6-5.5s6 2.2 6 5.5\"/><circle cx=\"17\" cy=\"9\" r=\"2.4\"/><path d=\"M16 13.6c2.8 0 5 1.8 5 4.9\"/></svg>",
                "check" => "<svg viewBox=\"0 0 24 24\" width=\"16\" height=\"16\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2.4\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\"><path d=\"M5 12.5l4.5 4.5L19 7.5\"/></svg>",
                "phone" => a + "<path d=\"M6.5 3.5h3l1.5 4.5-2 1.5a11 11 0 0 0 5.5 5.5l1.5-2 4.5 1.5v3a2 2 0 0 1-2 2A16 16 0 0 1 4.5 5.5a2 2 0 0 1 2-2z\"/></svg>",
                "mail" => a + "<rect x=\"3.5\" y=\"5.5\" width=\"17\" height=\"13\" rx=\"2\"/><path d=\"M4 7l8 6 8-6\"/></svg>",
                "card" => a + "<rect x=\"3\" y=\"6\" width=\"18\" height=\"12\" rx=\"2\"/><path d=\"M3 10h18M7 15h4\"/></svg>",
                _ => string.Empty
            };
        }

        private static string Mv(List<MediaItem> media, string src, string alt, bool bg, bool eager)
        {
            if (src == string.Empty)
            {
                return string.Empty;
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            if (Blocks.IsVideo(src))
            {
                string poster = src.Substring(0, src.Length - 4) + ".webp";
                return "<video src=\"" + Ui.Esc(src) + "\"" + (File.Exists(Path.Combine(Store.Root, poster)) ? " poster=\"" + Ui.Esc(poster) + "\"" : string.Empty) + " playsinline " + (bg ? "autoplay muted loop aria-hidden=\"true\"" : "controls preload=\"metadata\" aria-label=\"" + Ui.Esc(alt) + "\"") + "></video>";
            }
            return "<img src=\"" + Ui.Esc(src) + "\" alt=\"" + Ui.Esc(m != null && m.Alt != string.Empty ? m.Alt : alt) + "\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">";
        }

        private static string Btn(SiteData site, string label, string link, bool main)
        {
            return label == string.Empty || link == string.Empty ? string.Empty : "<a class=\"bk-btn" + (main ? string.Empty : " bk-btn-ghost") + "\" href=\"" + Ui.Esc(link) + "\"" + (link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">" + Ui.Esc(Tok(site, label)) + "</a>";
        }

        private static string Btns(SiteData site, Block b)
        {
            string x = Btn(site, b.Label, b.Link, true) + Btn(site, b.Label2, b.Link2, false);
            return x == string.Empty ? string.Empty : "<div class=\"bk-bbtns\">" + x + "</div>";
        }

        private static string Kick(SiteData site, Block b)
        {
            return b.Kicker == string.Empty ? string.Empty : "<div class=\"bk-eyebrow\">" + Ui.Esc(Tok(site, b.Kicker)) + "</div>";
        }

        private static string H2(SiteData site, Block b, string tag = "h2")
        {
            return b.Title == string.Empty ? string.Empty : "<" + tag + " class=\"bk-bh2\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">";
        }

        private static string Rich(SiteData site, string html, string cls)
        {
            return html == string.Empty ? string.Empty : "<div class=\"bk-rich" + (cls == string.Empty ? string.Empty : " " + cls) + "\">" + TokH(site, html) + "</div>";
        }

        private static string Head(SiteData site, Block b, bool lead, string tag = "h2")
        {
            string x = Kick(site, b) + H2(site, b, tag) + (lead ? Rich(site, b.Text, "bk-blead") : string.Empty);
            return x == string.Empty ? string.Empty : "<div class=\"bk-bhead\">" + x + "</div>";
        }

        private static string Sec(Block b, string inner)
        {
            return "<section class=\"bk-wrap bk-sec\" id=\"" + Ui.Esc(b.Id) + "\">" + inner + "</section>";
        }

        private static string Hero(SiteData site, List<MediaItem> media, Block b, bool h1)
        {
            string tag = h1 ? "h1" : "h2";
            bool vid = Blocks.IsVideo(b.Media);
            string mv = Mv(media, b.Media, Tok(site, b.Title), vid, h1);
            return "<section class=\"bk-bhero" + (mv == string.Empty ? " bk-bplain" : string.Empty) + "\" id=\"" + Ui.Esc(b.Id) + "\">" + (mv != string.Empty ? "<div class=\"bk-bhero-p\">" + mv + "</div>" : string.Empty) + "<div class=\"bk-wrap bk-bhero-in\"><div class=\"bk-bhero-t\">" + Kick(site, b) + "<" + tag + " class=\"bk-btitle\">" + Ui.Esc(Tok(site, b.Title)) + "</" + tag + ">" + Rich(site, b.Text, "bk-blead") + Btns(site, b) + "</div></div></section>";
        }

        private static string Split(SiteData site, List<MediaItem> media, Block b)
        {
            string btn = Btn(site, b.Label, b.Link, true);
            return Sec(b, "<div class=\"bk-bsplit" + (b.Flip ? " bk-bflip" : string.Empty) + (b.Media == string.Empty ? " bk-bnomedia" : string.Empty) + "\">" + (b.Media != string.Empty ? "<div class=\"bk-bsm\">" + Mv(media, b.Media, Tok(site, b.Title), false, false) + "</div>" : string.Empty) + "<div class=\"bk-bst\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + (btn != string.Empty ? "<div class=\"bk-bbtns\">" + btn + "</div>" : string.Empty) + "</div></div>");
        }

        private static string Cards(SiteData site, Block b)
        {
            int n = Math.Clamp(b.Items.Count, 1, 4);
            if (b.Items.Count == 6)
            {
                n = 3;
            }
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"bk-bcards bk-n" + n + "\">");
            foreach (BlockItem it in b.Items)
            {
                string ico = Icon(it.Icon);
                string inner = (ico != string.Empty ? "<span class=\"bk-wico\">" + ico + "</span>" : string.Empty) + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b>" + (it.Text != string.Empty ? "<p>" + Lines(Tok(site, it.Text)) + "</p>" : string.Empty);
                if (it.Link != string.Empty)
                {
                    sb.Append("<a class=\"bk-bcard\" href=\"" + Ui.Esc(it.Link) + "\">" + inner + "<span class=\"bk-bmore\">" + Ui.Esc(it.Label != string.Empty ? it.Label : "Learn more") + " &rarr;</span></a>");
                }
                else
                {
                    sb.Append("<div class=\"bk-bcard\">" + inner + "</div>");
                }
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Steps(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<ol class=\"bk-bsteps bk-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            int i = 1;
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<li><i>" + i++ + "</i><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><p>" + Lines(Tok(site, it.Text)) + "</p></li>");
            }
            sb.Append("</ol>");
            return Sec(b, sb.ToString());
        }

        private static string StatRow(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"bk-bstats bk-n" + Math.Clamp(b.Items.Count, 1, 6) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div><b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Initials(string name)
        {
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? "?" : string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        }

        private static string People(SiteData site, List<MediaItem> media, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, true) + "<div class=\"bk-bpeople bk-n" + Math.Clamp(b.Items.Count, 1, 4) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"bk-bperson\">" + (it.Media != string.Empty && !Blocks.IsVideo(it.Media) ? Mv(media, it.Media, it.Title, false, false) : "<span class=\"bk-bnoface\">" + Ui.Esc(Initials(it.Title)) + "</span>") + "<b>" + Ui.Esc(Tok(site, it.Title)) + "</b><span>" + Lines(Tok(site, it.Text)) + "</span></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Boxes(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder(Head(site, b, false) + "<div class=\"bk-bboxes bk-n" + Math.Clamp(b.Items.Count, 1, 3) + "\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<div class=\"bk-bbox\">" + (it.Title != string.Empty ? "<h3>" + Ui.Esc(Tok(site, it.Title)) + "</h3>" : string.Empty) + "<ul class=\"bk-bticks\">");
                foreach (string line in it.Text.Split('\n').Select(x => x.Trim()).Where(x => x != string.Empty))
                {
                    sb.Append("<li>" + Icon("check") + "<span>" + Ui.Esc(Tok(site, line)) + "</span></li>");
                }
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
            return Sec(b, sb.ToString());
        }

        private static string Faq(SiteData site, Block b)
        {
            StringBuilder sb = new StringBuilder("<div class=\"bk-bfaq\">" + Head(site, b, false) + "<div class=\"bk-bfaqs\">");
            foreach (BlockItem it in b.Items)
            {
                sb.Append("<details class=\"bk-bq\"><summary>" + Ui.Esc(Tok(site, it.Title)) + "</summary><p>" + Lines(Tok(site, it.Text)) + "</p></details>");
            }
            sb.Append("</div></div>");
            return Sec(b, sb.ToString());
        }

        private static string Quote(SiteData site, List<MediaItem> media, Block b)
        {
            return Sec(b, "<figure class=\"bk-bquote\">" + (b.Media != string.Empty && !Blocks.IsVideo(b.Media) ? Mv(media, b.Media, b.Title, false, false) : string.Empty) + "<blockquote>" + Lines(Tok(site, b.Text)) + "</blockquote>" + (b.Title != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Title)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Wide(SiteData site, List<MediaItem> media, Block b)
        {
            return b.Media == string.Empty ? string.Empty : Sec(b, "<figure class=\"bk-bmedia\">" + Mv(media, b.Media, b.Text, false, false) + (b.Text != string.Empty ? "<figcaption>" + Ui.Esc(Tok(site, b.Text)) + "</figcaption>" : string.Empty) + "</figure>");
        }

        private static string Cta(SiteData site, Block b)
        {
            return Sec(b, "<div class=\"bk-bcta\"><div>" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, "bk-blead") + "</div>" + Btns(site, b) + "</div>");
        }

        private string PageHtml(SiteData site, List<MediaItem> media, string key)
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            if (!site.Pages.TryGetValue(key, out PageDoc? doc))
            {
                return string.Empty;
            }
            foreach (Block b in doc.Blocks.Where(x => !x.Hidden))
            {
                string html = b.Type switch
                {
                    "hero" => Hero(site, media, b, first),
                    "text" => Sec(b, "<div class=\"bk-btext\">" + Kick(site, b) + H2(site, b) + Rich(site, b.Text, string.Empty) + "</div>"),
                    "split" => Split(site, media, b),
                    "cards" => Cards(site, b),
                    "steps" => Steps(site, b),
                    "stats" => StatRow(site, b),
                    "people" => People(site, media, b),
                    "boxes" => Boxes(site, b),
                    "faq" => Faq(site, b),
                    "quote" => Quote(site, media, b),
                    "media" => Wide(site, media, b),
                    "cta" => Cta(site, b),
                    "data" => DataPart(site, b, first),
                    _ => string.Empty
                };
                if (html.Length > 0 && (b.Type == "hero" || b.Type == "data"))
                {
                    first = false;
                }
                sb.Append(html);
            }
            return sb.ToString();
        }

    }
}
