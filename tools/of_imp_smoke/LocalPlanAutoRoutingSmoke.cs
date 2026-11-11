using OpenFill.Core.Mcp;

internal static class LocalPlanAutoRoutingSmoke
{
    public static void Run()
    {
        int checks = 0;
        void Check(string task, bool expect, string reason,
            string? continuation = null, string? mode = null, string? start = null)
        {
            var selected = LocalPlanAutoRouting.TrySelect(task, continuation, mode,
                start, out var url);
            if (selected != expect)
                throw new Exception("LOCALPLAN_AUTO_" + reason + " expected " + expect);
            if (selected && url != "http://127.0.0.1:18766/network.html")
                throw new Exception("LOCALPLAN_AUTO_WRONG_START_URL");
            checks++;
        }
        const string u = "http://127.0.0.1:18766/network.html";
        Check("Na demonstracyjnej stronie " + u + " ustaw filtr Search people na Contoso Systems.",true,"polish_filter");
        Check("Set filter Search people to Contoso on " + u,true,"english_filter");
        Check("W " + u + " ustaw filtr na Example.",true,"simple_filter");
        Check("W " + u + " ustaw filtr i zapisz zmiany.",false,"save_excluded");
        Check("W " + u + " ustaw filtr i wyślij wiadomość.",false,"send_excluded");
        Check("W " + u + " ustaw filtr i kup przedmiot.",false,"buy_excluded");
        Check("W " + u + " ustaw filtr, potem prześlij plik.",false,"upload_excluded");
        Check("W " + u + " ustaw filtr a potem zaloguj.",false,"login_excluded");
        Check("W " + u + " ustaw filtr i sprawdź http://127.0.0.1:18766/crm.html",false,"multiple_urls");
        Check("W https://www.linkedin.com/in/profile ustaw filtr na Example.",false,"sensitive_host");
        Check("W " + u + " ustaw filtr na Example.",false,"continuation","old-task");
        Check("W " + u + " ustaw filtr na Example.",false,"explicit_mode",mode:"local_plan_v1_draft");
        Check("W " + u + " ustaw filtr na Example.",false,"explicit_url",start:u);
        Check("W " + u + " wyślij wiadomość do kontaktu.",false,"no_safe_intent");
        Check("W https://user:pass@example.com/page ustaw filtr na Example.",false,"credentials_url");
        Check("W " + u + "\nustaw filtr na Example.",false,"multiline");
        Console.WriteLine($"LOCALPLAN_AUTO_ROUTING_{checks}_CHECKS_OK");
    }
}
