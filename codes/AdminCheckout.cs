using System.Text;
using System.Text.Json;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminCheckout : WebPage
    {
        private const string AreaKey = "checkout";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Checkout | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private sealed class LineIn
        {
            public string Sid { get; set; } = string.Empty;
            public string Price { get; set; } = string.Empty;
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
                response.SetElementContents("co-main", NoAccess("checkout"));
                return response;
            }
            List<Booking> bookings = await Store.LoadBookings();
            string id = Ui.Clip(GetDataValue("bid"), 40);
            response.SetElementContents("co-main", "<div class=\"co-ph\"><div><h1>Checkout</h1><p>Take payment when a visit is done: services, tip, card or cash. There is no online payment and no deposit.</p></div></div><div class=\"co-co\"><aside class=\"co-queue\" id=\"co-queue\">" + QueueHtml(c, site, bookings, id) + "</aside><section class=\"co-till\" id=\"co-till\">" + TillFor(c, site, bookings, id) + "</section></div>");
            response.ExecuteScript("AdminCheckoutJs.loaded('" + Ui.Js(id) + "');");
            return response;
        }

        private static List<Booking> Mine(Ctx c, List<Booking> bookings)
        {
            string own = Access.OwnStylist(c.U);
            return bookings.Where(b => own == string.Empty || b.StylistId == own).ToList();
        }

        private static string QueueItem(SiteData site, Booking b, string cur, string note)
        {
            string sty = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
            return "<button type=\"button\" class=\"co-qi" + (b.Id == cur ? " co-qon" : string.Empty) + "\" onclick=\"AdminCheckoutJs.pick('" + Ui.Js(b.Id) + "')\"><span><b>" + Ui.Esc(b.Name) + "</b><small>" + Ui.Time(b.Start) + (b.Start.Date != DateTime.Today ? " " + Ui.Day(b.Start) : string.Empty) + " &middot; " + Ui.Esc(b.ServiceName) + " &middot; " + Ui.Esc(sty) + "</small></span>" + note + "</button>";
        }

        private static string QueueHtml(Ctx c, SiteData site, List<Booking> all, string cur)
        {
            List<Booking> mine = Mine(c, all);
            List<Booking> waiting = mine.Where(b => b.Status == "done" && b.Paid == null).OrderBy(b => b.Start).ToList();
            List<Booking> chair = mine.Where(b => b.Status == "arrived" && b.Start.Date == DateTime.Today).OrderBy(b => b.Start).ToList();
            List<Booking> paid = mine.Where(b => b.Paid != null && b.Paid.When.ToLocalTime().Date == DateTime.Today).OrderByDescending(b => b.Paid!.When).ToList();
            StringBuilder sb = new StringBuilder("<div class=\"co-panel\"><h2>Waiting to pay <span class=\"co-count\">" + waiting.Count + "</span></h2>");
            sb.Append(waiting.Count == 0 ? "<p class=\"co-hint co-qempty\">Nobody is waiting. Mark a visit done in Bookings and it appears here.</p>" : "<div class=\"co-qlist\">" + string.Concat(waiting.Select(b => QueueItem(site, b, cur, "<em>" + Ui.Money(b.Price) + "</em>"))) + "</div>");
            sb.Append("<h2 class=\"co-qh\">In the chair</h2>");
            sb.Append(chair.Count == 0 ? "<p class=\"co-hint co-qempty\">No one is checked in right now.</p>" : "<div class=\"co-qlist\">" + string.Concat(chair.Select(b => QueueItem(site, b, cur, "<em class=\"co-pill co-pill-wait\">Arrived</em>"))) + "</div>");
            sb.Append("<h2 class=\"co-qh\">Paid today</h2>");
            if (paid.Count == 0)
            {
                sb.Append("<p class=\"co-hint co-qempty\">No payments yet today.</p>");
            }
            else
            {
                sb.Append("<div class=\"co-qlist\">" + string.Concat(paid.Select(b => QueueItem(site, b, cur, "<em>" + Ui.Money(b.Paid!.Total) + "</em>"))) + "</div><p class=\"co-qsum\">Services " + Ui.Money(paid.Sum(b => b.Paid!.ServiceTotal - b.Paid.Discount)) + " &middot; tips " + Ui.Money(paid.Sum(b => b.Paid!.Tip)) + "<br>Card " + Ui.Money(paid.Where(b => b.Paid!.Method == "card").Sum(b => b.Paid!.Total)) + " &middot; cash " + Ui.Money(paid.Where(b => b.Paid!.Method == "cash").Sum(b => b.Paid!.Total)) + "</p>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string TillFor(Ctx c, SiteData site, List<Booking> all, string id)
        {
            Booking? b = Mine(c, all).FirstOrDefault(x => x.Id == id);
            if (b == null)
            {
                return "<div class=\"co-panel co-empty\">" + (id.Length > 0 ? "That visit was not found." : "Choose a visit on the left to check out.") + "</div>";
            }
            if (b.Paid != null)
            {
                return ReceiptHtml(c, site, b);
            }
            if (b.Status == "arrived")
            {
                return "<div class=\"co-panel\">" + Top(site, b) + "<p class=\"co-hint\">" + Ui.Esc(b.Name) + " is still in the chair. Finish the visit to take payment.</p><div class=\"co-acts\">" + (Access.Can(c.U, AreaKey, 'E') ? "<button type=\"button\" class=\"co-btn\" onclick=\"AdminCheckoutJs.finish('" + Ui.Js(b.Id) + "','" + Ui.Js(b.Name) + "')\">Visit done, check out</button>" : string.Empty) + "</div></div>";
            }
            if (b.Status != "done")
            {
                return "<div class=\"co-panel\">" + Top(site, b) + "<p class=\"co-hint\">This visit is " + Ui.StatusName(b.Status).ToLowerInvariant() + ". Only visits that are done can be checked out.</p></div>";
            }
            SalonService? svc = site.Services.FirstOrDefault(x => x.Id == b.ServiceId);
            List<PayLine> lines = new List<PayLine> { new PayLine { ServiceId = b.ServiceId, Name = b.ServiceName, Price = svc?.Price ?? b.Price } };
            return FormHtml(c, site, b, lines);
        }

        private static string Top(SiteData site, Booking b)
        {
            string sty = site.Stylists.FirstOrDefault(s => s.Id == b.StylistId)?.Name ?? string.Empty;
            return "<div class=\"co-ttop\"><div><h2>" + Ui.Esc(b.Name) + "</h2><p class=\"co-hint\">" + Ui.Day(b.Start) + " " + Ui.Time(b.Start) + " &middot; " + Ui.Esc(sty) + " &middot; " + Ui.Esc(b.Ref) + "</p></div><div class=\"co-tlinks\"><a class=\"co-link\" href=\"AdminGuests?g=" + Ui.Esc(b.GuestId) + "\">Guest record</a><a class=\"co-link\" href=\"AdminBookings?d=" + Ui.Iso(b.Start) + "&amp;b=" + Ui.Esc(b.Id) + "\">Booking</a></div></div>";
        }

        private static string FormHtml(Ctx c, SiteData site, Booking b, List<PayLine> lines)
        {
            bool e = Access.Can(c.U, AreaKey, 'E');
            string d = e ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<div class=\"co-panel co-form\" id=\"co-payform\" data-bid=\"" + Ui.Esc(b.Id) + "\">" + Top(site, b));
            sb.Append("<h3>Services</h3><div class=\"co-lines\" id=\"co-lines\">");
            foreach (PayLine l in lines)
            {
                sb.Append(LineHtml(site, l, e));
            }
            sb.Append("</div>");
            if (e)
            {
                sb.Append("<div class=\"co-addl\"><select id=\"co-addsvc\" aria-label=\"Add a service\"><option value=\"\">Add another service&hellip;</option>" + string.Concat(site.Services.Where(s => s.Active).OrderBy(s => s.Order).Select(s => "<option value=\"" + Ui.Esc(s.Id) + "\" data-price=\"" + Ui.Num(s.Price) + "\">" + Ui.Esc(s.Name) + " &middot; " + Ui.Money(s.Price) + "</option>")) + "</select><button type=\"button\" class=\"co-btn co-btn-ghost co-btn-sm\" onclick=\"AdminCheckoutJs.addLine()\">Add</button></div>");
            }
            sb.Append("<div class=\"co-grid2 co-mt\"><label class=\"co-field\"><span>Discount (optional)</span><input type=\"text\" inputmode=\"decimal\" id=\"co-disc\" maxlength=\"10\" placeholder=\"0.00\" oninput=\"AdminCheckoutJs.quote()\"" + d + "><small>Taken off the services. The tip is worked out before the discount.</small></label></div>");
            sb.Append("<h3>Tip</h3><div class=\"co-tips\" role=\"radiogroup\" aria-label=\"Tip\">");
            sb.Append(TipBtn("none", "No tip", string.Empty, d));
            decimal total = lines.Sum(l => l.Price);
            foreach (int p in site.TipPresets)
            {
                sb.Append(TipBtn(p.ToString(System.Globalization.CultureInfo.InvariantCulture), p + "%", "<small id=\"co-tip" + p + "\">" + Ui.Money(Math.Round(total * p / 100m, 2, MidpointRounding.AwayFromZero)) + "</small>", d));
            }
            sb.Append(TipBtn("custom", "Custom", string.Empty, d) + "</div><label class=\"co-field co-custom\" id=\"co-custombox\" hidden><span>Custom tip amount</span><input type=\"text\" inputmode=\"decimal\" id=\"co-tipamt\" maxlength=\"8\" placeholder=\"0.00\" oninput=\"AdminCheckoutJs.quote()\"" + d + "></label>");
            sb.Append("<h3>Paid by</h3><div class=\"co-tips\" role=\"radiogroup\" aria-label=\"Payment method\"><button type=\"button\" class=\"co-tipb co-meth\" data-m=\"card\" aria-pressed=\"false\" onclick=\"AdminCheckoutJs.method('card')\"" + d + ">Card</button><button type=\"button\" class=\"co-tipb co-meth\" data-m=\"cash\" aria-pressed=\"false\" onclick=\"AdminCheckoutJs.method('cash')\"" + d + ">Cash</button></div>");
            sb.Append("<div class=\"co-totals\" id=\"co-totals\" aria-live=\"polite\">" + TotalsHtml(Diary.Price(site, lines, string.Empty, "none", string.Empty)) + "</div>");
            sb.Append("<em class=\"co-err\" id=\"co-e-pay\" role=\"alert\"></em>");
            if (e)
            {
                sb.Append("<div class=\"co-acts\"><button type=\"button\" class=\"co-btn co-paybtn\" onclick=\"AdminCheckoutJs.pay()\">Take payment</button></div>");
            }
            sb.Append("</div>");
            return sb.ToString();
        }

        private static string TipBtn(string mode, string label, string extra, string d)
        {
            return "<button type=\"button\" class=\"co-tipb" + (mode == "none" ? " co-on" : string.Empty) + "\" data-t=\"" + mode + "\" aria-pressed=\"" + (mode == "none" ? "true" : "false") + "\" onclick=\"AdminCheckoutJs.tip('" + mode + "')\"" + d + "><b>" + label + "</b>" + extra + "</button>";
        }

        private static string LineHtml(SiteData site, PayLine l, bool e)
        {
            SalonService? s = site.Services.FirstOrDefault(x => x.Id == l.ServiceId);
            bool from = s != null && s.From;
            return "<div class=\"co-line\" data-sid=\"" + Ui.Esc(l.ServiceId) + "\"><span><b>" + Ui.Esc(l.Name) + "</b>" + (from ? "<small>From-price service: enter the final price</small>" : "<small>Price list</small>") + "</span>" + (from && e ? "<input type=\"text\" inputmode=\"decimal\" class=\"co-lp\" value=\"" + Ui.Num(l.Price) + "\" maxlength=\"9\" aria-label=\"Price for " + Ui.Esc(l.Name) + "\" oninput=\"AdminCheckoutJs.quote()\">" : "<b class=\"co-lpx\" data-p=\"" + Ui.Num(l.Price) + "\">" + Ui.Money(l.Price) + "</b>") + (e ? "<button type=\"button\" class=\"co-ibtn\" aria-label=\"Remove " + Ui.Esc(l.Name) + "\" onclick=\"AdminCheckoutJs.dropLine(this)\"><svg viewBox=\"0 0 24 24\" width=\"18\" height=\"18\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" aria-hidden=\"true\"><path d=\"M6 6l12 12M18 6L6 18\"/></svg></button>" : string.Empty) + "</div>";
        }

        private static string TotalsHtml(Quote q)
        {
            return "<dl><dt>Services</dt><dd>" + Ui.Money(q.ServiceTotal) + "</dd>" + (q.Discount > 0 ? "<dt>Discount</dt><dd>&minus;" + Ui.Money(q.Discount) + "</dd>" : string.Empty) + "<dt>Tip" + (q.TipMode is "none" or "custom" ? string.Empty : " (" + q.TipMode + "%)") + "</dt><dd>" + Ui.Money(q.Tip) + "</dd><dt class=\"co-grand\">Total to pay</dt><dd class=\"co-grand\">" + Ui.Money(q.Total) + "</dd></dl>" + (q.Error.Length > 0 ? "<p class=\"co-err\">" + Ui.Esc(q.Error) + "</p>" : string.Empty);
        }

        private (List<PayLine>? Lines, string Error) ReadLines(SiteData site)
        {
            List<LineIn>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<LineIn>>(GetDataValue("lines") ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return (null, "The services could not be read. Reload the page.");
            }
            if (raw == null || raw.Count == 0)
            {
                return (null, "Add at least one service.");
            }
            if (raw.Count > 10)
            {
                return (null, "Up to 10 services on one bill.");
            }
            List<PayLine> lines = new List<PayLine>();
            foreach (LineIn r in raw)
            {
                SalonService? s = site.Services.FirstOrDefault(x => x.Id == r.Sid);
                if (s == null)
                {
                    return (null, "A service on the bill no longer exists.");
                }
                decimal price = s.Price;
                if (s.From)
                {
                    decimal? p = Ui.Dec(r.Price);
                    if (p == null || p < s.Price || p > Math.Max(s.Price * 5, s.Price + 500))
                    {
                        return (null, s.Name + " must be at least " + Ui.Money(s.Price) + ".");
                    }
                    price = Math.Round(p.Value, 2);
                }
                lines.Add(new PayLine { ServiceId = s.Id, Name = s.Name, Price = price });
            }
            return (lines, string.Empty);
        }

        public async Task<ApiResponse> Pick()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open checkout", string.Empty, "You do not have access to checkout.");
            }
            SiteData site = await Store.LoadSite();
            List<Booking> bookings = await Store.LoadBookings();
            string id = Ui.Clip(GetDataValue("bid"), 40);
            response.SetElementContents("co-queue", QueueHtml(c, site, bookings, id));
            response.SetElementContents("co-till", TillFor(c, site, bookings, id));
            response.ExecuteScript("AdminCheckoutJs.picked('" + Ui.Js(id) + "');");
            return response;
        }

        public async Task<ApiResponse> Line()
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
            SalonService? s = site.Services.FirstOrDefault(x => x.Active && x.Id == Ui.Clip(GetDataValue("sid"), 40));
            if (s == null)
            {
                return response;
            }
            response.SetElementContents("co-newline", LineHtml(site, new PayLine { ServiceId = s.Id, Name = s.Name, Price = s.Price }, true));
            response.ExecuteScript("AdminCheckoutJs.lineAdded();");
            return response;
        }

        public async Task<ApiResponse> Sum()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return response;
            }
            SiteData site = await Store.LoadSite();
            (List<PayLine>? lines, string err) = ReadLines(site);
            if (lines == null)
            {
                response.SetElementContents("co-totals", "<p class=\"co-err\">" + Ui.Esc(err) + "</p>");
                return response;
            }
            Quote q = Diary.Price(site, lines, GetDataValue("disc"), GetDataValue("tipmode"), GetDataValue("tipamt"));
            response.SetElementContents("co-totals", TotalsHtml(q));
            decimal total = lines.Sum(l => l.Price);
            foreach (int p in site.TipPresets)
            {
                response.SetElementContents("co-tip" + p, Ui.Money(Math.Round(total * p / 100m, 2, MidpointRounding.AwayFromZero)));
            }
            return response;
        }

        public async Task<ApiResponse> Pay()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("bid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "take payment", id, "You can look at checkout but not take payments.");
            }
            SiteData site = await Store.LoadSite();
            (List<PayLine>? lines, string err) = ReadLines(site);
            string method = GetDataValue("method") is "card" or "cash" ? GetDataValue("method") : string.Empty;
            Quote? q = null;
            if (lines != null)
            {
                q = Diary.Price(site, lines, GetDataValue("disc"), GetDataValue("tipmode"), GetDataValue("tipamt"));
                err = q.Error;
            }
            if (err.Length == 0 && method.Length == 0)
            {
                err = "Choose card or cash.";
            }
            if (err.Length > 0 || lines == null || q == null)
            {
                response.SetElementContents("co-e-pay", Ui.Esc(err));
                return response;
            }
            string own = Access.OwnStylist(c.U);
            Booking? done = null;
            using (await Store.Hold())
            {
                List<Booking> bookings = await Store.LoadBookings();
                Booking? b = bookings.FirstOrDefault(x => x.Id == id && (own == string.Empty || x.StylistId == own));
                if (b == null || b.Status != "done")
                {
                    err = "This visit is not ready for checkout.";
                }
                else if (b.Paid != null)
                {
                    err = "This visit is already paid.";
                }
                else
                {
                    b.Paid = new Payment { Lines = lines, ServiceTotal = q.ServiceTotal, Discount = q.Discount, TipMode = q.TipMode, Tip = q.Tip, Total = q.Total, Method = method, When = DateTime.UtcNow, By = c.U.Id };
                    await Store.SaveBookings(bookings);
                    done = b;
                }
            }
            if (done == null)
            {
                response.SetElementContents("co-e-pay", Ui.Esc(err));
                return response;
            }
            await Store.Log(c.U.Id, "checkout", done.Ref + " · " + done.Name + " · " + Ui.Money(done.Paid!.Total) + " " + method + " · tip " + Ui.Money(done.Paid.Tip), true);
            List<Booking> all = await Store.LoadBookings();
            response.SetElementContents("co-queue", QueueHtml(c, site, all, done.Id));
            response.SetElementContents("co-till", ReceiptHtml(c, site, done));
            response.SetElementContents("co-menu", await Auth.Menu("co", "AdminCheckout", c));
            response.ExecuteScript("AdminCheckoutJs.toast('Paid " + Ui.Js(Ui.Money(done.Paid.Total)) + " by " + method + "');");
            return response;
        }

        public async Task<ApiResponse> Finish()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("bid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "finish visit", id, "You can look at checkout but not change visits.");
            }
            SiteData site = await Store.LoadSite();
            string own = Access.OwnStylist(c.U);
            Booking? done = null;
            using (await Store.Hold())
            {
                List<Booking> bookings = await Store.LoadBookings();
                Booking? b = bookings.FirstOrDefault(x => x.Id == id && (own == string.Empty || x.StylistId == own));
                if (b != null && b.Status == "arrived")
                {
                    b.Status = "done";
                    b.Finished = DateTime.UtcNow;
                    await Store.SaveBookings(bookings);
                    done = b;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminCheckoutJs.toast('That visit cannot be finished.');");
                return response;
            }
            await Store.Log(c.U.Id, "booking done", done.Ref + " · " + done.Name, true);
            List<Booking> all = await Store.LoadBookings();
            response.SetElementContents("co-queue", QueueHtml(c, site, all, id));
            response.SetElementContents("co-till", TillFor(c, site, all, id));
            response.SetElementContents("co-menu", await Auth.Menu("co", "AdminCheckout", c));
            return response;
        }

        public async Task<ApiResponse> Reopen()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("bid"), 40);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "reopen payment", id, "Only staff with the reopen right can undo a payment.");
            }
            SiteData site = await Store.LoadSite();
            string why = Ui.Clip(GetDataValue("why"), 300);
            string own = Access.OwnStylist(c.U);
            Booking? done = null;
            decimal was = 0;
            using (await Store.Hold())
            {
                List<Booking> bookings = await Store.LoadBookings();
                Booking? b = bookings.FirstOrDefault(x => x.Id == id && (own == string.Empty || x.StylistId == own));
                if (b != null && b.Paid != null)
                {
                    was = b.Paid.Total;
                    b.Paid = null;
                    await Store.SaveBookings(bookings);
                    done = b;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminCheckoutJs.toast('That payment was not found.');");
                return response;
            }
            await Store.Log(c.U.Id, "reopen payment", done.Ref + " · " + done.Name + " · was " + Ui.Money(was) + (why.Length > 0 ? " · " + why : string.Empty), true);
            List<Booking> all = await Store.LoadBookings();
            response.SetElementContents("co-queue", QueueHtml(c, site, all, id));
            response.SetElementContents("co-till", TillFor(c, site, all, id));
            response.SetElementContents("co-menu", await Auth.Menu("co", "AdminCheckout", c));
            response.ExecuteScript("AdminCheckoutJs.toast('Payment reopened. Take it again when ready.');");
            return response;
        }

        private static string ReceiptHtml(Ctx c, SiteData site, Booking b)
        {
            Payment p = b.Paid!;
            StringBuilder sb = new StringBuilder("<div class=\"co-panel co-receipt\">" + Top(site, b) + "<div class=\"co-paidtag\"><span class=\"co-pill co-pill-go\">Paid by " + p.Method + "</span><span class=\"co-hint\">" + Ui.Esc(Ui.When(p.When)) + "</span></div><table class=\"co-rtable\"><tbody>");
            foreach (PayLine l in p.Lines)
            {
                sb.Append("<tr><td>" + Ui.Esc(l.Name) + "</td><td class=\"co-num\">" + Ui.Money(l.Price) + "</td></tr>");
            }
            sb.Append("<tr class=\"co-rsub\"><td>Services</td><td class=\"co-num\">" + Ui.Money(p.ServiceTotal) + "</td></tr>");
            if (p.Discount > 0)
            {
                sb.Append("<tr><td>Discount</td><td class=\"co-num\">&minus;" + Ui.Money(p.Discount) + "</td></tr>");
            }
            sb.Append("<tr><td>Tip" + (p.TipMode is "none" or "custom" ? string.Empty : " (" + p.TipMode + "%)") + "</td><td class=\"co-num\">" + Ui.Money(p.Tip) + "</td></tr><tr class=\"co-rtot\"><td>Total paid</td><td class=\"co-num\">" + Ui.Money(p.Total) + "</td></tr></tbody></table>");
            sb.Append("<div class=\"co-acts\"><button type=\"button\" class=\"co-btn co-btn-ghost\" onclick=\"window.print()\">Print receipt</button>");
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"co-btn co-btn-danger co-push\" onclick=\"AdminCheckoutJs.reopen('" + Ui.Js(b.Id) + "','" + Ui.Js(b.Name) + "')\">Reopen payment</button>");
            }
            sb.Append("</div></div>");
            return sb.ToString();
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminCheckoutJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminCheckoutJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"co-panel\"><h1>No access</h1><p class=\"co-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("co-brand", Ui.Esc(site.Brand));
            response.SetElementContents("co-menu", await Auth.Menu("co", "AdminCheckout", c));
            response.SetElementContents("co-who", Auth.Who("co", c));
        }

    }
}
