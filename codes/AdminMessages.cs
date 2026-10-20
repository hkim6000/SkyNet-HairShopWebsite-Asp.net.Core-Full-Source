using System.Text;
using HairShop.Core;
using HairShop.Models;
using SkyNet;

namespace HairShop.codes
{
    public class AdminMessages : WebPage
    {
        private const string AreaKey = "messages";
        private const int Size = 20;

        public override async Task OnInitialized()
        {
            HtmlDoc.SetTitle("Messages | Staff");
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
                response.SetElementContents("ms-main", NoAccess("messages"));
                return response;
            }
            List<ContactMessage> list = await Store.LoadMessages();
            response.SetElementContents("ms-main", "<div class=\"ms-ph\"><div><h1>Messages</h1><p>Notes sent from the Contact page. Reply by email from your own mail program.</p></div></div><div class=\"ms-tools\"><div class=\"ms-chips\" id=\"ms-chips\"><button type=\"button\" class=\"ms-chip ms-act\" data-f=\"\" onclick=\"AdminMessagesJs.show('')\">All</button><button type=\"button\" class=\"ms-chip\" data-f=\"unread\" onclick=\"AdminMessagesJs.show('unread')\">Unread</button></div><input type=\"search\" id=\"ms-term\" placeholder=\"Search name, email or text\" maxlength=\"40\" oninput=\"AdminMessagesJs.typed()\" aria-label=\"Search messages\"></div><div id=\"ms-list\">" + ListHtml(list, string.Empty, string.Empty, 1) + "</div>");
            return response;
        }

