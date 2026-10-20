using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminStylists : WebPage
    {
        private const string AreaKey = "stylists";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Stylists | Staff");
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
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                response.SetElementContents("sy-main", NoAccess("stylists"));
                return response;
            }
            response.SetElementContents("sy-main", "<div class=\"sy-ph\"><div><h1>Stylists</h1><p>Profiles for the website, the services each person does, and their working hours. Online booking only offers times inside these hours.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"sy-btn\" onclick=\"AdminStylistsJs.edit('')\">Add a stylist</button>" : string.Empty) + "</div><div id=\"sy-list\">" + ListHtml(site) + "</div>");
            return response;
        }

        private static string Week(SiteData site, Stylist s)
        {
            List<string> d = new List<string>();
            for (int i = 0; i < 7; i++)
            {
                if (s.Week[i].On)
                {
                    d.Add(Ui.DayNames[i].Substring(0, 3) + " " + s.Week[i].From + "&ndash;" + s.Week[i].To);
                }
            }
            return d.Count == 0 ? "No working days" : string.Join("<br>", d);
        }

        private static string ListHtml(SiteData site)
        {
            if (site.Stylists.Count == 0)
            {
                return "<div class=\"sy-panel sy-empty\">No stylists yet.</div>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"sy-cards\">");
            foreach (Stylist s in site.Stylists.OrderBy(x => x.Order))
            {
                List<TimeOff> off = s.Off.Where(o => (Ui.ParseDay(o.To.Length > 0 ? o.To : o.From) ?? DateTime.MinValue) >= DateTime.Today).ToList();
                sb.Append("<button type=\"button\" class=\"sy-sc" + (s.Active ? string.Empty : " sy-inact") + "\" onclick=\"AdminStylistsJs.edit('" + Ui.Js(s.Id) + "')\"><span class=\"sy-sch\">" + (s.Photo.Length > 0 ? "<img src=\"" + Ui.Esc(s.Photo) + "\" alt=\"\" width=\"56\" height=\"56\" loading=\"lazy\">" : "<span class=\"sy-ini\">" + Ui.Esc(Ui.Initials(s.Name)) + "</span>") + "<span><b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Role) + "</small>" + (s.Active ? string.Empty : "<span class=\"sy-pill sy-pill-off\">Not taking bookings</span>") + "</span></span><span class=\"sy-scw\">" + Week(site, s) + "</span><span class=\"sy-scf\">" + Ui.Count(s.Services.Count, "service", "services") + (off.Count > 0 ? " &middot; " + Ui.Count(off.Count, "day off ahead", "days off ahead") : string.Empty) + "</span></button>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string Check(string k, string label, bool on, string d)
        {
            return "<label class=\"sy-check\"><input type=\"checkbox\" data-k=\"" + k + "\"" + (on ? " checked" : string.Empty) + d + "> " + label + "</label>";
        }

        private static string OffRow(TimeOff o, string d)
        {
            return "<div class=\"sy-offr\"><input type=\"date\" class=\"sy-ofrom\" value=\"" + Ui.Esc(o.From) + "\" aria-label=\"First day off\"" + d + "><span>to</span><input type=\"date\" class=\"sy-oto\" value=\"" + Ui.Esc(o.To) + "\" aria-label=\"Last day off\"" + d + "><input type=\"text\" class=\"sy-onote\" maxlength=\"60\" value=\"" + Ui.Esc(o.Note) + "\" placeholder=\"Note, e.g. vacation\" aria-label=\"Note\"" + d + ">" + (d.Length == 0 ? "<button type=\"button\" class=\"sy-ibtn\" aria-label=\"Remove\" onclick=\"AdminStylistsJs.offDrop(this)\">&times;</button>" : string.Empty) + "</div>";
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("pid"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open stylist", id, "You do not have access to stylists.");
            }
            bool e = Access.Can(c.U, AreaKey, 'E');
            bool x = Access.Can(c.U, AreaKey, 'X');
            if (id.Length == 0 && !e)
            {
                return await Refuse(response, c, "add stylist", string.Empty, "You can look at stylists but not add them.");
            }
            SiteData site = await Store.LoadSite();
            Stylist s = site.Stylists.FirstOrDefault(v => v.Id == id) ?? new Stylist { Active = true, Order = (site.Stylists.Count + 1) * 10, Week = Enumerable.Range(0, 7).Select(i => new WorkDay { On = i >= 1 && i <= 5, From = "09:00", To = "17:00" }).ToList() };
            List<MediaItem> media = await Media.Load();
            string d = e ? string.Empty : " disabled";
            string dx = x ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<form class=\"sy-stack\" id=\"sy-form\" onsubmit=\"return AdminStylistsJs.save('" + Ui.Js(s.Id) + "')\" novalidate><div class=\"sy-prof\"><div class=\"sy-photo\"><div class=\"sy-pv\" id=\"sy-pv\">" + PhotoPreview(media, s.Photo) + "</div><input type=\"hidden\" id=\"sy-f-photo\" data-k=\"photo\" value=\"" + Ui.Esc(s.Photo) + "\">" + (e ? "<button type=\"button\" class=\"sy-btn sy-btn-ghost sy-btn-sm\" onclick=\"AdminStylistsJs.pkOpen()\">Choose photo</button><button type=\"button\" class=\"sy-btn sy-btn-ghost sy-btn-sm\" onclick=\"AdminStylistsJs.noPhoto()\">Remove</button>" : string.Empty) + "</div><div class=\"sy-stack\">");
            sb.Append("<div class=\"sy-grid2\"><label class=\"sy-field\"><span>Name</span><input type=\"text\" id=\"sy-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(s.Name) + "\"" + d + "><em class=\"sy-err\" id=\"sy-e-name\"></em></label><label class=\"sy-field\"><span>Role</span><input type=\"text\" id=\"sy-f-role\" data-k=\"role\" maxlength=\"60\" value=\"" + Ui.Esc(s.Role) + "\" placeholder=\"Senior stylist\"" + d + "></label></div>");
            sb.Append("<label class=\"sy-field\"><span>Short bio</span><textarea id=\"sy-f-bio\" data-k=\"bio\" maxlength=\"500\" rows=\"3\"" + d + ">" + Ui.Esc(s.Bio) + "</textarea><em class=\"sy-err\" id=\"sy-e-bio\"></em></label>");
            sb.Append("<div class=\"sy-grid2\"><label class=\"sy-field\"><span>Specialties</span><input type=\"text\" id=\"sy-f-spec\" data-k=\"spec\" maxlength=\"120\" value=\"" + Ui.Esc(s.Specialties) + "\"" + d + "></label><label class=\"sy-field\"><span>Order</span><input type=\"number\" id=\"sy-f-order\" data-k=\"order\" min=\"0\" max=\"9999\" value=\"" + s.Order + "\"" + d + "></label></div>");
            sb.Append(Check("active", "Taking bookings and shown on the website", s.Active, d) + "</div></div>");
            sb.Append("<fieldset class=\"sy-fs\"><legend>Services " + Ui.Esc(s.Name.Length > 0 ? s.Name.Split(' ')[0] : "they") + " offers</legend><div class=\"sy-svcs\">");
            foreach (Category cat in site.Categories)
            {
                foreach (SalonService v in site.Services.Where(v => v.Category == cat.Key).OrderBy(v => v.Order))
                {
                    sb.Append("<label class=\"sy-check\"><input type=\"checkbox\" class=\"sy-svc\" value=\"" + Ui.Esc(v.Id) + "\"" + (s.Services.Contains(v.Id) ? " checked" : string.Empty) + d + "> " + Ui.Esc(v.Name) + (v.Active ? string.Empty : " <small>(hidden)</small>") + "</label>");
                }
            }
            sb.Append("</div><em class=\"sy-err\" id=\"sy-e-svc\"></em></fieldset>");
            sb.Append("<fieldset class=\"sy-fs\"><legend>Working hours</legend>" + (x ? "<p class=\"sy-hint\">Times are limited by the salon&rsquo;s opening hours in Settings. Leave the break empty if there is none.</p>" : "<p class=\"sy-hint\">Only staff with the hours right can change these.</p>") + "<div class=\"sy-wk\"><div class=\"sy-wkh\"><span>Day</span><span>From</span><span>To</span><span>Break from</span><span>Break to</span></div>");
            for (int i = 0; i < 7; i++)
            {
                WorkDay w = s.Week[i];
                bool salonOpen = site.Hours[i].Open;
                sb.Append("<div class=\"sy-wkr\" data-day=\"" + i + "\"><label class=\"sy-check\"><input type=\"checkbox\" class=\"sy-won\"" + (w.On ? " checked" : string.Empty) + dx + "> " + Ui.DayNames[i] + (salonOpen ? string.Empty : " <small>(salon closed)</small>") + "</label><input type=\"time\" class=\"sy-wf\" value=\"" + Ui.Esc(w.From) + "\" step=\"900\" aria-label=\"" + Ui.DayNames[i] + " from\"" + dx + "><input type=\"time\" class=\"sy-wt\" value=\"" + Ui.Esc(w.To) + "\" step=\"900\" aria-label=\"" + Ui.DayNames[i] + " to\"" + dx + "><input type=\"time\" class=\"sy-wbf\" value=\"" + Ui.Esc(w.BreakFrom) + "\" step=\"900\" aria-label=\"" + Ui.DayNames[i] + " break from\"" + dx + "><input type=\"time\" class=\"sy-wbt\" value=\"" + Ui.Esc(w.BreakTo) + "\" step=\"900\" aria-label=\"" + Ui.DayNames[i] + " break to\"" + dx + "></div>");
            }
            sb.Append("</div><em class=\"sy-err\" id=\"sy-e-week\"></em></fieldset>");
            sb.Append("<fieldset class=\"sy-fs\"><legend>Days off</legend><div id=\"sy-offs\" class=\"sy-offs\">");
            foreach (TimeOff o in s.Off.OrderBy(o => o.From))
            {
                sb.Append(OffRow(o, dx));
            }
            sb.Append("</div>" + (x ? "<button type=\"button\" class=\"sy-btn sy-btn-ghost sy-btn-sm sy-addoff\" onclick=\"AdminStylistsJs.offAdd()\">+ Add days off</button><template id=\"sy-offtpl\">" + OffRow(new TimeOff(), string.Empty) + "</template>" : string.Empty) + "<em class=\"sy-err\" id=\"sy-e-off\"></em></fieldset>");
            sb.Append("<em class=\"sy-err\" id=\"sy-e-form\" role=\"alert\"></em>");
            if (e)
            {
                sb.Append("<div class=\"sy-acts\"><button type=\"submit\" class=\"sy-btn\">Save</button><button type=\"button\" class=\"sy-btn sy-btn-ghost\" onclick=\"AdminStylistsJs.closeModal()\">Cancel</button>" + (s.Id.Length > 0 ? "<button type=\"button\" class=\"sy-btn sy-btn-danger sy-push\" onclick=\"AdminStylistsJs.drop('" + Ui.Js(s.Id) + "','" + Ui.Js(s.Name) + "')\">Delete</button>" : string.Empty) + "</div>");
            }
            sb.Append("</form>");
            response.SetElementContents("sy-mbody", sb.ToString());
            response.ExecuteScript("AdminStylistsJs.openModal('" + (s.Id.Length == 0 ? "Add a stylist" : Ui.Js(s.Name)) + "');");
            return response;
        }

        private static string PhotoPreview(List<MediaItem> media, string src)
        {
            if (src.Length == 0)
            {
                return "<span class=\"sy-ini\">?</span>";
            }
            MediaItem? m = media.FirstOrDefault(x => x.Path == src);
            return "<img src=\"" + Ui.Esc(m != null && m.Thumb.Length > 0 ? m.Thumb : src) + "\" alt=\"\">";
        }

        private sealed class DayIn
        {
            public bool On { get; set; }
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
            public string BreakFrom { get; set; } = string.Empty;
            public string BreakTo { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("pid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save stylist", id, "You can look at stylists but not change them.");
            }
            bool x = Access.Can(c.U, AreaKey, 'X');
            JsonSerializerOptions opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string name = Ui.Clip(GetDataValue("name"), 80);
            string bio = (GetDataValue("bio") ?? string.Empty).Trim();
            string photo = Ui.Clip(GetDataValue("photo"), 200);
            Dictionary<string, string> err = new Dictionary<string, string>();
            if (name.Length < 2 || name.Length > 60)
            {
                err["name"] = "Enter a name.";
            }
            if (bio.Length > 500)
            {
                err["bio"] = "Keep the bio under 500 characters.";
            }
            List<string> svcs;
            List<DayIn>? week = null;
            List<TimeOff>? off = null;
            try
            {
                svcs = JsonSerializer.Deserialize<List<string>>(GetDataValue("svcs") ?? "[]", opts) ?? new List<string>();
                if (x)
                {
                    week = JsonSerializer.Deserialize<List<DayIn>>(GetDataValue("week") ?? "[]", opts);
                    off = JsonSerializer.Deserialize<List<TimeOff>>(GetDataValue("off") ?? "[]", opts);
                }
            }
            catch (JsonException)
            {
                response.SetElementContents("sy-e-form", "The form could not be read. Reload and try again.");
                return response;
            }
            List<WorkDay> days = new List<WorkDay>();
            if (x)
            {
                if (week == null || week.Count != 7)
                {
                    err["week"] = "The working hours could not be read.";
                }
                else
                {
                    for (int i = 0; i < 7; i++)
                    {
                        DayIn w = week[i];
                        int a = Diary.Min(w.From);
                        int b = Diary.Min(w.To);
                        int ba = Diary.Min(w.BreakFrom);
                        int bb = Diary.Min(w.BreakTo);
                        bool noBreak = (w.BreakFrom ?? string.Empty).Trim().Length == 0 && (w.BreakTo ?? string.Empty).Trim().Length == 0;
                        if (w.On && (a < 0 || b <= a))
                        {
                            err["week"] = Ui.DayNames[i] + ": the end time must be after the start time.";
                            break;
                        }
                        if (w.On && !noBreak && (ba < 0 || bb <= ba || ba <= a || bb >= b))
                        {
                            err["week"] = Ui.DayNames[i] + ": the break must sit inside the working hours, or leave it empty.";
                            break;
                        }
                        days.Add(new WorkDay { On = w.On, From = a >= 0 ? Diary.Hhmm(a) : "09:00", To = b >= 0 ? Diary.Hhmm(b) : "17:00", BreakFrom = noBreak || ba < 0 ? string.Empty : Diary.Hhmm(ba), BreakTo = noBreak || bb < 0 ? string.Empty : Diary.Hhmm(bb) });
                    }
                }
                foreach (TimeOff o in off ?? new List<TimeOff>())
                {
                    DateTime? f = Ui.ParseDay(o.From);
                    DateTime? t = Ui.ParseDay(o.To.Length > 0 ? o.To : o.From);
                    if (f == null || t == null || t < f || (t.Value - f.Value).TotalDays > 366)
                    {
                        err["off"] = "Each day off needs a first day, and the last day cannot be before it.";
                        break;
                    }
                    o.From = Ui.Iso(f.Value);
                    o.To = Ui.Iso(t.Value);
                    o.Note = Ui.Clip(o.Note, 60);
                }
                if ((off?.Count ?? 0) > 60)
                {
                    err["off"] = "Up to 60 periods off. Remove old ones first.";
                }
            }
            string saved = string.Empty;
            List<Booking> clashes = new List<Booking>();
            if (err.Count == 0)
            {
                List<MediaItem> media = await Media.Load();
                if (photo.Length > 0 && !media.Any(m => m.Path == photo && m.Kind == "photo"))
                {
                    err["name"] = "The photo is no longer in the media library. Choose it again.";
                }
                else
                {
                    using (await Store.Hold())
                    {
                        SiteData site = await Store.LoadSite();
                        Stylist? s = id.Length > 0 ? site.Stylists.FirstOrDefault(v => v.Id == id) : null;
                        if (s == null)
                        {
                            string key = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                            key = key.Length == 0 ? "stylist" : key.Length > 24 ? key.Substring(0, 24) : key;
                            string k = key;
                            int i = 2;
                            while (site.Stylists.Any(v => v.Id == k))
                            {
                                k = key + "-" + i++;
                            }
                            s = new Stylist { Id = k, Week = days.Count == 7 ? days : Enumerable.Range(0, 7).Select(n => new WorkDay()).ToList() };
                            site.Stylists.Add(s);
                        }
                        s.Name = name;
                        s.Role = Ui.Clip(GetDataValue("role"), 60);
                        s.Bio = bio;
                        s.Specialties = Ui.Clip(GetDataValue("spec"), 120);
                        s.Photo = photo;
                        s.Order = Math.Clamp(Ui.Int(GetDataValue("order"), s.Order), 0, 9999);
                        s.Active = GetDataValue("active") == "1";
                        s.Services = svcs.Where(v => site.Services.Any(z => z.Id == v)).Distinct().ToList();
                        if (x)
                        {
                            s.Week = days;
                            s.Off = off ?? new List<TimeOff>();
                        }
                        await Store.SaveSite(site);
                        saved = s.Name;
                        clashes = Diary.Clashes(site, await Store.LoadBookings(), s);
                    }
                }
            }
            foreach (string k in new[] { "name", "bio", "week", "off" })
            {
                response.SetElementContents("sy-e-" + k, err.TryGetValue(k, out string? m) ? Ui.Esc(m) : string.Empty);
            }
            if (saved.Length == 0)
            {
                return response;
            }
            await Store.Log(c.U.Id, id.Length == 0 ? "add stylist" : "edit stylist" + (x ? " and hours" : string.Empty), saved, true);
            response.SetElementContents("sy-list", ListHtml(await Store.LoadSite()));
            string note = clashes.Count == 0 ? "Stylist saved" : "Saved. " + Ui.Count(clashes.Count, "upcoming booking now falls", "upcoming bookings now fall") + " outside these hours, first on " + Ui.Day(clashes[0].Start) + ". Move them in Bookings.";
            response.ExecuteScript("AdminStylistsJs.shut();AdminStylistsJs.toast('" + Ui.Js(note) + "');");
            return response;
        }

        public async Task<ApiResponse> Drop()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("pid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete stylist", id, "You can look at stylists but not change them.");
            }
            List<Booking> bookings = await Store.LoadBookings();
            string name = string.Empty;
            string msg = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                Stylist? s = site.Stylists.FirstOrDefault(v => v.Id == id);
                if (s == null)
                {
                    msg = "That stylist was not found.";
                }
                else if (bookings.Any(b => b.StylistId == id))
                {
                    msg = s.Name + " has bookings on record. Untick Taking bookings instead.";
                }
                else
                {
                    name = s.Name;
                    site.Stylists.Remove(s);
                    await Store.SaveSite(site);
                }
            }
            if (name.Length == 0)
            {
                await Store.Log(c.U.Id, "delete stylist", id, false);
                response.ExecuteScript("AdminStylistsJs.toast('" + Ui.Js(msg) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "delete stylist", name, true);
            response.SetElementContents("sy-list", ListHtml(await Store.LoadSite()));
            response.ExecuteScript("AdminStylistsJs.shut();AdminStylistsJs.toast('Stylist deleted');");
            return response;
        }

        private static string PickGrid(List<MediaItem> media, string term)
        {
            List<MediaItem> rows = media.Where(m => m.Kind == "photo" && (term.Length == 0 || Ui.Has(m.Name, term) || Ui.Has(m.Alt, term))).OrderByDescending(m => m.Added).Take(300).ToList();
            if (rows.Count == 0)
            {
                return "<p class=\"sy-hint\">No photos match. Upload one above.</p>";
            }
            return string.Concat(rows.Select(m => "<button type=\"button\" class=\"sy-pki\" data-src=\"" + Ui.Esc(m.Path) + "\" data-thumb=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" onclick=\"AdminStylistsJs.pkPick(this)\" title=\"" + Ui.Esc(m.Name) + "\"><img src=\"" + Ui.Esc(m.Thumb.Length > 0 ? m.Thumb : m.Path) + "\" alt=\"\" loading=\"lazy\"><span>" + Ui.Esc(m.Name) + "</span></button>"));
        }

        public async Task<ApiResponse> Photos()
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
            response.SetElementContents("sy-pkgrid", PickGrid(await Media.Load(), Ui.Clip(GetDataValue("pterm"), 40)));
            return response;
        }

        public async Task<ApiResponse> Upload()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E') || !Access.Can(c.U, "media", 'E'))
            {
                response.ExecuteScript("AdminStylistsJs.pkUploaded('You need the media library edit right to upload.');");
                return response;
            }
            SiteData site = await Store.LoadSite();
            List<Incoming> files = await Media.Received();
            if (files.Count == 0)
            {
                response.ExecuteScript("AdminStylistsJs.pkUploaded('Choose a photo first.');");
                return response;
            }
            (int added, List<string> errors) = await Media.AddAll(files, site.MaxVideoMb, c.U.Name);
            if (added > 0)
            {
                await Store.Log(c.U.Id, "upload media", Ui.Count(added, "file", "files"), true);
            }
            response.SetElementContents("sy-pkgrid", PickGrid(await Media.Load(), string.Empty));
            response.ExecuteScript("AdminStylistsJs.pkUploaded('" + Ui.Js(((added > 0 ? Ui.Count(added, "file", "files") + " added. " : string.Empty) + string.Join(" ", errors)).Trim()) + "');");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminStylistsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminStylistsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"sy-panel\"><h1>No access</h1><p class=\"sy-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("sy-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sy-menu", await Auth.Menu("sy", "AdminStylists", c));
            response.SetElementContents("sy-who", Auth.Who("sy", c));
        }

    }
}
