using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminAccount : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("My account | Staff");
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
            bool demo = c.U.Id == "u-demo";
            string rights = c.U.Admin ? "Manager: every area" : string.Join(", ", Access.Areas.Where(a => Access.Level(c.U, a.Key).Length > 0).Select(a => a.Name + " (" + Access.Level(c.U, a.Key) + ")"));
            response.SetElementContents("my-main", "<div class=\"my-ph\"><div><h1>My account</h1><p>Your sign-in details.</p></div></div><div class=\"my-grid2 my-acc\"><section class=\"my-panel\"><h2>You</h2><dl class=\"my-kv my-mt\"><dt>Name</dt><dd>" + Ui.Esc(c.U.Name) + "</dd><dt>User name</dt><dd>" + Ui.Esc(c.U.Login) + "</dd><dt>Rights</dt><dd>" + Ui.Esc(rights.Length == 0 ? "None yet" : rights) + "</dd></dl></section><section class=\"my-panel\"><h2>Change password</h2>" + (demo ? "<p class=\"my-hint my-mt\">The demo manager has no password to change.</p>" : "<form class=\"my-stack my-mt\" id=\"my-form\" onsubmit=\"return AdminAccountJs.save()\" novalidate><label class=\"my-field\" for=\"my-f-cur\"><span>Current password</span><input type=\"password\" id=\"my-f-cur\" data-k=\"cur\" maxlength=\"100\" autocomplete=\"current-password\"></label><label class=\"my-field\" for=\"my-f-n1\"><span>New password</span><input type=\"password\" id=\"my-f-n1\" data-k=\"n1\" maxlength=\"100\" autocomplete=\"new-password\"><small>At least 8 characters.</small></label><label class=\"my-field\" for=\"my-f-n2\"><span>New password again</span><input type=\"password\" id=\"my-f-n2\" data-k=\"n2\" maxlength=\"100\" autocomplete=\"new-password\"></label><em class=\"my-err\" id=\"my-e-pass\" role=\"alert\"></em><div class=\"my-acts\"><button type=\"submit\" class=\"my-btn\">Change password</button></div><p class=\"my-hint\">You stay signed in here; other devices are signed out.</p></form>") + "</section></div>");
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
            string cur = GetDataValue("cur") ?? string.Empty;
            string n1 = GetDataValue("n1") ?? string.Empty;
            string n2 = GetDataValue("n2") ?? string.Empty;
            string msg = string.Empty;
            if (c.U.Id == "u-demo")
            {
                msg = "The demo manager has no password to change.";
            }
            else if (n1.Length < 8)
            {
                msg = "Choose a new password of at least 8 characters.";
            }
            else if (n1 != n2)
            {
                msg = "The two new passwords are not the same.";
            }
            else if (n1 == cur)
            {
                msg = "The new password must be different.";
            }
            else
            {
                using (await Store.Hold())
                {
                    List<Staff> staff = await Store.LoadStaff();
                    Staff? u = staff.FirstOrDefault(x => x.Id == c.U.Id);
                    if (u == null || !Access.Verify(cur, u.Hash))
                    {
                        msg = "Your current password is not right.";
                    }
                    else
                    {
                        u.Hash = Access.HashPassword(n1);
                        await Store.SaveStaff(staff);
                    }
                }
            }
            response.SetElementContents("my-e-pass", Ui.Esc(msg));
            if (msg.Length > 0)
            {
                await Store.Log(c.U.Id, "change own password", string.Empty, false);
                return response;
            }
            await Auth.CloseUser(c.U.Id, c.Token);
            await Store.Log(c.U.Id, "change own password", string.Empty, true);
            response.ExecuteScript("AdminAccountJs.saved();");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminAccountJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminAccountJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"my-panel\"><h1>No access</h1><p class=\"my-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("my-brand", Ui.Esc(site.Brand));
            response.SetElementContents("my-menu", await Auth.Menu("my", "AdminAccount", c));
            response.SetElementContents("my-who", Auth.Who("my", c));
        }

    }
}
