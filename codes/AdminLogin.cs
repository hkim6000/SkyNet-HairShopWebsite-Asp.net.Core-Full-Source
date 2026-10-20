using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminLogin : WebPage
    {
        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Staff sign in");
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("robots", "noindex, nofollow");
            SiteData site = await Store.LoadSite();
            bool demo = Store.Demo();
            string first = string.Empty;
            if (!demo)
            {
                await Store.EnsureFirstManager();
                string pass = Store.InitialPassword();
                if (Local.Request() && pass.Length > 0)
                {
                    first = "<div class=\"lg-first\" role=\"note\"><b>First sign-in</b><span>User name <code>admin</code></span><span>Password <code>" + Ui.Esc(pass) + "</code></span><small>Shown only on this computer, until you choose your own password. Also in appConfig/initial-admin.txt.</small></div>";
                }
            }
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_brand}", Ui.Esc(site.Brand))
                .Replace("{plhd_first}", first)
                .Replace("{plhd_demo}", demo ? "<p class=\"lg-demo\"><b>Demo mode.</b> Any user name and password signs in as a demo manager with every right.</p>" : string.Empty)
                .Replace("{plhd_user}", demo ? "demo" : string.Empty)
                .Replace("{plhd_pass}", demo ? "demo1234" : string.Empty);
        }

        public async Task<ApiResponse> Resume()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c != null)
            {
                response.ExecuteScript("AdminLoginJs.enter('" + Ui.Js(c.Token) + "');");
            }
            return response;
        }

        public async Task<ApiResponse> SignIn()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40);
            string pass = GetDataValue("pass") ?? string.Empty;
            List<Staff> staff = await Store.LoadStaff();
            Staff? u = staff.FirstOrDefault(x => x.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && x.Id != "u-demo");
            if (Auth.Paused(login))
            {
                response.SetElementContents("lg-msg", "Too many wrong passwords. Wait five minutes and try again.");
                return response;
            }
            bool good = u != null && u.Active && Access.Verify(pass, u.Hash);
            if (!good && Store.Demo())
            {
                Staff d = await Store.DemoManager();
                await Store.Log(d.Id, "sign in", "demo", true);
                response.ExecuteScript("AdminLoginJs.enter('" + await Auth.Open(d) + "');");
                return response;
            }
            if (!good || u == null)
            {
                Auth.Miss(login);
                if (u != null)
                {
                    await Store.Log(u.Id, "sign in", "wrong password", false);
                }
                response.SetElementContents("lg-msg", "That user name and password do not match.");
                return response;
            }
            Auth.Clear(login);
            if (u.MustChange)
            {
                response.SetElementContents("lg-msg", string.Empty);
                response.ExecuteScript("AdminLoginJs.choose();");
                return response;
            }
            await Store.Log(u.Id, "sign in", string.Empty, true);
            response.ExecuteScript("AdminLoginJs.enter('" + await Auth.Open(u) + "');");
            return response;
        }

        public async Task<ApiResponse> SetPassword()
        {
            ApiResponse response = new ApiResponse();
            string login = Ui.Clip(GetDataValue("user"), 40);
            string pass = GetDataValue("pass") ?? string.Empty;
            string n1 = GetDataValue("new1") ?? string.Empty;
            string n2 = GetDataValue("new2") ?? string.Empty;
            if (Auth.Paused(login))
            {
                response.SetElementContents("lg-msg", "Too many wrong passwords. Wait five minutes and try again.");
                return response;
            }
            Staff? done = null;
            string msg = string.Empty;
            using (await Store.Hold())
            {
                List<Staff> staff = await Store.LoadStaff();
                Staff? u = staff.FirstOrDefault(x => x.Login.Equals(login, StringComparison.OrdinalIgnoreCase) && x.Id != "u-demo");
                if (u == null || !u.Active || !Access.Verify(pass, u.Hash))
                {
                    Auth.Miss(login);
                    msg = "That user name and password do not match.";
                }
                else if (n1.Length < 8)
                {
                    msg = "Choose a new password of at least 8 characters.";
                }
                else if (n1 != n2)
                {
                    msg = "The two new passwords are not the same.";
                }
                else if (n1 == pass)
                {
                    msg = "Choose a password different from the one you were given.";
                }
                else
                {
                    u.Hash = Access.HashPassword(n1);
                    u.MustChange = false;
                    await Store.SaveStaff(staff);
                    done = u;
                }
            }
            if (done == null)
            {
                response.SetElementContents("lg-msg", Ui.Esc(msg));
                return response;
            }
            if (done.Admin)
            {
                Store.DropInitial();
            }
            await Auth.CloseUser(done.Id, string.Empty);
            await Store.Log(done.Id, "set own password", string.Empty, true);
            response.ExecuteScript("AdminLoginJs.enter('" + await Auth.Open(done) + "');");
            return response;
        }
    }
}
