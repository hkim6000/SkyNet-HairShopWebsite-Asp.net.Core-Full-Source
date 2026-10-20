using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminGuests : WebPage
    {
        private const string AreaKey = "guests";
        private const int Size = 20;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Guests | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private sealed class Tally
        {
            public int Done { get; set; }
            public int Upcoming { get; set; }
            public int NoShows { get; set; }
            public decimal Spent { get; set; }
            public decimal Tips { get; set; }
            public DateTime? Last { get; set; }
            public DateTime? Next { get; set; }
        }

        private static Tally TallyOf(List<Booking> visits)
        {
            DateTime now = DateTime.Now;
            Tally t = new Tally();
            t.Done = visits.Count(b => b.Status == "done");
            t.Upcoming = visits.Count(b => b.Status == "booked" && b.Start >= now.Date);
            t.NoShows = visits.Count(b => b.Status == "noshow");
            t.Spent = visits.Where(b => b.Paid != null).Sum(b => b.Paid!.ServiceTotal - b.Paid.Discount);
            t.Tips = visits.Where(b => b.Paid != null).Sum(b => b.Paid!.Tip);
            t.Last = visits.Where(b => b.Status == "done").Select(b => (DateTime?)b.Start).Max();
            t.Next = visits.Where(b => b.Status is "booked" or "arrived" && b.Start >= now.Date).Select(b => (DateTime?)b.Start).Min();
            return t;
        }

        private static List<Booking> VisitsOf(Ctx c, List<Booking> bookings, string gid)
        {
            string own = Access.OwnStylist(c.U);
            return bookings.Where(b => b.GuestId == gid && (own == string.Empty || b.StylistId == own)).ToList();
        }

        private static List<Guest> Visible(Ctx c, List<Guest> guests, List<Booking> bookings)
        {
            string own = Access.OwnStylist(c.U);
            if (own.Length == 0)
            {
                return guests;
            }
            HashSet<string> ids = bookings.Where(b => b.StylistId == own).Select(b => b.GuestId).ToHashSet();
            return guests.Where(g => ids.Contains(g.Id)).ToList();
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
                response.SetElementContents("gu-main", NoAccess("the guest register"));
                return response;
            }
            List<Guest> guests = await Store.LoadGuests();
            List<Booking> bookings = await Store.LoadBookings();
            StringBuilder sb = new StringBuilder("<div class=\"gu-ph\"><div><h1>Guests</h1><p>One record per guest, matched by phone or email. Every visit, from booking to check-in to payment.</p></div>" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"gu-btn\" onclick=\"AdminGuestsJs.open('')\">Add a guest</button>" : string.Empty) + "</div>");
            sb.Append("<div class=\"gu-tools\"><input type=\"search\" id=\"gu-term\" placeholder=\"Search name, phone or email\" maxlength=\"40\" oninput=\"AdminGuestsJs.typed()\" aria-label=\"Search guests\"><select id=\"gu-sort\" onchange=\"AdminGuestsJs.go()\" aria-label=\"Sort\"><option value=\"recent\">Most recent visit</option><option value=\"name\">Name A&ndash;Z</option><option value=\"spent\">Most spent</option><option value=\"next\">Next visit</option><option value=\"new\">Newest guests</option></select></div><div id=\"gu-list\">" + ListHtml(c, Visible(c, guests, bookings), bookings, string.Empty, "recent", 1) + "</div>");
            response.SetElementContents("gu-main", sb.ToString());
            response.ExecuteScript("AdminGuestsJs.loaded();");
            return response;
        }

        public async Task<ApiResponse> Rows()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "list guests", string.Empty, "You do not have access to the guest register.");
            }
            List<Guest> guests = await Store.LoadGuests();
            List<Booking> bookings = await Store.LoadBookings();
            response.SetElementContents("gu-list", ListHtml(c, Visible(c, guests, bookings), bookings, Ui.Clip(GetDataValue("term"), 40), GetDataValue("sort") ?? "recent", Math.Max(1, Ui.Int(GetDataValue("pg"), 1))));
            return response;
        }

        private static string ListHtml(Ctx c, List<Guest> guests, List<Booking> bookings, string term, string sort, int page)
        {
            string d = Diary.Digits(term);
            List<(Guest G, Tally T)> rows = guests.Where(g => term.Length == 0 || Ui.Has(g.Name, term) || Ui.Has(g.Email, term) || d.Length >= 3 && Diary.Digits(g.Phone).Contains(d)).Select(g => (g, TallyOf(VisitsOf(c, bookings, g.Id)))).ToList();
            rows = sort switch
            {
                "name" => rows.OrderBy(r => r.G.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                "spent" => rows.OrderByDescending(r => r.T.Spent).ThenBy(r => r.G.Name).ToList(),
                "next" => rows.OrderBy(r => r.T.Next ?? DateTime.MaxValue).ThenBy(r => r.G.Name).ToList(),
                "new" => rows.OrderByDescending(r => r.G.Created).ToList(),
                _ => rows.OrderByDescending(r => r.T.Last ?? r.T.Next ?? r.G.Created).ToList()
            };
            if (rows.Count == 0)
            {
                return "<div class=\"gu-panel gu-empty\">" + (guests.Count == 0 ? "No guests yet. They are added with their first booking, or with Add a guest." : "No guests match.") + "</div>";
            }
            int pages = (rows.Count + Size - 1) / Size;
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"gu-wrapt\"><table class=\"gu-table\"><thead><tr><th>Guest</th><th>Contact</th><th class=\"gu-num\">Visits</th><th>Last visit</th><th>Next visit</th><th class=\"gu-num\">Spent</th><th class=\"gu-num\">Tips</th></tr></thead><tbody>");
            foreach ((Guest g, Tally t) in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<tr class=\"gu-row\" onclick=\"AdminGuestsJs.open('" + Ui.Js(g.Id) + "')\"><td><b>" + Ui.Esc(g.Name) + "</b>" + (g.Notes.Length > 0 ? "<small>Has notes</small>" : string.Empty) + "</td><td>" + Ui.Esc(g.Phone) + "<small>" + Ui.Esc(g.Email) + "</small></td><td class=\"gu-num\">" + t.Done + (t.NoShows > 0 ? "<small>" + t.NoShows + " no-show</small>" : string.Empty) + "</td><td>" + (t.Last == null ? "&ndash;" : Ui.Day(t.Last.Value)) + "</td><td>" + (t.Next == null ? "&ndash;" : Ui.Day(t.Next.Value) + "<small>" + Ui.Time(t.Next.Value) + "</small>") + "</td><td class=\"gu-num\">" + Ui.Money(t.Spent) + "</td><td class=\"gu-num\">" + Ui.Money(t.Tips) + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("gu", "AdminGuestsJs.page", page, pages, rows.Count, "guest", "guests"));
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
            string gid = Ui.Clip(GetDataValue("gid"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open guest", gid, "You do not have access to the guest register.");
            }
            SiteData site = await Store.LoadSite();
            List<Booking> bookings = await Store.LoadBookings();
            if (gid.Length == 0)
            {
                if (!Access.Can(c.U, AreaKey, 'E'))
                {
                    return await Refuse(response, c, "add guest", string.Empty, "You can look at guests but not add them.");
                }
                response.SetElementContents("gu-mbody", FormHtml(new Guest(), true));
                response.ExecuteScript("AdminGuestsJs.opened('Add a guest','');");
                return response;
            }
            Guest? g = Visible(c, await Store.LoadGuests(), bookings).FirstOrDefault(x => x.Id == gid);
            if (g == null)
            {
                response.ExecuteScript("AdminGuestsJs.toast('That guest was not found.');AdminGuestsJs.gone();");
                return response;
            }
            response.SetElementContents("gu-mbody", DetailHtml(c, site, g, VisitsOf(c, bookings, g.Id)));
            response.ExecuteScript("AdminGuestsJs.opened('" + Ui.Js(g.Name) + "','" + Ui.Js(g.Id) + "');");
            return response;
        }

        private static string FormHtml(Guest g, bool canE)
        {
            string ro = canE ? string.Empty : " readonly";
            StringBuilder sb = new StringBuilder("<form class=\"gu-stack\" id=\"gu-form\" onsubmit=\"return AdminGuestsJs.save('" + Ui.Js(g.Id) + "')\" novalidate><div class=\"gu-grid3\">");
            sb.Append("<label class=\"gu-field\"><span>Name</span><input type=\"text\" id=\"gu-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(g.Name) + "\"" + ro + "><em class=\"gu-err\" id=\"gu-e-name\"></em></label>");
            sb.Append("<label class=\"gu-field\"><span>Phone</span><input type=\"tel\" id=\"gu-f-phone\" data-k=\"phone\" maxlength=\"24\" value=\"" + Ui.Esc(g.Phone) + "\"" + ro + "><em class=\"gu-err\" id=\"gu-e-phone\"></em></label>");
            sb.Append("<label class=\"gu-field\"><span>Email</span><input type=\"email\" id=\"gu-f-email\" data-k=\"email\" maxlength=\"80\" value=\"" + Ui.Esc(g.Email) + "\"" + ro + "><em class=\"gu-err\" id=\"gu-e-email\"></em></label></div>");
            sb.Append("<label class=\"gu-field\"><span>Notes</span><textarea id=\"gu-f-notes\" data-k=\"notes\" maxlength=\"1000\" rows=\"3\" placeholder=\"Hair preferences, color formulas, how they like their coffee\"" + ro + ">" + Ui.Esc(g.Notes) + "</textarea><small>Hair notes only. Do not record health or medical details.</small><em class=\"gu-err\" id=\"gu-e-notes\"></em></label>");
            if (canE)
            {
                sb.Append("<div class=\"gu-acts\"><button type=\"submit\" class=\"gu-btn\">" + (g.Id.Length == 0 ? "Add guest" : "Save guest") + "</button>" + (g.Id.Length == 0 ? "<button type=\"button\" class=\"gu-btn gu-btn-ghost\" onclick=\"AdminGuestsJs.closeModal()\">Cancel</button>" : string.Empty) + "</div>");
            }
            sb.Append("</form>");
            return sb.ToString();
        }

        private static string VisitRows(SiteData site, List<Booking> list, bool past)
        {
            if (list.Count == 0)
            {
                return "<p class=\"gu-hint\">" + (past ? "No past visits yet." : "Nothing booked.") + "</p>";
            }
            StringBuilder sb = new StringBuilder("<div class=\"gu-wrapt\"><table class=\"gu-table gu-vt\"><thead><tr><th>When</th><th>Service</th><th>Stylist</th><th>Visit</th><th class=\"gu-num\">Paid</th></tr></thead><tbody>");
            foreach (Booking b in list)
            {
                string sty = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
                string steps = "<span class=\"gu-flow\"><i class=\"gu-on\" title=\"Booked\">Booked</i><i class=\"" + (b.Arrived != null || b.Status is "arrived" or "done" ? "gu-on" : string.Empty) + "\" title=\"Checked in\">In</i><i class=\"" + (b.Paid != null ? "gu-on" : string.Empty) + "\" title=\"Paid\">Paid</i></span>";
                sb.Append("<tr><td><a class=\"gu-link\" href=\"AdminBookings?d=" + Ui.Iso(b.Start) + "&amp;b=" + Ui.Esc(b.Id) + "\">" + Ui.Day(b.Start) + "</a><small>" + b.Start.Year + " &middot; " + Ui.Time(b.Start) + "</small></td><td>" + Ui.Esc(b.ServiceName) + "</td><td>" + Ui.Esc(sty) + "</td><td>" + Ui.Status("gu", b.Status) + (b.Status is "noshow" or "cancelled" ? string.Empty : steps) + "</td><td class=\"gu-num\">" + (b.Paid != null ? "<a class=\"gu-link\" href=\"AdminCheckout?b=" + Ui.Esc(b.Id) + "\">" + Ui.Money(b.Paid.Total) + "</a><small>tip " + Ui.Money(b.Paid.Tip) + " &middot; " + b.Paid.Method + "</small>" : b.Status == "done" ? "<a class=\"gu-link\" href=\"AdminCheckout?b=" + Ui.Esc(b.Id) + "\">Check out</a>" : "&ndash;") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>");
            return sb.ToString();
        }

        private static string DetailHtml(Ctx c, SiteData site, Guest g, List<Booking> visits)
        {
            Tally t = TallyOf(visits);
            DateTime now = DateTime.Now;
            List<Booking> upcoming = visits.Where(b => b.Status is "booked" or "arrived" && b.Start >= now.Date || b.Status == "done" && b.Paid == null).OrderBy(b => b.Start).ToList();
            List<Booking> past = visits.Except(upcoming).OrderByDescending(b => b.Start).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"gu-gd\"><div class=\"gu-stats gu-gstats\"><div class=\"gu-stat\"><b>" + t.Done + "</b><span>" + (t.Done == 1 ? "visit" : "visits") + " done</span></div><div class=\"gu-stat\"><b>" + Ui.Money(t.Spent) + "</b><span>spent on services</span></div><div class=\"gu-stat\"><b>" + Ui.Money(t.Tips) + "</b><span>tips given</span></div><div class=\"gu-stat\"><b>" + t.NoShows + "</b><span>" + (t.NoShows == 1 ? "no-show" : "no-shows") + "</span></div></div>");
            sb.Append("<div class=\"gu-acts gu-gacts\">" + (Access.Can(c.U, "bookings", 'E') ? "<a class=\"gu-btn\" href=\"AdminBookings?guest=" + Ui.Esc(g.Id) + "\">New booking for " + Ui.Esc(g.Name.Split(' ')[0]) + "</a>" : string.Empty) + (g.Phone.Length > 0 ? "<a class=\"gu-btn gu-btn-ghost\" href=\"tel:" + Ui.Esc(Diary.Digits(g.Phone)) + "\">Call</a>" : string.Empty) + (g.Email.Length > 0 ? "<a class=\"gu-btn gu-btn-ghost\" href=\"mailto:" + Ui.Esc(g.Email) + "\">Email</a>" : string.Empty) + "</div>");
            sb.Append(FormHtml(g, Access.Can(c.U, AreaKey, 'E')));
            sb.Append("<h3 class=\"gu-gh\">Upcoming and open</h3>" + VisitRows(site, upcoming, false));
            sb.Append("<h3 class=\"gu-gh\">Past visits</h3>" + VisitRows(site, past, true));
            sb.Append("<p class=\"gu-hint gu-gmeta\">Guest since " + Ui.Esc(Ui.When(g.Created)) + (g.By.Length > 0 ? " &middot; added " + (g.By == "online" ? "by online booking" : "by " + Ui.Esc(g.By)) : string.Empty) + "</p>");
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<div class=\"gu-acts\"><button type=\"button\" class=\"gu-btn gu-btn-danger\" onclick=\"AdminGuestsJs.drop('" + Ui.Js(g.Id) + "','" + Ui.Js(g.Name) + "'," + visits.Count + ")\">Delete guest</button></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string gid = Ui.Clip(GetDataValue("gid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save guest", gid, "You can look at guests but not change them.");
            }
            string name = Ui.Clip(GetDataValue("name"), 80);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 100);
            string notes = (GetDataValue("notes") ?? string.Empty).Trim();
            Dictionary<string, string> err = new Dictionary<string, string>();
            if (name.Length < 2 || name.Length > 60)
            {
                err["name"] = "Enter the guest's name.";
            }
            if (phone.Length == 0 && email.Length == 0)
            {
                err["phone"] = "Enter a phone number or an email address.";
            }
            if (phone.Length > 0 && !Diary.IsPhone(phone))
            {
                err["phone"] = "Check the phone number.";
            }
            if (email.Length > 0 && !Diary.IsEmail(email))
            {
                err["email"] = "Check the email address.";
            }
            if (notes.Length > 1000)
            {
                err["notes"] = "Keep notes under 1,000 characters.";
            }
            Guest? saved = null;
            if (err.Count == 0)
            {
                using (await Store.Hold())
                {
                    List<Guest> guests = await Store.LoadGuests();
                    List<Booking> bookings = await Store.LoadBookings();
                    string d = Diary.Digits(phone);
                    Guest? same = guests.FirstOrDefault(x => x.Id != gid && (d.Length >= 7 && Diary.Digits(x.Phone) == d || email.Length > 0 && x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
                    Guest? g = gid.Length == 0 ? null : Visible(c, guests, bookings).FirstOrDefault(x => x.Id == gid);
                    if (same != null)
                    {
                        err["phone"] = "Another guest, " + same.Name + ", already has this phone or email.";
                    }
                    else if (gid.Length > 0 && g == null)
                    {
                        err["name"] = "That guest was not found.";
                    }
                    else
                    {
                        if (g == null)
                        {
                            g = new Guest { Id = Diary.NewId("g-"), Created = DateTime.UtcNow, By = c.U.Name };
                            guests.Add(g);
                        }
                        g.Name = name;
                        g.Phone = phone;
                        g.Email = email;
                        g.Notes = notes;
                        foreach (Booking b in bookings.Where(b => b.GuestId == g.Id && (b.Status is "booked" or "arrived")))
                        {
                            b.Name = name;
                            b.Phone = phone;
                            b.Email = email;
                        }
                        await Store.SaveGuests(guests);
                        await Store.SaveBookings(bookings);
                        saved = g;
                    }
                }
            }
            foreach (string k in new[] { "name", "phone", "email", "notes" })
            {
                response.SetElementContents("gu-e-" + k, err.TryGetValue(k, out string? m) ? Ui.Esc(m) : string.Empty);
            }
            if (saved == null)
            {
                return response;
            }
            await Store.Log(c.U.Id, gid.Length == 0 ? "add guest" : "edit guest", saved.Name, true);
            SiteData site = await Store.LoadSite();
            List<Booking> all = await Store.LoadBookings();
            List<Guest> list = await Store.LoadGuests();
            response.SetElementContents("gu-mbody", DetailHtml(c, site, saved, VisitsOf(c, all, saved.Id)));
            response.ExecuteScript("AdminGuestsJs.opened('" + Ui.Js(saved.Name) + "','" + Ui.Js(saved.Id) + "');AdminGuestsJs.refresh();AdminGuestsJs.toast('Guest saved');");
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
            string gid = Ui.Clip(GetDataValue("gid"), 40);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete guest", gid, "You are not allowed to delete guests.");
            }
            string name = string.Empty;
            string msg = string.Empty;
            using (await Store.Hold())
            {
                List<Guest> guests = await Store.LoadGuests();
                List<Booking> bookings = await Store.LoadBookings();
                Guest? g = guests.FirstOrDefault(x => x.Id == gid);
                if (g == null)
                {
                    msg = "That guest was not found.";
                }
                else if (bookings.Any(b => b.GuestId == gid))
                {
                    msg = g.Name + " has visits on record, so the guest cannot be deleted.";
                }
                else
                {
                    name = g.Name;
                    guests.Remove(g);
                    await Store.SaveGuests(guests);
                }
            }
            if (name.Length == 0)
            {
                await Store.Log(c.U.Id, "delete guest", gid, false);
                response.ExecuteScript("AdminGuestsJs.toast('" + Ui.Js(msg) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "delete guest", name, true);
            response.ExecuteScript("AdminGuestsJs.shut();AdminGuestsJs.gone();AdminGuestsJs.refresh();AdminGuestsJs.toast('Guest deleted');");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminGuestsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminGuestsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"gu-panel\"><h1>No access</h1><p class=\"gu-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("gu-brand", Ui.Esc(site.Brand));
            response.SetElementContents("gu-menu", await Auth.Menu("gu", "AdminGuests", c));
            response.SetElementContents("gu-who", Auth.Who("gu", c));
        }

    }
}
