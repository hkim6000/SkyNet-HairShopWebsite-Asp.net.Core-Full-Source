using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminActivity : WebPage
    {
        private const string AreaKey = "activity";
        private const int Size = 25;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Activity | Staff");
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
            bool all = Access.Can(c.U, AreaKey, 'V');
            List<Staff> staff = await Store.LoadStaff();
            StringBuilder sb = new StringBuilder("<div class=\"ac-ph\"><div><h1>" + (all ? "Activity log" : "My activity") + "</h1><p>" + (all ? "Every change and every refused request, newest first. The latest 5,000 are kept." : "What you have done in the staff area.") + "</p></div></div><div class=\"ac-tools\">");
            if (all)
            {
                sb.Append("<select id=\"ac-person\" onchange=\"AdminActivityJs.go()\" aria-label=\"Person\"><option value=\"\">Everyone</option>" + string.Concat(staff.OrderBy(s => s.Name).Select(s => "<option value=\"" + Ui.Esc(s.Id) + "\">" + Ui.Esc(s.Name) + "</option>")) + "</select>");
            }
            sb.Append("<select id=\"ac-ok\" onchange=\"AdminActivityJs.go()\" aria-label=\"Result\"><option value=\"\">All results</option><option value=\"no\">Refused only</option></select><input type=\"search\" id=\"ac-term\" placeholder=\"Search actions\" maxlength=\"40\" oninput=\"AdminActivityJs.typed()\" aria-label=\"Search activity\"></div><div id=\"ac-list\">" + ListHtml(await Store.LoadLog(), staff, all ? string.Empty : c.U.Id, string.Empty, string.Empty, 1) + "</div>");
            response.SetElementContents("ac-main", sb.ToString());
            return response;
        }

        private static string ListHtml(List<ActivityEntry> log, List<Staff> staff, string who, string ok, string term, int page)
        {
            List<ActivityEntry> rows = log.Where(a => (who.Length == 0 || a.UserId == who) && (ok != "no" || !a.Ok) && (term.Length == 0 || Ui.Has(a.Action, term) || Ui.Has(a.Target, term))).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ac-panel ac-empty\">Nothing logged yet.</div>";
            }
            int pages = (rows.Count + Size - 1) / Size;
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ac-wrapt\"><table class=\"ac-table\"><thead><tr><th>When</th><th>Who</th><th>Action</th><th>About</th><th>Result</th></tr></thead><tbody>");
            foreach (ActivityEntry a in rows.Skip((page - 1) * Size).Take(Size))
            {
                string name = staff.FirstOrDefault(s => s.Id == a.UserId)?.Name ?? a.UserId;
                sb.Append("<tr><td>" + Ui.Esc(Ui.When(a.When)) + "</td><td>" + Ui.Esc(name) + "</td><td>" + Ui.Esc(a.Action) + "</td><td class=\"ac-about\">" + Ui.Esc(a.Target) + "</td><td>" + (a.Ok ? "<span class=\"ac-pill ac-pill-go\">Done</span>" : "<span class=\"ac-pill ac-pill-bad\">Refused</span>") + "</td></tr>");
            }
            sb.Append("</tbody></table></div>" + Ui.Pager("ac", "AdminActivityJs.page", page, pages, rows.Count, "entry", "entries"));
            return sb.ToString();
        }

        public async Task<ApiResponse> Rows()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            bool all = Access.Can(c.U, AreaKey, 'V');
            string who = all ? Ui.Clip(GetDataValue("who"), 40) : c.U.Id;
            response.SetElementContents("ac-list", ListHtml(await Store.LoadLog(), await Store.LoadStaff(), who, GetDataValue("ok") == "no" ? "no" : string.Empty, Ui.Clip(GetDataValue("term"), 40), Math.Max(1, Ui.Int(GetDataValue("pg"), 1))));
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminActivityJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminActivityJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ac-panel\"><h1>No access</h1><p class=\"ac-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ac-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ac-menu", await Auth.Menu("ac", "AdminActivity", c));
            response.SetElementContents("ac-who", Auth.Who("ac", c));
        }

    }
}
