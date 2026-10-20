using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminUsers : WebPage
    {
        private const string AreaKey = "users";

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Users and rights | Staff");
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
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                await Store.Log(c.U.Id, "open users", string.Empty, false);
                response.SetElementContents("us-main", NoAccess("users and rights"));
                return response;
            }
            response.SetElementContents("us-main", "<div class=\"us-ph\"><div><h1>Users and rights</h1><p>Who can sign in to the staff area, and what each person may see and change.</p></div><button type=\"button\" class=\"us-btn\" onclick=\"AdminUsersJs.edit('')\">Add a user</button></div><div id=\"us-list\">" + ListHtml(site, await Store.LoadStaff()) + "</div>");
            return response;
        }

        private static string Summary(Staff u)
        {
            if (u.Admin)
            {
                return "Manager: everything";
            }
            List<string> parts = Access.Areas.Where(a => Access.Level(u, a.Key).Length > 0).Select(a => a.Name + " " + Access.Level(u, a.Key)).ToList();
            return parts.Count == 0 ? "No areas yet" : string.Join(", ", parts);
        }

        private static string ListHtml(SiteData site, List<Staff> staff)
        {
            StringBuilder sb = new StringBuilder("<div class=\"us-wrapt\"><table class=\"us-table\"><thead><tr><th>Name</th><th>User name</th><th>Rights</th><th>Status</th></tr></thead><tbody>");
            foreach (Staff u in staff.OrderByDescending(x => x.Admin).ThenBy(x => x.Name))
            {
                string sty = site.Stylists.FirstOrDefault(s => s.Id == u.StylistId)?.Name ?? string.Empty;
                sb.Append("<tr class=\"us-row\" onclick=\"AdminUsersJs.edit('" + Ui.Js(u.Id) + "')\"><td><b>" + Ui.Esc(u.Name) + "</b><small>" + Ui.Esc(u.Title) + (sty.Length > 0 ? " &middot; stylist " + Ui.Esc(sty) + (u.Own ? ", own records only" : string.Empty) : string.Empty) + "</small></td><td>" + Ui.Esc(u.Login) + "</td><td class=\"us-sum\">" + Ui.Esc(Summary(u)) + "</td><td>" + (!u.Active ? "<span class=\"us-pill us-pill-off\">Switched off</span>" : u.Id == "u-demo" ? "<span class=\"us-pill us-pill-wait\">Demo only</span>" : u.MustChange ? "<span class=\"us-pill us-pill-new\">Must set password</span>" : "<span class=\"us-pill us-pill-go\">Active</span>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div><p class=\"us-hint us-legend\">V view &middot; E edit &middot; X the area&rsquo;s extra right (cancel and no-show, reopen payments, delete guests, hours and days off, delete files or messages).</p>");
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
            string id = Ui.Clip(GetDataValue("uid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "open user", id, "Only managers can manage users.");
            }
            SiteData site = await Store.LoadSite();
            Staff u = (await Store.LoadStaff()).FirstOrDefault(x => x.Id == id) ?? new Staff { Active = true };
            bool self = u.Id == c.U.Id;
            StringBuilder sb = new StringBuilder("<form class=\"us-stack\" id=\"us-form\" onsubmit=\"return AdminUsersJs.save('" + Ui.Js(u.Id) + "')\" novalidate><div class=\"us-grid3\">");
            sb.Append("<label class=\"us-field\"><span>Name</span><input type=\"text\" id=\"us-f-name\" data-k=\"name\" maxlength=\"60\" value=\"" + Ui.Esc(u.Name) + "\"><em class=\"us-err\" id=\"us-e-name\"></em></label>");
            sb.Append("<label class=\"us-field\"><span>User name</span><input type=\"text\" id=\"us-f-login\" data-k=\"login\" maxlength=\"40\" autocomplete=\"off\" value=\"" + Ui.Esc(u.Login) + "\"><small>Letters, numbers, dot, dash.</small><em class=\"us-err\" id=\"us-e-login\"></em></label>");
            sb.Append("<label class=\"us-field\"><span>Job title</span><input type=\"text\" id=\"us-f-title\" data-k=\"title\" maxlength=\"40\" value=\"" + Ui.Esc(u.Title) + "\" placeholder=\"Front desk\"></label></div>");
            sb.Append("<div class=\"us-grid2\"><label class=\"us-field\"><span>Linked stylist</span><select id=\"us-f-sty\" data-k=\"sty\"><option value=\"\">Not a stylist</option>" + string.Concat(site.Stylists.OrderBy(s => s.Order).Select(s => "<option value=\"" + Ui.Esc(s.Id) + "\"" + (s.Id == u.StylistId ? " selected" : string.Empty) + ">" + Ui.Esc(s.Name) + "</option>")) + "</select></label><div class=\"us-stack us-checks\">");
            sb.Append("<label class=\"us-check\"><input type=\"checkbox\" data-k=\"own\"" + (u.Own ? " checked" : string.Empty) + "> Own records only: bookings, checkout, guests and tips of the linked stylist</label>");
            sb.Append("<label class=\"us-check\"><input type=\"checkbox\" data-k=\"active\"" + (u.Active ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "> Can sign in</label>");
            sb.Append("<label class=\"us-check\"><input type=\"checkbox\" data-k=\"admin\" id=\"us-f-admin\" onchange=\"AdminUsersJs.mgr()\"" + (u.Admin ? " checked" : string.Empty) + (self ? " disabled" : string.Empty) + "> Manager: every right, plus Settings and Users</label></div></div>");
            sb.Append("<fieldset class=\"us-fs\" id=\"us-grid\"" + (u.Admin ? " disabled" : string.Empty) + "><legend>Rights by area</legend><div class=\"us-wrapt\"><table class=\"us-table us-rights\"><thead><tr><th>Area</th><th>None</th><th>View</th><th>Edit</th><th>Extra</th></tr></thead><tbody>");
            foreach (Area a in Access.Areas.Where(a => !a.AdminOnly))
            {
                string lv = Access.Level(u, a.Key);
                string cur = lv.Contains('X') ? "VEX" : lv.Contains('E') ? "VE" : lv.Contains('V') ? "V" : string.Empty;
                sb.Append("<tr data-area=\"" + a.Key + "\"><td><b>" + Ui.Esc(a.Name) + "</b>" + (a.Extra.Length > 0 ? "<small>Extra: " + Ui.Esc(a.Extra) + "</small>" : string.Empty) + "</td>");
                foreach (string v in new[] { string.Empty, "V", "VE", "VEX" })
                {
                    bool allowed = v.Length == 0 || v == "V" || !a.NoEdit && (v == "VE" || a.Extra.Length > 0);
                    sb.Append("<td>" + (allowed ? "<input type=\"radio\" name=\"us-r-" + a.Key + "\" value=\"" + v + "\"" + (cur == v ? " checked" : string.Empty) + " aria-label=\"" + Ui.Esc(a.Name) + " " + (v.Length == 0 ? "none" : v) + "\">" : "<span class=\"us-na\">&ndash;</span>") + "</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table></div><p class=\"us-hint\">Settings and Users are for managers only.</p></fieldset>");
            sb.Append("<em class=\"us-err\" id=\"us-e-form\" role=\"alert\"></em><div class=\"us-acts\"><button type=\"submit\" class=\"us-btn\">" + (u.Id.Length == 0 ? "Add user" : "Save") + "</button><button type=\"button\" class=\"us-btn us-btn-ghost\" onclick=\"AdminUsersJs.closeModal()\">Cancel</button>");
            if (u.Id.Length > 0 && u.Id != "u-demo")
            {
                sb.Append("<button type=\"button\" class=\"us-btn us-btn-ghost us-push\" onclick=\"AdminUsersJs.reset('" + Ui.Js(u.Id) + "','" + Ui.Js(u.Name) + "')\">Reset password</button>");
            }
            sb.Append("</div></form>");
            response.SetElementContents("us-mbody", sb.ToString());
            response.ExecuteScript("AdminUsersJs.openModal('" + (u.Id.Length == 0 ? "Add a user" : Ui.Js(u.Name)) + "');");
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
            string id = Ui.Clip(GetDataValue("uid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "save user", id, "Only managers can manage users.");
            }
            SiteData site = await Store.LoadSite();
            string name = Ui.Clip(GetDataValue("name"), 80);
            string login = Ui.Clip(GetDataValue("login"), 60).ToLowerInvariant();
            Dictionary<string, string> err = new Dictionary<string, string>();
            if (name.Length < 2 || name.Length > 60)
            {
                err["name"] = "Enter a name.";
            }
            if (!Regex.IsMatch(login, "^[a-z0-9][a-z0-9._-]{2,39}$") || login == "demo")
            {
                err["login"] = "Use 3 to 40 letters, numbers, dots or dashes.";
            }
            Dictionary<string, string> perms;
            try
            {
                perms = JsonSerializer.Deserialize<Dictionary<string, string>>(GetDataValue("perms") ?? "{}") ?? new Dictionary<string, string>();
            }
            catch (JsonException)
            {
                perms = new Dictionary<string, string>();
            }
            string pass = string.Empty;
            Staff? saved = null;
            if (err.Count == 0)
            {
                using (await Store.Hold())
                {
                    List<Staff> staff = await Store.LoadStaff();
                    if (staff.Any(x => x.Id != id && x.Login.Equals(login, StringComparison.OrdinalIgnoreCase)))
                    {
                        err["login"] = "Another user has this user name.";
                    }
                    else
                    {
                        Staff? u = id.Length > 0 ? staff.FirstOrDefault(x => x.Id == id) : null;
                        if (id.Length > 0 && u == null)
                        {
                            err["name"] = "That user was not found.";
                        }
                        else
                        {
                            bool self = u != null && u.Id == c.U.Id;
                            if (u == null)
                            {
                                pass = Access.NewPassword();
                                u = new Staff { Id = Diary.NewId("u-"), Hash = Access.HashPassword(pass), MustChange = true };
                                staff.Add(u);
                            }
                            bool wasActive = u.Active;
                            u.Name = name;
                            u.Login = login;
                            u.Title = Ui.Clip(GetDataValue("title"), 40);
                            string sty = Ui.Clip(GetDataValue("sty"), 40);
                            u.StylistId = site.Stylists.Any(s => s.Id == sty) ? sty : string.Empty;
                            u.Own = u.StylistId.Length > 0 && GetDataValue("own") == "1";
                            if (!self)
                            {
                                u.Active = GetDataValue("active") == "1";
                                u.Admin = GetDataValue("admin") == "1";
                            }
                            u.Perms = Access.CleanAll(perms);
                            await Store.SaveStaff(staff);
                            if (wasActive && !u.Active)
                            {
                                await Auth.CloseUser(u.Id, string.Empty);
                            }
                            saved = u;
                        }
                    }
                }
            }
            foreach (string k in new[] { "name", "login" })
            {
                response.SetElementContents("us-e-" + k, err.TryGetValue(k, out string? m) ? m : string.Empty);
            }
            if (saved == null)
            {
                return response;
            }
            await Store.Log(c.U.Id, id.Length == 0 ? "add user" : "edit user", saved.Name + " · " + Summary(saved), true);
            response.SetElementContents("us-list", ListHtml(site, await Store.LoadStaff()));
            if (pass.Length > 0)
            {
                response.SetElementContents("us-mbody", PassBox(saved, pass));
                response.ExecuteScript("AdminUsersJs.clean();AdminUsersJs.toast('User added');");
            }
            else
            {
                response.ExecuteScript("AdminUsersJs.shut();AdminUsersJs.toast('User saved');");
            }
            return response;
        }

        private static string PassBox(Staff u, string pass)
        {
            return "<div class=\"us-stack\"><p>Give " + Ui.Esc(u.Name) + " these details. They choose their own password the first time they sign in.</p><div class=\"us-passbox\"><span>User name <code>" + Ui.Esc(u.Login) + "</code></span><span>Temporary password <code>" + Ui.Esc(pass) + "</code></span></div><p class=\"us-hint\">This password is shown only now.</p><div class=\"us-acts\"><button type=\"button\" class=\"us-btn\" onclick=\"AdminUsersJs.shut()\">Done</button></div></div>";
        }

        public async Task<ApiResponse> Reset()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("uid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "reset password", id, "Only managers can reset passwords.");
            }
            if (id == c.U.Id || id == "u-demo")
            {
                response.ExecuteScript("AdminUsersJs.toast('Change your own password in My account.');");
                return response;
            }
            string pass = Access.NewPassword();
            Staff? done = null;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Id == id);
                if (u != null)
                {
                    u.Hash = Access.HashPassword(pass);
                    u.MustChange = true;
                    await Store.SaveStaff(staff);
                    done = u;
                }
            }
            if (done == null)
            {
                response.ExecuteScript("AdminUsersJs.toast('That user was not found.');");
                return response;
            }
            await Auth.CloseUser(done.Id, string.Empty);
            await Store.Log(c.U.Id, "reset password", done.Name, true);
            response.SetElementContents("us-mbody", PassBox(done, pass));
            response.SetElementContents("us-list", ListHtml(await Store.LoadSite(), await Store.LoadStaff()));
            response.ExecuteScript("AdminUsersJs.clean();");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminUsersJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminUsersJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"us-panel\"><h1>No access</h1><p class=\"us-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("us-brand", Ui.Esc(site.Brand));
            response.SetElementContents("us-menu", await Auth.Menu("us", "AdminUsers", c));
            response.SetElementContents("us-who", Auth.Who("us", c));
        }

    }
}
