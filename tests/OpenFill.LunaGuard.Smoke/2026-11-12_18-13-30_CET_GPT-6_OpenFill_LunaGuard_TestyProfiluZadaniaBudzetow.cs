using OpenFill.Core.Agent;

var sample = "Konto ChatGPT. TYLKO ten adres czatu: https://chatgpt.com/c/6af3d494-becc-83ed-835a-c4eb18957610. Wyslij dokladnie jeden raz tekst wybudzenia do wskazanego czatu: AUTOMATYCZNE WYBUDZENIE.";
var tests = new (string Name, bool Actual, bool Expected)[]
{
    ("one-shot Polish", LunaTaskGuardProfile.IsOneShotChatGptSend(sample), true),
    ("one-shot accented", LunaTaskGuardProfile.IsOneShotChatGptSend("Wyślij dokładnie jeden raz tekst na https://chatgpt.com/c/abc123"), true),
    ("one-shot English", LunaTaskGuardProfile.IsOneShotChatGptSend("Send exactly once to https://chatgpt.com/c/abc123"), true),
    ("no implicit send", LunaTaskGuardProfile.IsOneShotChatGptSend("Read https://chatgpt.com/c/abc123"), false),
    ("no once qualifier", LunaTaskGuardProfile.IsOneShotChatGptSend("Wyslij tekst do https://chatgpt.com/c/abc123"), false),
    ("two URLs", LunaTaskGuardProfile.IsOneShotChatGptSend("Wyslij dokladnie jeden raz do https://chatgpt.com/c/abc oraz https://chatgpt.com/c/def"), false),
    ("wrong host", LunaTaskGuardProfile.IsOneShotChatGptSend("Wyslij dokladnie jeden raz do https://example.org/c/abc"), false),
    ("wrong path", LunaTaskGuardProfile.IsOneShotChatGptSend("Wyslij dokladnie jeden raz do https://chatgpt.com/"), false),
    ("reject terminal_exec", LunaTaskGuardProfile.IsAllowedOneShotTool("terminal_exec"), false),
    ("reject search_logs", LunaTaskGuardProfile.IsAllowedOneShotTool("search_logs"), false),
    ("reject file_query", LunaTaskGuardProfile.IsAllowedOneShotTool("file_query"), false),
    ("allow act_many", LunaTaskGuardProfile.IsAllowedOneShotTool("act_many"), true),
    ("allow finish", LunaTaskGuardProfile.IsAllowedOneShotTool("finish"), true)
};
int passed = 0;
foreach (var item in tests)
{
    if (item.Actual == item.Expected) { passed++; Console.WriteLine("PASS " + item.Name); }
    else Console.WriteLine("FAIL " + item.Name + " expected " + item.Expected + ", got " + item.Actual);
}
var one = LunaTaskGuardProfile.Defaults(sample);
var other = LunaTaskGuardProfile.Defaults("Find information on https://example.org");
bool limits = one == (0.04, 180, 90) && other == (0.15, 900, 150);
Console.WriteLine((limits ? "PASS " : "FAIL ") + "default budget thresholds");
if (!limits || passed != tests.Length) Environment.ExitCode = 1;
Console.WriteLine("TOTAL " + (passed + (limits ? 1 : 0)) + "/" + (tests.Length + 1));
