using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminServices : WebPage
    {
        private const string AreaKey = "services";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Services | Staff");
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
                response.SetElementContents("sc-main", NoAccess("services"));
                return response;
            }
            bool e = Access.Can(c.U, AreaKey, 'E');
            response.SetElementContents("sc-main", "<div class=\"sc-ph\"><div><h1>Services</h1><p>The price list on the website and in Checkout. Popular services show on the home page.</p></div>" + (e ? "<div class=\"sc-acts sc-top0\"><button type=\"button\" class=\"sc-btn sc-btn-ghost\" onclick=\"AdminServicesJs.cats()\">Categories</button><button type=\"button\" class=\"sc-btn\" onclick=\"AdminServicesJs.edit('')\">Add a service</button></div>" : string.Empty) + "</div><div id=\"sc-list\">" + ListHtml(site, e) + "</div>");
            return response;
        }

        private static string ListHtml(SiteData site, bool e)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Category cat in site.Categories)
            {
                List<SalonService> list = site.Services.Where(s => s.Category == cat.Key).OrderBy(s => s.Order).ToList();
                sb.Append("<section class=\"sc-panel\"><h2>" + Ui.Esc(cat.Label) + " <small class=\"sc-hint\">" + Ui.Count(list.Count, "service", "services") + "</small></h2>");
                if (list.Count == 0)
                {
                    sb.Append("<p class=\"sc-hint\">No services in this category.</p></section>");
                    continue;
                }
                sb.Append("<div class=\"sc-wrapt sc-flat\"><table class=\"sc-table\"><thead><tr><th>Service</th><th class=\"sc-num\">Time</th><th class=\"sc-num\">Price</th><th>Stylists</th><th>Shown</th></tr></thead><tbody>");
                foreach (SalonService s in list)
                {
                    int n = site.Stylists.Count(x => x.Active && x.Services.Contains(s.Id));
                    sb.Append("<tr class=\"sc-row\" onclick=\"AdminServicesJs.edit('" + Ui.Js(s.Id) + "')\"><td><b>" + Ui.Esc(s.Name) + "</b><small>" + Ui.Esc(s.Text) + "</small></td><td class=\"sc-num\">" + Ui.Minutes(s.Minutes) + "</td><td class=\"sc-num\">" + (s.From ? "from " : string.Empty) + Ui.Money(s.Price) + "</td><td>" + n + "</td><td>" + (s.Active ? "<span class=\"sc-pill sc-pill-go\">On sale</span>" : "<span class=\"sc-pill sc-pill-off\">Hidden</span>") + (s.Featured ? " <span class=\"sc-pill sc-pill-new\">Popular</span>" : string.Empty) + "</td></tr>");
                }
                sb.Append("</tbody></table></div></section>");
            }
            return sb.ToString();
        }

        public async Task<ApiResponse> Edit()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("sid"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open service", id, "You do not have access to services.");
            }
            SiteData site = await Store.LoadSite();
            SalonService s = site.Services.FirstOrDefault(x => x.Id == id) ?? new SalonService { Category = site.Categories.FirstOrDefault()?.Key ?? string.Empty, Minutes = 45, Order = (site.Services.Count + 1) * 10 };
            bool e = Access.Can(c.U, AreaKey, 'E');
            if (id.Length == 0 && !e)
            {
                return await Refuse(response, c, "add service", string.Empty, "You can look at services but not add them.");
            }
            string d = e ? string.Empty : " disabled";
            StringBuilder sb = new StringBuilder("<form class=\"sc-stack\" id=\"sc-form\" onsubmit=\"return AdminServicesJs.save('" + Ui.Js(s.Id) + "')\" novalidate>");
            sb.Append("<div class=\"sc-grid2\"><label class=\"sc-field\"><span>Name</span><input type=\"text\" id=\"sc-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(s.Name) + "\"" + d + "><em class=\"sc-err\" id=\"sc-e-name\"></em></label>");
            sb.Append("<label class=\"sc-field\"><span>Category</span><select id=\"sc-f-cat\" data-k=\"cat\"" + d + ">" + string.Concat(site.Categories.Select(x => "<option value=\"" + Ui.Esc(x.Key) + "\"" + (x.Key == s.Category ? " selected" : string.Empty) + ">" + Ui.Esc(x.Label) + "</option>")) + "</select></label></div>");
            sb.Append("<label class=\"sc-field\"><span>Description</span><input type=\"text\" id=\"sc-f-text\" data-k=\"text\" maxlength=\"200\" value=\"" + Ui.Esc(s.Text) + "\"" + d + "><em class=\"sc-err\" id=\"sc-e-text\"></em></label>");
            sb.Append("<div class=\"sc-grid3\"><label class=\"sc-field\"><span>Minutes</span><input type=\"number\" id=\"sc-f-min\" data-k=\"min\" min=\"5\" max=\"480\" step=\"5\" value=\"" + s.Minutes + "\"" + d + "><small>Time in the chair. Booked slots use this.</small><em class=\"sc-err\" id=\"sc-e-min\"></em></label>");
            sb.Append("<label class=\"sc-field\"><span>Price (USD)</span><input type=\"text\" inputmode=\"decimal\" id=\"sc-f-price\" data-k=\"price\" maxlength=\"9\" value=\"" + Ui.Num(s.Price) + "\"" + d + "><small>0 shows as Free.</small><em class=\"sc-err\" id=\"sc-e-price\"></em></label>");
            sb.Append("<label class=\"sc-field\"><span>Order</span><input type=\"number\" id=\"sc-f-order\" data-k=\"order\" min=\"0\" max=\"9999\" value=\"" + s.Order + "\"" + d + "><small>Lower comes first.</small></label></div>");
            sb.Append("<label class=\"sc-check\"><input type=\"checkbox\" data-k=\"from\"" + (s.From ? " checked" : string.Empty) + d + "> A &ldquo;from&rdquo; price: the final price depends on hair length and is set at checkout</label>");
            sb.Append("<label class=\"sc-check\"><input type=\"checkbox\" data-k=\"active\"" + (s.Active || s.Id.Length == 0 ? " checked" : string.Empty) + d + "> On sale: shown on the website and bookable</label>");
            sb.Append("<label class=\"sc-check\"><input type=\"checkbox\" data-k=\"featured\"" + (s.Featured ? " checked" : string.Empty) + d + "> Popular: shown on the home page</label>");
            if (s.Id.Length > 0)
            {
                List<string> who = site.Stylists.Where(x => x.Services.Contains(s.Id)).Select(x => x.Name).ToList();
                sb.Append("<p class=\"sc-hint\">Offered by: " + (who.Count == 0 ? "nobody yet. Tick it for a stylist on the Stylists page." : Ui.Esc(string.Join(", ", who))) + "</p>");
            }
            if (e)
            {
                sb.Append("<div class=\"sc-acts\"><button type=\"submit\" class=\"sc-btn\">Save</button><button type=\"button\" class=\"sc-btn sc-btn-ghost\" onclick=\"AdminServicesJs.closeModal()\">Cancel</button>" + (s.Id.Length > 0 ? "<button type=\"button\" class=\"sc-btn sc-btn-danger sc-push\" onclick=\"AdminServicesJs.drop('" + Ui.Js(s.Id) + "','" + Ui.Js(s.Name) + "')\">Delete</button>" : string.Empty) + "</div>");
            }
            sb.Append("</form>");
            response.SetElementContents("sc-mbody", sb.ToString());
            response.ExecuteScript("AdminServicesJs.openModal('" + (s.Id.Length == 0 ? "Add a service" : Ui.Js(s.Name)) + "');");
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
            string id = Ui.Clip(GetDataValue("sid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save service", id, "You can look at services but not change them.");
            }
            string name = Ui.Clip(GetDataValue("name"), 80);
            string text = Ui.Clip(GetDataValue("text"), 240);
            int min = Ui.Int(GetDataValue("min"), -1);
            decimal? price = Ui.Dec(GetDataValue("price"));
            Dictionary<string, string> err = new Dictionary<string, string>();
            if (name.Length < 2 || name.Length > 60)
            {
                err["name"] = "Give the service a name.";
            }
            if (text.Length > 200)
            {
                err["text"] = "Keep the description under 200 characters.";
            }
            if (min < 5 || min > 480 || min % 5 != 0)
            {
                err["min"] = "Use 5 to 480 minutes, in steps of 5.";
            }
            if (price == null || price < 0 || price > 5000)
            {
                err["price"] = "Enter a price from 0 to 5,000.";
            }
            string saved = string.Empty;
            if (err.Count == 0)
            {
                using (await Store.Hold())
                {
                    SiteData site = await Store.LoadSite();
                    string cat = site.Categories.Any(x => x.Key == GetDataValue("cat")) ? GetDataValue("cat") : site.Categories.FirstOrDefault()?.Key ?? string.Empty;
                    if (site.Services.Any(x => x.Id != id && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        err["name"] = "Another service already has this name.";
                    }
                    else
                    {
                        SalonService? s = id.Length > 0 ? site.Services.FirstOrDefault(x => x.Id == id) : null;
                        if (s == null)
                        {
                            string key = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                            key = key.Length == 0 ? "service" : key.Length > 30 ? key.Substring(0, 30) : key;
                            string k = key;
                            int i = 2;
                            while (site.Services.Any(x => x.Id == k))
                            {
                                k = key + "-" + i++;
                            }
                            s = new SalonService { Id = k };
                            site.Services.Add(s);
                        }
                        s.Name = name;
                        s.Text = text;
                        s.Category = cat;
                        s.Minutes = min;
                        s.Price = Math.Round(price!.Value, 2);
                        s.Order = Math.Clamp(Ui.Int(GetDataValue("order"), s.Order), 0, 9999);
                        s.From = GetDataValue("from") == "1";
                        s.Active = GetDataValue("active") == "1";
                        s.Featured = GetDataValue("featured") == "1";
                        await Store.SaveSite(site);
                        saved = s.Name;
                    }
                }
            }
            foreach (string k in new[] { "name", "text", "min", "price" })
            {
                response.SetElementContents("sc-e-" + k, err.TryGetValue(k, out string? m) ? m : string.Empty);
            }
            if (saved.Length == 0)
            {
                return response;
            }
            await Store.Log(c.U.Id, id.Length == 0 ? "add service" : "edit service", saved, true);
            response.SetElementContents("sc-list", ListHtml(await Store.LoadSite(), true));
            response.ExecuteScript("AdminServicesJs.shut();AdminServicesJs.toast('Service saved');");
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
            string id = Ui.Clip(GetDataValue("sid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "delete service", id, "You can look at services but not change them.");
            }
            List<Booking> bookings = await Store.LoadBookings();
            string msg = string.Empty;
            string name = string.Empty;
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite();
                SalonService? s = site.Services.FirstOrDefault(x => x.Id == id);
                if (s == null)
                {
                    msg = "That service was not found.";
                }
                else if (bookings.Any(b => b.ServiceId == id || b.Paid != null && b.Paid.Lines.Any(l => l.ServiceId == id)))
                {
                    msg = s.Name + " has bookings on record. Untick On sale to hide it instead.";
                }
                else
                {
                    name = s.Name;
                    site.Services.Remove(s);
                    foreach (Stylist st in site.Stylists)
                    {
                        st.Services.Remove(id);
                    }
                    await Store.SaveSite(site);
                }
            }
            if (name.Length == 0)
            {
                await Store.Log(c.U.Id, "delete service", id, false);
                response.ExecuteScript("AdminServicesJs.toast('" + Ui.Js(msg) + "');");
                return response;
            }
            await Store.Log(c.U.Id, "delete service", name, true);
            response.SetElementContents("sc-list", ListHtml(await Store.LoadSite(), true));
            response.ExecuteScript("AdminServicesJs.shut();AdminServicesJs.toast('Service deleted');");
            return response;
        }

        public async Task<ApiResponse> Cats()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "edit categories", string.Empty, "You can look at services but not change them.");
            }
            SiteData site = await Store.LoadSite();
            StringBuilder sb = new StringBuilder("<form class=\"sc-stack\" id=\"sc-catform\" onsubmit=\"return AdminServicesJs.saveCats()\" novalidate><p class=\"sc-hint\">Rename, reorder or add categories. A category with services cannot be removed.</p><div class=\"sc-cats\" id=\"sc-cats\">");
            foreach (Category cat in site.Categories)
            {
                sb.Append(CatRow(cat.Key, cat.Label, site.Services.Count(s => s.Category == cat.Key)));
            }
            sb.Append("</div><button type=\"button\" class=\"sc-btn sc-btn-ghost sc-btn-sm sc-addcat\" onclick=\"AdminServicesJs.addCat()\">+ Add a category</button><template id=\"sc-cattpl\">" + CatRow(string.Empty, string.Empty, 0) + "</template><em class=\"sc-err\" id=\"sc-e-cats\"></em><div class=\"sc-acts\"><button type=\"submit\" class=\"sc-btn\">Save categories</button><button type=\"button\" class=\"sc-btn sc-btn-ghost\" onclick=\"AdminServicesJs.closeModal()\">Cancel</button></div></form>");
            response.SetElementContents("sc-mbody", sb.ToString());
            response.ExecuteScript("AdminServicesJs.openModal('Categories');");
            return response;
        }

        private static string CatRow(string key, string label, int n)
        {
            return "<div class=\"sc-cat\" data-key=\"" + Ui.Esc(key) + "\"><input type=\"text\" maxlength=\"40\" value=\"" + Ui.Esc(label) + "\" placeholder=\"Category name\" aria-label=\"Category name\"><small>" + Ui.Count(n, "service", "services") + "</small><button type=\"button\" class=\"sc-ibtn\" aria-label=\"Move up\" onclick=\"AdminServicesJs.catUp(this)\">&uarr;</button>" + (n == 0 ? "<button type=\"button\" class=\"sc-ibtn\" aria-label=\"Remove\" onclick=\"this.parentNode.remove()\">&times;</button>" : "<span class=\"sc-ibtn\" aria-hidden=\"true\"></span>") + "</div>";
        }

        private sealed class CatIn
        {
            public string Key { get; set; } = string.Empty;
            public string Label { get; set; } = string.Empty;
        }

        public async Task<ApiResponse> SaveCats()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save categories", string.Empty, "You can look at services but not change them.");
            }
            List<CatIn>? raw;
            try
            {
                raw = JsonSerializer.Deserialize<List<CatIn>>(GetDataValue("cats") ?? "[]", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                raw = null;
            }
            string err = string.Empty;
            if (raw == null || raw.Count == 0 || raw.Count > 20)
            {
                err = "Keep between 1 and 20 categories.";
            }
            else if (raw.Any(x => x.Label.Trim().Length < 2 || x.Label.Trim().Length > 40))
            {
                err = "Every category needs a name of 2 to 40 characters.";
            }
            else if (raw.Select(x => x.Label.Trim().ToLowerInvariant()).Distinct().Count() != raw.Count)
            {
                err = "Two categories have the same name.";
            }
            if (err.Length == 0)
            {
                using (await Store.Hold())
                {
                    SiteData site = await Store.LoadSite();
                    List<Category> list = new List<Category>();
                    foreach (CatIn x in raw!)
                    {
                        string key = site.Categories.Any(k => k.Key == x.Key) ? x.Key : Regex.Replace(x.Label.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
                        if (key.Length == 0 || list.Any(k => k.Key == key))
                        {
                            key = "cat-" + (list.Count + 1);
                        }
                        list.Add(new Category { Key = key, Label = x.Label.Trim() });
                    }
                    Category? lost = site.Categories.FirstOrDefault(k => !list.Any(n => n.Key == k.Key) && site.Services.Any(s => s.Category == k.Key));
                    if (lost != null)
                    {
                        err = lost.Label + " still has services. Move them first.";
                    }
                    else
                    {
                        site.Categories = list;
                        await Store.SaveSite(site);
                    }
                }
            }
            response.SetElementContents("sc-e-cats", Ui.Esc(err));
            if (err.Length > 0)
            {
                return response;
            }
            await Store.Log(c.U.Id, "edit categories", raw!.Count + " categories", true);
            response.SetElementContents("sc-list", ListHtml(await Store.LoadSite(), true));
            response.ExecuteScript("AdminServicesJs.shut();AdminServicesJs.toast('Categories saved');");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminServicesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminServicesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"sc-panel\"><h1>No access</h1><p class=\"sc-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("sc-brand", Ui.Esc(site.Brand));
            response.SetElementContents("sc-menu", await Auth.Menu("sc", "AdminServices", c));
            response.SetElementContents("sc-who", Auth.Who("sc", c));
        }

    }
}