        private static string ListHtml(List<ContactMessage> all, string only, string term, int page)
        {
            List<ContactMessage> rows = all.Where(m => (only != "unread" || !m.Read) && (term.Length == 0 || Ui.Has(m.Name, term) || Ui.Has(m.Email, term) || Ui.Has(m.Text, term) || Ui.Has(m.Topic, term))).OrderByDescending(m => m.When).ToList();
            if (rows.Count == 0)
            {
                return "<div class=\"ms-panel ms-empty\">" + (all.Count == 0 ? "No messages yet. They arrive here from the Contact page." : "No messages match.") + "</div>";
            }
            int pages = (rows.Count + Size - 1) / Size;
            page = Math.Min(page, pages);
            StringBuilder sb = new StringBuilder("<div class=\"ms-inbox\">");
            foreach (ContactMessage m in rows.Skip((page - 1) * Size).Take(Size))
            {
                sb.Append("<button type=\"button\" class=\"ms-msg" + (m.Read ? string.Empty : " ms-unread") + "\" onclick=\"AdminMessagesJs.open('" + Ui.Js(m.Id) + "')\"><span class=\"ms-mfrom\"><b>" + Ui.Esc(m.Name) + "</b><small>" + Ui.Esc(Ui.When(m.When)) + "</small></span><span class=\"ms-mtopic\">" + Ui.Esc(m.Topic) + "</span><span class=\"ms-mtext\">" + Ui.Esc(m.Text.Length > 140 ? m.Text.Substring(0, 140) + "…" : m.Text) + "</span></button>");
            }
            sb.Append("</div>" + Ui.Pager("ms", "AdminMessagesJs.page", page, pages, rows.Count, "message", "messages"));
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
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "list messages", string.Empty, "You do not have access to messages.");
            }
            response.SetElementContents("ms-list", ListHtml(await Store.LoadMessages(), GetDataValue("only") == "unread" ? "unread" : string.Empty, Ui.Clip(GetDataValue("term"), 40), Math.Max(1, Ui.Int(GetDataValue("pg"), 1))));
            return response;
        }

        public async Task<ApiResponse> Open()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("mid"), 40);
            if (!Access.Can(c.U, AreaKey, 'V'))
            {
                return await Refuse(response, c, "open message", id, "You do not have access to messages.");
            }
            ContactMessage? m = null;
            using (await Store.Hold())
            {
                List<ContactMessage> list = await Store.LoadMessages();
                m = list.FirstOrDefault(x => x.Id == id);
                if (m != null && !m.Read && Access.Can(c.U, AreaKey, 'E'))
                {
                    m.Read = true;
                    await Store.SaveMessages(list);
                }
            }
            if (m == null)
            {
                response.ExecuteScript("AdminMessagesJs.toast('That message was not found.');");
                return response;
            }
            List<Guest> guests = await Store.LoadGuests();
            Guest? g = Diary.Match(guests, m.Phone, m.Email);
            StringBuilder sb = new StringBuilder("<div class=\"ms-stack\"><dl class=\"ms-kv\"><dt>From</dt><dd><b>" + Ui.Esc(m.Name) + "</b>" + (g != null && Access.Can(c.U, "guests", 'V') ? " &middot; <a class=\"ms-link\" href=\"AdminGuests?g=" + Ui.Esc(g.Id) + "\">guest record</a>" : string.Empty) + "</dd><dt>Email</dt><dd><a class=\"ms-link\" href=\"mailto:" + Ui.Esc(m.Email) + "?subject=" + Uri.EscapeDataString("Re: " + m.Topic) + "\">" + Ui.Esc(m.Email) + "</a></dd>" + (m.Phone.Length > 0 ? "<dt>Phone</dt><dd><a class=\"ms-link\" href=\"tel:" + Ui.Esc(Diary.Digits(m.Phone)) + "\">" + Ui.Esc(m.Phone) + "</a></dd>" : string.Empty) + "<dt>Topic</dt><dd>" + Ui.Esc(m.Topic) + "</dd><dt>Sent</dt><dd>" + Ui.Esc(Ui.When(m.When)) + "</dd></dl><div class=\"ms-mtxt\">" + Ui.Esc(m.Text).Replace("\n", "<br>") + "</div><div class=\"ms-acts\"><a class=\"ms-btn\" href=\"mailto:" + Ui.Esc(m.Email) + "?subject=" + Uri.EscapeDataString("Re: " + m.Topic) + "\">Reply by email</a>");
            if (Access.Can(c.U, AreaKey, 'E'))
            {
                sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-ghost\" onclick=\"AdminMessagesJs.unread('" + Ui.Js(m.Id) + "')\">Mark as unread</button>");
            }
            if (Access.Can(c.U, AreaKey, 'X'))
            {
                sb.Append("<button type=\"button\" class=\"ms-btn ms-btn-danger ms-push\" onclick=\"AdminMessagesJs.drop('" + Ui.Js(m.Id) + "','" + Ui.Js(m.Name) + "')\">Delete</button>");
            }
            sb.Append("</div></div>");
            response.SetElementContents("ms-mbody", sb.ToString());
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.opened('" + Ui.Js(m.Topic) + "');");
            return response;
        }

        public async Task<ApiResponse> Unread()
        {
            ApiResponse response = new ApiResponse();
            Ctx? c = await Guard(response);
            if (c == null)
            {
                return response;
            }
            string id = Ui.Clip(GetDataValue("mid"), 40);
            if (!Access.Can(c.U, AreaKey, 'E'))
            {
                return await Refuse(response, c, "mark message", id, "You can read messages but not change them.");
            }
            using (await Store.Hold())
            {
                List<ContactMessage> list = await Store.LoadMessages();
                ContactMessage? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    m.Read = false;
                    await Store.SaveMessages(list);
                }
            }
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.shut();AdminMessagesJs.refresh();AdminMessagesJs.toast('Marked as unread');");
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
            string id = Ui.Clip(GetDataValue("mid"), 40);
            if (!Access.Can(c.U, AreaKey, 'X'))
            {
                return await Refuse(response, c, "delete message", id, "You are not allowed to delete messages.");
            }
            string name = string.Empty;
            using (await Store.Hold())
            {
                List<ContactMessage> list = await Store.LoadMessages();
                ContactMessage? m = list.FirstOrDefault(x => x.Id == id);
                if (m != null)
                {
                    name = m.Name;
                    list.Remove(m);
                    await Store.SaveMessages(list);
                }
            }
            await Store.Log(c.U.Id, "delete message", name.Length > 0 ? "from " + name : id, name.Length > 0);
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.ExecuteScript("AdminMessagesJs.shut();AdminMessagesJs.refresh();AdminMessagesJs.toast('Message deleted');");
            return response;
        }

        private async Task<Ctx?> Guard(ApiResponse response)
        {
            Ctx? c = await Auth.Check(GetDataValue("tok"));
            if (c == null)
            {
                response.ExecuteScript("AdminMessagesJs.expired();");
            }
            return c;
        }

        private static async Task<ApiResponse> Refuse(ApiResponse response, Ctx c, string action, string target, string message)
        {
            await Store.Log(c.U.Id, action, target, false);
            response.ExecuteScript("AdminMessagesJs.toast('" + Ui.Js(message) + "');");
            return response;
        }

        private static string NoAccess(string what)
        {
            return "<div class=\"ms-panel\"><h1>No access</h1><p class=\"ms-hint\">You do not have access to " + what + ". Ask a manager if you need it.</p></div>";
        }

        private static async Task Shell(ApiResponse response, Ctx c, SiteData site)
        {
            response.SetElementContents("ms-brand", Ui.Esc(site.Brand));
            response.SetElementContents("ms-menu", await Auth.Menu("ms", "AdminMessages", c));
            response.SetElementContents("ms-who", Auth.Who("ms", c));
        }

    }
}
