using System.Text;
using System.Text.Json;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminSettings : WebPage
    {
        private const string AreaKey = "settings";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Settings | Staff");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
        }

        private static string Field(string k, string label, string value, int max, string type = "text", string hint = "")
        {
            return "<label class=\"se-field\" for=\"se-f-" + k + "\"><span>" + label + "</span><input type=\"" + type + "\" id=\"se-f-" + k + "\" data-k=\"" + k + "\" maxlength=\"" + max + "\" value=\"" + Ui.Esc(value) + "\">" + (hint.Length > 0 ? "<small>" + hint + "</small>" : string.Empty) + "<em class=\"se-err\" id=\"se-e-" + k + "\"></em></label>";
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                await Store.Log(c.U.Id, "open settings", string.Empty, false);
                response.SetElementContents("se-main", NoAccess("settings"));
                return response;
            }
            StringBuilder sb = new StringBuilder("<div class=\"se-ph\"><div><h1>Settings</h1><p>Salon details, opening hours, booking rules and tips. Changes show on the website straight away.</p></div></div><form class=\"se-stack\" id=\"se-form\" onsubmit=\"return AdminSettingsJs.save()\" novalidate>");
            sb.Append("<section class=\"se-panel\"><h2>The salon</h2><div class=\"se-grid2 se-mt\">" + Field("brand", "Salon name", site.Brand, 40) + Field("tagline", "Tagline", site.Tagline, 120) + Field("phone", "Phone", site.Phone, 24, "tel") + Field("email", "Email", site.Email, 80, "email") + Field("street", "Street address", site.Street, 80) + Field("city", "Town, state and ZIP", site.City, 80) + "</div>" + Field("mapnote", "Directions note", site.MapNote, 200, "text", "Shown under the map on the Contact page.") + "</section>");
            sb.Append("<section class=\"se-panel\"><h2>Opening hours</h2><p class=\"se-hint\">Online booking never offers times outside these hours, whatever a stylist&rsquo;s own hours say.</p><div class=\"se-hrs\">");
            for (int i = 0; i < 7; i++)
            {
                DayHours h = site.Hours[i];
                sb.Append("<div class=\"se-hr\" data-day=\"" + i + "\"><label class=\"se-check\"><input type=\"checkbox\" class=\"se-hopen\"" + (h.Open ? " checked" : string.Empty) + "> " + Ui.DayNames[i] + "</label><input type=\"time\" class=\"se-hfrom\" value=\"" + Ui.Esc(h.From) + "\" step=\"900\" aria-label=\"" + Ui.DayNames[i] + " opens\"><span>to</span><input type=\"time\" class=\"se-hto\" value=\"" + Ui.Esc(h.To) + "\" step=\"900\" aria-label=\"" + Ui.DayNames[i] + " closes\"></div>");
            }
            sb.Append("</div><em class=\"se-err\" id=\"se-e-hours\"></em></section>");
            sb.Append("<section class=\"se-panel\"><h2>Booking rules</h2><div class=\"se-grid2 se-mt\"><label class=\"se-field\" for=\"se-f-step\"><span>Start times every</span><select id=\"se-f-step\" data-k=\"step\">" + string.Concat(new[] { 10, 15, 20, 30 }.Select(n => "<option value=\"" + n + "\"" + (n == site.SlotStep ? " selected" : string.Empty) + ">" + n + " minutes</option>")) + "</select></label>");
            sb.Append(Field("buffer", "Clean-up time between visits (minutes)", site.Buffer.ToString(), 2, "number", "0 to 60. Kept free after each booking.") + Field("horizon", "Book online up to (days ahead)", site.HorizonDays.ToString(), 3, "number", "7 to 365.") + Field("lead", "Online booking closes (minutes before)", site.LeadMinutes.ToString(), 4, "number", "0 to 2880. Staff can still book walk-ins.") + "</div></section>");
            sb.Append("<section class=\"se-panel\"><h2>Checkout</h2><p class=\"se-hint\">Three tip buttons, as a percentage of the service total before any discount. Prices are in US dollars.</p><div class=\"se-grid3 se-mt\">" + Field("tip1", "Tip button 1 (%)", site.TipPresets[0].ToString(), 2, "number") + Field("tip2", "Tip button 2 (%)", site.TipPresets[1].ToString(), 2, "number") + Field("tip3", "Tip button 3 (%)", site.TipPresets[2].ToString(), 2, "number") + "</div><em class=\"se-err\" id=\"se-e-tips\"></em></section>");
            sb.Append("<section class=\"se-panel\"><h2>Media</h2><div class=\"se-grid2 se-mt\">" + Field("video", "Largest video upload (MB)", site.MaxVideoMb.ToString(), 3, "number", "1 to 100. Photos are limited to " + Media.PhotoMb + " MB.") + "</div></section>");
            sb.Append("<div class=\"se-acts se-sticky\"><button type=\"submit\" class=\"se-btn\">Save settings</button><span class=\"se-hint\" id=\"se-saved\"></span></div></form>");
            response.SetElementContents("se-main", sb.ToString());
            return response;
        }

        private sealed class HourIn
        {
            public bool Open { get; set; }
            public string From { get; set; } = string.Empty;
            public string To { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> Save()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save settings", string.Empty, "Only managers can change settings.");
            }
            Dictionary<string, string> err = new Dictionary<string, string>();
            string brand = Ui.Clip(GetDataValue("brand"), 60);
            string phone = Ui.Clip(GetDataValue("phone"), 30);
            string email = Ui.Clip(GetDataValue("email"), 100);
            if (brand.Length < 2 || brand.Length > 40)
            {
                err["brand"] = "Enter the salon name (2 to 40 characters).";
            }
            if (!Diary.IsPhone(phone))
            {
                err["phone"] = "Enter a phone number.";
            }
            if (!Diary.IsEmail(email))
            {
                err["email"] = "Enter an email address.";
            }
            int buffer = Ui.Int(GetDataValue("buffer"), -1);
            int horizon = Ui.Int(GetDataValue("horizon"), -1);
            int lead = Ui.Int(GetDataValue("lead"), -1);
            int video = Ui.Int(GetDataValue("video"), -1);
            int step = Ui.Int(GetDataValue("step"), 15);
            if (buffer < 0 || buffer > 60)
            {
                err["buffer"] = "Use 0 to 60 minutes.";
            }
            if (horizon < 7 || horizon > 365)
            {
                err["horizon"] = "Use 7 to 365 days.";
            }
            if (lead < 0 || lead > 2880)
            {
                err["lead"] = "Use 0 to 2880 minutes.";
            }
            if (video < 1 || video > Media.CeilingMb)
            {
                err["video"] = "Use 1 to " + Media.CeilingMb + " MB.";
            }
            List<int> tips = new[] { "tip1", "tip2", "tip3" }.Select(k => Ui.Int(GetDataValue(k), -1)).ToList();
            if (tips.Any(t => t < 1 || t > 50) || tips.Distinct().Count() != 3)
            {
                err["tips"] = "Use three different whole numbers from 1 to 50.";
            }
            List<HourIn>? hours = null;
            try
            {
                hours = JsonSerializer.Deserialize<List<HourIn>>(GetDataValue("hours") ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                hours = null;
            }
            if (hours == null || hours.Count != 7)
            {
                err["hours"] = "The opening hours could not be read.";
            }
            else
            {
                for (int i = 0; i < 7; i++)
                {
                    if (hours[i].Open && (Diary.Min(hours[i].From) < 0 || Diary.Min(hours[i].To) <= Diary.Min(hours[i].From)))
                    {
                        err["hours"] = Ui.DayNames[i] + ": closing time must be after opening time.";
                        break;
                    }
                }
                if (!hours.Any(h => h.Open))
                {
                    err["hours"] = "Open at least one day a week.";
                }
            }
            foreach (string k in new[] { "brand", "tagline", "phone", "email", "street", "city", "mapnote", "buffer", "horizon", "lead", "video", "tips", "hours" })
            {
                response.SetElementContents("se-e-" + k, err.TryGetValue(k, out string? m) ? m : string.Empty);
            }
            if (err.Count > 0)
            {
                response.ExecuteScript("AdminSettingsJs.toast('Check the highlighted fields.');");
                return response;
            }
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                site.Brand = brand;
                site.Tagline = Ui.Clip(GetDataValue("tagline"), 120);
                site.Phone = phone;
                site.Email = email;
                site.Street = Ui.Clip(GetDataValue("street"), 80);
                site.City = Ui.Clip(GetDataValue("city"), 80);
                site.MapNote = Ui.Clip(GetDataValue("mapnote"), 200);
                site.SlotStep = step is 10 or 15 or 20 or 30 ? step : 15;
                site.Buffer = buffer;
                site.HorizonDays = horizon;
                site.LeadMinutes = lead;
                site.MaxVideoMb = video;
                site.TipPresets = tips.OrderBy(t => t).ToList();
                site.Hours = hours!.Select(h => new DayHours { Open = h.Open, From = Diary.Min(h.From) >= 0 ? Diary.Hhmm(Diary.Min(h.From)) : "09:00", To = Diary.Min(h.To) >= 0 ? Diary.Hhmm(Diary.Min(h.To)) : "18:00" }).ToList();
                await Store.SaveSite(site);
            }
            await Store.Log(c.U.Id, "save settings", brand, true);
            response.SetElementContents("se-brand", Ui.Esc(brand));
            response.SetElementContents("se-saved", "Saved " + Ui.Esc(Ui.When(DateTime.UtcNow)));
            response.ExecuteScript("AdminSettingsJs.clean();AdminSettingsJs.toast('Settings saved');");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminSettingsJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminSettingsJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"se-panel\"><h1>No access</h1><p class=\"se-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("se-brand", Ui.Esc(site.Brand));
            response.SetElementContents("se-menu", await Auth.Menu("se", "AdminSettings", c));
            response.SetElementContents("se-who", Auth.Who("se", c));
        }

    }
}
